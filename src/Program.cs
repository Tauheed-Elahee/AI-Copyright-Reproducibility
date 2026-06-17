using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Azure.Identity;

namespace AICopyrightReproducibility
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            string configPath = args.Length > 0 ? args[0] : "config.json";
            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine($"Config file not found: {configPath}");
                return 1;
            }
            JsonSerializerOptions readOpts = new JsonSerializerOptions
            {
                PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
            };
            string configDir = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ".";
            string stamp     = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string logDir    = Path.Combine(configDir, "log");
            Directory.CreateDirectory(logDir);
            StreamWriter logWriter = new StreamWriter(
                Path.Combine(logDir, $"harness-{stamp}.log"), append: false) { AutoFlush = true };
            Console.SetOut(new TeeWriter(Console.Out, logWriter));

            RunConfig cfg = JsonSerializer.Deserialize<RunConfig>(
                File.ReadAllText(configPath), readOpts)
                ?? throw new InvalidOperationException("Failed to deserialise config.");
            Console.WriteLine($"Loaded config : {configPath}");

            static string AbsPath(string dir, string file) =>
                Path.IsPathRooted(file) ? file : Path.Combine(dir, file);

            // Load query library
            Dictionary<string, QueryConfig> queriesDict = new();
            if (cfg.Experiment.File.Queries is not null)
            {
                string p = AbsPath(configDir, cfg.Experiment.File.Queries);
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(p));
                cfg.Queries = doc.RootElement.GetProperty("queries")
                    .EnumerateArray()
                    .Select(e => JsonSerializer.Deserialize<QueryConfig>(e.GetRawText(), readOpts)!)
                    .ToList();
                queriesDict = cfg.Queries.ToDictionary(q => q.Label);
                Console.WriteLine($"Loaded queries: {p}");
            }

            // Load deployments
            if (cfg.Experiment.File.Deployments is not null)
            {
                string p = AbsPath(configDir, cfg.Experiment.File.Deployments);
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(p));
                cfg.Deployments = doc.RootElement.GetProperty("deployments")
                    .EnumerateArray()
                    .Select(e => JsonSerializer.Deserialize<DeploymentConfig>(e.GetRawText(), readOpts)!)
                    .ToList();
                Console.WriteLine($"Loaded deployments: {p}");
            }

            // Load text library
            var textsDict = new Dictionary<string, TextEntry>(StringComparer.Ordinal);
            if (cfg.Experiment.File.Texts is not null)
            {
                string p = AbsPath(configDir, cfg.Experiment.File.Texts);
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(p));
                foreach (TextDbEntry entry in doc.RootElement.GetProperty("texts")
                    .EnumerateArray()
                    .Select(e => JsonSerializer.Deserialize<TextDbEntry>(e.GetRawText(), readOpts)!))
                {
                    var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    extras["full"]  = entry.Content.Title.Full;
                    extras["short"] = entry.Content.Title.Short;
                    if (entry.Content.Title.ExtraFields is not null)
                        foreach (var (k, v) in entry.Content.Title.ExtraFields)
                            FlattenJson(v, k, extras);
                    textsDict[entry.Label] = new TextEntry(
                        entry.Content.Title.Full,
                        entry.Content.Title.Short,
                        entry.Content.SectionHeadings,
                        new Dictionary<string, string>(entry.Content.Aliases, StringComparer.OrdinalIgnoreCase),
                        extras);
                }
                Console.WriteLine($"Loaded texts  : {p}");
            }

            // Load prompts and bind
            List<BoundPrompt> boundPrompts = new();
            if (cfg.Experiment.File.Prompts is not null)
            {
                string p = AbsPath(configDir, cfg.Experiment.File.Prompts);
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(p));
                List<PromptEntry> prompts = doc.RootElement.GetProperty("prompts")
                    .EnumerateArray()
                    .Select(e => JsonSerializer.Deserialize<PromptEntry>(e.GetRawText(), readOpts)!)
                    .ToList();
                boundPrompts = HarnessUtils.BindPrompts(queriesDict, textsDict, prompts);
                Console.WriteLine($"Loaded prompts: {p}");
            }

            string outDir = Path.Combine(cfg.Experiment.OutputRoot, stamp);
            Directory.CreateDirectory(outDir);
            OutputWriter.WriteRunConfig(cfg, outDir);

            string secretsPath = Path.Combine(configDir, "secrets.json");
            Dictionary<string, string> secrets = File.Exists(secretsPath)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(
                      File.ReadAllText(secretsPath), readOpts) ?? new()
                : new();
            HarnessUtils.ResolveSecrets(cfg, secrets);
            Console.WriteLine($"Loaded secrets: {secretsPath}");

            string fallbackScope = cfg.Deployments
                .Select(a => a.Connection.TokenScope)
                .FirstOrDefault(s => s != null)
                ?? "https://ai.azure.com/.default";

            DefaultAzureCredential credential = new DefaultAzureCredential();
            using HttpClient http = new HttpClient();

            Dictionary<string, IDeploymentExecutor> executors = cfg.Deployments.ToDictionary(
                d => d.Label,
                d => d.Mode switch
                {
                    DeploymentMode.AzureModeApi   => (IDeploymentExecutor)new AzureModeApi(d, credential, fallbackScope),
                    DeploymentMode.AzureAgentApi  => new AzureAgentApiExecutor(http, credential, d, fallbackScope),
                    DeploymentMode.StandardOpenAI => new StandardOpenAIExecutor(d),
                    _ => throw new InvalidOperationException($"Unknown deployment mode: {d.Mode}")
                });

            Console.WriteLine($"Deployments: {string.Join(", ", cfg.Deployments.Select(a => a.Label))}");
            Console.WriteLine($"Bound prompts: {boundPrompts.Count} ({string.Join(", ", boundPrompts.Select(b => $"{b.TextLabel}/{b.QueryLabel}"))})");
            Console.WriteLine($"Sets    : {cfg.Experiment.Iterations.Set}  Reps/set: {cfg.Experiment.Iterations.Rep}");
            Console.WriteLine(new string('-', 80));

            using HarnessRunner runner = new HarnessRunner(cfg, boundPrompts, executors, outDir);
            List<RunRecord> records = await runner.RunAllAsync();

            JsonSerializerOptions jsonOpts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(Path.Combine(outDir, "manifest.json"),
                JsonSerializer.Serialize(records, jsonOpts));
            OutputWriter.WriteManifestCsv(records, Path.Combine(outDir, "manifest.csv"));

            OutputWriter.WriteIdentityGroups(records);
            if (records.Any(r => r.SectionCount > 0))
            {
                OutputWriter.WriteSummaryCountsCsv(records, Path.Combine(outDir, "summary_counts.csv"));
                OutputWriter.WriteSummaryPctCsv(records, Path.Combine(outDir, "summary_pct.csv"));
                OutputWriter.WriteConsoleSummary(records);
                OutputWriter.WriteConsolePctSummary(records);
            }

            Console.WriteLine($"\nOutput written to: {Path.GetFullPath(outDir)}");
            return 0;
        }

        private static void FlattenJson(JsonElement element, string prefix, Dictionary<string, string> result)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty prop in element.EnumerateObject())
                        FlattenJson(prop.Value, prefix + "." + prop.Name, result);
                    break;
                case JsonValueKind.String:
                    result[prefix] = element.GetString() ?? "";
                    break;
                default:
                    result[prefix] = element.GetRawText();
                    break;
            }
        }
    }
}
