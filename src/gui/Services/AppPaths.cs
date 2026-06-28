using System;
using System.IO;

namespace AICopyrightReproducibility.Gui.Services
{
    /// <summary>
    /// XDG Base Directory Specification paths for the GUI component.
    ///
    /// Linux  — honours $XDG_CONFIG_HOME / $XDG_DATA_HOME / $XDG_STATE_HOME / $XDG_CACHE_HOME
    ///          with the standard fallbacks (~/.config, ~/.local/share, ~/.local/state, ~/.cache).
    /// macOS  — maps to ~/Library/Application Support (config/data/state) and ~/Library/Caches.
    /// Windows — maps to %APPDATA% (config) and %LOCALAPPDATA% (data/state/cache).
    ///
    /// All directories are rooted at aicr/gui/ so sibling components (cli, tui) can
    /// share the same aicr/ parent without collision.
    /// </summary>
    internal static class AppPaths
    {
        private const string App  = "aicr";
        private const string Comp = "gui";

        // ~/.config/aicr/gui/        (Linux)
        // ~/Library/Application Support/aicr/gui/  (macOS)
        // %APPDATA%\aicr\gui\        (Windows)
        //
        // SpecialFolder.ApplicationData already resolves $XDG_CONFIG_HOME on Linux (.NET 6+).
        public static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            App, Comp);

        // ~/.local/share/aicr/gui/   (Linux)
        // ~/Library/Application Support/aicr/gui/  (macOS)
        // %LOCALAPPDATA%\aicr\gui\   (Windows)
        //
        // SpecialFolder.LocalApplicationData resolves $XDG_DATA_HOME on Linux (.NET 6+).
        public static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            App, Comp);

        // ~/.local/state/aicr/gui/   (Linux — $XDG_STATE_HOME)
        // ~/Library/Application Support/aicr/gui/  (macOS — no separate state convention)
        // %LOCALAPPDATA%\aicr\gui\   (Windows — no separate state convention)
        public static readonly string StateDir = ResolveLinuxXdg(
            "XDG_STATE_HOME", ".local/state",
            fallback: Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

        // ~/.cache/aicr/gui/         (Linux — $XDG_CACHE_HOME)
        // ~/Library/Caches/aicr/gui/ (macOS)
        // %LOCALAPPDATA%\aicr\gui\   (Windows — no separate cache folder beyond Temp)
        public static readonly string CacheDir = ResolveCacheDir();

        private static string ResolveLinuxXdg(string envVar, string linuxDefault, string fallback)
        {
            string baseDir;
            if (OperatingSystem.IsLinux())
            {
                baseDir = Environment.GetEnvironmentVariable(envVar)
                    ?? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        linuxDefault);
            }
            else
            {
                baseDir = fallback;
            }
            return Path.Combine(baseDir, App, Comp);
        }

        private static string ResolveCacheDir()
        {
            string baseDir;
            if (OperatingSystem.IsLinux())
            {
                baseDir = Environment.GetEnvironmentVariable("XDG_CACHE_HOME")
                    ?? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".cache");
            }
            else if (OperatingSystem.IsMacOS())
            {
                baseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Caches");
            }
            else
            {
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            return Path.Combine(baseDir, App, Comp);
        }
    }
}
