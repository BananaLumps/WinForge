
using System.IO.Pipes;
using System.Text.Json;

namespace WinForge.IPC
{
    public class MessageReceivedEventArgs : EventArgs
    {
        public string From { get; set; }
        public string Message { get; set; }
    }

    public class PipeMessenger : IDisposable
    {
        private readonly string _pipeName;
        private bool _running = true;

        public event EventHandler<MessageReceivedEventArgs> OnMessageReceived;

        public PipeMessenger(string pipeName)
        {
            _pipeName = pipeName;
            Task.Run(() => StartServer());
        }

        private async Task StartServer()
        {
            while (_running)
            {
                var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync();

                _ = Task.Run(async () =>
                {
                    using var reader = new StreamReader(server);
                    string msg = await reader.ReadToEndAsync();

                    try
                    {
                        var data = JsonSerializer.Deserialize<PipeMessage>(msg);
                        OnMessageReceived?.Invoke(this, new MessageReceivedEventArgs { From = data.From, Message = data.Message });
                    }
                    catch
                    {
                        // ignore malformed messages
                    }
                });
            }
        }

        public async Task SendMessageAsync(string toPipeName, string from, string message)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", toPipeName, PipeDirection.Out);
                await client.ConnectAsync(1000);

                using var writer = new StreamWriter(client) { AutoFlush = true };
                var json = JsonSerializer.Serialize(new PipeMessage { From = from, Message = message });
                await writer.WriteAsync(json);
            }
            catch
            {
                // handle connection failure or timeout
            }
        }

        public void Dispose()
        {
            _running = false;
        }

        private class PipeMessage
        {
            public string From { get; set; }
            public string Message { get; set; }
        }
    }
}
