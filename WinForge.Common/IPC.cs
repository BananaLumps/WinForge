
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using WinForge.Common;

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
        public static void RegisterListener(string pipeName, EventHandler<IPCMessage> onMessageReceived, EventHandler<IPCMessage> onResponse)
        {
            var messenger = _messengers.GetOrAdd(pipeName, name => new PipeMessenger(name));
            messenger.OnMessageReceived += onMessageReceived;
            messenger.OnResponseReceived += onResponse;
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
    //ToDo: Add message confirmation system
    public class IPCMessage
    {
        /// <summary> Pipe name to send the message to. </summary>        
        public string To { get; set; }
        /// <summary> Pipe name of the sender. </summary>        
        public string From { get; set; }
        /// <summary> The message content. </summary>        
        public string Message { get; set; }
        /// <summary> Optional data payloads, can be null. </summary>       
        public object[]? Data { get; set; }
        /// <summary> Timestamp of when the message was created. </summary>       
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Guid MessageId { get; set; } = Guid.NewGuid(); // Unique ID for tracking
        public Guid ResponseId { get; set; } = Guid.Empty; // ID to match responses to requests
        public IPCMessageType MessageType { get; set; } = IPCMessageType.Notification; // Type of the message

        /// <summary> Response constructor, used to create a response message from a request. </summary>       
        public IPCMessage(IPCMessage request, string response, object[]? data = null)
        {
            To = request.From;
            From = request.To;
            Message = response;
            Data = data;
            ResponseId = request.ResponseId;
            MessageType = IPCMessageType.Response;
        }
        public IPCMessage(string to, string from, string message, IPCMessageType messageType, object[]? data = null)
        {
            To = to;
            From = from;
            Message = message;
            Data = data;
            MessageType = messageType;
            if (messageType == IPCMessageType.Request)
            {
                ResponseId = Guid.NewGuid();
            }
        }
    }
    public enum IPCMessageType
    {
        Request, // A message that expects a response
        Response, // Reply to a request
        Notification, // Informational message without a response expected
        Command // Command to execute, may or may not expect a response
    }
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
    public class HTTPManager : IDisposable
    {
        private const int UdpPort = 5600;
        private const int TcpPort = 5601;
        private static readonly TimeSpan BeaconInterval = TimeSpan.FromSeconds(15);

        private static readonly CancellationTokenSource _cts = new();
        private static readonly ConcurrentDictionary<string, TcpClient> _connectedClients = new();

        /// <summary> Returns a list of all currently connected client pipe names. </summary>        
        public static IReadOnlyCollection<string> GetConnectedTCPClientPipeNames()
        {
            return _connectedClients.Keys.ToList();
        }
        /// <summary> Starts UDP beacon + TCP server in background.</summary>
        public static void StartServer(string serverPipeName)
        {
            Task.Run(() => BroadcastLoopAsync(serverPipeName, _cts.Token));
            Task.Run(() => TcpServerLoopAsync(_cts.Token));
        }
        /// <summary> Stops beacon & TCP server and closes all sockets.</summary>
        public void Dispose() => _cts.Cancel();
        /// <summary> Listen once for a UDP beacon and return its IPEndPoint.</summary>
        public static async Task<IPEndPoint?> ListenForBeaconAsync(int timeoutMs = 35000)
        {
            using var udp = new UdpClient(UdpPort) { EnableBroadcast = true };
            var tokenSource = new CancellationTokenSource(timeoutMs);

            try
            {
                var result = await udp.ReceiveAsync(tokenSource.Token);
                return result.RemoteEndPoint;
            }
            catch (OperationCanceledException)
            {
                return null; // timeout
            }
        }
        /// <summary> Connect to server and send our pipe name as JSON handshake.</summary>
        public static async Task<TcpClient> ConnectAsync(IPEndPoint serverEndPoint, string clientPipeName, int timeoutMs = 3000)
        {
            var tcp = new TcpClient();
            await tcp.ConnectAsync(serverEndPoint.Address, TcpPort);
            using var ns = tcp.GetStream();
            using var wtr = new StreamWriter(ns) { AutoFlush = true };

            var handshake = JsonSerializer.Serialize(new { Pipe = clientPipeName });
            await wtr.WriteLineAsync(handshake);

            return tcp;
        }
        private static async Task BroadcastLoopAsync(string pipeName, CancellationToken token)
        {
            using var udp = new UdpClient { EnableBroadcast = true };
            var beacon = JsonSerializer.Serialize(new { Pipe = pipeName });
            var bytes = Encoding.UTF8.GetBytes(beacon);
            var endPoint = new IPEndPoint(IPAddress.Broadcast, UdpPort);

            while (!token.IsCancellationRequested)
            {
                try { await udp.SendAsync(bytes, bytes.Length, endPoint); }
                catch { /* ignore send errors */ }

                try { await Task.Delay(BeaconInterval, token); }
                catch (OperationCanceledException) { break; }
            }
        }
        private static async Task TcpServerLoopAsync(CancellationToken token)
        {
            TcpListener listener = new(IPAddress.Any, TcpPort);
            listener.Start();

            while (!token.IsCancellationRequested)
            {
                TcpClient? client = null;
                try { client = await listener.AcceptTcpClientAsync(token); }
                catch when (token.IsCancellationRequested) { break; }
                catch { continue; }

                // Handle this connection in the background
                _ = Task.Run(() => HandleClientAsync(client, token));
            }

            listener.Stop();
        }
        private static async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            var ns = client.GetStream();
            var sr = new StreamReader(ns, leaveOpen: true);
            var pipeName = string.Empty;
            EventHandler<IPCMessage>? handler = null;

            // Expect first line to be JSON { "Pipe": "SomeName" }
            string? line;
            try { line = await sr.ReadLineAsync(); }
            catch { client.Dispose(); return; }

            if (line == null)
            {
                client.Dispose();
                return;
            }

            try
            {
                var obj = JsonSerializer.Deserialize<JsonElement>(line);
                pipeName = obj.GetProperty("Pipe").GetString() ?? "Unknown";

                _connectedClients[pipeName] = client; // keep track by pipe name
                Common.Logger.Info($"Client '{pipeName}' connected.");


            }
            catch
            {
                client.Dispose(); // malformed handshake
            }
            handler = (_, args) =>
            {
                _ = Task.Run(() =>
                {
                    bool ok = SendToClient(pipeName, args);
                });
            };
            Client.RegisterListener(pipeName, handler, handler);
        }
        public static async Task<IPEndPoint?> WaitForBeaconAsync(int timeoutMs = 35000, CancellationToken ct = default)
        {
            using var udp = new UdpClient(UdpPort) { EnableBroadcast = true };

            // Combine caller token with our own timeout token
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                // Wait for the first broadcast datagram
                var result = await udp.ReceiveAsync(linkedCts.Token);

                // Decode and parse the JSON payload
                var json = Encoding.UTF8.GetString(result.Buffer);
                var root = JsonSerializer.Deserialize<JsonElement>(json);
                var pipeName = root.GetProperty("Pipe").GetString() ?? "Unknown";
                if (pipeName == "WinForge.Base")
                {
                    return (result.RemoteEndPoint);
                }
                return null; // Ignore beacons from other applications
            }
            catch (OperationCanceledException)
            {
                // timeout or external cancellation
                return null;
            }
            catch (Exception ex)
            {
                // malformed datagram or JSON error – log and ignore
                Logger.Warn($"Beacon listener error: {ex.Message}");
                return null;
            }
        }

        /// <summary> Sends a message to a connected TCP client using its pipe name. </summary>        
        public static bool SendToClient(string pipeName, IPCMessage message)
        {
            if (!_connectedClients.TryGetValue(pipeName, out var tcpClient))
                return false;

            try
            {
                var stream = tcpClient.GetStream();
                var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
                var json = JsonSerializer.Serialize(message);
                writer.WriteLine(json);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn($"[SendToClient] Failed to send to '{pipeName}': {ex.Message}");

                if (_connectedClients.TryRemove(pipeName, out var dead))
                    dead.Dispose();

                return false;
            }
        }
    }
}
