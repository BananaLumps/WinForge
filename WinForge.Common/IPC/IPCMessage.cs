namespace WinForge.IPC
{
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
}
