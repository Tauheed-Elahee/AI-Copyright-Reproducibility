using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using AICopyrightReproducibility.Config;

namespace AICopyrightReproducibility.Gui.ViewModels
{
    public sealed class QueryLinkRowViewModel : ViewModelBase
    {
        private readonly ObservableCollection<QueryLinkRowViewModel> _allRows;
        private readonly IReadOnlyList<string>                       _allQueryLabels;
        private string _queryLabel = "";

        public QueryLinkRowViewModel(
            ObservableCollection<QueryLinkRowViewModel> allRows,
            IReadOnlyList<string>                       allQueryLabels)
        {
            _allRows        = allRows;
            _allQueryLabels = allQueryLabels;
        }

        public string QueryLabel
        {
            get => _queryLabel;
            set => this.RaiseAndSetIfChanged(ref _queryLabel, value);
        }

        public IReadOnlyList<string> AvailableQuerySuggestions =>
            _allQueryLabels
                .Where(s => s == _queryLabel || !_allRows.Any(r => r != this && r.QueryLabel == s))
                .ToList();

        internal void NotifyAvailableSuggestions() =>
            this.RaisePropertyChanged(nameof(AvailableQuerySuggestions));
    }

    public sealed class PromptRowViewModel : ViewModelBase
    {
        private readonly ObservableCollection<PromptRowViewModel> _allRows;
        private readonly IReadOnlyList<string>                    _allTextLabels;
        private readonly IReadOnlyList<string>                    _allQueryLabels;
        private string _textLabel = "";

        public string TextLabel
        {
            get => _textLabel;
            set => this.RaiseAndSetIfChanged(ref _textLabel, value);
        }

        public IReadOnlyList<string> AvailableTextSuggestions =>
            _allTextLabels
                .Where(s => s == _textLabel || !_allRows.Any(r => r != this && r.TextLabel == s))
                .ToList();

        internal void NotifyAvailableTextSuggestions() =>
            this.RaisePropertyChanged(nameof(AvailableTextSuggestions));

        public ObservableCollection<QueryLinkRowViewModel> QueryLinkRows { get; } = new();

        public string QueriesSummary =>
            QueryLinkRows.Count == 0
                ? "(none)"
                : string.Join(", ", QueryLinkRows.Select(r => r.QueryLabel));

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddQueryLinkCommand    { get; }
        public ReactiveCommand<QueryLinkRowViewModel, System.Reactive.Unit> DeleteQueryLinkCommand { get; }

        public PromptRowViewModel(
            ObservableCollection<PromptRowViewModel> allRows,
            IReadOnlyList<string>                    allTextLabels,
            IReadOnlyList<string>                    allQueryLabels)
        {
            _allRows        = allRows;
            _allTextLabels  = allTextLabels;
            _allQueryLabels = allQueryLabels;

            QueryLinkRows.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                    foreach (QueryLinkRowViewModel row in e.NewItems)
                        row.PropertyChanged += (_, args) =>
                        {
                            if (args.PropertyName == nameof(QueryLinkRowViewModel.QueryLabel))
                                RefreshQuerySuggestions();
                        };

                RefreshQuerySuggestions();
            };

            AddQueryLinkCommand = ReactiveCommand.Create(() =>
                QueryLinkRows.Add(new QueryLinkRowViewModel(QueryLinkRows, _allQueryLabels)));
            DeleteQueryLinkCommand = ReactiveCommand.Create<QueryLinkRowViewModel>(
                row => QueryLinkRows.Remove(row));
        }

        private void RefreshQuerySuggestions()
        {
            this.RaisePropertyChanged(nameof(QueriesSummary));
            foreach (var row in QueryLinkRows)
                row.NotifyAvailableSuggestions();
        }

        public static PromptRowViewModel From(
            PromptEntry p,
            ObservableCollection<PromptRowViewModel> allRows,
            IReadOnlyList<string>                    allTextLabels,
            IReadOnlyList<string>                    allQueryLabels)
        {
            var vm = new PromptRowViewModel(allRows, allTextLabels, allQueryLabels) { TextLabel = p.Text };
            foreach (var q in p.Queries)
                vm.QueryLinkRows.Add(new QueryLinkRowViewModel(vm.QueryLinkRows, allQueryLabels) { QueryLabel = q });
            return vm;
        }

        public PromptEntry ToConfig() => new()
        {
            Text    = TextLabel,
            Queries = QueryLinkRows.Select(r => r.QueryLabel)
                                   .Where(s => !string.IsNullOrWhiteSpace(s))
                                   .Distinct()
                                   .ToArray()
        };
    }
}
