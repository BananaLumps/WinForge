using WinForge.IPC;

namespace WinForge.Common
{
    public interface ICommunication
    {

        s
        /// <summary>
        /// The name of the named pipe used for IPC communication. To avoid conflicts, ensure that the name is unique across all registered pipes. It is recommended to use the project name as a prefix to the pipe name. Eg, WinForge.Common.MyPipeName
        /// </summary>
        public string PipeName { get; set; }
        public Task SendMessageAsync(IPCMessage message);
        public Task<IPCMessage> SendMessageAndWaitForResponseAsync(IPCMessage message, int timeoutMs = 0);
        public PipeMessenger RegisterListener(string pipeName, EventHandler<IPCMessage> onMessageReceived, EventHandler<IPCMessage> onResponseReceived, EventHandler<IPCMessage> onCommandReceived);
        public void UnregisterListener(string pipeName, EventHandler<IPCMessage> handler);
        public void Shutdown(string pipeName);
    }
}
