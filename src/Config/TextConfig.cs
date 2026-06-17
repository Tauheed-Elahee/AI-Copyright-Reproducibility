using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AICopyrightReproducibility.Config
{
    public sealed class TextTitleConfig
    {
        public string Full  { get; set; } = "";
        public string Short { get; set; } = "";
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtraFields { get; set; }
    }

    public sealed class TextContentConfig
    {
        public TextTitleConfig            Title           { get; set; } = new();
        public string[]                   SectionHeadings { get; set; } = System.Array.Empty<string>();
        public Dictionary<string, string> Aliases         { get; set; } = new();
    }

    public sealed class TextDbEntry
    {
        public string            Label   { get; set; } = "";
        public TextContentConfig Content { get; set; } = new();
    }

    internal sealed record TextEntry(
        string TitleFull,
        string TitleShort,
        string[] Sections,
        Dictionary<string, string> Aliases,
        Dictionary<string, string> TitleExtras);
}
