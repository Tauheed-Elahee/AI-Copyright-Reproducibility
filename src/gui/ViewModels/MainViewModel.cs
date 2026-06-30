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
    public sealed record RecentProjectEntry(string Path, string Name, DateTimeOffset LastOpened);

    public sealed record RunOption(string Label, string? OutputDirectory)
    {
        public bool IsCurrent => OutputDirectory is null;
    }

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
        private int                   _selectedTabIndex = 0;

        private bool _showVerbose = true;
        private bool _showInfo    = true;
        private bool _showWarning = true;
        private bool _showError   = true;

        private readonly List<LogLine> _allLogLines = new();

        private List<DeploymentResultRow> _currentRunResults    = new();
        private string?                   _currentRunOutputDir;
        private RunOption?                _selectedRunOption;

        private CancellationTokenSource? _runCts;
        private Func<Task<string?>>?     _browseDelegate;

        private static string RecentFilePath =>
            System.IO.Path.Combine(AppPaths.StateDir, "recent.json");

        public string ProjectDir
        {
            get => _projectDir;
            set => this.RaiseAndSetIfChanged(ref _projectDir, value);
        }

        public LoadedProjectSummary? Summary
        {
            get => _summary;
            private set
            {
                this.RaiseAndSetIfChanged(ref _summary, value);
                this.RaisePropertyChanged(nameof(HasProject));
            }
        }

        public RunState State
        {
            get => _state;
            private set
            {
                this.RaiseAndSetIfChanged(ref _state, value);
                this.RaisePropertyChanged(nameof(IsRunning));
                this.RaisePropertyChanged(nameof(IsOutputAvailable));
                this.RaisePropertyChanged(nameof(RunCompleted));
                this.RaisePropertyChanged(nameof(IsRunCompleted));
                this.RaisePropertyChanged(nameof(IsRunFailed));
                this.RaisePropertyChanged(nameof(WindowTitle));
            }
        }

        public int ProgressCompleted
        {
            get => _progressCompleted;
            private set
            {
                this.RaiseAndSetIfChanged(ref _progressCompleted, value);
                this.RaisePropertyChanged(nameof(WindowTitle));
            }
        }

        public int ProgressTotal
        {
            get => _progressTotal;
            private set
            {
                this.RaiseAndSetIfChanged(ref _progressTotal, value);
                this.RaisePropertyChanged(nameof(WindowTitle));
            }
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

        private DeploymentsViewModel? _deployments;
        private SecretsViewModel?     _secrets;
        private InputsViewModel?      _inputs;

        public DeploymentsViewModel? Deployments
        {
            get => _deployments;
            private set => this.RaiseAndSetIfChanged(ref _deployments, value);
        }

        public SecretsViewModel? Secrets
        {
            get => _secrets;
            private set => this.RaiseAndSetIfChanged(ref _secrets, value);
        }

        public InputsViewModel? Inputs
        {
            get => _inputs;
            private set => this.RaiseAndSetIfChanged(ref _inputs, value);
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
                this.RaisePropertyChanged(nameof(IsOpenPage));
                this.RaisePropertyChanged(nameof(IsExperimentsPage));
                this.RaisePropertyChanged(nameof(IsDeploymentsPage));
                this.RaisePropertyChanged(nameof(IsSecretsPage));
                this.RaisePropertyChanged(nameof(IsInputsPage));
                this.RaisePropertyChanged(nameof(IsRunPage));
                this.RaisePropertyChanged(nameof(IsOutputPage));
                this.RaisePropertyChanged(nameof(IsSettingsPage));
                this.RaisePropertyChanged(nameof(IsLicensePage));
                this.RaisePropertyChanged(nameof(IsAboutPage));
            }
        }

        public bool IsOpenPage        => _selectedTabIndex == 0;
        public bool IsExperimentsPage => _selectedTabIndex == 1;
        public bool IsDeploymentsPage => _selectedTabIndex == 2;
        public bool IsSecretsPage     => _selectedTabIndex == 3;
        public bool IsInputsPage      => _selectedTabIndex == 4;
        public bool IsRunPage         => _selectedTabIndex == 5;
        public bool IsOutputPage      => _selectedTabIndex == 6;
        public bool IsSettingsPage    => _selectedTabIndex == 7;
        public bool IsLicensePage     => _selectedTabIndex == 8;
        public bool IsAboutPage       => _selectedTabIndex == 9;

        public bool ShowVerbose
        {
            get => _showVerbose;
            set { this.RaiseAndSetIfChanged(ref _showVerbose, value); ApplyLogFilter(); }
        }

        public bool ShowInfo
        {
            get => _showInfo;
            set { this.RaiseAndSetIfChanged(ref _showInfo, value); ApplyLogFilter(); }
        }

        public bool ShowWarning
        {
            get => _showWarning;
            set { this.RaiseAndSetIfChanged(ref _showWarning, value); ApplyLogFilter(); }
        }

        public bool ShowError
        {
            get => _showError;
            set { this.RaiseAndSetIfChanged(ref _showError, value); ApplyLogFilter(); }
        }

        public bool IsRunning         => _state == RunState.Running;
        public bool HasResults        => Results.Count > 0;
        public bool HasProject        => _summary != null;
        public bool IsOutputAvailable => _state != RunState.Idle || AvailableRuns.Count > 1;
        public bool HasRunSelector    => AvailableRuns.Count > 1;
        public bool RunCompleted      => _state == RunState.Completed || _state == RunState.Failed;
        public bool IsRunCompleted    => _state == RunState.Completed;
        public bool IsRunFailed       => _state == RunState.Failed;
        public bool HasRecentProjects => RecentProjects.Count > 0;

        public string AppVersion { get; } = GetGuiVersion().ToString();

        public string WindowTitle => _state switch
        {
            RunState.Running   => $"aicr — {(_progressTotal > 0 ? 100 * _progressCompleted / _progressTotal : 0)}% ({_progressCompleted}/{_progressTotal})",
            RunState.Completed => "aicr — Run complete",
            RunState.Failed    => "aicr — Run failed",
            _                  => "aicr — AI Copyright Reproducibility"
        };

        public ObservableCollection<LogLine>             LogLines       { get; } = new();
        public ObservableCollection<DeploymentResultRow> Results        { get; } = new();
        public ObservableCollection<RecentProjectEntry>  RecentProjects { get; } = new();
        public ObservableCollection<RunOption>           AvailableRuns  { get; } = new();
        public ChartsViewModel                           Charts         { get; } = new();

        public RunOption? SelectedRunOption
        {
            get => _selectedRunOption;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedRunOption, value);
                Results.Clear();
                if (value is null || value.IsCurrent)
                {
                    OutputDir = _currentRunOutputDir;
                    foreach (var row in _currentRunResults) Results.Add(row);
                    Charts.LoadFrom(_currentRunResults);
                }
                else
                {
                    OutputDir = value.OutputDirectory;
                    Charts.LoadFrom(System.Array.Empty<DeploymentResultRow>());
                    _ = LoadHistoricalRunAsync(value.OutputDirectory!);
                }
            }
        }

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> BrowseCommand           { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> LoadConfigCommand       { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RunCommand              { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> CancelCommand           { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> OpenOutputFolderCommand { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> GoToOutputCommand       { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SelectAllLogsCommand    { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SelectNoneLogsCommand   { get; }
        public ReactiveCommand<string, System.Reactive.Unit>               OpenRecentCommand       { get; }

        public MainViewModel()
        {
            Results.CollectionChanged        += (_, _) => this.RaisePropertyChanged(nameof(HasResults));
            RecentProjects.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(HasRecentProjects));
            AvailableRuns.CollectionChanged  += (_, _) =>
            {
                this.RaisePropertyChanged(nameof(IsOutputAvailable));
                this.RaisePropertyChanged(nameof(HasRunSelector));
            };

            LoadRecentProjects();

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

            GoToOutputCommand     = ReactiveCommand.Create(() => { SelectedTabIndex = 6; });
            SelectAllLogsCommand  = ReactiveCommand.Create(() => SetAllFilters(true));
            SelectNoneLogsCommand = ReactiveCommand.Create(() => SetAllFilters(false));
            OpenRecentCommand     = ReactiveCommand.CreateFromTask<string>(ExecuteOpenRecent);
        }

        public void SetBrowseDelegate(Func<Task<string?>> del) => _browseDelegate = del;

        private async Task ExecuteBrowse()
        {
            if (_browseDelegate == null) return;
            string? path = await _browseDelegate();
            if (!string.IsNullOrWhiteSpace(path))
                ProjectDir = path;
        }

        private async Task ExecuteOpenRecent(string path)
        {
            ProjectDir = path;
            await ExecuteLoadConfig();
        }

        private async Task ExecuteLoadConfig()
        {
            Settings = null;
            try
            {
                Summary      = await Task.Run(() => ProjectLoader.Preview(ProjectDir));
                ErrorMessage = null;
                ProgressTotal = Math.Max(Summary.TotalRuns, 1);

                var result = await Task.Run(() =>
                {
                    RunConfig cfg = JsonSerializer.Deserialize<RunConfig>(
                        File.ReadAllText(System.IO.Path.Combine(ProjectDir, "project.json")),
                        ProjectLoader.ReadOpts)!;
                    string configLocDir = System.IO.Path.Combine(ProjectDir, cfg.Project.Fs.Config.Dir);
                    string inputLocDir  = System.IO.Path.Combine(ProjectDir, cfg.Project.Fs.Input.Dir);

                    // ── Experiment settings ───────────────────────────────────
                    ExperimentConfig expCfg = cfg.Experiment;
                    string? expFilePath = null;
                    if (cfg.Project.Fs.Config.Files?.Experiment is { } ef)
                    {
                        string p = System.IO.Path.IsPathRooted(ef) ? ef : System.IO.Path.Combine(configLocDir, ef);
                        if (File.Exists(p))
                        {
                            expCfg = JsonSerializer.Deserialize<ExperimentConfig>(
                                File.ReadAllText(p), ProjectLoader.ReadOpts) ?? expCfg;
                            expFilePath = p;
                        }
                    }
                    var settingsVm = new ExperimentSettingsViewModel();
                    settingsVm.LoadFrom(expCfg, expFilePath);

                    // ── Deployments ───────────────────────────────────────────
                    List<DeploymentConfig> deployments = cfg.Deployments;
                    if (cfg.Project.Fs.Config.Files?.Deployments is { } df)
                    {
                        string p = System.IO.Path.IsPathRooted(df) ? df : System.IO.Path.Combine(configLocDir, df);
                        if (File.Exists(p))
                        {
                            using var doc = JsonDocument.Parse(File.ReadAllText(p));
                            deployments = doc.RootElement.GetProperty("deployments")
                                .EnumerateArray()
                                .Select(e => JsonSerializer.Deserialize<DeploymentConfig>(
                                    e.GetRawText(), ProjectLoader.ReadOpts)!)
                                .ToList();
                        }
                    }

                    // ── Secrets ───────────────────────────────────────────────
                    string secretsPath = System.IO.Path.Combine(configLocDir, "secrets.json");
                    SecretsConfig secretsCfg = File.Exists(secretsPath)
                        ? JsonSerializer.Deserialize<SecretsConfig>(
                              File.ReadAllText(secretsPath), ProjectLoader.ReadOpts) ?? new()
                        : new();

                    // ── Queries ───────────────────────────────────────────────
                    List<QueryConfig> queries     = cfg.Queries;
                    string?           queriesFile = null;
                    if (cfg.Project.Fs.Input.Files?.Queries is { } qf)
                    {
                        string p = System.IO.Path.IsPathRooted(qf) ? qf : System.IO.Path.Combine(inputLocDir, qf);
                        if (File.Exists(p))
                        {
                            using var doc = JsonDocument.Parse(File.ReadAllText(p));
                            queries = doc.RootElement.GetProperty("queries")
                                .EnumerateArray()
                                .Select(e => JsonSerializer.Deserialize<QueryConfig>(
                                    e.GetRawText(), ProjectLoader.ReadOpts)!)
                                .ToList();
                            queriesFile = p;
                        }
                    }

                    // ── Texts ─────────────────────────────────────────────────
                    List<TextDbEntry> texts    = new();
                    string?           textsFile = null;
                    if (cfg.Project.Fs.Input.Files?.Texts is { } tf)
                    {
                        string p = System.IO.Path.IsPathRooted(tf) ? tf : System.IO.Path.Combine(inputLocDir, tf);
                        if (File.Exists(p))
                        {
                            using var doc = JsonDocument.Parse(File.ReadAllText(p));
                            texts = doc.RootElement.GetProperty("texts")
                                .EnumerateArray()
                                .Select(e => JsonSerializer.Deserialize<TextDbEntry>(
                                    e.GetRawText(), ProjectLoader.ReadOpts)!)
                                .ToList();
                            textsFile = p;
                        }
                    }

                    // ── Prompts ───────────────────────────────────────────────
                    List<PromptEntry> prompts = new();
                    if (cfg.Project.Fs.Input.Files?.Prompts is { } pf)
                    {
                        string p = System.IO.Path.IsPathRooted(pf) ? pf : System.IO.Path.Combine(inputLocDir, pf);
                        if (File.Exists(p))
                        {
                            using var doc = JsonDocument.Parse(File.ReadAllText(p));
                            prompts = doc.RootElement.GetProperty("prompts")
                                .EnumerateArray()
                                .Select(e => JsonSerializer.Deserialize<PromptEntry>(
                                    e.GetRawText(), ProjectLoader.ReadOpts)!)
                                .ToList();
                        }
                    }

                    // ── Output dir scan ───────────────────────────────────────
                    string outputBase = System.IO.Path.Combine(ProjectDir, cfg.Project.Fs.Output.Dir);
                    List<RunOption> runs = ScanForPreviousRuns(outputBase);

                    return new
                    {
                        Settings     = settingsVm,
                        PreviousRuns = runs,
                        Deployments  = deployments,
                        Secrets      = secretsCfg,
                        Queries      = queries,
                        QueriesFile  = queriesFile,
                        Texts        = texts,
                        TextsFile    = textsFile,
                        Prompts      = prompts
                    };
                });

                Settings = result.Settings;

                var deploymentsVm = new DeploymentsViewModel();
                deploymentsVm.LoadFrom(result.Deployments);
                Deployments = deploymentsVm;

                var secretsVm = new SecretsViewModel();
                secretsVm.LoadFrom(result.Secrets);
                Secrets = secretsVm;

                var inputsVm = new InputsViewModel();
                inputsVm.LoadFrom(result.Queries, result.QueriesFile, result.Texts, result.TextsFile, result.Prompts);
                Inputs = inputsVm;

                _currentRunResults   = new List<DeploymentResultRow>();
                _currentRunOutputDir = null;
                AvailableRuns.Clear();
                AvailableRuns.Add(new RunOption("Current run", null));
                foreach (var run in result.PreviousRuns) AvailableRuns.Add(run);
                SelectedRunOption = AvailableRuns[0];

                SaveRecentProjects();
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
            _allLogLines.Clear();
            Results.Clear();

            var channelWriter = new ChannelTextWriter();
            var drainTask = Task.Run(async () =>
            {
                await foreach (string text in channelWriter.Reader.ReadAllAsync())
                {
                    var line = new LogLine(text, ParseLevel(text));
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _allLogLines.Add(line);
                        if (PassesFilter(line)) LogLines.Add(line);
                    });
                }
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
                        File.ReadAllText(System.IO.Path.Combine(ProjectDir, "project.json")),
                        ProjectLoader.ReadOpts)!;
                    string logDir = System.IO.Path.Combine(ProjectDir, cfgPreview.Project.Fs.Log.Dir);
                    Directory.CreateDirectory(logDir);

                    using StreamWriter fileWriter = new StreamWriter(
                        System.IO.Path.Combine(logDir, $"aicr-{stamp}.log"), append: false) { AutoFlush = true };
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
                    File.WriteAllText(System.IO.Path.Combine(loaded.OutDir, "manifest.json"),
                        JsonSerializer.Serialize(records, jsonOpts));
                    OutputWriter.WriteManifestCsv(records, System.IO.Path.Combine(loaded.OutDir, "manifest.csv"));
                    OutputWriter.WriteIdentityGroups(records, logger);
                    if (records.Any(r => r.SectionCount > 0))
                    {
                        OutputWriter.WriteSummaryCountsCsv(records,
                            System.IO.Path.Combine(loaded.OutDir, "summary_counts.csv"));
                        OutputWriter.WriteSummaryPctCsv(records,
                            System.IO.Path.Combine(loaded.OutDir, "summary_pct.csv"));
                        OutputWriter.WriteConsoleSummary(records, logger);
                        OutputWriter.WriteConsolePctSummary(records, logger);
                    }

                    string resolvedOutDir = System.IO.Path.GetFullPath(loaded.OutDir);
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
                        System.IO.Path.Combine(ProjectDir, "project.json"),
                        JsonSerializer.Serialize(new { project = loaded.Config.Project }, writeBackOpts));

                    var rows = records
                        .GroupBy(r => r.Deployment)
                        .Select(g => new DeploymentResultRow
                        {
                            Deployment    = g.Key,
                            SuccessCount  = g.Count(r => r.Status == 200),
                            ErrorCount    = g.Count(r => r.Status != 200),
                            AvgDurationMs = g.Any() ? g.Average(r => r.DurationMs) : 0,
                            Durations     = g.Select(r => r.DurationMs).ToArray()
                        })
                        .OrderBy(r => r.Deployment)
                        .ToList();

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _currentRunResults   = rows;
                        _currentRunOutputDir = resolvedOutDir;
                        AvailableRuns.Insert(1, new RunOption(FormatStamp(stamp), resolvedOutDir));

                        foreach (var row in rows) Results.Add(row);
                        Charts.LoadFrom(rows);
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

        // ── Log filter helpers ────────────────────────────────────────────────

        private static Logger.Level ParseLevel(string line)
        {
            if (line.Contains("[VERBOSE]")) return Logger.Level.Verbose;
            if (line.Contains("[ERROR]"))   return Logger.Level.Error;
            if (line.Contains("[WARN]"))    return Logger.Level.Warning;
            return Logger.Level.Info;
        }

        private bool PassesFilter(LogLine line) => line.Level switch
        {
            Logger.Level.Verbose => _showVerbose,
            Logger.Level.Info    => _showInfo,
            Logger.Level.Warning => _showWarning,
            Logger.Level.Error   => _showError,
            _                    => true
        };

        private void SetAllFilters(bool value)
        {
            _showVerbose = value;
            _showInfo    = value;
            _showWarning = value;
            _showError   = value;
            this.RaisePropertyChanged(nameof(ShowVerbose));
            this.RaisePropertyChanged(nameof(ShowInfo));
            this.RaisePropertyChanged(nameof(ShowWarning));
            this.RaisePropertyChanged(nameof(ShowError));
            ApplyLogFilter();
        }

        private void ApplyLogFilter()
        {
            LogLines.Clear();
            foreach (var line in _allLogLines)
                if (PassesFilter(line)) LogLines.Add(line);
        }

        // ── Recent projects ───────────────────────────────────────────────────

        private void LoadRecentProjects()
        {
            try
            {
                string file = RecentFilePath;
                if (!File.Exists(file)) return;
                var entries = JsonSerializer.Deserialize<List<RecentProjectEntry>>(
                    File.ReadAllText(file)) ?? new();
                foreach (var e in entries.Take(10))
                    RecentProjects.Add(e);
            }
            catch { /* silently ignore — recent list is non-critical */ }
        }

        private void SaveRecentProjects()
        {
            try
            {
                var entry = new RecentProjectEntry(
                    ProjectDir,
                    _summary?.ProjectName ?? System.IO.Path.GetFileName(ProjectDir) ?? ProjectDir,
                    DateTimeOffset.UtcNow);

                var entries = RecentProjects
                    .Where(e => !string.Equals(e.Path, ProjectDir, StringComparison.OrdinalIgnoreCase))
                    .Prepend(entry)
                    .Take(10)
                    .ToList();

                string file = RecentFilePath;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
                File.WriteAllText(file, JsonSerializer.Serialize(entries));

                RecentProjects.Clear();
                foreach (var e in entries) RecentProjects.Add(e);
            }
            catch { /* silently ignore */ }
        }

        // ── Output history helpers ────────────────────────────────────────────

        private static List<RunOption> ScanForPreviousRuns(string outputBase)
        {
            var runs = new List<RunOption>();
            if (!Directory.Exists(outputBase)) return runs;

            foreach (string dir in Directory.GetDirectories(outputBase).OrderByDescending(d => d))
            {
                string name = System.IO.Path.GetFileName(dir);
                if (DateTime.TryParseExact(name, "yyyyMMdd-HHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _))
                    runs.Add(new RunOption(FormatStamp(name), dir));
            }
            return runs;
        }

        private static string FormatStamp(string stamp)
        {
            if (DateTime.TryParseExact(stamp, "yyyyMMdd-HHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
                return dt.ToString("yyyy-MM-dd HH:mm:ss");
            return stamp;
        }

        private async Task LoadHistoricalRunAsync(string outDir)
        {
            try
            {
                string manifestPath = System.IO.Path.Combine(outDir, "manifest.json");
                if (!File.Exists(manifestPath)) return;

                var rows = await Task.Run(() =>
                {
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var records = JsonSerializer.Deserialize<List<RunRecord>>(
                        File.ReadAllText(manifestPath), opts) ?? new();
                    return records
                        .GroupBy(r => r.Deployment)
                        .Select(g => new DeploymentResultRow
                        {
                            Deployment    = g.Key,
                            SuccessCount  = g.Count(r => r.Status == 200),
                            ErrorCount    = g.Count(r => r.Status != 200),
                            AvgDurationMs = g.Any() ? g.Average(r => r.DurationMs) : 0,
                            Durations     = g.Select(r => r.DurationMs).ToArray()
                        })
                        .OrderBy(r => r.Deployment)
                        .ToList();
                });

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_selectedRunOption?.OutputDirectory == outDir)
                    {
                        Results.Clear();
                        foreach (var row in rows) Results.Add(row);
                        Charts.LoadFrom(rows);
                    }
                });
            }
            catch { /* silently ignore corrupt or missing manifests */ }
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
