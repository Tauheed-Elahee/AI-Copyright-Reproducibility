using System;
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
    public sealed class SecretRowViewModel : ViewModelBase
    {
        private string _key   = "";
        private string _value = "";

        public string Key   { get => _key;   set => this.RaiseAndSetIfChanged(ref _key, value); }
        public string Value { get => _value; set => this.RaiseAndSetIfChanged(ref _value, value); }
    }

    public sealed class SecretsViewModel : ViewModelBase
    {
        private SecretRowViewModel? _selectedRow;
        private string?             _filePath;
        private string?             _saveError;
        private bool                _saveSuccess;

        public ObservableCollection<SecretRowViewModel> Rows { get; } = new();

        public SecretRowViewModel? SelectedRow
        {
            get => _selectedRow;
            set => this.RaiseAndSetIfChanged(ref _selectedRow, value);
        }

        public bool    CanEdit     => _filePath != null;
        public bool    HasRows     => Rows.Count > 0;

        public string? SaveError
        {
            get => _saveError;
            private set => this.RaiseAndSetIfChanged(ref _saveError, value);
        }

        public bool SaveSuccess
        {
            get => _saveSuccess;
            private set => this.RaiseAndSetIfChanged(ref _saveSuccess, value);
        }

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SaveCommand   { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddCommand    { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> DeleteCommand { get; }

        public SecretsViewModel()
        {
            Rows.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(HasRows));

            SaveCommand   = ReactiveCommand.Create(ExecuteSave);
            AddCommand    = ReactiveCommand.Create(ExecuteAdd);
            DeleteCommand = ReactiveCommand.Create(ExecuteDelete,
                this.WhenAnyValue(x => x.SelectedRow).Select(r => r != null));
        }

        public void LoadFrom(SecretsConfig cfg, string? filePath)
        {
            Rows.Clear();
            foreach (var kv in cfg.Keys)
                Rows.Add(new SecretRowViewModel { Key = kv.Key, Value = kv.Value });
            SelectedRow = Rows.FirstOrDefault();
            _filePath   = filePath;
            this.RaisePropertyChanged(nameof(CanEdit));
            SaveError   = null;
            SaveSuccess = false;
        }

        private void ExecuteSave()
        {
            if (_filePath == null) return;
            try
            {
                var dict    = Rows.ToDictionary(r => r.Key, r => r.Value);
                var wrapper = new { keys = dict };
                var opts    = new JsonSerializerOptions(ProjectLoader.ReadOpts) { WriteIndented = true };
                File.WriteAllText(_filePath, JsonSerializer.Serialize(wrapper, opts));
                SaveError   = null;
                SaveSuccess = true;
                _ = Task.Delay(2500).ContinueWith(_ =>
                    Dispatcher.UIThread.Post(() => SaveSuccess = false));
            }
            catch (Exception ex)
            {
                SaveError = $"Save failed: {ex.Message}";
            }
        }

        private void ExecuteAdd()
        {
            var r = new SecretRowViewModel { Key = "new_key" };
            Rows.Add(r);
            SelectedRow = r;
        }

        private void ExecuteDelete()
        {
            if (SelectedRow == null) return;
            int idx = Rows.IndexOf(SelectedRow);
            Rows.Remove(SelectedRow);
            SelectedRow = Rows.Count > 0 ? Rows[Math.Max(0, idx - 1)] : null;
        }
    }
}
