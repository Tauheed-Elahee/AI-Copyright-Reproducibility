using System.IO;
using System.Text;
using System.Threading.Channels;

namespace AICopyrightReproducibility.Gui.Services
{
    internal sealed class ChannelTextWriter : TextWriter
    {
        private readonly Channel<string> _channel = Channel.CreateBounded<string>(
            new BoundedChannelOptions(4096) { FullMode = BoundedChannelFullMode.DropOldest });

        public ChannelReader<string> Reader => _channel.Reader;

        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value)
            => _channel.Writer.TryWrite(value ?? "");

        protected override void Dispose(bool disposing)
        {
            _channel.Writer.TryComplete();
            base.Dispose(disposing);
        }
    }
}
