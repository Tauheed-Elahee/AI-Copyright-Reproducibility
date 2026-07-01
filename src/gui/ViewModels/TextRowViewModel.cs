using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using ReactiveUI;
using AICopyrightReproducibility.Config;

namespace AICopyrightReproducibility.Gui.ViewModels
{
    public sealed class AliasRowViewModel : ViewModelBase
    {
        private string _key   = "";
        private string _value = "";

        public string Key   { get => _key;   set => this.RaiseAndSetIfChanged(ref _key,   value); }
        public string Value { get => _value; set => this.RaiseAndSetIfChanged(ref _value, value); }
    }

    public sealed class SectionHeadingRowViewModel : ViewModelBase
    {
        private string _text = "";
        public string Text { get => _text; set => this.RaiseAndSetIfChanged(ref _text, value); }
    }

    public sealed class TextRowViewModel : ViewModelBase
    {
        private string _label           = "";
        private string _titleFull       = "";
        private string _titleShort      = "";
        private string _sectionHeadings = "";
        private bool   _isSectionMassEditMode;

        private Dictionary<string, JsonElement>? _extraFields;

        public string Label
        {
            get => _label;
            set => this.RaiseAndSetIfChanged(ref _label, value);
        }

        public string TitleFull
        {
            get => _titleFull;
            set => this.RaiseAndSetIfChanged(ref _titleFull, value);
        }

        public string TitleShort
        {
            get => _titleShort;
            set => this.RaiseAndSetIfChanged(ref _titleShort, value);
        }

        // ── Section headings ──────────────────────────────────────────────────

        public ObservableCollection<SectionHeadingRowViewModel> SectionRows { get; } = new();

        public string SectionHeadings
        {
            get => _sectionHeadings;
            set => this.RaiseAndSetIfChanged(ref _sectionHeadings, value);
        }

        public bool IsSectionMassEditMode
        {
            get => _isSectionMassEditMode;
            private set
            {
                this.RaiseAndSetIfChanged(ref _isSectionMassEditMode, value);
                this.RaisePropertyChanged(nameof(SectionModeButtonText));
            }
        }

        public string SectionModeButtonText =>
            _isSectionMassEditMode ? "List" : "Mass Edit";

        public int SectionCount => SectionRows.Count;

        public IReadOnlyList<string> SectionHeadingTexts =>
            SectionRows.Select(r => r.Text).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>    ToggleSectionModeCommand { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>    AddSectionCommand        { get; }
        public ReactiveCommand<SectionHeadingRowViewModel, System.Reactive.Unit> DeleteSectionCommand  { get; }

        // ── Aliases ───────────────────────────────────────────────────────────

        public ObservableCollection<AliasRowViewModel> AliasRows { get; } = new();

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddAliasCommand    { get; }
        public ReactiveCommand<AliasRowViewModel, System.Reactive.Unit>    DeleteAliasCommand { get; }

        public TextRowViewModel()
        {
            SectionRows.CollectionChanged += (_, _) =>
            {
                this.RaisePropertyChanged(nameof(SectionCount));
                this.RaisePropertyChanged(nameof(SectionHeadingTexts));
            };

            ToggleSectionModeCommand = ReactiveCommand.Create(ExecuteToggleSectionMode);
            AddSectionCommand        = ReactiveCommand.Create(() =>
                SectionRows.Add(new SectionHeadingRowViewModel()));
            DeleteSectionCommand     = ReactiveCommand.Create<SectionHeadingRowViewModel>(
                row => SectionRows.Remove(row));

            AddAliasCommand    = ReactiveCommand.Create(() => AliasRows.Add(new AliasRowViewModel()));
            DeleteAliasCommand = ReactiveCommand.Create<AliasRowViewModel>(
                row => AliasRows.Remove(row));
        }

        private void ExecuteToggleSectionMode()
        {
            if (!_isSectionMassEditMode)
            {
                // List → Mass Edit: serialise rows to string
                SectionHeadings = string.Join('\n',
                    SectionRows.Select(r => r.Text));
            }
            else
            {
                // Mass Edit → List: parse string back to rows
                SectionRows.Clear();
                foreach (var line in _sectionHeadings
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    SectionRows.Add(new SectionHeadingRowViewModel { Text = line });
            }
            IsSectionMassEditMode = !_isSectionMassEditMode;
        }

        public static TextRowViewModel From(TextDbEntry t)
        {
            var vm = new TextRowViewModel
            {
                Label        = t.Label,
                TitleFull    = t.Content.Title.Full,
                TitleShort   = t.Content.Title.Short,
                _extraFields = t.Content.Title.ExtraFields
            };

            foreach (var h in t.Content.SectionHeadings)
                vm.SectionRows.Add(new SectionHeadingRowViewModel { Text = h });
            vm._sectionHeadings = string.Join('\n', t.Content.SectionHeadings);

            foreach (var (k, v) in t.Content.Aliases)
                vm.AliasRows.Add(new AliasRowViewModel { Key = k, Value = v });

            return vm;
        }

        public TextDbEntry ToConfig()
        {
            string[] headings = _isSectionMassEditMode
                ? _sectionHeadings.Split('\n',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : SectionRows.Select(r => r.Text)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();

            return new TextDbEntry
            {
                Label = Label,
                Content = new TextContentConfig
                {
                    Title = new TextTitleConfig
                    {
                        Full        = TitleFull,
                        Short       = TitleShort,
                        ExtraFields = _extraFields
                    },
                    SectionHeadings = headings,
                    Aliases = AliasRows
                        .Where(r => !string.IsNullOrWhiteSpace(r.Key))
                        .ToDictionary(r => r.Key, r => r.Value)
                }
            };
        }
    }
}
