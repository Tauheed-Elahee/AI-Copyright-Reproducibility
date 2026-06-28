using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using AICopyrightReproducibility.Gui.ViewModels;
using AICopyrightReproducibility.Gui.Views;

namespace AICopyrightReproducibility.Gui
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var vm = new MainViewModel();
                var win = new MainWindow { DataContext = vm };
                desktop.MainWindow = win;

                vm.SetBrowseDelegate(async () =>
                {
                    var result = await win.StorageProvider.OpenFolderPickerAsync(
                        new FolderPickerOpenOptions
                        {
                            Title       = "Select project directory",
                            AllowMultiple = false
                        });
                    return result.Count > 0 ? result[0].TryGetLocalPath() : null;
                });
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
