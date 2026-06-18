using System;
using System.IO;

namespace AICopyrightReproducibility.Utils
{
    public sealed class Logger : IDisposable
    {
        public enum Level { Verbose = 0, Info = 1, Warning = 2, Error = 3 }

        private readonly TextWriter _out;
        private readonly TextWriter _err;
        private readonly TextWriter _file;
        private readonly TextWriter? _sysFile;
        private Level _minConsole;
        private readonly object _lock = new();

        public Logger(TextWriter @out, TextWriter err, TextWriter file,
                      Level minConsole, TextWriter? sysFile = null)
        {
            _out        = @out;
            _err        = err;
            _file       = file;
            _sysFile    = sysFile;
            _minConsole = minConsole;
        }

        public void SetLevel(Level level) => _minConsole = level;

        public void Verbose(string msg) => Write(Level.Verbose, msg);
        public void Info(string msg)    => Write(Level.Info,    msg);
        public void Warn(string msg)    => Write(Level.Warning, msg);
        public void Error(string msg)   => Write(Level.Error,   msg);

        private void Write(Level level, string msg)
        {
            string tagged    = Tag(level, msg);
            string sysTagged = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} {tagged}";
            lock (_lock)
            {
                _file.WriteLine(tagged);
                _sysFile?.WriteLine(sysTagged);
            }
            if (level < _minConsole) return;
            TextWriter dest = level >= Level.Warning ? _err : _out;
            dest.WriteLine(tagged);
        }

        private static string Tag(Level level, string msg) => level switch
        {
            Level.Verbose => $"[VERBOSE] {msg}",
            Level.Warning => $"[WARN]    {msg}",
            Level.Error   => $"[ERROR]   {msg}",
            _             => msg
        };

        public void Dispose()
        {
            _file.Dispose();
            _sysFile?.Dispose();
        }
    }
}
