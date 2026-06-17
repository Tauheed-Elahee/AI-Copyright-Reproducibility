using System;
using System.IO;
using System.Text;

namespace AICopyrightReproducibility
{
    internal sealed class TeeWriter : TextWriter
    {
        private readonly TextWriter _console;
        private readonly TextWriter _file;
        private readonly object _lock = new();

        internal TeeWriter(TextWriter console, TextWriter file)
        {
            _console = console;
            _file    = file;
        }

        public override Encoding Encoding => _console.Encoding;

        public override void Write(char value)
        {
            _console.Write(value);
            lock (_lock) _file.Write(value);
        }

        public override void Write(string? value)
        {
            _console.Write(value);
            lock (_lock) _file.Write(value);
        }

        public override void WriteLine()
        {
            _console.WriteLine();
            lock (_lock) _file.WriteLine();
        }

        public override void WriteLine(string? value)
        {
            _console.WriteLine(value);
            lock (_lock) _file.WriteLine(value);
        }

        public override void Flush()
        {
            _console.Flush();
            lock (_lock) _file.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _file.Dispose();
            base.Dispose(disposing);
        }
    }
}
