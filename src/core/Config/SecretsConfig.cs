using System.Collections.Generic;

namespace AICopyrightReproducibility.Config
{
    public sealed class SecretsConfig
    {
        public SecretsApiConfig Api { get; set; } = new();

        public Dictionary<string, string> Flatten()
        {
            var d = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var kv in Api.Endpoints) d[kv.Key] = kv.Value;
            foreach (var kv in Api.Keys)      d[kv.Key] = kv.Value;
            return d;
        }
    }

    public sealed class SecretsApiConfig
    {
        public Dictionary<string, string> Endpoints { get; set; } = new();
        public Dictionary<string, string> Keys      { get; set; } = new();
    }
}
