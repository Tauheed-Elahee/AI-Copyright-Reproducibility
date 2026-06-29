using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI;

namespace AICopyrightReproducibility.Gui.ViewModels
{
    public sealed class ChartsViewModel : ViewModelBase
    {
        private bool _hasData;

        public bool HasData
        {
            get => _hasData;
            private set => this.RaiseAndSetIfChanged(ref _hasData, value);
        }

        public string[]  Labels       { get; private set; } = Array.Empty<string>();
        public double[]  SuccessRates { get; private set; } = Array.Empty<double>();
        public long[][]  Durations    { get; private set; } = Array.Empty<long[]>();

        public event EventHandler? Updated;

        public void LoadFrom(IReadOnlyList<DeploymentResultRow> rows)
        {
            if (rows.Count == 0)
            {
                Labels       = Array.Empty<string>();
                SuccessRates = Array.Empty<double>();
                Durations    = Array.Empty<long[]>();
                HasData      = false;
            }
            else
            {
                Labels       = rows.Select(r => r.Deployment).ToArray();
                SuccessRates = rows.Select(r =>
                    r.TotalRuns > 0 ? 100.0 * r.SuccessCount / r.TotalRuns : 0).ToArray();
                Durations    = rows.Select(r => r.Durations.ToArray()).ToArray();
                HasData      = true;
            }

            Updated?.Invoke(this, EventArgs.Empty);
        }
    }
}
