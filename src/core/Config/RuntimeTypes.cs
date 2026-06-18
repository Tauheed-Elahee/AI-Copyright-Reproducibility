using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AICopyrightReproducibility.Config
{
    public sealed class QueryConfig
    {
        public string   Label         { get; set; } = "";
        public string[] Types         { get; set; } = System.Array.Empty<string>();
        public string   SystemMessage { get; set; } = "";
        public string   UserPrompt    { get; set; } = "";
        [JsonIgnore] public bool ListTask  => System.Array.IndexOf(Types, "list_task")  >= 0;
        [JsonIgnore] public bool OrderTask => System.Array.IndexOf(Types, "order_task") >= 0;
    }

    public sealed class PromptEntry
    {
        public string   Text    { get; set; } = "";
        public string[] Queries { get; set; } = System.Array.Empty<string>();
    }

    public sealed class BoundPrompt
    {
        public string   QueryLabel         { get; init; } = "";
        public bool     ListTask           { get; init; }
        public bool     OrderTask          { get; init; }
        public string   SystemMessage      { get; init; } = "";
        public string   UserPromptTemplate { get; init; } = "";
        public string   TextLabel          { get; init; } = "";
        public string   TitleFull          { get; init; } = "";
        public string   TitleShort         { get; init; } = "";
        public string[] Sections           { get; init; } = System.Array.Empty<string>();
        public Dictionary<string, string>  Aliases      { get; init; } = new();
        public Dictionary<string, string>  TitleExtras  { get; init; } = new();
    }

    public sealed class RunConfig
    {
        public ProjectConfig          Project     { get; set; } = new();
        public ExperimentConfig       Experiment  { get; set; } = new();
        public List<QueryConfig>      Queries     { get; set; } = new();
        public List<DeploymentConfig> Deployments { get; set; } = new();
    }

    public sealed class ProjectSnapshot
    {
        public string? Name   { get; set; }
        public string? Author { get; set; }
        public string? Date   { get; set; }
    }

    public sealed class DeploymentSnapshot
    {
        public string                          Label      { get; set; } = "";
        public DeploymentMode                  Mode       { get; set; }
        public string                          Url        { get; set; } = "";
        public string                          AuthType   { get; set; } = "";
        public string?                         TokenScope { get; set; }
        public string                          AuthHeader { get; set; } = "Authorization";
        public string?                         AuthScheme { get; set; }
        public Dictionary<string, JsonElement> Parameters { get; set; } = new();
    }

    public sealed class RunSnapshot
    {
        public string                   CapturedUtc { get; set; } = "";
        public ProjectSnapshot?         Project     { get; set; }
        public ExperimentConfig         Experiment  { get; set; } = new();
        public List<QueryConfig>        Queries     { get; set; } = new();
        public List<DeploymentSnapshot> Deployments { get; set; } = new();
    }

    public sealed class RunRecord
    {
        public string  Deployment          { get; set; } = "";
        public int     Set                 { get; set; }
        public string  TextLabel           { get; set; } = "";
        public string  QueryLabel          { get; set; } = "";
        public int     SectionCount        { get; set; }
        public bool    ListTask            { get; set; }
        public bool    OrderTask           { get; set; }
        public int     Index               { get; set; }
        public string  TimestampUtc        { get; set; } = "";
        public int     Status              { get; set; }
        public long    DurationMs          { get; set; }
        public int     RetryCount          { get; set; }
        public string? Model               { get; set; }
        public string? SystemFingerprint   { get; set; }
        public string? ResponseId          { get; set; }
        public long?   Created             { get; set; }
        public int?    PromptTokens        { get; set; }
        public int?    CompletionTokens    { get; set; }
        public int?    TotalTokens         { get; set; }
        public string? FinishReason        { get; set; }
        public string? ContentSha256       { get; set; }
        public string? ContentSha256Short  { get; set; }
        public string? SemanticSha256      { get; set; }
        public string? SemanticSha256Short { get; set; }
        public string  RawFile             { get; set; } = "";
        public string[] Lists              { get; set; } = System.Array.Empty<string>();
        public string?  ContentText        { get; set; }
        public int      ExactMatches       { get; set; }
        public int      Coverage           { get; set; }
        public int      Hallucinations     { get; set; }
        public bool     Li1First           { get; set; }
        public int?     PositionScore      { get; set; }
        public int?     MinMoves           { get; set; }
        public float?   OrderPct           { get; set; }
        public bool     TitleHit           { get; set; }
        public bool     TextbookHit        { get; set; }
        public float?[] ListItemLogprobs   { get; set; } = System.Array.Empty<float?>();
        public float?   ListLogprobMean    { get; set; }
        public float?   ListLogprobMedian  { get; set; }
        public float?   ListLogprobMode    { get; set; }
        public float?   TitleLogprob       { get; set; }
        public string?  Error              { get; set; }
        public Dictionary<string, string>? ResponseHeaders { get; set; }
        [JsonIgnore] internal string? RawJson { get; set; }
    }
}
