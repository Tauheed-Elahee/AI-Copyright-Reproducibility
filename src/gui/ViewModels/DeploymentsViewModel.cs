using System;
using System.Collections.Generic;
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
    public sealed class ConnectionFieldRowViewModel : ViewModelBase
    {
        private string _key = "", _value = "";
        public string Key   { get => _key;   set => this.RaiseAndSetIfChanged(ref _key,   value); }
        public string Value { get => _value; set => this.RaiseAndSetIfChanged(ref _value, value); }
    }

    public sealed class ParameterRowViewModel : ViewModelBase
    {
        private string _key = "", _value = "";
        public string Key   { get => _key;   set => this.RaiseAndSetIfChanged(ref _key,   value); }
        public string Value { get => _value; set => this.RaiseAndSetIfChanged(ref _value, value); }
    }

    public sealed class UnsupportedParamRowViewModel : ViewModelBase
    {
        private string _key             = "";
        private string _intendedValue   = "";
        private string _errorType       = "";
        private string _reason          = "";
        private string _responseStatus  = "";
        private string _responseContent = "";

        public string Key             { get => _key;             set => this.RaiseAndSetIfChanged(ref _key,             value); }
        public string IntendedValue   { get => _intendedValue;   set => this.RaiseAndSetIfChanged(ref _intendedValue,   value); }
        public string ErrorType       { get => _errorType;       set => this.RaiseAndSetIfChanged(ref _errorType,       value); }
        public string Reason          { get => _reason;          set => this.RaiseAndSetIfChanged(ref _reason,          value); }
        public string ResponseStatus  { get => _responseStatus;  set => this.RaiseAndSetIfChanged(ref _responseStatus,  value); }
        public string ResponseContent { get => _responseContent; set => this.RaiseAndSetIfChanged(ref _responseContent, value); }
    }

    public sealed class DeploymentRowViewModel : ViewModelBase
    {
        private string         _label    = "";
        private DeploymentMode _mode     = DeploymentMode.AzureModeApi;
        private string         _endpoint = "";

        public static IReadOnlyList<DeploymentMode> AvailableModes { get; } =
            Enum.GetValues<DeploymentMode>().ToArray();

        public string         Label    { get => _label;    set => this.RaiseAndSetIfChanged(ref _label,    value); }
        public DeploymentMode Mode     { get => _mode;     set => this.RaiseAndSetIfChanged(ref _mode,     value); }
        public string         Endpoint { get => _endpoint; set => this.RaiseAndSetIfChanged(ref _endpoint, value); }

        public ObservableCollection<ConnectionFieldRowViewModel> ConnectionFieldRows { get; } = new();
        public ObservableCollection<ParameterRowViewModel>       ParameterRows       { get; } = new();

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>      AddConnectionFieldCommand    { get; }
        public ReactiveCommand<ConnectionFieldRowViewModel, System.Reactive.Unit> DeleteConnectionFieldCommand { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>      AddParameterCommand          { get; }
        public ReactiveCommand<ParameterRowViewModel, System.Reactive.Unit>     DeleteParameterCommand       { get; }

        public ObservableCollection<UnsupportedParamRowViewModel> UnsupportedParameterRows { get; } = new();

        private UnsupportedParamRowViewModel? _selectedUnsupportedParam;
        public UnsupportedParamRowViewModel? SelectedUnsupportedParam
        {
            get => _selectedUnsupportedParam;
            set => this.RaiseAndSetIfChanged(ref _selectedUnsupportedParam, value);
        }

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddUnsupportedParameterCommand    { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> DeleteUnsupportedParameterCommand { get; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> PromoteToParametersCommand        { get; }
        public ReactiveCommand<ParameterRowViewModel, System.Reactive.Unit> DemoteToUnsupportedCommand       { get; }

        public DeploymentRowViewModel()
        {
            AddConnectionFieldCommand    = ReactiveCommand.Create(() => ConnectionFieldRows.Add(new ConnectionFieldRowViewModel()));
            DeleteConnectionFieldCommand = ReactiveCommand.Create<ConnectionFieldRowViewModel>(r => ConnectionFieldRows.Remove(r));
            AddParameterCommand    = ReactiveCommand.Create(() => ParameterRows.Add(new ParameterRowViewModel()));
            DeleteParameterCommand = ReactiveCommand.Create<ParameterRowViewModel>(r => ParameterRows.Remove(r));

            var hasSelectedUnsupported = this.WhenAnyValue(x => x.SelectedUnsupportedParam).Select(r => r != null);

            AddUnsupportedParameterCommand = ReactiveCommand.Create(() =>
            {
                var r = new UnsupportedParamRowViewModel();
                UnsupportedParameterRows.Add(r);
                SelectedUnsupportedParam = r;
            });
            DeleteUnsupportedParameterCommand = ReactiveCommand.Create(() =>
            {
                if (SelectedUnsupportedParam == null) return;
                int idx = UnsupportedParameterRows.IndexOf(SelectedUnsupportedParam);
                UnsupportedParameterRows.Remove(SelectedUnsupportedParam);
                SelectedUnsupportedParam = UnsupportedParameterRows.Count > 0
                    ? UnsupportedParameterRows[Math.Max(0, idx - 1)] : null;
            }, hasSelectedUnsupported);
            PromoteToParametersCommand = ReactiveCommand.Create(() =>
            {
                if (SelectedUnsupportedParam == null) return;
                ParameterRows.Add(new ParameterRowViewModel
                    { Key = SelectedUnsupportedParam.Key, Value = SelectedUnsupportedParam.IntendedValue });
                var toRemove = SelectedUnsupportedParam;
                SelectedUnsupportedParam = null;
                UnsupportedParameterRows.Remove(toRemove);
            }, hasSelectedUnsupported);
            DemoteToUnsupportedCommand = ReactiveCommand.Create<ParameterRowViewModel>(r =>
            {
                var newRow = new UnsupportedParamRowViewModel { Key = r.Key, IntendedValue = r.Value };
                UnsupportedParameterRows.Add(newRow);
                ParameterRows.Remove(r);
                SelectedUnsupportedParam = newRow;
            });
        }

        public static DeploymentRowViewModel From(DeploymentConfig d)
        {
            var vm = new DeploymentRowViewModel
            {
                Label    = d.Label,
                Mode     = d.Mode,
                Endpoint = d.Connection.Endpoint ?? "",
            };
            foreach (var (k, v) in d.Connection.Fields)
                vm.ConnectionFieldRows.Add(new ConnectionFieldRowViewModel { Key = k, Value = v });
            foreach (var (k, v) in d.Parameters)
                vm.ParameterRows.Add(new ParameterRowViewModel { Key = k, Value = v.ToString() });
            foreach (var (k, v) in d.UnsupportedParameters)
                vm.UnsupportedParameterRows.Add(new UnsupportedParamRowViewModel
                {
                    Key             = k,
                    IntendedValue   = v.IntendedValue?.ToString() ?? "",
                    ErrorType       = v.ErrorType,
                    Reason          = v.Reason,
                    ResponseStatus  = v.Response.Status.ToString(),
                    ResponseContent = v.Response.Content,
                });
            vm.SelectedUnsupportedParam = vm.UnsupportedParameterRows.FirstOrDefault();
            return vm;
        }

        public DeploymentConfig ToConfig() => new()
        {
            Label      = Label,
            Mode       = Mode,
            Connection = new DeploymentConnectionConfig
            {
                Endpoint = string.IsNullOrWhiteSpace(Endpoint) ? null : Endpoint,
                Fields   = ConnectionFieldRows
                               .Where(r => !string.IsNullOrWhiteSpace(r.Key))
                               .ToDictionary(r => r.Key, r => r.Value),
            },
            Parameters = ParameterRows
                             .Where(r => !string.IsNullOrWhiteSpace(r.Key))
                             .ToDictionary(r => r.Key, r => ParseJsonValue(r.Value)),
            UnsupportedParameters = UnsupportedParameterRows
            .Where(r => !string.IsNullOrWhiteSpace(r.Key))
            .ToDictionary(r => r.Key, r => new UnsupportedParam
            {
                IntendedValue = string.IsNullOrWhiteSpace(r.IntendedValue)
                    ? (JsonElement?)null
                    : ParseJsonValue(r.IntendedValue),
                ErrorType = r.ErrorType,
                Reason    = r.Reason,
                Response  = new UnsupportedParamResponse
                {
                    Status  = int.TryParse(r.ResponseStatus, out var s) ? s : 0,
                    Content = r.ResponseContent,
                }
            }),
        };

        private static JsonElement ParseJsonValue(string s)
        {
            string json;
            if (long.TryParse(s, out _))
                json = s;
            else if (double.TryParse(s, System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out _))
                json = s;
            else if (s is "true" or "false")
                json = s;
            else
                json = JsonSerializer.Serialize(s);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    public sealed class DeploymentsViewModel : ViewModelBase
    {
        private DeploymentRowViewModel? _selectedDeployment;
        private string?                 _filePath;
        private string?                 _saveError;
        private bool                    _saveSuccess;

        public ObservableCollection<DeploymentRowViewModel> Deployments { get; } = new();

        public DeploymentRowViewModel? SelectedDeployment
        {
            get => _selectedDeployment;
            set => this.RaiseAndSetIfChanged(ref _selectedDeployment, value);
        }

        public bool    CanEdit  => _filePath != null;
        public bool    HasItems => Deployments.Count > 0;

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

        public DeploymentsViewModel()
        {
            Deployments.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(HasItems));

            SaveCommand   = ReactiveCommand.Create(ExecuteSave);
            AddCommand    = ReactiveCommand.Create(ExecuteAdd);
            DeleteCommand = ReactiveCommand.Create(ExecuteDelete,
                this.WhenAnyValue(x => x.SelectedDeployment).Select(d => d != null));
        }

        public void LoadFrom(List<DeploymentConfig> deployments, string? filePath)
        {
            Deployments.Clear();
            foreach (var d in deployments)
                Deployments.Add(DeploymentRowViewModel.From(d));
            SelectedDeployment = Deployments.FirstOrDefault();
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
                var wrapper = new { deployments = Deployments.Select(d => d.ToConfig()).ToList() };
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
            var d = new DeploymentRowViewModel { Label = "new_deployment" };
            Deployments.Add(d);
            SelectedDeployment = d;
        }

        private void ExecuteDelete()
        {
            if (SelectedDeployment == null) return;
            int idx = Deployments.IndexOf(SelectedDeployment);
            Deployments.Remove(SelectedDeployment);
            SelectedDeployment = Deployments.Count > 0 ? Deployments[Math.Max(0, idx - 1)] : null;
        }
    }
}
