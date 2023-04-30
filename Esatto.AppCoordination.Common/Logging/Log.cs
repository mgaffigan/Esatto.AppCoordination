using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDD = System.Diagnostics.Debug;

namespace Esatto.AppCoordination
{
    public static class Log
    {
        private static EventLog EventLog;

        static Log()
        {
            EventLog = new EventLog();
            EventLog.Source = "esAppCoordination";
        }

        public static void Info(string message, int eventid) => LogMessage(EventLogEntryType.Information, message, eventid);
        public static void Warn(string message, int eventid) => LogMessage(EventLogEntryType.Warning, message, eventid);
        public static void Error(string message, int eventid) => LogMessage(EventLogEntryType.Error, message, eventid);

        private static void LogMessage(EventLogEntryType type, string message, int eventid)
        {
            SDD.WriteLine($"{DateTime.Now:yyyy-MM-dd hh:mm:ss.ffff}-{type}-{eventid}: {message}");

            try
            {
                EventLog.WriteEntry(message, type, eventid);
            }
            catch (Exception ex)
            {
                SDD.WriteLine("Failed to log message:\r\n" + ex.ToString());
            }
        }

        public static void Debug(string message)
        {
            SDD.WriteLine($"{DateTime.Now:yyyy-MM-dd hh:mm:ss.ffff}-Debug-0: {message}");
        }
    }
}
