using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using AICopyrightReproducibility.Config;

namespace AICopyrightReproducibility.Gui.ViewModels
{
    public sealed class TypeRowViewModel : ViewModelBase
    {
        private string _text = "";
        public string Text { get => _text; set => this.RaiseAndSetIfChanged(ref _text, value); }
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
            TypeRows.CollectionChanged += (_, _) =>
                this.RaisePropertyChanged(nameof(Types));

            AddTypeCommand    = ReactiveCommand.Create(() =>
                TypeRows.Add(new TypeRowViewModel()));
            DeleteTypeCommand = ReactiveCommand.Create<TypeRowViewModel>(
                row => TypeRows.Remove(row));
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
                vm.TypeRows.Add(new TypeRowViewModel { Text = t });
            return vm;
        }

        public QueryConfig ToConfig() => new()
        {
            Label         = Label,
            Types         = TypeRows.Select(r => r.Text)
                                    .Where(s => !string.IsNullOrWhiteSpace(s))
                                    .ToArray(),
            SystemMessage = SystemMessage,
            UserPrompt    = UserPrompt
        };
    }
}
