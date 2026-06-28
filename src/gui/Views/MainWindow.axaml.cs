using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using AICopyrightReproducibility.Gui.ViewModels;

namespace AICopyrightReproducibility.Gui.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.LogLines.CollectionChanged += OnLogLinesChanged;
        }

        private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
                Dispatcher.UIThread.Post(() => LogScrollViewer?.ScrollToEnd());
        }
    }
}
