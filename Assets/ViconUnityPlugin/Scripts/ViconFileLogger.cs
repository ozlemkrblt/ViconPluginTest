using System;
using System.IO;
using UnityEngine;

namespace Assets.ViconUnityPlugin.Scripts
{
    /*
    Handles intercepting Unity Debug.Log messages and rotuing them to a safe file,
    while optionally passing specific logs back to the main Unity console. 
    Having a log file for every trial is efficienter and easier to track the data. 
    */


    public class ViconFileLogger : IDisposable
    {
        private string m_DebugLogPath;
        private readonly object m_FileLock = new object();

        // the oldlog handler variable that will be used to store the custom unity log handler before we replace
        // it with our custom one
        private ILogHandler m_OldLogHandler;

        // action to pass logs from the CustomLogHandler back to this instance
        private static Action<string, string, LogType> s_ForwardLog;

        public void Initialize()
        {
            // Initialize log file path (writes to persistent data path so it works on Editor and builds)
            try
            {
                string safeTimestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                m_DebugLogPath = Path.Combine(Application.persistentDataPath, $"debug_{safeTimestamp}.log");

                // Ensure directory exists
                string dir = Path.GetDirectoryName(m_DebugLogPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Ensure the file exists
                if (!File.Exists(m_DebugLogPath))
                {
                    File.AppendAllText(m_DebugLogPath, $"# Debug log at {DateTime.Now:O}{Environment.NewLine}");
                }

                // Subscribe to Unity log events so every Debug.Log/Warning/Error is captured
                Application.logMessageReceived += HandleUnityLog;
                Application.logMessageReceivedThreaded += HandleUnityLog;

                // Set the static forward to our instance method
                s_ForwardLog = HandleUnityLog;

                // Swap out the Unity Log Handler
                m_OldLogHandler = Debug.unityLogger.logHandler;
                Debug.unityLogger.logHandler = new CustomLogHandler(m_OldLogHandler, s_ForwardLog);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CONSOLE] Failed to initialize debug log file: {e}");
            }


        }

        public void AppendLog(string line)
        {
            if (string.IsNullOrEmpty(m_DebugLogPath))
                return;

            try
            {
                lock (m_FileLock)
                {
                    File.AppendAllText(m_DebugLogPath, $"{line}{Environment.NewLine}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CONSOLE] Failed to write debug log: {ex}");
            }

        }


        private void HandleUnityLog(string condition, string stackTrace, LogType type)
        {
            if (string.IsNullOrEmpty(m_DebugLogPath))
                return;

            string time = DateTime.Now.ToString("O");
            string line = $"LOG,{time},{type},{condition}";
            if (type == LogType.Exception || type == LogType.Error)
            {
                line += $"{Environment.NewLine}STACKTRACE: {stackTrace}";
            }

            try
            {
                lock (m_FileLock)
                {
                    File.AppendAllText(m_DebugLogPath, line + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Debug.unityLogger.LogError("ViconFileLogger", $"Failed to write to log file: {ex}");
            }
        }

        public void Dispose()
        {
            // Unsubscribe from Unity log events
            Application.logMessageReceived -= HandleUnityLog;
            Application.logMessageReceivedThreaded -= HandleUnityLog;

            // Restore original log handler so Unity behaves normally after destruction
            if (m_OldLogHandler != null)
            {
                Debug.unityLogger.logHandler = m_OldLogHandler;
                m_OldLogHandler = null;
            }

            s_ForwardLog = null;
        }

    } // end of ViconFileLogger class



    // -------------------------------------------------------------------------
    // Nested Log Handler Class
    // -------------------------------------------------------------------------
    class CustomLogHandler : ILogHandler
    {
        private ILogHandler m_OriginalHandler;
        private Action<string, string, LogType> m_ForwardAction;

        public CustomLogHandler(ILogHandler originalHandler, Action<string, string, LogType> forwardAction)
        {
            m_OriginalHandler = originalHandler;
            m_ForwardAction = forwardAction;
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            string message;
            try
            {
                message = (args != null && args.Length > 0) ? string.Format(format, args) : format;
            }
            catch (Exception)
            {
                message = format;
            }

            // Forward to file writer
            m_ForwardAction?.Invoke(message, string.Empty, logType);

            // Check if it should ALSO go to the Unity Console
            if (logType == LogType.Error || logType == LogType.Exception || message.StartsWith("[CONSOLE]"))
            {
                string cleanMessage = message.Replace("[CONSOLE]", "").Trim();
                m_OriginalHandler.LogFormat(logType, context, "{0}", cleanMessage);
            }
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            m_ForwardAction?.Invoke(exception.Message, exception.StackTrace, LogType.Exception);
            m_OriginalHandler.LogException(exception, context);
        }

    } // end of CustomLogHandler class
} // end of namespace