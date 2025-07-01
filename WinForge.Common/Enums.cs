namespace WinForge.Common
{
    public enum LogLevel
    {
        Info,
        Plugin,
        Warning,
        Error,
        Debug,
        Verbose
    }
    public enum ModuleStatus
    {
        NotStarted = 0,
        Starting = 1,
        Running = 2,
        Stopping = 3,
        Stopped = 4,
        Error = 5
    }
}
namespace WinForge.IPC
{
    public enum IPCMessageType
    {
        Request, // A message that expects a response
        Response, // Reply to a request
        Notification, // Informational message without a response expected
        Command // Command to execute, may or may not expect a response
    }
}