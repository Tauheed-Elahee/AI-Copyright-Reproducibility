using System.Collections.Generic;
using System.Text.Json;

namespace AICopyrightReproducibility.Config
{
    public sealed class UnsupportedParamResponse
    {
        public int    Status  { get; set; }
        public string Content { get; set; } = "";
    }

    public sealed class UnsupportedParam
    {
        public JsonElement?             IntendedValue { get; set; }
        public string                   ErrorType     { get; set; } = "";   // "response" | "output" | "ignored"
        public UnsupportedParamResponse Response      { get; set; } = new();
        public string                   Reason        { get; set; } = "";
    }

    public enum DeploymentMode { AzureModeApi, AzureAgentApi, StandardOpenAI }

    public sealed class DeploymentConnectionConfig
    {
        public string?                    Endpoint { get; set; }
        public Dictionary<string, string> Fields   { get; set; } = new();
    }

    public sealed class ResolvedConnectionConfig
    {
        public string                     Url        { get; set; } = "";
        public string                     AuthType   { get; set; } = "";
        public string?                    TokenScope { get; set; }
        public string?                    ApiKey     { get; set; }
        public string                     AuthHeader { get; set; } = "Authorization";
        public string?                    AuthScheme { get; set; } = "Bearer";
        public Dictionary<string, string> Fields     { get; set; } = new();
    }

    public sealed class DeploymentConfig
    {
        public string                               Label                 { get; set; } = "";
        public DeploymentMode                       Mode                  { get; set; } = DeploymentMode.AzureModeApi;
        public DeploymentConnectionConfig           Connection            { get; set; } = new();
        public Dictionary<string, JsonElement>      Parameters            { get; set; } = new();
        public Dictionary<string, UnsupportedParam> UnsupportedParameters { get; set; } = new();
    }
}
