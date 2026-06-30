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
    public sealed record PromptDisplayItem(string TextLabel, string Queries);

    public sealed class InputsViewModel : ViewModelBase
    {
        // ── Queries state ─────────────────────────────────────────────────────
        private QueryRowViewModel? _selectedQuery;
        private string?            _queriesFilePath;
        private string?            _queriesSaveError;
        private bool               _queriesSaveSuccess;

        // ── Texts state ───────────────────────────────────────────────────────
        private TextRowViewModel? _selectedText;
        private string?           _textsFilePath;
        private string?           _textsSaveError;
        private bool              _textsSaveSuccess;

        // ── Collections ───────────────────────────────────────────────────────
        public ObservableCollection<QueryRowViewModel> Queries { get; } = new();
        public ObservableCollection<TextRowViewModel>  Texts   { get; } = new();
        public ObservableCollection<PromptDisplayItem> Prompts { get; } = new();

        // ── Query properties ──────────────────────────────────────────────────
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

        // ── Text properties ───────────────────────────────────────────────────
        public TextRowViewModel? SelectedText
        {
            get => _selectedText;
            set => this.RaiseAndSetIfChanged(ref _selectedText, value);
        }

        public string? TextsFilePath
        {
            get => _textsFilePath;
            private set
            {
                this.RaiseAndSetIfChanged(ref _textsFilePath, value);
                this.RaisePropertyChanged(nameof(CanEditTexts));
            }
        }

        public bool CanEditTexts => _textsFilePath != null;

        public string? TextsSaveError
        {
            get => _textsSaveError;
            private set => this.RaiseAndSetIfChanged(ref _textsSaveError, value);
        }

        public bool TextsSaveSuccess
        {
            get => _textsSaveSuccess;
            private set => this.RaiseAndSetIfChanged(ref _textsSaveSuccess, value);
        }

        // ── Derived counts ────────────────────────────────────────────────────
        public bool HasQueries => Queries.Count > 0;
        public bool HasTexts   => Texts.Count > 0;
        public bool HasPrompts => Prompts.Count > 0;

        public string TextsHeader   => $"Texts ({Texts.Count})";
        public string QueriesHeader => $"Queries ({Queries.Count})";
        public string PromptsHeader => $"Prompts ({Prompts.Count})";

        // ── Commands ──────────────────────────────────────────────────────────
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SaveQueriesCommand { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddQueryCommand    { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> DeleteQueryCommand { get; }

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SaveTextsCommand { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddTextCommand   { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> DeleteTextCommand { get; }

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
            DeleteQueryCommand = ReactiveCommand.Create(ExecuteDeleteQuery,
                this.WhenAnyValue(x => x.SelectedQuery).Select(q => q != null));

            SaveTextsCommand = ReactiveCommand.Create(ExecuteSaveTexts);
            AddTextCommand   = ReactiveCommand.Create(ExecuteAddText);
            DeleteTextCommand = ReactiveCommand.Create(ExecuteDeleteText,
                this.WhenAnyValue(x => x.SelectedText).Select(t => t != null));
        }

        public void LoadFrom(
            List<QueryConfig> queries, string? queriesFilePath,
            List<TextDbEntry> texts,   string? textsFilePath,
            List<PromptEntry> prompts)
        {
            Queries.Clear();
            foreach (var q in queries)
                Queries.Add(QueryRowViewModel.From(q));
            SelectedQuery = Queries.FirstOrDefault();

            Texts.Clear();
            foreach (var t in texts)
                Texts.Add(TextRowViewModel.From(t));
            SelectedText = Texts.FirstOrDefault();

            Prompts.Clear();
            foreach (var p in prompts)
                Prompts.Add(new PromptDisplayItem(
                    p.Text,
                    string.Join(", ", p.Queries)));

            QueriesFilePath    = queriesFilePath;
            QueriesSaveError   = null;
            QueriesSaveSuccess = false;

            TextsFilePath    = textsFilePath;
            TextsSaveError   = null;
            TextsSaveSuccess = false;
        }

        // ── Query commands ────────────────────────────────────────────────────

        private void ExecuteSaveQueries()
        {
            if (_queriesFilePath == null) return;
            try
            {
                var wrapper = new { queries = Queries.Select(q => q.ToConfig()).ToList() };
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
            SelectedQuery = Queries.Count > 0 ? Queries[Math.Max(0, idx - 1)] : null;
        }

        // ── Text commands ─────────────────────────────────────────────────────

        private void ExecuteSaveTexts()
        {
            if (_textsFilePath == null) return;
            try
            {
                var wrapper = new { texts = Texts.Select(t => t.ToConfig()).ToList() };
                var opts    = new JsonSerializerOptions(ProjectLoader.ReadOpts) { WriteIndented = true };
                File.WriteAllText(_textsFilePath, JsonSerializer.Serialize(wrapper, opts));
                TextsSaveError   = null;
                TextsSaveSuccess = true;
                _ = Task.Delay(2500).ContinueWith(_ =>
                    Dispatcher.UIThread.Post(() => TextsSaveSuccess = false));
            }
            catch (Exception ex)
            {
                TextsSaveError = $"Save failed: {ex.Message}";
            }
        }

        private void ExecuteAddText()
        {
            var t = new TextRowViewModel { Label = "new_text" };
            Texts.Add(t);
            SelectedText = t;
        }

        private void ExecuteDeleteText()
        {
            if (SelectedText == null) return;
            int idx = Texts.IndexOf(SelectedText);
            Texts.Remove(SelectedText);
            SelectedText = Texts.Count > 0 ? Texts[Math.Max(0, idx - 1)] : null;
        }
    }
}
