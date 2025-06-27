
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using WinForge.Common;

namespace WinForge.IPC
{
    public class HTTPManager : IDisposable
    {
        private const int UdpPort = 5600;
        private const int TcpPort = 5601;
        private static readonly TimeSpan BeaconInterval = TimeSpan.FromSeconds(15);
        private static ICommunication communication = new Client(); // Default communication method

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
            communication.PipeName = serverPipeName; // Ensure we have a communication instance
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
                Logger.Instance.Info($"Client '{pipeName}' connected.");
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
            communication.RegisterListener(pipeName, handler, handler, handler);
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
                Logger.Instance.Warn($"Beacon listener error: {ex.Message}");
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
                Logger.Instance.Warn($"[SendToClient] Failed to send to '{pipeName}': {ex.Message}");

                if (_connectedClients.TryRemove(pipeName, out var dead))
                    dead.Dispose();

                return false;
            }
        }
    }
}
