using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using AICopyrightReproducibility.Config;
using AICopyrightReproducibility.Utils;

namespace AICopyrightReproducibility.Gui.ViewModels
{
    public sealed record TextDisplayItem(string Label, string Title, int SectionCount);
    public sealed record PromptDisplayItem(string TextLabel, string Queries);

    public sealed class InputsViewModel : ViewModelBase
    {
        private QueryRowViewModel? _selectedQuery;
        private string?            _queriesFilePath;
        private string?            _queriesSaveError;
        private bool               _queriesSaveSuccess;

        public ObservableCollection<QueryRowViewModel>  Queries { get; } = new();
        public ObservableCollection<TextDisplayItem>    Texts   { get; } = new();
        public ObservableCollection<PromptDisplayItem>  Prompts { get; } = new();

        public QueryRowViewModel? SelectedQuery
        {
            get => _selectedQuery;
            set => this.RaiseAndSetIfChanged(ref _selectedQuery, value);
        }

        public string? QueriesFilePath
        {
            get => _queriesFilePath;
            private set
            {
                this.RaiseAndSetIfChanged(ref _queriesFilePath, value);
                this.RaisePropertyChanged(nameof(CanEditQueries));
            }
        }

        public bool CanEditQueries => _queriesFilePath != null;

        public string? QueriesSaveError
        {
            get => _queriesSaveError;
            private set => this.RaiseAndSetIfChanged(ref _queriesSaveError, value);
        }

        public bool QueriesSaveSuccess
        {
            get => _queriesSaveSuccess;
            private set => this.RaiseAndSetIfChanged(ref _queriesSaveSuccess, value);
        }

        public bool HasQueries => Queries.Count > 0;
        public bool HasTexts   => Texts.Count > 0;
        public bool HasPrompts => Prompts.Count > 0;

        public string TextsHeader   => $"Texts ({Texts.Count})";
        public string QueriesHeader => $"Queries ({Queries.Count})";
        public string PromptsHeader => $"Prompts ({Prompts.Count})";

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SaveQueriesCommand { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddQueryCommand    { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> DeleteQueryCommand { get; }

        public InputsViewModel()
        {
            Queries.CollectionChanged += (_, _) =>
            {
                this.RaisePropertyChanged(nameof(HasQueries));
                this.RaisePropertyChanged(nameof(QueriesHeader));
            };
            Texts.CollectionChanged += (_, _) =>
            {
                this.RaisePropertyChanged(nameof(HasTexts));
                this.RaisePropertyChanged(nameof(TextsHeader));
            };
            Prompts.CollectionChanged += (_, _) =>
            {
                this.RaisePropertyChanged(nameof(HasPrompts));
                this.RaisePropertyChanged(nameof(PromptsHeader));
            };

            SaveQueriesCommand = ReactiveCommand.Create(ExecuteSaveQueries);
            AddQueryCommand    = ReactiveCommand.Create(ExecuteAddQuery);

            var canDelete = this.WhenAnyValue(x => x.SelectedQuery).Select(q => q != null);
            DeleteQueryCommand = ReactiveCommand.Create(ExecuteDeleteQuery, canDelete);
        }

        public void LoadFrom(
            List<QueryConfig> queries, string? queriesFilePath,
            List<TextDbEntry> texts,
            List<PromptEntry> prompts)
        {
            Queries.Clear();
            foreach (var q in queries)
                Queries.Add(QueryRowViewModel.From(q));
            SelectedQuery = Queries.FirstOrDefault();

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

            QueriesFilePath    = queriesFilePath;
            QueriesSaveError   = null;
            QueriesSaveSuccess = false;
        }

        private void ExecuteSaveQueries()
        {
            if (_queriesFilePath == null) return;
            try
            {
                var list    = Queries.Select(q => q.ToConfig()).ToList();
                var wrapper = new { queries = list };
                var opts    = new JsonSerializerOptions(ProjectLoader.ReadOpts) { WriteIndented = true };
                File.WriteAllText(_queriesFilePath, JsonSerializer.Serialize(wrapper, opts));
                QueriesSaveError   = null;
                QueriesSaveSuccess = true;
                _ = Task.Delay(2500).ContinueWith(_ =>
                    Dispatcher.UIThread.Post(() => QueriesSaveSuccess = false));
            }
            catch (Exception ex)
            {
                QueriesSaveError = $"Save failed: {ex.Message}";
            }
        }

        private void ExecuteAddQuery()
        {
            var q = new QueryRowViewModel { Label = "new_query" };
            Queries.Add(q);
            SelectedQuery = q;
        }

        private void ExecuteDeleteQuery()
        {
            if (SelectedQuery == null) return;
            int idx = Queries.IndexOf(SelectedQuery);
            Queries.Remove(SelectedQuery);
            SelectedQuery = Queries.Count > 0
                ? Queries[Math.Max(0, idx - 1)]
                : null;
        }
    }
}
