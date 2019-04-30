using System;
using UnityEngine;

namespace ModuleManager.Logging
{
    public class LogMessage : ILogMessage
    {
        private const string DATETIME_FORMAT_STRING = "HH:mm:ss.fff";

        public LogType LogType { get; }
        public DateTime Timestamp { get; }
        public string Message { get; }

        public LogMessage(LogType logType, string message)
        {
            LogType = logType;
            Timestamp = DateTime.Now;
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public LogMessage(ILogMessage logMessage, string newMessage)
        {
            if (logMessage == null) throw new ArgumentNullException(nameof(logMessage));
            LogType = logMessage.LogType;
            Timestamp = logMessage.Timestamp;
            Message = newMessage ?? throw new ArgumentNullException(nameof(newMessage));
        }

        public string ToLogString()
        {
            string prefix;
            if (LogType == LogType.Log)
                prefix = "LOG";
            else if (LogType == LogType.Warning)
                prefix = "WRN";
            else if (LogType == LogType.Error)
                prefix = "ERR";
            else if (LogType == LogType.Assert)
                prefix = "AST";
            else if (LogType == LogType.Exception)
                prefix = "EXC";
            else
                prefix = "???";
            
            return $"[{prefix} {Timestamp.ToString(DATETIME_FORMAT_STRING)}] {Message}";
        }

        public override string ToString()
        {
            return $"[{GetType().FullName} LogType={LogType} Message={Message}]";
        }
    }
}
