using System.Globalization;

namespace WinForge.Common
{
    public static class Logger
    {
        public enum LogLevel
        {
            Info,
            Plugin,
            Warning,
            Error,
            Debug
        }

        private static readonly object _memoryLock = new();

        private static StreamWriter? _fileWriter;
        private static readonly object _fileLock = new();
        public static Task InitializeAsync()
        {
            RotateLogs(Settings.Application.LogFilePath, Settings.Application.MaxLogFiles);
            EnableFileLogging(Settings.Application.LogFilePath);

            return Task.CompletedTask;
        }
        /// <summary> Enable file logging to the specified file path. </summary>       
        public static void EnableFileLogging(string filePath)
        {
            lock (_fileLock)
            {
                _fileWriter = new StreamWriter(File.Open(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    AutoFlush = true
                };
            }
        }
        /// <summary> Write a log message to console and to file
        /// </summary>
        /// <param name="message">Log Message.</param>
        /// <param name="level">Log Level. Default: LogLevel.Info</param>
        /// <param name="tag">Tag of the logging module. Default:  Null</param>
        /// <param name="includeTimestamp">Should the log line include a timestamp. Default: True</param>
        public static void Log(string message, LogLevel level = LogLevel.Info, string? tag = null, bool includeTimestamp = true)
        {
            if (tag == null) tag = string.Empty;
            var output = includeTimestamp
                ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][{tag}][{level}] {message}"
                : $"[{tag}][{level}] {message}";

            lock (_memoryLock)
            {
                switch (level)
                {
                    case LogLevel.Info:
                        Console.ForegroundColor = ConsoleColor.Gray;
                        break;
                    case LogLevel.Warning:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case LogLevel.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case LogLevel.Debug:
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        break;
                }

                Console.WriteLine(output);
                Console.ResetColor();
            }
        }
        private static void RotateLogs(string filePath, int maxFiles)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            if (!File.Exists(filePath))
                return;

            // Get actual creation or last write time of the log file
            var fileTime = File.GetLastWriteTimeUtc(filePath);
            string timestamp = fileTime.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

            string dir = Path.GetDirectoryName(filePath)!;
            string baseN = Path.GetFileNameWithoutExtension(filePath);
            string ext = Path.GetExtension(filePath);
            string rotatedPath = Path.Combine(dir, $"{baseN}_{timestamp}{ext}");

            File.Move(filePath, rotatedPath);

            // Delete old rotated logs if over max count
            string searchPattern = $"{baseN}_*{ext}";
            var rotatedFiles = Directory.GetFiles(dir, searchPattern)
                                        .OrderByDescending(f => File.GetCreationTimeUtc(f))
                                        .ToList();

            foreach (var old in rotatedFiles.Skip(maxFiles))
            {
                try { File.Delete(old); } catch { /* ignore */ }
            }
        }

        public static void Info(string message) => Log(message, LogLevel.Info);
        public static void Warn(string message) => Log(message, LogLevel.Warning);
        public static void Error(string message) => Log(message, LogLevel.Error);
        public static void Debug(string message) => Log(message, LogLevel.Debug);
    }
}
