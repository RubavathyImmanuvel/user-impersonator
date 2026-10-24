using System.Diagnostics;

namespace UserImpersonator;

internal static class ImpersonationLog
{
    internal const string EventSource = "UserImpersonator";

    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "UserImpersonator");

    internal static string HistoryPath { get; } = Path.Combine(DirectoryPath, "impersonation.log");

    internal static string LatestReportPath { get; } = Path.Combine(DirectoryPath, "latest-report.txt");

    internal static void Write(string report)
    {
        Directory.CreateDirectory(DirectoryPath);
        string stamped =
            $"======== {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} ========{Environment.NewLine}" +
            report +
            $"{Environment.NewLine}--- demo complete ---{Environment.NewLine}";

        File.AppendAllText(HistoryPath, stamped + Environment.NewLine);
        File.WriteAllText(LatestReportPath, stamped);
        TryWriteEventLog(stamped);
    }

    private static void TryWriteEventLog(string report)
    {
        try
        {
            if (!EventLog.SourceExists(EventSource))
            {
                EventLog.CreateEventSource(EventSource, "Application");
            }

            const int maxEventLength = 30000;
            if (report.Length > maxEventLength)
            {
                report = report[..maxEventLength];
            }

            EventLog.WriteEntry(EventSource, report, EventLogEntryType.Information);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }
}
