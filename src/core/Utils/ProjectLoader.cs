using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Identity;
using AICopyrightReproducibility.Config;
using AICopyrightReproducibility.Executors;
using AICopyrightReproducibility.Executors.Azure;
using AICopyrightReproducibility.Executors.Standard;
using NuGet.Versioning;

namespace AICopyrightReproducibility.Utils
{
    public sealed class LoadedProject : IDisposable
    {
        public RunConfig                                     Config              { get; }
        public List<BoundPrompt>                             BoundPrompts        { get; }
        public Dictionary<string, IDeploymentExecutor>      Executors           { get; }
        public Dictionary<string, ResolvedConnectionConfig> ResolvedConnections { get; }
        public string                                        OutDir              { get; }
        public string                                        Stamp               { get; }

        private readonly HttpClient? _http;

        internal LoadedProject(
            RunConfig                                     config,
            List<BoundPrompt>                             boundPrompts,
            Dictionary<string, IDeploymentExecutor>      executors,
            Dictionary<string, ResolvedConnectionConfig> resolvedConnections,
            string                                        outDir,
            string                                        stamp,
            HttpClient?                                   http)
        {
            Config              = config;
            BoundPrompts        = boundPrompts;
            Executors           = executors;
            ResolvedConnections = resolvedConnections;
            OutDir              = outDir;
            Stamp               = stamp;
            _http               = http;
        }

        public void Dispose() => _http?.Dispose();
    }

    public sealed record LoadedProjectSummary(
        string              ProjectName,
        string?             Author,
        int                 DeploymentCount,
        IReadOnlyList<string> DeploymentLabels,
        int                 Sets,
        int                 Reps,
        int                 BoundPromptCount,
        int                 TotalRuns,
        bool                IsReadOnly);

    public static class ProjectLoader
    {
        public static readonly JsonSerializerOptions ReadOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
                new SemanticVersionJsonConverter()
            }
        };

        public static LoadedProjectSummary Preview(string projectDir)
        {
            string configPath = Path.Combine(projectDir, "project.json");
            RunConfig cfg = JsonSerializer.Deserialize<RunConfig>(
                File.ReadAllText(configPath), ReadOpts)
                ?? throw new InvalidOperationException("Failed to deserialise project.json.");

            string configLocDir = Path.Combine(projectDir, cfg.Project.Fs.Config.Dir);
            string inputLocDir  = Path.Combine(projectDir, cfg.Project.Fs.Input.Dir);

            ExperimentConfig experiment = cfg.Experiment;
            if (cfg.Project.Fs.Config.Files?.Experiment is { } experimentFile)
            {
                string p = AbsPath(configLocDir, experimentFile);
                if (File.Exists(p))
                    experiment = JsonSerializer.Deserialize<ExperimentConfig>(
                        File.ReadAllText(p), ReadOpts) ?? experiment;
            }

            List<DeploymentConfig> deployments = new();
            if (cfg.Project.Fs.Config.Files?.Deployments is { } deploymentsFile)
            {
                string p = AbsPath(configLocDir, deploymentsFile);
                if (File.Exists(p))
                {
                    using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(p));
                    deployments = doc.RootElement.GetProperty("deployments")
                        .EnumerateArray()
                        .Select(e => JsonSerializer.Deserialize<DeploymentConfig>(e.GetRawText(), ReadOpts)!)
                        .ToList();
                }
            }

            int boundPromptCount = 0;
            if (cfg.Project.Fs.Input.Files?.Prompts is { } promptsFile)
            {
                string p = AbsPath(inputLocDir, promptsFile);
                if (File.Exists(p))
                {
                    using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(p));
                    boundPromptCount = doc.RootElement.GetProperty("prompts").GetArrayLength();
                }
            }

            int sets  = experiment.Iterations.Set;
            int reps  = experiment.Iterations.Rep;
            int total = sets * boundPromptCount * reps * deployments.Count;

            return new LoadedProjectSummary(
                ProjectName:      cfg.Project.Name ?? System.IO.Path.GetFileName(projectDir),
                Author:           cfg.Project.Author,
                DeploymentCount:  deployments.Count,
                DeploymentLabels: deployments.Select(d => d.Label).ToList().AsReadOnly(),
                Sets:             sets,
                Reps:             reps,
                BoundPromptCount: boundPromptCount,
                TotalRuns:        total,
                IsReadOnly:       cfg.Project.Edition?.ReadOnly == true);
        }

        public static LoadedProject Load(
            string projectDir, Logger logger, string stamp, SemanticVersion? harnessVersion = null)
        {
            string configDir    = projectDir;
            string configLocDir = "";
            string inputLocDir  = "";

            RunConfig cfg = JsonSerializer.Deserialize<RunConfig>(
                File.ReadAllText(Path.Combine(projectDir, "project.json")), ReadOpts)
                ?? throw new InvalidOperationException("Failed to deserialise config.");

            if (harnessVersion is not null
                && cfg.Project.Version?.Compatible is { } compat
                && harnessVersion.CompareTo(compat) < 0)
            {
                logger.Warn($"Project requires aicr >= {compat}; running with {harnessVersion}.");
            }

            configLocDir = Path.Combine(configDir, cfg.Project.Fs.Config.Dir);
            inputLocDir  = Path.Combine(configDir, cfg.Project.Fs.Input.Dir);

            static Logger.Level ParseLevel(string? s) => s?.ToLowerInvariant() switch
            {
                "verbose" => Logger.Level.Verbose,
                "warning" => Logger.Level.Warning,
                "error"   => Logger.Level.Error,
                _         => Logger.Level.Info
            };

            if (cfg.Project.Fs.Config.Files?.Experiment is { } experimentFile)
            {
                string p = AbsPath(configLocDir, experimentFile);
                cfg.Experiment = JsonSerializer.Deserialize<ExperimentConfig>(
                    File.ReadAllText(p), ReadOpts)
                    ?? throw new InvalidOperationException("Failed to deserialise experiment config.");
                logger.SetLevel(ParseLevel(cfg.Experiment.LogLevel));
                logger.Info($"Loaded experiment: {p}");
            }

            Dictionary<string, QueryConfig> queriesDict = new();
            if (cfg.Project.Fs.Input.Files?.Queries is { } queriesFile)
            {
                string p = AbsPath(inputLocDir, queriesFile);
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(p));
                cfg.Queries = doc.RootElement.GetProperty("queries")
                    .EnumerateArray()
                    .Select(e => JsonSerializer.Deserialize<QueryConfig>(e.GetRawText(), ReadOpts)!)
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
                    .Select(e => JsonSerializer.Deserialize<DeploymentConfig>(e.GetRawText(), ReadOpts)!)
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
                    .Select(e => JsonSerializer.Deserialize<TextDbEntry>(e.GetRawText(), ReadOpts)!))
                {
                    var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    extras["full"]  = entry.Content.Title.Full;
                    extras["short"] = entry.Content.Title.Short;
                    if (entry.Content.Title.ExtraFields is not null)
                        foreach (var (k, v) in entry.Content.Title.ExtraFields)
                            HarnessUtils.FlattenJson(v, k, extras);
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
                    .Select(e => JsonSerializer.Deserialize<PromptEntry>(e.GetRawText(), ReadOpts)!)
                    .ToList();
                boundPrompts = HarnessUtils.BindPrompts(queriesDict, textsDict, prompts);
                logger.Info($"Loaded prompts: {p}");
            }

            string outDir = Path.Combine(configDir, cfg.Project.Fs.Output.Dir, stamp);
            Directory.CreateDirectory(outDir);

            string endpointsPath = Path.Combine(configLocDir, "endpoints.json");
            EndpointsConfig endpointsCfg = File.Exists(endpointsPath)
                ? JsonSerializer.Deserialize<EndpointsConfig>(
                      File.ReadAllText(endpointsPath), ReadOpts) ?? new()
                : new();
            logger.Info($"Loaded endpoints: {endpointsPath}");

            string secretsPath = Path.Combine(configLocDir, "secrets.json");
            SecretsConfig secretsCfg = File.Exists(secretsPath)
                ? JsonSerializer.Deserialize<SecretsConfig>(
                      File.ReadAllText(secretsPath), ReadOpts) ?? new()
                : new();
            logger.Info($"Loaded secrets: {secretsPath}");

            bool needsAzure = cfg.Deployments.Any(d =>
                d.Mode is DeploymentMode.AzureModeApi or DeploymentMode.AzureAgentApi);
            DefaultAzureCredential? credential = needsAzure ? new DefaultAzureCredential() : null;

            bool needsHttpClient = cfg.Deployments.Any(d => d.Mode is DeploymentMode.AzureAgentApi);
            HttpClient? http = needsHttpClient ? new HttpClient() : null;

            var resolvedConnections = new Dictionary<string, ResolvedConnectionConfig>();
            Dictionary<string, IDeploymentExecutor> executors = cfg.Deployments.ToDictionary(
                d => d.Label,
                d =>
                {
                    ResolvedConnectionConfig r = HarnessUtils.ResolveConnection(
                        d.Connection, endpointsCfg, secretsCfg, d.Label);
                    resolvedConnections[d.Label] = r;
                    return d.Mode switch
                    {
                        DeploymentMode.AzureModeApi   => (IDeploymentExecutor)new AzureModeApi(r, credential!, logger),
                        DeploymentMode.AzureAgentApi  => new AzureAgentApiExecutor(http!, credential!, r, logger),
                        DeploymentMode.StandardOpenAI => new StandardOpenAIExecutor(r, d.Parameters, logger),
                        _ => throw new InvalidOperationException($"Unknown deployment mode: {d.Mode}")
                    };
                });

            return new LoadedProject(cfg, boundPrompts, executors, resolvedConnections, outDir, stamp, http);
        }

        private static string AbsPath(string dir, string file) =>
            Path.IsPathRooted(file) ? file : Path.Combine(dir, file);
    }
}
