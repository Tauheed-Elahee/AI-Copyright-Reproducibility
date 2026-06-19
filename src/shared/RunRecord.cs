using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AICopyrightReproducibility.Config;

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
    public DateTimeOffset TimestampUtc  { get; set; }
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
    [JsonIgnore] public string? RawJson { get; set; }
}
