using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using NuGet.Versioning;
using AICopyrightReproducibility;
using AICopyrightReproducibility.Config;
using AICopyrightReproducibility.Utils;
using AICopyrightReproducibility.Gui.Services;

namespace AICopyrightReproducibility.Gui.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        public enum RunState { Idle, Running, Completed, Failed }

        private string                _projectDir       = "";
        private LoadedProjectSummary? _summary;
        private RunState              _state            = RunState.Idle;
        private int                   _progressCompleted;
        private int                   _progressTotal    = 1;
        private string?               _errorMessage;
        private string?               _outputDir;
        private string?               _currentOperation;
        private ExperimentSettingsViewModel? _settings;

        private CancellationTokenSource? _runCts;
        private Func<Task<string?>>?     _browseDelegate;

        public string ProjectDir
        {
            get => _projectDir;
            set => this.RaiseAndSetIfChanged(ref _projectDir, value);
        }

        public LoadedProjectSummary? Summary
        {
            get => _summary;
            private set => this.RaiseAndSetIfChanged(ref _summary, value);
        }

        public RunState State
        {
            get => _state;
            private set
            {
                this.RaiseAndSetIfChanged(ref _state, value);
                this.RaisePropertyChanged(nameof(IsRunning));
            }
        }

        public int ProgressCompleted
        {
            get => _progressCompleted;
            private set => this.RaiseAndSetIfChanged(ref _progressCompleted, value);
        }

        public int ProgressTotal
        {
            get => _progressTotal;
            private set => this.RaiseAndSetIfChanged(ref _progressTotal, value);
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
        }

        public string? OutputDir
        {
            get => _outputDir;
            private set => this.RaiseAndSetIfChanged(ref _outputDir, value);
        }

        public string? CurrentOperation
        {
            get => _currentOperation;
            private set => this.RaiseAndSetIfChanged(ref _currentOperation, value);
        }

        public ExperimentSettingsViewModel? Settings
        {
            get => _settings;
            private set => this.RaiseAndSetIfChanged(ref _settings, value);
        }

        public bool IsRunning  => _state == RunState.Running;
        public bool HasResults => Results.Count > 0;

        public ObservableCollection<string>              LogLines { get; } = new();
        public ObservableCollection<DeploymentResultRow> Results  { get; } = new();

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> BrowseCommand           { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> LoadConfigCommand       { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RunCommand              { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> CancelCommand           { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> OpenOutputFolderCommand { get; }

        public MainViewModel()
        {
            Results.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(HasResults));

            BrowseCommand = ReactiveCommand.CreateFromTask(ExecuteBrowse);

            var canLoad = this.WhenAnyValue(x => x.ProjectDir,
                dir => !string.IsNullOrWhiteSpace(dir));
            LoadConfigCommand = ReactiveCommand.CreateFromTask(ExecuteLoadConfig, canLoad);

            var canRun = this.WhenAnyValue(
                x => x.Summary,
                x => x.State,
                (summary, state) => summary != null && !summary.IsReadOnly &&
                    (state == RunState.Idle || state == RunState.Completed || state == RunState.Failed));
            RunCommand = ReactiveCommand.CreateFromTask(ExecuteRun, canRun);

            CancelCommand = ReactiveCommand.Create(
                () => _runCts?.Cancel(),
                this.WhenAnyValue(x => x.IsRunning));

            OpenOutputFolderCommand = ReactiveCommand.Create(
                () =>
                {
                    if (OutputDir is not null)
                        Process.Start(new ProcessStartInfo { FileName = OutputDir, UseShellExecute = true });
                },
                this.WhenAnyValue(x => x.OutputDir).Select(d => d != null));
        }

        public void SetBrowseDelegate(Func<Task<string?>> del) => _browseDelegate = del;

        private async Task ExecuteBrowse()
        {
            if (_browseDelegate == null) return;
            string? path = await _browseDelegate();
            if (!string.IsNullOrWhiteSpace(path))
                ProjectDir = path;
        }

        private async Task ExecuteLoadConfig()
        {
            Settings = null;
            try
            {
                Summary      = await Task.Run(() => ProjectLoader.Preview(ProjectDir));
                ErrorMessage = null;
                ProgressTotal = Math.Max(Summary.TotalRuns, 1);

                Settings = await Task.Run(() =>
                {
                    RunConfig cfg = JsonSerializer.Deserialize<RunConfig>(
                        File.ReadAllText(Path.Combine(ProjectDir, "project.json")),
                        ProjectLoader.ReadOpts)!;
                    string configLocDir = Path.Combine(ProjectDir, cfg.Project.Fs.Config.Dir);

                    ExperimentConfig expCfg = cfg.Experiment;
                    string? expFilePath = null;
                    if (cfg.Project.Fs.Config.Files?.Experiment is { } f)
                    {
                        string p = Path.IsPathRooted(f) ? f : Path.Combine(configLocDir, f);
                        if (File.Exists(p))
                        {
                            expCfg = JsonSerializer.Deserialize<ExperimentConfig>(
                                File.ReadAllText(p), ProjectLoader.ReadOpts) ?? expCfg;
                            expFilePath = p;
                        }
                    }

                    var vm = new ExperimentSettingsViewModel();
                    vm.LoadFrom(expCfg, expFilePath);
                    return vm;
                });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load config: {ex.Message}";
            }
        }

        private async Task ExecuteRun()
        {
            State             = RunState.Running;
            ErrorMessage      = null;
            OutputDir         = null;
            CurrentOperation  = null;
            ProgressCompleted = 0;
            ProgressTotal     = Math.Max(_summary?.TotalRuns ?? 1, 1);
            LogLines.Clear();
            Results.Clear();

            var channelWriter = new ChannelTextWriter();
            var drainTask = Task.Run(async () =>
            {
                await foreach (string line in channelWriter.Reader.ReadAllAsync())
                    await Dispatcher.UIThread.InvokeAsync(() => LogLines.Add(line));
            });

            var progress = new Progress<RunProgressEvent>(evt =>
            {
                ProgressCompleted = evt.Completed;
                ProgressTotal     = evt.Total;
                CurrentOperation  = $"{evt.LastRecord.Deployment}  ·  " +
                                    $"{evt.LastRecord.TextLabel} / {evt.LastRecord.QueryLabel}";
            });

            _runCts = new CancellationTokenSource();
            var ct = _runCts.Token;

            try
            {
                await Task.Run(async () =>
                {
                    string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",
                        System.Globalization.CultureInfo.InvariantCulture);

                    RunConfig cfgPreview = JsonSerializer.Deserialize<RunConfig>(
                        File.ReadAllText(Path.Combine(ProjectDir, "project.json")),
                        ProjectLoader.ReadOpts)!;
                    string logDir = Path.Combine(ProjectDir, cfgPreview.Project.Fs.Log.Dir);
                    Directory.CreateDirectory(logDir);

                    using StreamWriter fileWriter = new StreamWriter(
                        Path.Combine(logDir, $"aicr-{stamp}.log"), append: false) { AutoFlush = true };
                    using Logger logger = new Logger(
                        TextWriter.Null, TextWriter.Null, fileWriter, Logger.Level.Verbose);
                    logger.SetExtraWriter(channelWriter);

                    logger.Info($"═══ RUN STARTED {stamp} ═══");

                    using LoadedProject loaded = ProjectLoader.Load(ProjectDir, logger, stamp, GetGuiVersion());

                    logger.Info($"Deployments: {string.Join(", ", loaded.Config.Deployments.Select(a => a.Label))}");
                    logger.Info($"Bound prompts: {loaded.BoundPrompts.Count} " +
                        $"({string.Join(", ", loaded.BoundPrompts.Select(b => $"{b.TextLabel}/{b.QueryLabel}"))})");
                    logger.Info($"Sets: {loaded.Config.Experiment.Iterations.Set}  " +
                        $"Reps/set: {loaded.Config.Experiment.Iterations.Rep}");
                    logger.Info(new string('-', 80));

                    using HarnessRunner runner = new HarnessRunner(
                        loaded.Config, loaded.BoundPrompts, loaded.Executors,
                        loaded.OutDir, logger, progress);
                    List<RunRecord> records = await runner.RunAllAsync(ct);

                    logger.Info("═══ RUN COMPLETED ═══");

                    JsonSerializerOptions jsonOpts = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(Path.Combine(loaded.OutDir, "manifest.json"),
                        JsonSerializer.Serialize(records, jsonOpts));
                    OutputWriter.WriteManifestCsv(records, Path.Combine(loaded.OutDir, "manifest.csv"));
                    OutputWriter.WriteIdentityGroups(records, logger);
                    if (records.Any(r => r.SectionCount > 0))
                    {
                        OutputWriter.WriteSummaryCountsCsv(records,
                            Path.Combine(loaded.OutDir, "summary_counts.csv"));
                        OutputWriter.WriteSummaryPctCsv(records,
                            Path.Combine(loaded.OutDir, "summary_pct.csv"));
                        OutputWriter.WriteConsoleSummary(records, logger);
                        OutputWriter.WriteConsolePctSummary(records, logger);
                    }

                    string resolvedOutDir = Path.GetFullPath(loaded.OutDir);
                    logger.Info($"\nOutput written to: {resolvedOutDir}");

                    if (loaded.Config.Project.Location != ProjectDir)
                        loaded.Config.Project.Location = ProjectDir;
                    if (loaded.Config.Project.Version is null)
                        loaded.Config.Project.Version = new VersionConfig();
                    loaded.Config.Project.Version.LastRun = GetGuiVersion();
                    var writeBackOpts = new JsonSerializerOptions
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
                    File.WriteAllText(
                        Path.Combine(ProjectDir, "project.json"),
                        JsonSerializer.Serialize(new { project = loaded.Config.Project }, writeBackOpts));

                    var rows = records
                        .GroupBy(r => r.Deployment)
                        .Select(g => new DeploymentResultRow
                        {
                            Deployment    = g.Key,
                            SuccessCount  = g.Count(r => r.Status == 200),
                            ErrorCount    = g.Count(r => r.Status != 200),
                            AvgDurationMs = g.Any() ? g.Average(r => r.DurationMs) : 0
                        })
                        .OrderBy(r => r.Deployment)
                        .ToList();

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var row in rows) Results.Add(row);
                        OutputDir        = resolvedOutDir;
                        CurrentOperation = null;
                        State            = RunState.Completed;
                    });
                }, ct);
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CurrentOperation = null;
                    State            = RunState.Idle;
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ErrorMessage     = ex.Message;
                    CurrentOperation = null;
                    State            = RunState.Failed;
                });
            }
            finally
            {
                _runCts?.Dispose();
                _runCts = null;
                channelWriter.Dispose();
                await drainTask;
            }
        }

        private static SemanticVersion GetGuiVersion()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v != null
                ? new SemanticVersion(v.Major, v.Minor, v.Build)
                : new SemanticVersion(0, 0, 0);
        }
    }
}
