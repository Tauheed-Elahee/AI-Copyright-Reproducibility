using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AICopyrightReproducibility.Config;
using AICopyrightReproducibility.Executors;
using AICopyrightReproducibility.Utils;

namespace AICopyrightReproducibility
{
    public record RunProgressEvent(int Completed, int Total, RunRecord LastRecord);

    public sealed class HarnessRunner : IDisposable
    {
        private readonly List<BoundPrompt> _boundPrompts;
        private readonly Dictionary<string, IDeploymentExecutor> _executors;
        private readonly string _outDir;
        private readonly IterationsConfig _iterations;
        private readonly bool _omitNullFields;
        private readonly RetryConfig _retry;
        private readonly List<DeploymentConfig> _deployments;
        private readonly SeedConfig _seed;
        private readonly TimingPauseRunConfig _pause;
        private readonly ParallelConfig _par;
        private readonly SemaphoreSlim? _sem;
        private readonly Logger _logger;
        private readonly IProgress<RunProgressEvent>? _progress;
        private readonly int _total;
        private int _completed;

        public HarnessRunner(
            RunConfig cfg,
            List<BoundPrompt> boundPrompts,
            Dictionary<string, IDeploymentExecutor> executors,
            string outDir,
            Logger logger,
            IProgress<RunProgressEvent>? progress = null)
        {
            _boundPrompts    = boundPrompts;
            _executors       = executors;
            _outDir          = outDir;
            _iterations      = cfg.Experiment.Iterations;
            _omitNullFields  = cfg.Experiment.OmitNullFields;
            _retry           = cfg.Experiment.Retry;
            _deployments     = cfg.Deployments;
            _seed            = cfg.Experiment.Seed;
            _pause           = cfg.Experiment.Timing.Pause.Run;
            _par             = cfg.Experiment.Parallel;
            _logger          = logger;
            _progress        = progress;
            _total           = _iterations.Set * boundPrompts.Count * _iterations.Rep * cfg.Deployments.Count;
            Directory.CreateDirectory(Path.Combine(outDir, HarnessUtils.RawRunsDir));
            bool anyParallel = _par.Level.Deployment || _par.Level.Rep || _par.Level.Prompt;
            int  cap         = _par.MaxConcurrency > 0 ? _par.MaxConcurrency : int.MaxValue;
            _sem = anyParallel ? new SemaphoreSlim(cap, cap) : null;
        }

        public async Task<List<RunRecord>> RunAllAsync()
        {
            List<RunRecord> records = new();
            for (int s = 1; s <= _iterations.Set; s++)
            {
                BoundPrompt[] boundOrder = _boundPrompts.ToArray();
                new Random(_seed.Shuffle.Run.Prompt + s).Shuffle(boundOrder);

                if (_par.Level.Prompt)
                {
                    var tasks = new List<Task<RunRecord[]>>();
                    for (int i = 0; i < boundOrder.Length; i++)
                    {
                        tasks.Add(RunBound(s, boundOrder[i]));
                        if (_pause.Prompt > 0 && i < boundOrder.Length - 1)
                            await Task.Delay(TimeSpan.FromSeconds(_pause.Prompt));
                    }
                    foreach (RunRecord rec in (await Task.WhenAll(tasks)).SelectMany(x => x))
                        records.Add(rec);
                }
                else
                {
                    foreach (BoundPrompt bound in boundOrder)
                    {
                        foreach (RunRecord rec in await RunBound(s, bound))
                            records.Add(rec);
                        if (_pause.Prompt > 0) await Task.Delay(TimeSpan.FromSeconds(_pause.Prompt));
                    }
                }

                if (s < _iterations.Set && _pause.Set > 0)
                {
                    DateTimeOffset resume = DateTimeOffset.UtcNow.AddSeconds(_pause.Set);
                    _logger.Info($"Set {s}/{_iterations.Set} complete. " +
                        $"Resuming at {resume:HH:mm:ss} UTC.");
                    await Task.Delay(TimeSpan.FromSeconds(_pause.Set));
                }
            }
            return records;
        }

        private async Task<RunRecord[]> RunBound(int s, BoundPrompt bound)
        {
            if (_par.Level.Rep)
            {
                var tasks = new List<Task<RunRecord[]>>();
                for (int r = 1; r <= _iterations.Rep; r++)
                {
                    tasks.Add(RunRep(s, bound, r));
                    if (_pause.Rep > 0 && r < _iterations.Rep)
                        await Task.Delay(TimeSpan.FromSeconds(_pause.Rep));
                }
                return (await Task.WhenAll(tasks)).SelectMany(x => x).ToArray();
            }

            List<RunRecord> recs = new();
            for (int r = 1; r <= _iterations.Rep; r++)
            {
                recs.AddRange(await RunRep(s, bound, r));
                if (_pause.Rep > 0) await Task.Delay(TimeSpan.FromSeconds(_pause.Rep));
            }
            return recs.ToArray();
        }

        private async Task<RunRecord[]> RunRep(int s, BoundPrompt bound, int r)
        {
            int globalRep = (s - 1) * _iterations.Rep + r;
            QueryConfig resolvedQuery = new QueryConfig
            {
                Label         = bound.QueryLabel,
                SystemMessage = bound.SystemMessage,
                UserPrompt    = HarnessUtils.ResolvePrompt(
                    bound.UserPromptTemplate, bound.TitleFull, bound.Sections,
                    globalRep, _seed.Shuffle.Content, bound.TitleExtras)
            };
            DeploymentConfig[] deploymentOrder = _deployments.ToArray();
            new Random(_seed.Shuffle.Run.Deployment + globalRep).Shuffle(deploymentOrder);

            if (_par.Level.Deployment)
            {
                var tasks = new List<Task<RunRecord>>();
                for (int i = 0; i < deploymentOrder.Length; i++)
                {
                    tasks.Add(RunDeployment(s, deploymentOrder[i], resolvedQuery, r, bound));
                    if (_pause.Deployment > 0 && i < deploymentOrder.Length - 1)
                        await Task.Delay(TimeSpan.FromSeconds(_pause.Deployment));
                }
                return await Task.WhenAll(tasks);
            }

            List<RunRecord> recs = new();
            foreach (DeploymentConfig d in deploymentOrder)
            {
                recs.Add(await RunDeployment(s, d, resolvedQuery, r, bound));
                if (_pause.Deployment > 0) await Task.Delay(TimeSpan.FromSeconds(_pause.Deployment));
            }
            return recs.ToArray();
        }

        private async Task<RunRecord> RunDeployment(int s, DeploymentConfig deployment, QueryConfig resolvedQuery, int r, BoundPrompt bound)
        {
            if (_sem is not null) await _sem.WaitAsync().ConfigureAwait(false);
            try
            {
                string rawFileName = $"set{s:D2}_rep{r:D2}_{bound.QueryLabel}_{deployment.Label}.json";
                RunRecord rec = await _executors[deployment.Label].ExecuteAsync(
                    resolvedQuery, deployment.Label, deployment.Parameters,
                    _omitNullFields, r, _outDir, rawFileName,
                    _retry);
                rec.Set          = s;
                rec.TextLabel    = bound.TextLabel;
                rec.QueryLabel   = bound.QueryLabel;
                rec.ListTask     = bound.ListTask;
                rec.OrderTask    = bound.OrderTask;
                rec.SectionCount = bound.Sections.Length;
                if (bound.Sections.Length > 0)
                    ScoringUtils.ScoreRecord(rec, bound);
                ScoringUtils.ExtractLogprobs(rec.RawJson!, rec, bound.TitleShort);
                _logger.Info(
                    $"[{rec.Deployment,-20}] set={s}/{_iterations.Set} " +
                    $"rep={r,2}/{_iterations.Rep} " +
                    $"text={bound.TextLabel,-20} query={bound.QueryLabel,-24} status={rec.Status} {rec.DurationMs,6}ms " +
                    $"out={rec.CompletionTokens?.ToString() ?? "-"} " +
                    $"sem={rec.SemanticSha256Short ?? "(none)"}" +
                    (rec.Error is null ? "" : $"  ERROR: {rec.Error}"));
                _progress?.Report(new RunProgressEvent(
                    Interlocked.Increment(ref _completed), _total, rec));
                return rec;
            }
            finally
            {
                _sem?.Release();
            }
        }

        public void Dispose() => _sem?.Dispose();
    }
}
