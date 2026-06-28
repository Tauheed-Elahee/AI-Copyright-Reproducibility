using System;
using System.IO;
using System.Reactive.Linq;
using System.Text.Json;
using ReactiveUI;
using AICopyrightReproducibility.Config;
using AICopyrightReproducibility.Utils;

namespace AICopyrightReproducibility.Gui.ViewModels
{
    public sealed class ExperimentSettingsViewModel : ViewModelBase
    {
        // Iterations
        private decimal _sets = 1;
        private decimal _reps = 1;

        // Parallelism
        private bool    _parallelDeployment;
        private bool    _parallelRep;
        private bool    _parallelPrompt;
        private decimal _maxConcurrency;

        // Retry
        private decimal _retryMaxAttempts   = 3;
        private decimal _retryInitialDelayS = 1;
        private decimal _retryMaxDelayS     = 60;

        // Timing pauses
        private decimal _pauseSetS;
        private decimal _pausePromptS;
        private decimal _pauseRepS;
        private decimal _pauseDeploymentS;

        // Seeds
        private decimal _seedShufflePrompt;
        private decimal _seedShuffleDeployment;
        private decimal _seedShuffleContent;

        // Output
        private string  _logLevel       = "info";
        private bool    _omitNullFields = true;

        private string? _experimentFilePath;
        private string? _saveError;

        public decimal Sets                 { get => _sets;                 set => this.RaiseAndSetIfChanged(ref _sets,                 value); }
        public decimal Reps                 { get => _reps;                 set => this.RaiseAndSetIfChanged(ref _reps,                 value); }
        public bool    ParallelDeployment   { get => _parallelDeployment;   set => this.RaiseAndSetIfChanged(ref _parallelDeployment,   value); }
        public bool    ParallelRep          { get => _parallelRep;          set => this.RaiseAndSetIfChanged(ref _parallelRep,          value); }
        public bool    ParallelPrompt       { get => _parallelPrompt;       set => this.RaiseAndSetIfChanged(ref _parallelPrompt,       value); }
        public decimal MaxConcurrency       { get => _maxConcurrency;       set => this.RaiseAndSetIfChanged(ref _maxConcurrency,       value); }
        public decimal RetryMaxAttempts     { get => _retryMaxAttempts;     set => this.RaiseAndSetIfChanged(ref _retryMaxAttempts,     value); }
        public decimal RetryInitialDelayS   { get => _retryInitialDelayS;   set => this.RaiseAndSetIfChanged(ref _retryInitialDelayS,   value); }
        public decimal RetryMaxDelayS       { get => _retryMaxDelayS;       set => this.RaiseAndSetIfChanged(ref _retryMaxDelayS,       value); }
        public decimal PauseSetS            { get => _pauseSetS;            set => this.RaiseAndSetIfChanged(ref _pauseSetS,            value); }
        public decimal PausePromptS         { get => _pausePromptS;         set => this.RaiseAndSetIfChanged(ref _pausePromptS,         value); }
        public decimal PauseRepS            { get => _pauseRepS;            set => this.RaiseAndSetIfChanged(ref _pauseRepS,            value); }
        public decimal PauseDeploymentS     { get => _pauseDeploymentS;     set => this.RaiseAndSetIfChanged(ref _pauseDeploymentS,     value); }
        public decimal SeedShufflePrompt    { get => _seedShufflePrompt;    set => this.RaiseAndSetIfChanged(ref _seedShufflePrompt,    value); }
        public decimal SeedShuffleDeployment{ get => _seedShuffleDeployment;set => this.RaiseAndSetIfChanged(ref _seedShuffleDeployment,value); }
        public decimal SeedShuffleContent   { get => _seedShuffleContent;   set => this.RaiseAndSetIfChanged(ref _seedShuffleContent,   value); }
        public string  LogLevel             { get => _logLevel;             set => this.RaiseAndSetIfChanged(ref _logLevel,             value); }
        public bool    OmitNullFields       { get => _omitNullFields;       set => this.RaiseAndSetIfChanged(ref _omitNullFields,       value); }

        public string? ExperimentFilePath
        {
            get => _experimentFilePath;
            private set
            {
                this.RaiseAndSetIfChanged(ref _experimentFilePath, value);
                this.RaisePropertyChanged(nameof(CanEdit));
            }
        }

        public bool CanEdit => _experimentFilePath != null;

        public string? SaveError
        {
            get => _saveError;
            private set => this.RaiseAndSetIfChanged(ref _saveError, value);
        }

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SaveCommand { get; }

        public static readonly string[] LogLevels = { "info", "verbose", "warning", "error" };

        public ExperimentSettingsViewModel()
        {
            var canSave = this.WhenAnyValue(x => x.ExperimentFilePath).Select(p => p != null);
            SaveCommand = ReactiveCommand.Create(ExecuteSave, canSave);
        }

        public void LoadFrom(ExperimentConfig cfg, string? filePath)
        {
            Sets                  = cfg.Iterations.Set;
            Reps                  = cfg.Iterations.Rep;
            ParallelDeployment    = cfg.Parallel.Level.Deployment;
            ParallelRep           = cfg.Parallel.Level.Rep;
            ParallelPrompt        = cfg.Parallel.Level.Prompt;
            MaxConcurrency        = cfg.Parallel.MaxConcurrency;
            RetryMaxAttempts      = cfg.Retry.MaxAttempts;
            RetryInitialDelayS    = (decimal)cfg.Retry.InitialDelayS;
            RetryMaxDelayS        = (decimal)cfg.Retry.MaxDelayS;
            PauseSetS             = cfg.Timing.Pause.Run.Set;
            PausePromptS          = cfg.Timing.Pause.Run.Prompt;
            PauseRepS             = cfg.Timing.Pause.Run.Rep;
            PauseDeploymentS      = cfg.Timing.Pause.Run.Deployment;
            SeedShufflePrompt     = cfg.Seed.Shuffle.Run.Prompt;
            SeedShuffleDeployment = cfg.Seed.Shuffle.Run.Deployment;
            SeedShuffleContent    = cfg.Seed.Shuffle.Content;
            LogLevel              = cfg.LogLevel;
            OmitNullFields        = cfg.OmitNullFields;
            ExperimentFilePath    = filePath;
            SaveError             = null;
        }

        private void ExecuteSave()
        {
            try
            {
                var cfg = new ExperimentConfig
                {
                    Iterations = new IterationsConfig
                    {
                        Set = Math.Max(1, (int)Sets),
                        Rep = Math.Max(1, (int)Reps)
                    },
                    Parallel = new ParallelConfig
                    {
                        MaxConcurrency = Math.Max(0, (int)MaxConcurrency),
                        Level = new ParallelLevelConfig
                        {
                            Deployment = ParallelDeployment,
                            Rep        = ParallelRep,
                            Prompt     = ParallelPrompt
                        }
                    },
                    Retry = new RetryConfig
                    {
                        MaxAttempts   = Math.Max(1, (int)RetryMaxAttempts),
                        InitialDelayS = Math.Max(0.0, (double)RetryInitialDelayS),
                        MaxDelayS     = Math.Max(0.0, (double)RetryMaxDelayS)
                    },
                    Timing = new TimingConfig
                    {
                        Pause = new TimingPauseConfig
                        {
                            Run = new TimingPauseRunConfig
                            {
                                Set        = Math.Max(0, (int)PauseSetS),
                                Prompt     = Math.Max(0, (int)PausePromptS),
                                Rep        = Math.Max(0, (int)PauseRepS),
                                Deployment = Math.Max(0, (int)PauseDeploymentS)
                            }
                        }
                    },
                    Seed = new SeedConfig
                    {
                        Shuffle = new SeedShuffleConfig
                        {
                            Run     = new SeedShuffleRunConfig
                            {
                                Prompt     = (int)SeedShufflePrompt,
                                Deployment = (int)SeedShuffleDeployment
                            },
                            Content = (int)SeedShuffleContent
                        }
                    },
                    LogLevel       = LogLevel,
                    OmitNullFields = OmitNullFields
                };

                var writeOpts = new JsonSerializerOptions(ProjectLoader.ReadOpts) { WriteIndented = true };
                File.WriteAllText(ExperimentFilePath!, JsonSerializer.Serialize(cfg, writeOpts));
                SaveError = null;
            }
            catch (Exception ex)
            {
                SaveError = $"Save failed: {ex.Message}";
            }
        }
    }
}
