using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using AICopyrightReproducibility.Config;

namespace AICopyrightReproducibility.Gui.ViewModels
{
    public sealed record ParameterEntry(string Key, string Value);

    public sealed record DeploymentDisplayItem(
        string  Label,
        string  Mode,
        string? Endpoint,
        IReadOnlyList<ParameterEntry> Parameters)
    {
        public bool HasEndpoint   => !string.IsNullOrEmpty(Endpoint);
        public bool HasParameters => Parameters.Count > 0;
        public bool HasBody       => HasEndpoint || HasParameters;
    }

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
                    d.Parameters
                        .Select(kv => new ParameterEntry(kv.Key, kv.Value.ToString()))
                        .ToList()));
        }
    }
}
