using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Reflection;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AICopyrightReproducibility.Config;
using AICopyrightReproducibility.Utils;
using NuGet.Versioning;

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

            var lockDestArg = new Argument<DirectoryInfo[]>("destination")
            {
                Description = "One or more project directories to lock.",
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

            var lockCommand = new Command("lock",
                "Lock a project to prevent further runs (sets edition.read_only = true).")
            {
                lockDestArg
            };

            var versionCommand = new Command("version", "Print the aicr version.");

            var rootCommand = new RootCommand(
                "Run a copyright reproducibility experiment and generate summary statistics.")
            {
                projectDirArg,
                generateCommand,
                runCommand,
                createCommand,
                lockCommand,
                versionCommand
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

            versionCommand.SetAction((ParseResult _) =>
            {
                Console.WriteLine($"aicr {GetHarnessVersion()}");
                return 0;
            });

            lockCommand.SetAction((ParseResult pr) =>
            {
                foreach (var dir in pr.GetValue(lockDestArg)!)
                {
                    int code = RunLock(dir);
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

            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

            RunConfig cfgPreview;
            StreamWriter logWriter;
            try
            {
                cfgPreview = JsonSerializer.Deserialize<RunConfig>(
                    File.ReadAllText(configPath), ProjectLoader.ReadOpts)
                    ?? throw new InvalidOperationException("Failed to deserialise config.");

                if (cfgPreview.Project.Edition?.ReadOnly == true)
                {
                    Console.Error.WriteLine(
                        $"[ERROR] Project edition is marked read_only. Run aborted: {configPath}");
                    return 1;
                }

                string logDir = Path.Combine(projectDir, cfgPreview.Project.Fs.Log.Dir);
                Directory.CreateDirectory(logDir);
                logWriter = new StreamWriter(
                    Path.Combine(logDir, $"aicr-{stamp}.log"), append: false) { AutoFlush = true };
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
                    Path.Combine(sysLogDir, $"aicr-{stamp}.log"), append: false) { AutoFlush = true };
            }
            catch { /* system log unavailable (e.g. read-only environment); continue without it */ }

            using Logger logger = new Logger(Console.Out, Console.Error, logWriter,
                                             Logger.Level.Info, sysLogWriter);
            logger.Info($"Loaded config : {configPath}");

            SemanticVersion harnessVersion = GetHarnessVersion();

            try
            {
                using LoadedProject loaded = ProjectLoader.Load(projectDir, logger, stamp, harnessVersion);

                OutputWriter.WriteRunConfig(new RunSnapshot
                {
                    CapturedUtc = DateTimeOffset.UtcNow,
                    Project     = new ProjectSnapshot
                    {
                        Name   = loaded.Config.Project.Name,
                        Author = loaded.Config.Project.Author,
                        Date   = loaded.Config.Project.Date
                    },
                    Experiment  = loaded.Config.Experiment,
                    Queries     = loaded.Config.Queries,
                    Deployments = loaded.Config.Deployments.Select(d =>
                    {
                        ResolvedConnectionConfig r = loaded.ResolvedConnections[d.Label];
                        return new DeploymentSnapshot
                        {
                            Label      = d.Label,
                            Mode       = d.Mode,
                            Url        = r.Url,
                            AuthType   = r.AuthType,
                            TokenScope = r.TokenScope,
                            AuthHeader = r.AuthHeader,
                            AuthScheme = r.AuthScheme,
                            Parameters = d.Parameters
                        };
                    }).ToList()
                }, loaded.OutDir);

                logger.Info($"Deployments: {string.Join(", ", loaded.Config.Deployments.Select(a => a.Label))}");
                logger.Info($"Bound prompts: {loaded.BoundPrompts.Count} ({string.Join(", ", loaded.BoundPrompts.Select(b => $"{b.TextLabel}/{b.QueryLabel}"))})");
                logger.Info($"Sets    : {loaded.Config.Experiment.Iterations.Set}  Reps/set: {loaded.Config.Experiment.Iterations.Rep}");
                logger.Info(new string('-', 80));

                using HarnessRunner runner = new HarnessRunner(
                    loaded.Config, loaded.BoundPrompts, loaded.Executors, loaded.OutDir, logger);
                List<RunRecord> records = await runner.RunAllAsync();

                JsonSerializerOptions jsonOpts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(Path.Combine(loaded.OutDir, "manifest.json"),
                    JsonSerializer.Serialize(records, jsonOpts));
                OutputWriter.WriteManifestCsv(records, Path.Combine(loaded.OutDir, "manifest.csv"));

                OutputWriter.WriteIdentityGroups(records, logger);
                if (records.Any(r => r.SectionCount > 0))
                {
                    OutputWriter.WriteSummaryCountsCsv(records, Path.Combine(loaded.OutDir, "summary_counts.csv"));
                    OutputWriter.WriteSummaryPctCsv(records, Path.Combine(loaded.OutDir, "summary_pct.csv"));
                    OutputWriter.WriteConsoleSummary(records, logger);
                    OutputWriter.WriteConsolePctSummary(records, logger);
                }

                logger.Info($"\nOutput written to: {Path.GetFullPath(loaded.OutDir)}");

                if (loaded.Config.Project.Location != projectDir)
                {
                    logger.Info($"Updated location: {loaded.Config.Project.Location} -> {projectDir}");
                    loaded.Config.Project.Location = projectDir;
                }
                if (loaded.Config.Project.Version is null)
                    loaded.Config.Project.Version = new VersionConfig();
                loaded.Config.Project.Version.LastRun = harnessVersion;
                JsonSerializerOptions writeBackOpts = new JsonSerializerOptions
                {
                    WriteIndented          = true,
                    PropertyNamingPolicy   = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters =
                    {
                        new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
                        new SemanticVersionJsonConverter()
                    }
                };
                File.WriteAllText(configPath,
                    JsonSerializer.Serialize(new { project = loaded.Config.Project }, writeBackOpts));
                logger.Info($"Updated last_run in: {configPath}");

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
            SemanticVersion harnessVersion = GetHarnessVersion();

            var projectManifest = new
            {
                project = new ProjectConfig
                {
                    Name     = projectName,
                    Author   = "",
                    Date     = DateOnly.FromDateTime(DateTime.UtcNow),
                    Version  = new VersionConfig { Created = harnessVersion, Compatible = harnessVersion },
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
                Converters =
                {
                    new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
                    new SemanticVersionJsonConverter()
                }
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
            Console.WriteLine($"  4. aicr run \"{destination.FullName}\"");
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

        private static int RunLock(DirectoryInfo destination)
        {
            string projectPath = Path.Combine(destination.FullName, "project.json");
            if (!File.Exists(projectPath))
            {
                Console.Error.WriteLine($"project.json not found in: {destination.FullName}");
                return 1;
            }

            RunConfig cfg = JsonSerializer.Deserialize<RunConfig>(
                File.ReadAllText(projectPath), ProjectLoader.ReadOpts)
                ?? throw new InvalidOperationException("Failed to deserialise project.json");

            if (cfg.Project.Edition is null)
                cfg.Project.Edition = new EditionConfig { Number = 0, Date = DateOnly.FromDateTime(DateTime.UtcNow) };
            cfg.Project.Edition.ReadOnly = true;

            JsonSerializerOptions writeOpts = new JsonSerializerOptions
            {
                WriteIndented          = true,
                PropertyNamingPolicy   = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters =
                {
                    new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
                    new SemanticVersionJsonConverter()
                }
            };

            File.WriteAllText(projectPath,
                JsonSerializer.Serialize(new { project = cfg.Project }, writeOpts));
            Console.WriteLine($"Locked: {projectPath}");
            return 0;
        }

        private static SemanticVersion GetHarnessVersion()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v != null
                ? new SemanticVersion(v.Major, v.Minor, v.Build)
                : new SemanticVersion(0, 0, 0);
        }

    }
}
