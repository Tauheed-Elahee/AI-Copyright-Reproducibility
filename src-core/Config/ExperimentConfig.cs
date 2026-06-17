namespace AICopyrightReproducibility.Config
{
    public sealed class IterationsConfig
    {
        public int Set { get; set; } = 1;
        public int Rep { get; set; } = 1;
    }

    public sealed class TimingPauseRunConfig
    {
        public int Set        { get; set; } = 0;
        public int Prompt     { get; set; } = 0;
        public int Rep        { get; set; } = 0;
        public int Deployment { get; set; } = 0;
    }

    public sealed class TimingPauseConfig
    {
        public TimingPauseRunConfig Run { get; set; } = new();
    }

    public sealed class TimingConfig
    {
        public TimingPauseConfig Pause { get; set; } = new();
    }

    public sealed class SeedShuffleRunConfig
    {
        public int Prompt     { get; set; } = 0;
        public int Deployment { get; set; } = 0;
    }

    public sealed class SeedShuffleConfig
    {
        public SeedShuffleRunConfig Run     { get; set; } = new();
        public int                  Content { get; set; } = 0;
    }

    public sealed class SeedConfig
    {
        public SeedShuffleConfig Shuffle { get; set; } = new();
    }

    public sealed class RetryConfig
    {
        public int    MaxAttempts   { get; set; } = 3;
        public double InitialDelayS { get; set; } = 1.0;
        public double MaxDelayS     { get; set; } = 60.0;
    }

    public sealed class ParallelLevelConfig
    {
        public bool Deployment { get; set; } = false;
        public bool Rep        { get; set; } = false;
        public bool Prompt     { get; set; } = false;
    }

    public sealed class ParallelConfig
    {
        public int                 MaxConcurrency { get; set; } = 0;
        public ParallelLevelConfig Level          { get; set; } = new();
    }

    public sealed class FileConfig
    {
        public string? Experiment  { get; set; }
        public string? Queries     { get; set; }
        public string? Texts       { get; set; }
        public string? Prompts     { get; set; }
        public string? Deployments { get; set; }
    }

    public sealed class LocationConfig
    {
        public string      Dir   { get; set; } = "";
        public FileConfig? Files { get; set; }
    }

    public sealed class LocationsConfig
    {
        public LocationConfig Config { get; set; } = new() { Dir = "config" };
        public LocationConfig Input  { get; set; } = new() { Dir = "input"  };
        public LocationConfig Output { get; set; } = new() { Dir = "output" };
        public LocationConfig Log    { get; set; } = new() { Dir = "log"    };
    }

    public sealed class ExperimentConfig
    {
        public IterationsConfig Iterations     { get; set; } = new();
        public bool             OmitNullFields { get; set; } = true;
        public ParallelConfig   Parallel       { get; set; } = new();
        public RetryConfig      Retry          { get; set; } = new();
        public TimingConfig     Timing         { get; set; } = new();
        public SeedConfig       Seed           { get; set; } = new();
        public string?          CapturedUtc    { get; set; }
        public string           LogLevel       { get; set; } = "info";
    }
}
