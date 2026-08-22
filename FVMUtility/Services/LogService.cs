using System.Text;
using Microsoft.Extensions.Configuration;

namespace FVUFileMove.Services
{
    public class LogService
    {
        private readonly string _logDirectory;
        private readonly bool _loggingEnabled;
        private readonly object _lock = new object();

        public LogService(IConfiguration configuration)
        {

            _loggingEnabled =
        configuration.GetValue<bool>(
            "ProcessingSettings:EnableLogging");

            _logDirectory =
                configuration["FileSettings:LogPath"]
                ?? throw new InvalidOperationException(
                    "LogPath is not configured.");

            Directory.CreateDirectory(_logDirectory);
        }

        public void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public void Warning(string message)
        {
            WriteLog("WARNING", message);
        }

        public void Error(string message)
        {
            WriteLog("ERROR", message);
        }

        public void Error(
            string message,
            Exception ex)
        {
            WriteLog(
                "ERROR",
                message
                + " | Exception: "
                + ex.Message
                + " | StackTrace: "
                + ex.StackTrace);
        }

        private void WriteLog(
            string level,
            string message)
        {

            if (!_loggingEnabled)
            {
                return;
            }

            try
            {
                string fileName =
                    $"FVMUtility_{DateTime.Now:yyyy-MM-dd}.txt";

                string logFilePath =
                    Path.Combine(
                        _logDirectory,
                        fileName);

                string logLine =
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | "
                    + $"{level,-7} | "
                    + $"{message}";

                lock (_lock)
                {
                    File.AppendAllText(
                        logFilePath,
                        logLine
                        + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never stop the application.
            }
        }
    }
}