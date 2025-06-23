using System.IO.Pipes;
using System.Text.Json;

namespace WinForge.IPC
{
    public class PipeMessenger : IDisposable
    {
        private readonly string _pipeName;
        private bool _running = true;
        public event EventHandler<IPCMessage>? OnMessageReceived;
        public event EventHandler<IPCMessage>? OnResponseReceived;
        public event EventHandler<IPCMessage>? OnCommandReceived;
        public PipeMessenger(string pipeName)
        {
            _pipeName = pipeName;
            Task.Run(() => StartServer());
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "This is a Windows only application")]
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
                        var data = JsonSerializer.Deserialize<IPCMessage>(msg);
                        switch (data!.MessageType)
                        {
                            case IPCMessageType.Request:
                                OnMessageReceived?.Invoke(this, data);
                                break;
                            case IPCMessageType.Response:
                                OnResponseReceived?.Invoke(this, data);
                                break;
                            case IPCMessageType.Notification:
                                OnMessageReceived?.Invoke(this, data);
                                break;
                            case IPCMessageType.Command:
                                OnCommandReceived?.Invoke(this, data);
                                break;
                        }

                    }
                    catch
                    {
                        // ignore malformed messages
                    }
                });
            }
        }
        public void Dispose()
        {
            _running = false;
        }
    }
}
