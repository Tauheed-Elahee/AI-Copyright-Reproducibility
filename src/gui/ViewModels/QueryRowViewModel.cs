using System;
using ReactiveUI;
using AICopyrightReproducibility.Config;

namespace AICopyrightReproducibility.Gui.ViewModels
{
    public sealed class QueryRowViewModel : ViewModelBase
    {
        private string _label         = "";
        private string _types         = "";
        private string _systemMessage = "";
        private string _userPrompt    = "";

        public string Label
        {
            get => _label;
            set => this.RaiseAndSetIfChanged(ref _label, value);
        }

        public string Types
        {
            get => _types;
            set => this.RaiseAndSetIfChanged(ref _types, value);
        }

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

        public static QueryRowViewModel From(QueryConfig q) => new()
        {
            Label         = q.Label,
            Types         = q.Types.Length == 0 ? "" : string.Join(", ", q.Types),
            SystemMessage = q.SystemMessage,
            UserPrompt    = q.UserPrompt
        };

        public QueryConfig ToConfig() => new()
        {
            Label         = Label,
            Types         = string.IsNullOrWhiteSpace(Types)
                ? Array.Empty<string>()
                : Types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            SystemMessage = SystemMessage,
            UserPrompt    = UserPrompt
        };
    }
}
