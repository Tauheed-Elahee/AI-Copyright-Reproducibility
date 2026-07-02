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

        // ── Prompts state ─────────────────────────────────────────────────────
        private PromptRowViewModel? _selectedPrompt;
        private string?             _promptsFilePath;
        private string?             _promptsSaveError;
        private bool                _promptsSaveSuccess;

        // ── Collections ───────────────────────────────────────────────────────
        public ObservableCollection<QueryRowViewModel>  Queries    { get; } = new();
        public ObservableCollection<TextRowViewModel>   Texts      { get; } = new();
        public ObservableCollection<PromptRowViewModel> PromptRows { get; } = new();

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

        // ── Prompt properties ─────────────────────────────────────────────────
        public PromptRowViewModel? SelectedPrompt
        {
            get => _selectedPrompt;
            set => this.RaiseAndSetIfChanged(ref _selectedPrompt, value);
        }

        public string? PromptsFilePath
        {
            get => _promptsFilePath;
            private set
            {
                this.RaiseAndSetIfChanged(ref _promptsFilePath, value);
                this.RaisePropertyChanged(nameof(CanEditPrompts));
            }
        }

        public bool CanEditPrompts => _promptsFilePath != null;

        public string? PromptsSaveError
        {
            get => _promptsSaveError;
            private set => this.RaiseAndSetIfChanged(ref _promptsSaveError, value);
        }

        public bool PromptsSaveSuccess
        {
            get => _promptsSaveSuccess;
            private set => this.RaiseAndSetIfChanged(ref _promptsSaveSuccess, value);
        }

        // ── Derived counts ────────────────────────────────────────────────────
        public bool HasQueries => Queries.Count > 0;
        public bool HasTexts   => Texts.Count > 0;
        public bool HasPrompts => PromptRows.Count > 0;

        private static readonly IReadOnlyList<string> BuiltInPlaceholders =
            new[] { "{title}", "{sections}", "{sections_shuffled}" };

        public IReadOnlyList<string> AvailablePlaceholders
        {
            get
            {
                var extras = Texts
                    .SelectMany(t => t.ExtraFieldRows)
                    .Select(r => r.Key)
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Distinct()
                    .OrderBy(k => k)
                    .Select(k => "{" + k + "}");
                return BuiltInPlaceholders.Concat(extras).ToList();
            }
        }

        public string TextsHeader   => $"Texts ({Texts.Count})";
        public string QueriesHeader => $"Queries ({Queries.Count})";
        public string PromptsHeader => $"Prompts ({PromptRows.Count})";

        // ── Commands ──────────────────────────────────────────────────────────
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SaveQueriesCommand { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddQueryCommand    { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> DeleteQueryCommand { get; }

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SaveTextsCommand  { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddTextCommand    { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> DeleteTextCommand { get; }

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SavePromptsCommand  { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddPromptCommand    { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> DeletePromptCommand { get; }

        public InputsViewModel()
        {
            Queries.CollectionChanged += (_, _) =>
            {
                this.RaisePropertyChanged(nameof(HasQueries));
                this.RaisePropertyChanged(nameof(QueriesHeader));
            };
            Texts.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                    foreach (TextRowViewModel t in e.NewItems)
                        SubscribeTextExtraFields(t);
                this.RaisePropertyChanged(nameof(HasTexts));
                this.RaisePropertyChanged(nameof(TextsHeader));
                this.RaisePropertyChanged(nameof(AvailablePlaceholders));
            };
            PromptRows.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                    foreach (PromptRowViewModel row in e.NewItems)
                        row.PropertyChanged += (_, args) =>
                        {
                            if (args.PropertyName == nameof(PromptRowViewModel.TextLabel))
                                RefreshPromptSuggestions();
                        };
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

            SavePromptsCommand  = ReactiveCommand.Create(ExecuteSavePrompts);
            AddPromptCommand    = ReactiveCommand.Create(ExecuteAddPrompt);
            DeletePromptCommand = ReactiveCommand.Create(ExecuteDeletePrompt,
                this.WhenAnyValue(x => x.SelectedPrompt).Select(p => p != null));
        }

        public void LoadFrom(
            List<QueryConfig> queries, string? queriesFilePath,
            List<TextDbEntry> texts,   string? textsFilePath,
            List<PromptEntry> prompts, string? promptsFilePath)
        {
            Queries.Clear();
            foreach (var q in queries)
                Queries.Add(QueryRowViewModel.From(q));
            SelectedQuery = Queries.FirstOrDefault();

            Texts.Clear();
            foreach (var t in texts)
                Texts.Add(TextRowViewModel.From(t));
            SelectedText = Texts.FirstOrDefault();

            var textLabels  = Texts.Select(t => t.Label).ToList();
            var queryLabels = Queries.Select(q => q.Label).ToList();
            PromptRows.Clear();
            foreach (var p in prompts)
                PromptRows.Add(PromptRowViewModel.From(p, PromptRows, textLabels, queryLabels));
            SelectedPrompt = PromptRows.FirstOrDefault();

            QueriesFilePath    = queriesFilePath;
            QueriesSaveError   = null;
            QueriesSaveSuccess = false;

            TextsFilePath    = textsFilePath;
            TextsSaveError   = null;
            TextsSaveSuccess = false;

            PromptsFilePath    = promptsFilePath;
            PromptsSaveError   = null;
            PromptsSaveSuccess = false;
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

        // ── Prompt commands ───────────────────────────────────────────────────

        private void ExecuteSavePrompts()
        {
            if (_promptsFilePath == null) return;
            try
            {
                var wrapper = new { prompts = PromptRows.Select(p => p.ToConfig()).ToList() };
                var opts    = new JsonSerializerOptions(ProjectLoader.ReadOpts) { WriteIndented = true };
                File.WriteAllText(_promptsFilePath, JsonSerializer.Serialize(wrapper, opts));
                PromptsSaveError   = null;
                PromptsSaveSuccess = true;
                _ = Task.Delay(2500).ContinueWith(_ =>
                    Dispatcher.UIThread.Post(() => PromptsSaveSuccess = false));
            }
            catch (Exception ex)
            {
                PromptsSaveError = $"Save failed: {ex.Message}";
            }
        }

        private void ExecuteAddPrompt()
        {
            var textLabels  = Texts.Select(t => t.Label).ToList();
            var queryLabels = Queries.Select(q => q.Label).ToList();
            var p = new PromptRowViewModel(PromptRows, textLabels, queryLabels) { TextLabel = "new_text" };
            PromptRows.Add(p);
            SelectedPrompt = p;
        }

        private void SubscribeTextExtraFields(TextRowViewModel t)
        {
            t.ExtraFieldRows.CollectionChanged += (_, fe) =>
            {
                if (fe.NewItems != null)
                    foreach (ExtraFieldRowViewModel r in fe.NewItems)
                        r.PropertyChanged += (_, _) =>
                            this.RaisePropertyChanged(nameof(AvailablePlaceholders));
                this.RaisePropertyChanged(nameof(AvailablePlaceholders));
            };
        }

        private void RefreshPromptSuggestions()
        {
            foreach (var row in PromptRows)
                row.NotifyAvailableTextSuggestions();
        }

        private void ExecuteDeletePrompt()
        {
            if (SelectedPrompt == null) return;
            int idx = PromptRows.IndexOf(SelectedPrompt);
            PromptRows.Remove(SelectedPrompt);
            SelectedPrompt = PromptRows.Count > 0 ? PromptRows[Math.Max(0, idx - 1)] : null;
        }
    }
}
