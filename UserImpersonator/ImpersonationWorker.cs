using Microsoft.Extensions.Hosting;

namespace UserImpersonator;

/// <summary>
/// Runs once when the service starts. The process identity is LocalSystem in session 0.
/// Impersonation replaces the thread token with the interactive console user, then restores it.
/// </summary>
internal sealed class ImpersonationWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Return control so the Service Control Manager can mark the service as running
        // before the demo queries tokens and translates group names.
        await Task.Yield();
        await Task.Run(WriteDemo, stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void WriteDemo()
    {
        try
        {
            ImpersonationLog.Write(BuildReport());
        }
        catch (Exception ex)
        {
            ImpersonationLog.Write("Impersonation failed: " + ex);
        }
    }

    private static string BuildReport()
    {
        using var output = new StringWriter();
        output.WriteLine("Thread impersonation changes WindowsIdentity on this thread.");
        output.WriteLine("The service process stays in session 0.");
        IdentityReport.Write(output, "Before impersonation (service identity)");

        try
        {
            Impersonation.RunAsActiveConsoleUser(token =>
            {
                string? profile = Impersonation.TryGetProfileDirectory(token);
                IdentityReport.Write(
                    output,
                    "During impersonation (active console user)",
                    profile ?? "(profile directory unavailable)");
            });
            IdentityReport.Write(output, "After revert (service identity)");
        }
        catch (Exception ex)
        {
            output.WriteLine();
            output.WriteLine($"Impersonation failed: {ex.Message}");
        }

        return output.ToString();
    }
}
