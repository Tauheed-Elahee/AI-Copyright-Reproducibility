using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using AICopyrightReproducibility.Config;

namespace AICopyrightReproducibility.Gui.ViewModels
{
    public sealed record DeploymentDisplayItem(
        string  Label,
        string  Mode,
        string? Endpoint,
        string  ParameterSummary);

    public sealed class DeploymentsViewModel : ViewModelBase
    {
        public ObservableCollection<DeploymentDisplayItem> Items { get; } = new();
        public bool HasItems => Items.Count > 0;

        public void LoadFrom(IEnumerable<DeploymentConfig> deployments)
        {
            Items.Clear();
            foreach (var d in deployments)
                Items.Add(new DeploymentDisplayItem(
                    d.Label,
                    d.Mode.ToString(),
                    d.Connection.Endpoint,
                    FormatParams(d.Parameters)));
        }

        private static string FormatParams(Dictionary<string, JsonElement> p) =>
            p.Count == 0 ? "" : string.Join("  ·  ", p.Select(kv => $"{kv.Key}={kv.Value}"));
    }
}
