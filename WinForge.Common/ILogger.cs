
namespace WinForge.Common
{
    public interface ILogger
    {
        string? Tag { get; set; }
        Task InitializeAsync();
        void EnableFileLogging(string filePath);
        void Log(string message, LogLevel level = LogLevel.Info, string? tag = null, bool includeTimestamp = true);
        void Log(string message, LogLevel level = LogLevel.Info, bool includeTimestamp = true);
        void Log(string message, LogLevel level = LogLevel.Info);
        void Log(string message, string tag, LogLevel level = LogLevel.Info, bool includeTimestamp = true);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
        void Debug(string message);
    }
}