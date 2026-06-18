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
using AICopyrightReproducibility.Config;
using AICopyrightReproducibility.Executors;
using AICopyrightReproducibility.Executors.Azure;
using AICopyrightReproducibility.Executors.Standard;
using AICopyrightReproducibility.Utils;

namespace AICopyrightReproducibility
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            string projectDir;
            if (args.Length > 0)
            {
                projectDir = Path.GetFullPath(args[0]);
            }
            else if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "config.json")))
            {
                projectDir = Directory.GetCurrentDirectory();
            }
            else
            {
                Console.Error.WriteLine("Usage: harness <project-dir>");
                Console.Error.WriteLine("  Or run from within a project directory that contains config.json.");
                return 1;
            }

            string configPath = Path.Combine(projectDir, "config.json");
            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine($"config.json not found in: {projectDir}");
                return 1;
            }

            JsonSerializerOptions readOpts = new JsonSerializerOptions
            {
                PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
            };
            string configDir = projectDir;
            string stamp     = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

            RunConfig cfg;
            StreamWriter logWriter;
            try
            {
                cfg = JsonSerializer.Deserialize<RunConfig>(
                    File.ReadAllText(configPath), readOpts)
                    ?? throw new InvalidOperationException("Failed to deserialise config.");

                string logDir = Path.Combine(configDir, cfg.Locations.Log.Dir);
                Directory.CreateDirectory(logDir);
                logWriter = new StreamWriter(
                    Path.Combine(logDir, $"harness-{stamp}.log"), append: false) { AutoFlush = true };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ERROR] {ex.Message}");
                return 1;
            }

            StreamWriter? sysLogWriter = null;
            try
            {
                string sysLogDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ai-copyright-reproducibility", "logs");
                Directory.CreateDirectory(sysLogDir);
                sysLogWriter = new StreamWriter(
                    Path.Combine(sysLogDir, $"harness-{stamp}.log"), append: false) { AutoFlush = true };
            }
            catch { /* system log unavailable (e.g. read-only environment); continue without it */ }

            using Logger logger = new Logger(Console.Out, Console.Error, logWriter,
                                             Logger.Level.Info, sysLogWriter);
            logger.Info($"Loaded config : {configPath}");

            static string AbsPath(string dir, string file) =>
                Path.IsPathRooted(file) ? file : Path.Combine(dir, file);

            static Logger.Level ParseLevel(string? s) => s?.ToLowerInvariant() switch
            {
                "verbose" => Logger.Level.Verbose,
                "warning" => Logger.Level.Warning,
                "error"   => Logger.Level.Error,
                _         => Logger.Level.Info
            };

            try
            {
                string configLocDir = Path.Combine(configDir, cfg.Locations.Config.Dir);

                if (cfg.Locations.Config.Files?.Experiment is { } experimentFile)
                {
                    string p = AbsPath(configLocDir, experimentFile);
                    cfg.Experiment = JsonSerializer.Deserialize<ExperimentConfig>(
                        File.ReadAllText(p), readOpts)
                        ?? throw new InvalidOperationException("Failed to deserialise experiment config.");
                    logger.SetLevel(ParseLevel(cfg.Experiment.LogLevel));
                    logger.Info($"Loaded experiment: {p}");
                }

                string inputLocDir = Path.Combine(configDir, cfg.Locations.Input.Dir);

                Dictionary<string, QueryConfig> queriesDict = new();
                if (cfg.Locations.Input.Files?.Queries is { } queriesFile)
                {
                    string p = AbsPath(inputLocDir, queriesFile);
                    using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(p));
                    cfg.Queries = doc.RootElement.GetProperty("queries")
                        .EnumerateArray()
                        .Select(e => JsonSerializer.Deserialize<QueryConfig>(e.GetRawText(), readOpts)!)
                        .ToList();
                    queriesDict = cfg.Queries.ToDictionary(q => q.Label);
                    logger.Info($"Loaded queries: {p}");
                }

                if (cfg.Locations.Config.Files?.Deployments is { } deploymentsFile)
                {
                    string p = AbsPath(configLocDir, deploymentsFile);
                    using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(p));
                    cfg.Deployments = doc.RootElement.GetProperty("deployments")
                        .EnumerateArray()
                        .Select(e => JsonSerializer.Deserialize<DeploymentConfig>(e.GetRawText(), readOpts)!)
                        .ToList();
                    logger.Info($"Loaded deployments: {p}");
                }

                var textsDict = new Dictionary<string, TextEntry>(StringComparer.Ordinal);
                if (cfg.Locations.Input.Files?.Texts is { } textsFile)
                {
                    string p = AbsPath(inputLocDir, textsFile);
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
                    logger.Info($"Loaded texts  : {p}");
                }

                List<BoundPrompt> boundPrompts = new();
                if (cfg.Locations.Input.Files?.Prompts is { } promptsFile)
                {
                    string p = AbsPath(inputLocDir, promptsFile);
                    using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(p));
                    List<PromptEntry> prompts = doc.RootElement.GetProperty("prompts")
                        .EnumerateArray()
                        .Select(e => JsonSerializer.Deserialize<PromptEntry>(e.GetRawText(), readOpts)!)
                        .ToList();
                    boundPrompts = HarnessUtils.BindPrompts(queriesDict, textsDict, prompts);
                    logger.Info($"Loaded prompts: {p}");
                }

                string outDir = Path.Combine(configDir, cfg.Locations.Output.Dir, stamp);
                Directory.CreateDirectory(outDir);
                OutputWriter.WriteRunConfig(cfg, outDir);

                string secretsPath = Path.Combine(configLocDir, "secrets.json");
                Dictionary<string, string> secrets = File.Exists(secretsPath)
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(
                          File.ReadAllText(secretsPath), readOpts) ?? new()
                    : new();
                HarnessUtils.ResolveSecrets(cfg, secrets);
                logger.Info($"Loaded secrets: {secretsPath}");

                string fallbackScope = cfg.Deployments
                    .Select(a => a.Connection.TokenScope)
                    .FirstOrDefault(s => s != null)
                    ?? "https://ai.azure.com/.default";

                bool needsAzure = cfg.Deployments.Any(d =>
                    d.Mode is DeploymentMode.AzureModeApi or DeploymentMode.AzureAgentApi);
                DefaultAzureCredential? credential = needsAzure ? new DefaultAzureCredential() : null;

                bool needsHttpClient = cfg.Deployments.Any(d => d.Mode is DeploymentMode.AzureAgentApi);
                using HttpClient? http = needsHttpClient ? new HttpClient() : null;

                Dictionary<string, IDeploymentExecutor> executors = cfg.Deployments.ToDictionary(
                    d => d.Label,
                    d => d.Mode switch
                    {
                        DeploymentMode.AzureModeApi   => (IDeploymentExecutor)new AzureModeApi(d, credential!, fallbackScope, logger),
                        DeploymentMode.AzureAgentApi  => new AzureAgentApiExecutor(http!, credential!, d, fallbackScope, logger),
                        DeploymentMode.StandardOpenAI => new StandardOpenAIExecutor(d, logger),
                        _ => throw new InvalidOperationException($"Unknown deployment mode: {d.Mode}")
                    });

                logger.Info($"Deployments: {string.Join(", ", cfg.Deployments.Select(a => a.Label))}");
                logger.Info($"Bound prompts: {boundPrompts.Count} ({string.Join(", ", boundPrompts.Select(b => $"{b.TextLabel}/{b.QueryLabel}"))})");
                logger.Info($"Sets    : {cfg.Experiment.Iterations.Set}  Reps/set: {cfg.Experiment.Iterations.Rep}");
                logger.Info(new string('-', 80));

                using HarnessRunner runner = new HarnessRunner(cfg, boundPrompts, executors, outDir, logger);
                List<RunRecord> records = await runner.RunAllAsync();

                JsonSerializerOptions jsonOpts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(Path.Combine(outDir, "manifest.json"),
                    JsonSerializer.Serialize(records, jsonOpts));
                OutputWriter.WriteManifestCsv(records, Path.Combine(outDir, "manifest.csv"));

                OutputWriter.WriteIdentityGroups(records, logger);
                if (records.Any(r => r.SectionCount > 0))
                {
                    OutputWriter.WriteSummaryCountsCsv(records, Path.Combine(outDir, "summary_counts.csv"));
                    OutputWriter.WriteSummaryPctCsv(records, Path.Combine(outDir, "summary_pct.csv"));
                    OutputWriter.WriteConsoleSummary(records, logger);
                    OutputWriter.WriteConsolePctSummary(records, logger);
                }

                logger.Info($"\nOutput written to: {Path.GetFullPath(outDir)}");
                return 0;
            }
            catch (Exception ex)
            {
                logger.Error($"Fatal: {ex.Message}");
                return 1;
            }
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
