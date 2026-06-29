using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AICopyrightReproducibility.Config;

namespace AICopyrightReproducibility.Gui.ViewModels
{
    public sealed record QueryDisplayItem(string Label, string Types, string UserPromptPreview);
    public sealed record TextDisplayItem(string Label, string Title, int SectionCount);
    public sealed record PromptDisplayItem(string TextLabel, string Queries);

    public sealed class InputsViewModel : ViewModelBase
    {
        public ObservableCollection<QueryDisplayItem>  Queries { get; } = new();
        public ObservableCollection<TextDisplayItem>   Texts   { get; } = new();
        public ObservableCollection<PromptDisplayItem> Prompts { get; } = new();

        public bool HasQueries => Queries.Count > 0;
        public bool HasTexts   => Texts.Count > 0;
        public bool HasPrompts => Prompts.Count > 0;

        public void LoadFrom(
            List<QueryConfig> queries,
            List<TextDbEntry> texts,
            List<PromptEntry> prompts)
        {
            Queries.Clear();
            foreach (var q in queries)
                Queries.Add(new QueryDisplayItem(
                    q.Label,
                    q.Types.Length == 0 ? "(none)" : string.Join(", ", q.Types),
                    q.UserPrompt.Length > 100 ? q.UserPrompt[..100] + "…" : q.UserPrompt));

            Texts.Clear();
            foreach (var t in texts)
                Texts.Add(new TextDisplayItem(
                    t.Label,
                    t.Content.Title.Full,
                    t.Content.SectionHeadings.Length));

            Prompts.Clear();
            foreach (var p in prompts)
                Prompts.Add(new PromptDisplayItem(
                    p.Text,
                    string.Join(", ", p.Queries)));
        }
    }
}
