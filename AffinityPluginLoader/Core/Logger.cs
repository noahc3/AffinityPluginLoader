using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;

namespace AffinityPluginLoader.Core
{
    /// <summary>
    /// Logging API for APL and plugins.
    /// Supports console and file output with log levels and rotation.
    /// </summary>
    public static class Logger
    {
        public enum LogLevel
        {
            DEBUG = 0,
            INFO = 1,
            WARNING = 2,
            ERROR = 3,
            NONE = 4 // Disables all logging
        }

        private static LogLevel _minimumLevel = LogLevel.INFO;
        private static bool _fileLoggingEnabled = false;
        private static string _logFilePath = null;
        private static readonly object _lockObj = new object();
        private static bool _initialized = false;
        private static StreamWriter _fileWriter = null;
        private static bool _hasConsole = false;

        // P/Invoke for console attachment
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetConsoleWindow();

        private const int ATTACH_PARENT_PROCESS = -1;

        /// <summary>
        /// Initialize the logger. Call this once at startup.
        /// </summary>
        public static void Initialize()
        {
            lock (_lockObj)
            {
                if (_initialized)
                    return;

                // Try to attach to parent console for output visibility
                AttachToConsole();

                // Parse APL_LOGGING environment variable
                string aplLogging = Environment.GetEnvironmentVariable("APL_LOGGING");
                if (!string.IsNullOrEmpty(aplLogging))
                {
                    // Parse log level
                    if (Enum.TryParse(aplLogging.ToUpperInvariant(), out LogLevel level))
                    {
                        _minimumLevel = level;
                        _fileLoggingEnabled = true;
                    }
                    else
                    {
                        // Invalid log level, default to DEBUG and warn
                        _minimumLevel = LogLevel.DEBUG;
                        _fileLoggingEnabled = true;
                        Console.WriteLine($"[WARNING] Invalid APL_LOGGING value '{aplLogging}'. Using DEBUG. Valid values: DEBUG, INFO, WARNING, ERROR, NONE");
                    }
                }

                // Setup file logging if enabled
                if (_fileLoggingEnabled)
                {
                    SetupFileLogging();
                }

                _initialized = true;

                // Log startup message with timezone info
                var now = DateTime.Now;
                var timezone = TimeZoneInfo.Local;
                Info($"APL logging initialized");
                Info($"Local timezone: {timezone.DisplayName} (UTC{(timezone.BaseUtcOffset.TotalHours >= 0 ? "+" : "")}{timezone.BaseUtcOffset.TotalHours:0.##})");
                Info($"Log level: {_minimumLevel}");
                if (_fileLoggingEnabled)
                {
                    Info($"File logging enabled: {_logFilePath}");
                }
            }
        }

        private static void AttachToConsole()
        {
            try
            {
                // Check if we already have a console
                if (GetConsoleWindow() != IntPtr.Zero)
                {
                    _hasConsole = true;
                    return;
                }

                // Try to attach to parent process console (AffinityHook)
                if (AttachConsole(ATTACH_PARENT_PROCESS))
                {
                    _hasConsole = true;
                    // Reopen standard output to the console
                    try
                    {
                        var stdOut = Console.OpenStandardOutput();
                        Console.SetOut(new StreamWriter(stdOut, Console.OutputEncoding) { AutoFlush = true });
                        var stdErr = Console.OpenStandardError();
                        Console.SetError(new StreamWriter(stdErr, Console.OutputEncoding) { AutoFlush = true });
                    }
                    catch
                    {
                        // If reopening fails, we still have the console attached
                    }
                    return;
                }

                // If attaching to parent failed, we won't allocate a new console
                // (Affinity is a GUI app and allocating a new console creates a popup window)
                _hasConsole = false;
            }
            catch
            {
                _hasConsole = false;
            }
        }

        private static void SetupFileLogging()
        {
            try
            {
                // Determine log file path (plugins/logs/apl.latest.log)
                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string pluginsDir = Path.Combine(assemblyDir, "plugins");
                string logsDir = Path.Combine(pluginsDir, "logs");

                // Create plugins/logs directory if it doesn't exist
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }

                _logFilePath = Path.Combine(logsDir, "apl.latest.log");

                // Rotate existing log files
                RotateLogFiles(_logFilePath);

                // Open log file for writing
                _fileWriter = new StreamWriter(_logFilePath, append: false, Encoding.UTF8)
                {
                    AutoFlush = true
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to setup file logging: {ex.Message}");
                _fileLoggingEnabled = false;
            }
        }

        private static void RotateLogFiles(string logFilePath)
        {
            try
            {
                // If current log file doesn't exist, nothing to rotate
                if (!File.Exists(logFilePath))
                    return;

                // Get directory and base name (e.g., "apl.latest.log" -> "apl", ".log")
                string logDir = Path.GetDirectoryName(logFilePath);

                // Delete apl.5.log if it exists
                string log5 = Path.Combine(logDir, "apl.5.log");
                if (File.Exists(log5))
                {
                    File.Delete(log5);
                }

                // Cycle logs: apl.4.log -> apl.5.log, apl.3.log -> apl.4.log, etc.
                for (int i = 4; i >= 1; i--)
                {
                    string oldLog = Path.Combine(logDir, $"apl.{i}.log");
                    string newLog = Path.Combine(logDir, $"apl.{i + 1}.log");
                    if (File.Exists(oldLog))
                    {
                        File.Move(oldLog, newLog);
                    }
                }

                // Move current log to apl.1.log
                File.Move(logFilePath, Path.Combine(logDir, "apl.1.log"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to rotate log files: {ex.Message}");
            }
        }

        private static void Log(LogLevel level, string message)
        {
            if (!_initialized)
            {
                // Auto-initialize on first log call
                Initialize();
            }

            // Filter by minimum level
            if (level < _minimumLevel)
                return;

            // Get the plugin name from the calling assembly
            string pluginName = GetCallingPluginName();

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string levelStr = level.ToString();
            string logLine = $"[{timestamp}] [{levelStr}] [APL/{pluginName}] {message}";

            lock (_lockObj)
            {
                // Write to console if we have one attached
                if (_hasConsole)
                {
                    try
                    {
                        Console.WriteLine(logLine);
                    }
                    catch
                    {
                        // Console write failed, disable it
                        _hasConsole = false;
                    }
                }

                // Write to file if enabled
                if (_fileLoggingEnabled && _fileWriter != null)
                {
                    try
                    {
                        _fileWriter.WriteLine(logLine);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to write to log file: {ex.Message}");
                    }
                }
            }
        }

        public static void Debug(string message)
        {
            Log(LogLevel.DEBUG, message);
        }

        public static void Info(string message)
        {
            Log(LogLevel.INFO, message);
        }

        public static void Warning(string message)
        {
            Log(LogLevel.WARNING, message);
        }

        public static void Error(string message)
        {
            Log(LogLevel.ERROR, message);
        }

        public static void Error(string message, Exception ex)
        {
            Error($"{message}: {ex.Message}");
            if (ex.StackTrace != null)
            {
                Error($"Stack trace:\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Get the name of the plugin/assembly that called the logger
        /// </summary>
        private static string GetCallingPluginName()
        {
            try
            {
                // Walk up the stack to find the first assembly that isn't this one
                var stackTrace = new System.Diagnostics.StackTrace();
                var frames = stackTrace.GetFrames();

                if (frames != null)
                {
                    var loggerAssembly = typeof(Logger).Assembly;

                    foreach (var frame in frames)
                    {
                        var method = frame.GetMethod();
                        if (method != null)
                        {
                            var declaringType = method.DeclaringType;
                            if (declaringType != null)
                            {
                                var assembly = declaringType.Assembly;

                                // Skip our own assembly
                                if (assembly != loggerAssembly)
                                {
                                    // Get the plugin name using the same approach as PluginManager
                                    var nameAttr = assembly.GetCustomAttribute<AssemblyTitleAttribute>();
                                    return nameAttr?.Title ?? assembly.GetName().Name;
                                }
                            }
                        }
                    }
                }

                // Fallback to "APL" if we can't determine the caller
                return "Core";
            }
            catch
            {
                return "Core";
            }
        }

        /// <summary>
        /// Cleanup resources. Call this on shutdown if needed.
        /// </summary>
        public static void Shutdown()
        {
            lock (_lockObj)
            {
                if (_fileWriter != null)
                {
                    _fileWriter.Flush();
                    _fileWriter.Close();
                    _fileWriter = null;
                }
            }
        }
    }
}
