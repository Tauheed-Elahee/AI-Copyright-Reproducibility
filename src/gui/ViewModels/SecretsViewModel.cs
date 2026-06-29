using System;
using System.Collections.ObjectModel;
using AICopyrightReproducibility.Config;
using ReactiveUI;

namespace AICopyrightReproducibility.Gui.ViewModels
{
    public sealed class SecretKeyEntry : ReactiveObject
    {
        private readonly string _actual;
        private bool            _show;

        public string Key { get; }

        public bool ShowValue
        {
            get => _show;
            set
            {
                this.RaiseAndSetIfChanged(ref _show, value);
                this.RaisePropertyChanged(nameof(DisplayValue));
            }
        }

        public string DisplayValue =>
            _show ? _actual : new string('•', Math.Min(_actual.Length, 16));

        public SecretKeyEntry(string key, string actual)
        {
            Key     = key;
            _actual = actual;
        }
    }

    public sealed class SecretsViewModel : ViewModelBase
    {
        public ObservableCollection<SecretKeyEntry> Items { get; } = new();
        public bool HasItems => Items.Count > 0;

        public void LoadFrom(SecretsConfig cfg)
        {
            Items.Clear();
            foreach (var kv in cfg.Keys)
                Items.Add(new SecretKeyEntry(kv.Key, kv.Value));
        }
    }
}
