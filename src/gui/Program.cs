using Avalonia;
using Avalonia.ReactiveUI;
using AICopyrightReproducibility.Gui;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .LogToTrace()
    .UseReactiveUI()
    .StartWithClassicDesktopLifetime(args);
