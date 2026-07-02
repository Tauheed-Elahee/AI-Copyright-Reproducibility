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
        private string                    _textLabel      = "";
        private readonly IReadOnlyList<string> _allQueryLabels;

        public string TextLabel
        {
            get => _textLabel;
            set => this.RaiseAndSetIfChanged(ref _textLabel, value);
        }

        public ObservableCollection<QueryLinkRowViewModel> QueryLinkRows { get; } = new();

        public string QueriesSummary =>
            QueryLinkRows.Count == 0
                ? "(none)"
                : string.Join(", ", QueryLinkRows.Select(r => r.QueryLabel));

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddQueryLinkCommand    { get; }
        public ReactiveCommand<QueryLinkRowViewModel, System.Reactive.Unit> DeleteQueryLinkCommand { get; }

        public PromptRowViewModel(IReadOnlyList<string> allQueryLabels)
        {
            _allQueryLabels = allQueryLabels;

            QueryLinkRows.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                    foreach (QueryLinkRowViewModel row in e.NewItems)
                        row.PropertyChanged += (_, args) =>
                        {
                            if (args.PropertyName == nameof(QueryLinkRowViewModel.QueryLabel))
                                RefreshSuggestions();
                        };

                RefreshSuggestions();
            };

            AddQueryLinkCommand = ReactiveCommand.Create(() =>
                QueryLinkRows.Add(new QueryLinkRowViewModel(QueryLinkRows, _allQueryLabels)));
            DeleteQueryLinkCommand = ReactiveCommand.Create<QueryLinkRowViewModel>(
                row => QueryLinkRows.Remove(row));
        }

        private void RefreshSuggestions()
        {
            this.RaisePropertyChanged(nameof(QueriesSummary));
            foreach (var row in QueryLinkRows)
                row.NotifyAvailableSuggestions();
        }

        public static PromptRowViewModel From(PromptEntry p, IReadOnlyList<string> allQueryLabels)
        {
            var vm = new PromptRowViewModel(allQueryLabels) { TextLabel = p.Text };
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
