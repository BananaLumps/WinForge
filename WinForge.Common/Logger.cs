using System.Globalization;

namespace WinForge.Common
{
    public class Logger : ILogger
    {

        private static readonly object _memoryLock = new();
        private static StreamWriter? _fileWriter;
        private static readonly object _fileLock = new();
        public string? Tag { get; set; } = null;
        /// <summary> Creates a new Logger instance and initializes it synchronously. </summary>
        public Logger()
        {
            InitializeAsync().GetAwaiter().GetResult();
        }
        public Task InitializeAsync()
        {
            RotateLogs(Settings.Application.LogFilePath, Settings.Application.MaxLogFiles);
            EnableFileLogging(Settings.Application.LogFilePath);

            return Task.CompletedTask;
        }
        /// <summary> Enable file logging to the specified file path. </summary>       
        public void EnableFileLogging(string filePath)
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
        public void Log(string message, LogLevel level = LogLevel.Info, string? tag = null, bool includeTimestamp = true)
        {
            if (tag == null) tag = string.Empty;
            var output = includeTimestamp
                ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][{tag}][{level}] {message}"
                : $"[{tag}][{level}] {message}";

            lock (_memoryLock)
            {
                ColourSelector(level);
                Console.WriteLine(output);
                Console.ResetColor();
            }
        }
        /// <summary> Log a message to console and to file with the current tag.
        public void Log(string message, LogLevel level = LogLevel.Info, bool includeTimestamp = true)
        {
            if (Tag == null) Tag = string.Empty;
            var output = includeTimestamp
                ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][{Tag}][{level}] {message}"
                : $"[{Tag}][{level}] {message}";

            lock (_memoryLock)
            {
                ColourSelector(level);
                Console.WriteLine(output);
                Console.ResetColor();
            }
        }
        /// <summary>  Log using default settings.
        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (Tag == null) Tag = string.Empty;
            var output = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][{Tag}][{level}] {message}";

            lock (_memoryLock)
            {
                ColourSelector(level);
                Console.WriteLine(output);
                Console.ResetColor();
            }
        }
        /// <summary> Log a message to console and to file with a specific tag.
        public void Log(string message, string tag, LogLevel level = LogLevel.Info, bool includeTimestamp = true)
        {
            if (tag == null) tag = string.Empty;
            var output = includeTimestamp
                ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][{tag}][{level}] {message}"
                : $"[{tag}][{level}] {message}";

            lock (_memoryLock)
            {
                ColourSelector(level);
                Console.WriteLine(output);
                Console.ResetColor();
            }
        }
        /// <summary> Sets the colour of the console based on the log level.
        private static void ColourSelector(LogLevel level)
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
        }
        /// <summary> Rotate the log files based on the specified file path and maximum number of files. Removes oldest log files if over max </summary>s
        private void RotateLogs(string filePath, int maxFiles)
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

        /// <summary> Log an informational message. </summary>
        public void Info(string message) => Log(message, LogLevel.Info);
        /// <summary> Log a warning message. </summary>
        public void Warn(string message) => Log(message, LogLevel.Warning);
        /// <summary> Log a error message. </summary>
        public void Error(string message) => Log(message, LogLevel.Error);
        /// <summary> Log a debug message. </summary>
        public void Debug(string message) => Log(message, LogLevel.Debug);
    }
}
