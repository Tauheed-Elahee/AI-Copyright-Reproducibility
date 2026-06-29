using System;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using AICopyrightReproducibility.Gui.ViewModels;
using FluentAvalonia.UI.Controls;
using ReactiveUI;
using ScottPlot;

namespace AICopyrightReproducibility.Gui.Views
{
    public partial class MainWindow : Window
    {
        private bool _suppressNavSync;

        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            vm.LogLines.CollectionChanged += OnLogLinesChanged;
            vm.Charts.Updated += (_, _) => RefreshCharts(vm.Charts);

            vm.WhenAnyValue(x => x.SelectedTabIndex)
              .Subscribe(idx => Dispatcher.UIThread.Post(() => SyncNavViewSelection(idx)));

            SyncNavViewSelection(vm.SelectedTabIndex);
        }

        private void OnNavViewSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
        {
            if (_suppressNavSync) return;
            if (e.SelectedItem is NavigationViewItem item &&
                item.Tag is string tagStr &&
                int.TryParse(tagStr, out int idx) &&
                DataContext is MainViewModel vm)
                vm.SelectedTabIndex = idx;
        }

        private void SyncNavViewSelection(int idx)
        {
            _suppressNavSync = true;
            var item = FindNavItem(idx);
            if (item is not null) NavView.SelectedItem = item;
            _suppressNavSync = false;
        }

        private NavigationViewItem? FindNavItem(int idx)
        {
            var tag = idx.ToString();
            foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
                if (item.Tag as string == tag) return item;
            foreach (var item in NavView.FooterMenuItems.OfType<NavigationViewItem>())
                if (item.Tag as string == tag) return item;
            return null;
        }

        private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
                Dispatcher.UIThread.Post(() => LogScrollViewer?.ScrollToEnd());
        }

        private void RefreshCharts(ChartsViewModel charts)
        {
            RefreshSuccessChart(charts);
            RefreshDurationChart(charts);
        }

        private void RefreshSuccessChart(ChartsViewModel charts)
        {
            var plot = SuccessChart.Plot;
            plot.Clear();

            if (charts.HasData && charts.Labels.Length > 0)
            {
                var bars = plot.Add.Bars(charts.SuccessRates);
                bars.Horizontal = false;

                var positions = Enumerable.Range(0, charts.Labels.Length).Select(i => (double)i).ToArray();
                plot.Axes.Bottom.SetTicks(positions, charts.Labels);
                plot.Axes.Left.Label.Text = "Success %";
                plot.Axes.SetLimitsY(0, 105);
                plot.Title("Success Rate");
            }

            SuccessChart.Refresh();
        }

        private void RefreshDurationChart(ChartsViewModel charts)
        {
            var plot = DurationChart.Plot;
            plot.Clear();

            if (charts.HasData && charts.Labels.Length > 0)
            {
                for (int i = 0; i < charts.Labels.Length; i++)
                {
                    var d = charts.Durations[i];
                    if (d.Length == 0) continue;

                    var sorted = d.OrderBy(x => x).ToArray();
                    var box = new Box
                    {
                        Position   = i,
                        WhiskerMin = sorted[0],
                        BoxMin     = Percentile(sorted, 25),
                        BoxMiddle  = Percentile(sorted, 50),
                        BoxMax     = Percentile(sorted, 75),
                        WhiskerMax = sorted[^1],
                    };
                    plot.Add.Box(box);
                }

                var positions = Enumerable.Range(0, charts.Labels.Length).Select(i => (double)i).ToArray();
                plot.Axes.Bottom.SetTicks(positions, charts.Labels);
                plot.Axes.Left.Label.Text = "ms";
                plot.Title("Duration Distribution (ms)");
            }

            DurationChart.Refresh();
        }

        private static double Percentile(long[] sorted, double p)
        {
            double pos = (sorted.Length - 1) * p / 100.0;
            int    lo  = (int)pos;
            int    hi  = Math.Min(lo + 1, sorted.Length - 1);
            return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
        }
    }
}
