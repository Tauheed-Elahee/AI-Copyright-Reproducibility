using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Reflection;
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
            var projectDirArg = new Argument<DirectoryInfo?>("project-dir")
            {
                Description = "Project directory containing project.json. " +
                              "Defaults to the current directory.",
                Arity = ArgumentArity.ZeroOrOne
            };

            var runDirArg = new Argument<DirectoryInfo?>("run-dir")
            {
                Description = "Run directory containing manifest.json. " +
                              "Defaults to the current directory.",
                Arity = ArgumentArity.ZeroOrOne
            };

            var runDestArg = new Argument<DirectoryInfo[]>("destination")
            {
                Description = "One or more project directories to run.",
                Arity = ArgumentArity.OneOrMore,
                CustomParser = ParseDestinations
            };

            var createDestArg = new Argument<DirectoryInfo[]>("destination")
            {
                Description = "One or more directories in which to create a new project.",
                Arity = ArgumentArity.OneOrMore,
                CustomParser = ParseDestinations
            };

            var summaryCommand = new Command("summary",
                "Regenerate manifest.csv and summary CSVs from an existing manifest.json.")
            {
                runDirArg
            };

            var generateCommand = new Command("generate",
                "Generate files from an existing run.")
            {
                summaryCommand
            };

            var runCommand = new Command("run",
                "Run an experiment from one or more project directories.")
            {
                runDestArg
            };

            var createCommand = new Command("create",
                "Create a new project directory with template config files.")
            {
                createDestArg
            };

            var rootCommand = new RootCommand(
                "Run a copyright reproducibility experiment and generate summary statistics.")
            {
                projectDirArg,
                generateCommand,
                runCommand,
                createCommand
            };

            rootCommand.SetAction(async (ParseResult pr) =>
                await RunExperiment(pr.GetValue(projectDirArg)));

            summaryCommand.SetAction(async (ParseResult pr) =>
                await RunGenerateSummary(pr.GetValue(runDirArg)));

            runCommand.SetAction(async (ParseResult pr) =>
            {
                foreach (var dir in pr.GetValue(runDestArg)!)
                {
                    int code = await RunExperiment(dir);
                    if (code != 0) return code;
                }
                return 0;
            });

            createCommand.SetAction(async (ParseResult pr) =>
            {
                foreach (var dir in pr.GetValue(createDestArg)!)
                {
                    int code = await RunCreate(dir);
                    if (code != 0) return code;
                }
                return 0;
            });

            return await rootCommand.Parse(args).InvokeAsync();
        }

        private static DirectoryInfo[] ParseDestinations(
            System.CommandLine.Parsing.ArgumentResult result)
        {
            var dirs = new List<DirectoryInfo>();
            foreach (var token in result.Tokens)
            {
                string path = Path.GetFullPath(token.Value);
                if (File.Exists(path))
                    result.AddError($"'{path}' is a file. A directory path is required.");
                else
                    dirs.Add(new DirectoryInfo(path));
            }
            return dirs.ToArray();
        }

        private static async Task<int> RunExperiment(DirectoryInfo? projectDirInfo)
        {
            string projectDir;
            if (projectDirInfo != null)
            {
                projectDir = projectDirInfo.FullName;
            }
            else if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "project.json")))
            {
                projectDir = Directory.GetCurrentDirectory();
            }
            else
            {
                Console.Error.WriteLine(
                    "No project-dir given and no project.json found in the current directory.");
                return 1;
            }

            string configPath = Path.Combine(projectDir, "project.json");
            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine($"project.json not found in: {projectDir}");
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

                if (cfg.Project.Edition?.ReadOnly == true)
                {
                    Console.Error.WriteLine(
                        $"[ERROR] Project edition is marked read_only. Run aborted: {configPath}");
                    return 1;
                }

                string logDir = Path.Combine(configDir, cfg.Project.Fs.Log.Dir);
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
                string configLocDir = Path.Combine(configDir, cfg.Project.Fs.Config.Dir);

                if (cfg.Project.Fs.Config.Files?.Experiment is { } experimentFile)
                {
                    string p = AbsPath(configLocDir, experimentFile);
                    cfg.Experiment = JsonSerializer.Deserialize<ExperimentConfig>(
                        File.ReadAllText(p), readOpts)
                        ?? throw new InvalidOperationException("Failed to deserialise experiment config.");
                    logger.SetLevel(ParseLevel(cfg.Experiment.LogLevel));
                    logger.Info($"Loaded experiment: {p}");
                }

                string inputLocDir = Path.Combine(configDir, cfg.Project.Fs.Input.Dir);

                Dictionary<string, QueryConfig> queriesDict = new();
                if (cfg.Project.Fs.Input.Files?.Queries is { } queriesFile)
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

                if (cfg.Project.Fs.Config.Files?.Deployments is { } deploymentsFile)
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
                if (cfg.Project.Fs.Input.Files?.Texts is { } textsFile)
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
                if (cfg.Project.Fs.Input.Files?.Prompts is { } promptsFile)
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

                string outDir = Path.Combine(configDir, cfg.Project.Fs.Output.Dir, stamp);
                Directory.CreateDirectory(outDir);
                OutputWriter.WriteRunConfig(cfg, outDir);

                string endpointsPath = Path.Combine(configLocDir, "endpoints.json");
                EndpointsConfig endpointsCfg = File.Exists(endpointsPath)
                    ? JsonSerializer.Deserialize<EndpointsConfig>(
                          File.ReadAllText(endpointsPath), readOpts) ?? new()
                    : new();
                logger.Info($"Loaded endpoints: {endpointsPath}");

                string secretsPath = Path.Combine(configLocDir, "secrets.json");
                SecretsConfig secretsCfg = File.Exists(secretsPath)
                    ? JsonSerializer.Deserialize<SecretsConfig>(
                          File.ReadAllText(secretsPath), readOpts) ?? new()
                    : new();
                logger.Info($"Loaded secrets: {secretsPath}");

                bool needsAzure = cfg.Deployments.Any(d =>
                    d.Mode is DeploymentMode.AzureModeApi or DeploymentMode.AzureAgentApi);
                DefaultAzureCredential? credential = needsAzure ? new DefaultAzureCredential() : null;

                bool needsHttpClient = cfg.Deployments.Any(d => d.Mode is DeploymentMode.AzureAgentApi);
                using HttpClient? http = needsHttpClient ? new HttpClient() : null;

                Dictionary<string, IDeploymentExecutor> executors = cfg.Deployments.ToDictionary(
                    d => d.Label,
                    d =>
                    {
                        ResolvedConnectionConfig r = HarnessUtils.ResolveConnection(d.Connection, endpointsCfg, secretsCfg, d.Label);
                        return d.Mode switch
                        {
                            DeploymentMode.AzureModeApi   => (IDeploymentExecutor)new AzureModeApi(r, credential!, logger),
                            DeploymentMode.AzureAgentApi  => new AzureAgentApiExecutor(http!, credential!, r, logger),
                            DeploymentMode.StandardOpenAI => new StandardOpenAIExecutor(r, d.Parameters, logger),
                            _ => throw new InvalidOperationException($"Unknown deployment mode: {d.Mode}")
                        };
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

        private static async Task<int> RunGenerateSummary(DirectoryInfo? runDirInfo)
        {
            string runDir = runDirInfo?.FullName ?? Directory.GetCurrentDirectory();

            string manifestPath = Path.Combine(runDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                Console.Error.WriteLine($"manifest.json not found in: {runDir}");
                return 1;
            }

            string statsStamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            using StreamWriter statsLog = new StreamWriter(
                Path.Combine(runDir, $"stats-{statsStamp}.log"), append: false) { AutoFlush = true };
            using Logger statsLogger = new Logger(Console.Out, Console.Error, statsLog, Logger.Level.Info);

            JsonSerializerOptions manifestOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            List<RunRecord> statsRecords = JsonSerializer.Deserialize<List<RunRecord>>(
                File.ReadAllText(manifestPath), manifestOpts)
                ?? throw new InvalidOperationException("Failed to deserialise manifest.json");

            statsLogger.Info($"Loaded {statsRecords.Count} records from: {manifestPath}");

            OutputWriter.WriteManifestCsv(statsRecords, Path.Combine(runDir, "manifest.csv"));
            OutputWriter.WriteIdentityGroups(statsRecords, statsLogger);
            if (statsRecords.Any(r => r.SectionCount > 0))
            {
                OutputWriter.WriteSummaryCountsCsv(statsRecords, Path.Combine(runDir, "summary_counts.csv"));
                OutputWriter.WriteSummaryPctCsv(statsRecords, Path.Combine(runDir, "summary_pct.csv"));
                OutputWriter.WriteConsoleSummary(statsRecords, statsLogger);
                OutputWriter.WriteConsolePctSummary(statsRecords, statsLogger);
            }

            statsLogger.Info($"\nOutput written to: {Path.GetFullPath(runDir)}");
            return await Task.FromResult(0);
        }

        private static Task<int> RunCreate(DirectoryInfo destination)
        {
            if (destination.Exists && destination.EnumerateFileSystemInfos().Any())
            {
                Console.Error.WriteLine($"Directory already exists and is not empty: {destination.FullName}");
                return Task.FromResult(1);
            }

            Directory.CreateDirectory(Path.Combine(destination.FullName, "config"));
            Directory.CreateDirectory(Path.Combine(destination.FullName, "input"));
            Directory.CreateDirectory(Path.Combine(destination.FullName, "output"));
            Directory.CreateDirectory(Path.Combine(destination.FullName, "log"));

            string projectName = destination.Name.EndsWith(".project", StringComparison.OrdinalIgnoreCase)
                ? destination.Name[..^".project".Length]
                : destination.Name;
            string harnessVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";

            var projectManifest = new
            {
                project = new ProjectConfig
                {
                    Name     = projectName,
                    Author   = "",
                    Date     = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    Version  = harnessVersion,
                    Location = destination.FullName,
                    Edition  = (EditionConfig?)null,
                    Fs       = new FsConfig
                    {
                        Config = new LocationConfig { Dir = "config", Files = new FileConfig { Experiment = "experiment.json", Deployments = "deployments.json" } },
                        Input  = new LocationConfig { Dir = "input",  Files = new FileConfig { Texts = "text.json", Queries = "queries.json", Prompts = "prompts.json" } },
                        Output = new LocationConfig { Dir = "output" },
                        Log    = new LocationConfig { Dir = "log" }
                    }
                }
            };
            JsonSerializerOptions createOpts = new JsonSerializerOptions
            {
                WriteIndented          = true,
                PropertyNamingPolicy   = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters             = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
            };
            File.WriteAllText(Path.Combine(destination.FullName, "project.json"),
                JsonSerializer.Serialize(projectManifest, createOpts));
            File.WriteAllText(Path.Combine(destination.FullName, "config", "experiment.json"),     TemplateExperimentJson);
            File.WriteAllText(Path.Combine(destination.FullName, "config", "deployments.json"),    TemplateDeploymentsJson);
            File.WriteAllText(Path.Combine(destination.FullName, "config", "endpoints.template.json"), TemplateEndpointsJson);
            File.WriteAllText(Path.Combine(destination.FullName, "config", "endpoints.json"),         TemplateEndpointsJson);
            File.WriteAllText(Path.Combine(destination.FullName, "config", "secrets.template.json"),  TemplateSecretsJson);
            File.WriteAllText(Path.Combine(destination.FullName, "config", "secrets.json"),           TemplateSecretsJson);
            File.WriteAllText(Path.Combine(destination.FullName, "input", "queries.json"),            TemplateQueriesJson);
            File.WriteAllText(Path.Combine(destination.FullName, "input", "prompts.json"),            TemplatePromptsJson);
            File.WriteAllText(Path.Combine(destination.FullName, "input", "text.json"),               TemplateTextJson);

            Console.WriteLine($"Created project: {destination.FullName}");
            Console.WriteLine("Next steps:");
            Console.WriteLine($"  1. Fill in config/endpoints.json with your endpoint URLs.");
            Console.WriteLine($"  2. Fill in config/secrets.json with your API keys.");
            Console.WriteLine($"  3. Edit config/deployments.json, input/text.json, input/queries.json.");
            Console.WriteLine($"  4. harness run \"{destination.FullName}\"");
            return Task.FromResult(0);
        }

        private const string TemplateExperimentJson =
            """
            {
              "experiment": {
                "iterations": { "set": 1, "rep": 1 },
                "omit_null_fields": true,
                "log_level": "info",
                "timing": { "pause": { "run": { "set": 0, "prompt": 0, "rep": 0, "deployment": 0 } } },
                "seed": { "shuffle": { "run": { "prompt": 0, "deployment": 0 }, "content": 0 } },
                "parallel": {
                  "max_concurrency": 1,
                  "level": { "deployment": false, "rep": false, "prompt": false }
                }
              }
            }
            """;

        private const string TemplateDeploymentsJson =
            """
            {
              "deployments": [
                {
                  "label": "my-deployment",
                  "mode": "StandardOpenAI",
                  "connection": {
                    "endpoint": "https://api.openai.com",
                    "api_key": "${openai_api_key}"
                  },
                  "parameters": {
                    "model": "gpt-4o-mini",
                    "temperature": 0.0,
                    "max_completion_tokens": 1024,
                    "stream": false
                  }
                }
              ]
            }
            """;

        private const string TemplateEndpointsJson =
            """
            {
              "endpoints": {
                "my_endpoint": {
                  "url": "https://example.com/endpoint",
                  "auth": {
                    "type": "api_key",
                    "key": "my_key",
                    "header": "Authorization",
                    "scheme": "Bearer"
                  },
                  "fields": []
                }
              }
            }
            """;

        private const string TemplateSecretsJson =
            """
            {
              "keys": {
                "my_key": ""
              }
            }
            """;

        private const string TemplateQueriesJson =
            """
            {
              "queries": [
                {
                  "label": "my_query",
                  "types": [],
                  "system_message": "",
                  "user_prompt": "What is {title}?"
                }
              ]
            }
            """;

        private const string TemplatePromptsJson =
            """
            {
              "prompts": [
                {
                  "text": "my_text",
                  "queries": [
                    "my_query"
                  ]
                }
              ]
            }
            """;

        private const string TemplateTextJson =
            """
            {
              "texts": [
                {
                  "label": "my_text",
                  "content": {
                    "title": {
                      "full": "My Text Title",
                      "short": "My Text",
                      "year": 1970
                    },
                    "section_headings": [
                      "Introduction",
                      "Conclusion"
                    ],
                    "aliases": {}
                  }
                }
              ]
            }
            """;

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
