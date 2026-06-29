using System;
using System.Collections.Generic;

namespace AICopyrightReproducibility.Gui.ViewModels
{
    public sealed class DeploymentResultRow
    {
        public string             Deployment    { get; init; } = "";
        public int                SuccessCount  { get; init; }
        public int                ErrorCount    { get; init; }
        public double             AvgDurationMs { get; init; }
        public IReadOnlyList<long> Durations    { get; init; } = Array.Empty<long>();
        public int                TotalRuns      => SuccessCount + ErrorCount;
        public double             SuccessRatePct => TotalRuns > 0 ? 100.0 * SuccessCount / TotalRuns : 0;
    }
}
