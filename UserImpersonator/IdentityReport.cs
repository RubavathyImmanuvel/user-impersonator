using System.Diagnostics;
using System.Security.Principal;

namespace UserImpersonator;

internal static class IdentityReport
{
    public static void Print(string heading, string? profileDirectory = null)
        => Write(Console.Out, heading, profileDirectory);

    public static void Write(TextWriter output, string heading, string? profileDirectory = null)
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();

        output.WriteLine();
        output.WriteLine($"=== {heading} ===");
        output.WriteLine($"WindowsIdentity    : {identity.Name}");
        output.WriteLine($"SID                : {identity.User?.Value ?? "(none)"}");
        output.WriteLine($"Authentication     : {identity.AuthenticationType ?? "(none)"}");
        output.WriteLine($"ImpersonationLevel : {identity.ImpersonationLevel}");
        output.WriteLine($"IsAuthenticated    : {identity.IsAuthenticated}");
        output.WriteLine($"IsSystem           : {identity.IsSystem}");
        output.WriteLine($"Environment user   : {Environment.UserDomainName}\\{Environment.UserName}");
        output.WriteLine($"Process session    : {Process.GetCurrentProcess().SessionId}");

        if (profileDirectory is not null)
        {
            output.WriteLine($"Profile directory  : {profileDirectory}");
        }

        if (identity.Groups is null)
        {
            return;
        }

        output.WriteLine("Groups:");
        var shown = 0;
        foreach (IdentityReference group in identity.Groups)
        {
            if (shown == 8)
            {
                output.WriteLine("  ...");
                break;
            }

            output.WriteLine($"  {FormatGroup(group)}");
            shown++;
        }
    }

    private static string FormatGroup(IdentityReference group)
    {
        try
        {
            return group.Translate(typeof(NTAccount)).Value;
        }
        catch (IdentityNotMappedException)
        {
            return group.Value;
        }
        catch (SystemException)
        {
            return group.Value;
        }
    }
}
