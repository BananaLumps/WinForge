using System.IO.Pipes;
using System.Text.Json;
using WinForge.Common;

namespace WinForge.IPC
{
    public class PipeMessenger : IDisposable
    {
        private readonly string _pipeName;
        private bool _running = true;
        private const int BUFFER_SIZE = 4096;

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
                try
                {
                    using var server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous
                    );
                    Logger.Instance.Log($"Starting pipe server for: {_pipeName}", LogLevel.Info, "WinForge.Common.PipeMessenger");
                    await server.WaitForConnectionAsync();
                    await HandleClientConnection(server);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Error in pipe server: {ex.Message}", LogLevel.Error, "WinForge.Common.PipeMessenger");
                    await Task.Delay(1000); // Prevent tight loop on failure
                }
            }
        }

        private async Task HandleClientConnection(NamedPipeServerStream server)
        {
            Logger.Instance.Log($"Client connected to pipe: {_pipeName}", LogLevel.Verbose, "WinForge.Common.PipeMessenger");
            byte[] buffer = new byte[BUFFER_SIZE];
            using var ms = new MemoryStream();

            try
            {
                int bytesRead;
                do
                {
                    bytesRead = await server.ReadAsync(buffer);
                    await ms.WriteAsync(buffer.AsMemory(0, bytesRead));
                } while (!server.IsMessageComplete);

                string messageJson = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                Logger.Instance.Log($"Received message: {messageJson}", LogLevel.Info, "WinForge.Common.PipeMessenger");
                if (string.IsNullOrEmpty(messageJson)) return;
                var message = default(IPCMessage);
                try
                {
                    message = JsonSerializer.Deserialize<IPCMessage>(messageJson);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"[Deserialize Error] {ex.Message}\n Check client and server pipes are identical. InOut on both works best.", LogLevel.Error, "WinForge.Common.PipeMessenger");
                }
                if (message == null) return;

                switch (message.MessageType)
                {
                    case IPCMessageType.Request:
                        Logger.Instance.Log($"Request received: {message.Message}", LogLevel.Verbose, "WinForge.Common.PipeMessenger");
                        OnMessageReceived?.Invoke(this, message);
                        break;
                    case IPCMessageType.Response:
                        Logger.Instance.Log($"Response received: {message.Message}", LogLevel.Verbose, "WinForge.Common.PipeMessenger");
                        OnResponseReceived?.Invoke(this, message);
                        break;
                    case IPCMessageType.Notification:
                        Logger.Instance.Log($"Notification received: {message.Message}", LogLevel.Verbose, "WinForge.Common.PipeMessenger");
                        OnMessageReceived?.Invoke(this, message);
                        break;
                    case IPCMessageType.Command:
                        Logger.Instance.Log($"Command received: {message.Message}", LogLevel.Verbose, "WinForge.Common.PipeMessenger");
                        OnCommandReceived?.Invoke(this, message);
                        break;
                }
            }
            catch (Exception e)
            {
                Logger.Instance.Log($"Error handling client connection: {e.Message}", LogLevel.Error, "WinForge.Common.PipeMessenger");
            }
            finally
            {
                if (server.IsConnected)
                {
                    server.Disconnect();
                }
            }
        }

        public async Task SendMessageAsync(IPCMessage message)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await client.ConnectAsync(5000); // 5 second timeout

                var messageJson = JsonSerializer.Serialize(message);
                var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageJson);

                await client.WriteAsync(messageBytes);
                await client.FlushAsync();
            }
            catch (Exception e)
            {
                Logger.Instance.Log($"Error sending message: {e.Message}", LogLevel.Error, "WinForge.Common.PipeMessenger");
            }
        }

        public void Dispose()
        {
            _running = false;
            GC.SuppressFinalize(this);
        }
    }
}
