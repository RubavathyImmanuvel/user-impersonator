using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace UserImpersonator;

/// <summary>
/// Impersonates the active console user on the current thread, then restores the service identity.
/// </summary>
internal static class Impersonation
{
    public static void RunAsActiveConsoleUser(Action<SafeAccessTokenHandle> action)
    {
        uint sessionId = NativeMethods.WTSGetActiveConsoleSessionId();
        if (sessionId == 0xFFFFFFFF)
        {
            throw new InvalidOperationException("There is no active console session.");
        }

        if (!NativeMethods.WTSQueryUserToken(sessionId, out SafeAccessTokenHandle token))
        {
            int error = Marshal.GetLastWin32Error();
            token.Dispose();
            throw new Win32Exception(
                error,
                $"WTSQueryUserToken failed for console session {sessionId}. " +
                "The service must run as LOCAL SYSTEM in session 0, with a user logged on at the console.");
        }

        using (token)
        {
            WindowsIdentity.RunImpersonated(token, () => action(token));
        }
    }

    public static string? TryGetProfileDirectory(SafeAccessTokenHandle token)
    {
        uint capacity = 260;
        var buffer = new StringBuilder((int)capacity);
        if (NativeMethods.GetUserProfileDirectory(token, buffer, ref capacity))
        {
            return buffer.ToString();
        }

        int error = Marshal.GetLastWin32Error();
        if (error != NativeMethods.ErrorInsufficientBuffer || capacity == 0)
        {
            return null;
        }

        buffer = new StringBuilder((int)capacity);
        return NativeMethods.GetUserProfileDirectory(token, buffer, ref capacity)
            ? buffer.ToString()
            : null;
    }
}
