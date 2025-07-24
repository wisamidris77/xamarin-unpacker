using System;
using System.IO;

namespace XamarinBlobConverter
{
    public class Logger
    {
        private readonly string _logFile;
        private readonly object _lockObject = new object();

        public Logger()
        {
            _logFile = Path.Combine(Directory.GetCurrentDirectory(), $"conversion_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            Log("=== Xamarin Blob Converter Log Started ===");
            Log($"Log file: {_logFile}");
            Log($"Current time: {DateTime.Now}");
            Log($"Current directory: {Directory.GetCurrentDirectory()}");
        }

        public void Log(string message)
        {
            var timestampedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            
            lock (_lockObject)
            {
                // Write to console
                Console.WriteLine(timestampedMessage);
                
                // Write to log file
                try
                {
                    File.AppendAllText(_logFile, timestampedMessage + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing to log file: {ex.Message}");
                }
            }
        }

        public void LogError(string message, Exception? ex = null)
        {
            var errorMessage = $"ERROR: {message}";
            if (ex != null)
            {
                errorMessage += $" - {ex.Message}";
            }
            Log(errorMessage);
            
            if (ex != null)
            {
                Log($"Stack trace: {ex.StackTrace}");
            }
        }

        public void LogWarning(string message)
        {
            Log($"WARNING: {message}");
        }

        public void LogSuccess(string message)
        {
            Log($"SUCCESS: {message}");
        }

        public void LogSeparator()
        {
            Log(new string('=', 50));
        }

        public string GetLogFilePath()
        {
            return _logFile;
        }
    }
} 