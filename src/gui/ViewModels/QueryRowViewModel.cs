using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using AICopyrightReproducibility.Config;

namespace AICopyrightReproducibility.Gui.ViewModels
{
    public sealed class TypeRowViewModel : ViewModelBase
    {
        private readonly ObservableCollection<TypeRowViewModel> _allRows;
        private string _text = "";

        public TypeRowViewModel(ObservableCollection<TypeRowViewModel> allRows)
        {
            _allRows = allRows;
        }

        public string Text { get => _text; set => this.RaiseAndSetIfChanged(ref _text, value); }

        public IReadOnlyList<string> AvailableTypeSuggestions =>
            QueryRowViewModel.TypeSuggestions
                .Where(s => s == _text || !_allRows.Any(r => r != this && r.Text == s))
                .ToList();

        internal void NotifyAvailableSuggestions() =>
            this.RaisePropertyChanged(nameof(AvailableTypeSuggestions));
    }

    public sealed class QueryRowViewModel : ViewModelBase
    {
        private string _label         = "";
        private string _systemMessage = "";
        private string _userPrompt    = "";

        public static readonly IReadOnlyList<string> TypeSuggestions =
            new[] { "list_task", "order_task" };

        public string Label
        {
            get => _label;
            set => this.RaiseAndSetIfChanged(ref _label, value);
        }

        public ObservableCollection<TypeRowViewModel> TypeRows { get; } = new();

        public string Types =>
            TypeRows.Count == 0 ? "(none)" : string.Join(", ", TypeRows.Select(r => r.Text));

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddTypeCommand    { get; }
        public ReactiveCommand<TypeRowViewModel, System.Reactive.Unit>     DeleteTypeCommand { get; }

        public string SystemMessage
        {
            get => _systemMessage;
            set => this.RaiseAndSetIfChanged(ref _systemMessage, value);
        }

        public string UserPrompt
        {
            get => _userPrompt;
            set
            {
                this.RaiseAndSetIfChanged(ref _userPrompt, value);
                this.RaisePropertyChanged(nameof(UserPromptPreview));
            }
        }

        public string UserPromptPreview =>
            _userPrompt.Length > 100 ? _userPrompt[..100] + "…" : _userPrompt;

        public QueryRowViewModel()
        {
            TypeRows.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                    foreach (TypeRowViewModel row in e.NewItems)
                        row.PropertyChanged += (_, args) =>
                        {
                            if (args.PropertyName == nameof(TypeRowViewModel.Text))
                                RefreshSuggestions();
                        };

                RefreshSuggestions();
            };

            AddTypeCommand    = ReactiveCommand.Create(() =>
                TypeRows.Add(new TypeRowViewModel(TypeRows)));
            DeleteTypeCommand = ReactiveCommand.Create<TypeRowViewModel>(
                row => TypeRows.Remove(row));
        }

        private void RefreshSuggestions()
        {
            this.RaisePropertyChanged(nameof(Types));
            foreach (var row in TypeRows)
                row.NotifyAvailableSuggestions();
        }

        public static QueryRowViewModel From(QueryConfig q)
        {
            var vm = new QueryRowViewModel
            {
                Label         = q.Label,
                SystemMessage = q.SystemMessage,
                UserPrompt    = q.UserPrompt
            };
            foreach (var t in q.Types)
                vm.TypeRows.Add(new TypeRowViewModel(vm.TypeRows) { Text = t });
            return vm;
        }

        public QueryConfig ToConfig() => new()
        {
            Label         = Label,
            Types         = TypeRows.Select(r => r.Text)
                                    .Where(s => !string.IsNullOrWhiteSpace(s))
                                    .Distinct()
                                    .ToArray(),
            SystemMessage = SystemMessage,
            UserPrompt    = UserPrompt
        };
    }
}
