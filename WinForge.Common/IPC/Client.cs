
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using WinForge.Common;

namespace WinForge.IPC
{
    public class Client : ICommunication
    {
        public static event EventHandler<IPCMessage>? OnMessageReceived;
        public static event EventHandler<IPCMessage>? OnResponseReceived;
        private static readonly ConcurrentDictionary<string, PipeMessenger> _messengers = new();

        public string PipeName { get; set; }

        /// <summary> Sends a message to the specified named pipe. Does not wait for a response. </summary>
        public async Task SendMessageAsync(IPCMessage message)
        {
            using var client = new NamedPipeClientStream(".", message.To, PipeDirection.Out);
            await client.ConnectAsync(1000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            var json = JsonSerializer.Serialize(message);
            await writer.WriteAsync(json);
        }

        /// <summary> Sends a message and waits for a response. If timeout is set to 0 it will use the default timeout as set in application settings.</summary>
        public async Task<IPCMessage> SendMessageAndWaitForResponseAsync(IPCMessage message, int timeoutMs = 0)
        {
            if (timeoutMs == 0)
                timeoutMs = Settings.Application.IPCResponseTimeout;

            message.MessageType = IPCMessageType.Request;

            // Task that will finish when the correct response arrives
            var tcs = new TaskCompletionSource<IPCMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(object? sender, IPCMessage res)
            {
                if (res.ResponseId == message.ResponseId)
                {
                    tcs.TrySetResult(res);
                }
            }

            OnResponseReceived += Handler;

            try
            {
                await SendMessageAsync(message);

                using var cts = new CancellationTokenSource(timeoutMs);
                using (cts.Token.Register(() => tcs.TrySetCanceled(cts.Token)))
                {
                    return await tcs.Task;   // Wait here until event completes the TCS
                }
            }
            finally
            {
                // Always detach to avoid leaks
                OnResponseReceived -= Handler;
            }
        }

        /// <summary> Register an event handler for a given pipe name. </summary>        
        public PipeMessenger RegisterListener(string pipeName, EventHandler<IPCMessage> onMessageReceived, EventHandler<IPCMessage> onResponse, EventHandler<IPCMessage> onCommand)
        {
            var messenger = _messengers.GetOrAdd(pipeName, name => new PipeMessenger(name));
            messenger.OnMessageReceived += onMessageReceived;
            messenger.OnResponseReceived += onResponse;
            messenger.OnCommandReceived += onCommand;
            return messenger;
        }

        /// <summary> Unregister an event handler for a given pipe name. </summary>
        public void UnregisterListener(string pipeName, EventHandler<IPCMessage> handler)
        {
            if (_messengers.TryGetValue(pipeName, out var messenger))
            {
                messenger.OnMessageReceived -= handler;
            }
        }

        /// <summary> Dispose of the PipeMessenger for a given pipe name. </summary>
        public void Shutdown(string pipeName)
        {
            if (_messengers.TryRemove(pipeName, out var messenger))
            {
                messenger.Dispose();
            }
        }
    }
}
