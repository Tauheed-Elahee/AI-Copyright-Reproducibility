using System.Collections.Generic;

namespace AICopyrightReproducibility.Config
{
    public sealed class AuthConfig
    {
        public string  Type   { get; set; } = "";
        public string? Scope  { get; set; }
        public string? Key    { get; set; }
        public string  Header { get; set; } = "Authorization";
        public string? Scheme { get; set; } = "Bearer";
    }

    public sealed class EndpointConfig
    {
        public string       Url    { get; set; } = "";
        public AuthConfig   Auth   { get; set; } = new();
        public List<string> Fields { get; set; } = new();
    }

    public sealed class EndpointsConfig
    {
        public Dictionary<string, EndpointConfig> Endpoints { get; set; } = new();
    }

    public sealed class SecretsConfig
    {
        public Dictionary<string, string> Keys { get; set; } = new();
    }
}
