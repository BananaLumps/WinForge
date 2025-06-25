
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;

namespace WinForge.IPC
{
    public static class Client
    {
        public static event EventHandler<IPCMessage>? OnMessageReceived;
        public static event EventHandler<IPCMessage>? OnResponseReceived;

        private static readonly ConcurrentDictionary<string, PipeMessenger> _messengers = new();

        /// <summary> Sends a message to the specified named pipe. Does not wait for a response. </summary>
        public static async Task SendMessageAsync(IPCMessage message)
        {
            using var client = new NamedPipeClientStream(".", message.To, PipeDirection.Out);
            await client.ConnectAsync(1000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            var json = JsonSerializer.Serialize(message);
            await writer.WriteAsync(json);
        }

        /// <summary> Sends a message and waits for a response. </summary>
        public static async Task SendMessageAndWaitAsync(IPCMessage message, int timeoutMs = 0)
        {
            if (timeoutMs == 0) timeoutMs = Settings.Application.IPCResponseTimeout;
            //ToDo: Implement a response waiting mechanism
            await Task.Delay(1000);
        }

        /// <summary> Register an event handler for a given pipe name. </summary>        
        public static PipeMessenger RegisterListener(string pipeName, EventHandler<IPCMessage> onMessageReceived, EventHandler<IPCMessage> onResponse, EventHandler<IPCMessage> onCommand)
        {
            var messenger = _messengers.GetOrAdd(pipeName, name => new PipeMessenger(name));
            messenger.OnMessageReceived += onMessageReceived;
            messenger.OnResponseReceived += onResponse;
            messenger.OnCommandReceived += onCommand;
            return messenger;
        }

        /// <summary> Unregister an event handler for a given pipe name. </summary>
        public static void UnregisterListener(string pipeName, EventHandler<IPCMessage> handler)
        {
            if (_messengers.TryGetValue(pipeName, out var messenger))
            {
                messenger.OnMessageReceived -= handler;
            }
        }

        /// <summary> Dispose of the PipeMessenger for a given pipe name. </summary>
        public static void Shutdown(string pipeName)
        {
            if (_messengers.TryRemove(pipeName, out var messenger))
            {
                messenger.Dispose();
            }
        }
    }
}
