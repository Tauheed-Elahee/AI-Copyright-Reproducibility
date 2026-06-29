using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AICopyrightReproducibility.Utils;

namespace AICopyrightReproducibility.Gui.Converters
{
    public sealed class LogLevelBrushConverter : IValueConverter
    {
        public static readonly LogLevelBrushConverter Instance = new();

        private static readonly IBrush VerboseBrush = new SolidColorBrush(Color.FromRgb(168, 168, 168));
        private static readonly IBrush WarningBrush  = new SolidColorBrush(Color.FromRgb(220, 130,   0));
        private static readonly IBrush ErrorBrush    = new SolidColorBrush(Color.FromRgb(210,  50,  45));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is Logger.Level level ? level switch
            {
                Logger.Level.Verbose => VerboseBrush,
                Logger.Level.Warning => WarningBrush,
                Logger.Level.Error   => ErrorBrush,
                _                    => AvaloniaProperty.UnsetValue   // Info — inherit from theme
            } : AvaloniaProperty.UnsetValue;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
