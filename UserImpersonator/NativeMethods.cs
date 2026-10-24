using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace UserImpersonator;

internal static class NativeMethods
{
    internal const int ErrorInsufficientBuffer = 122;

    [DllImport("kernel32.dll")]
    internal static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    internal static extern bool WTSQueryUserToken(uint sessionId, out SafeAccessTokenHandle token);

    [DllImport("userenv.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool GetUserProfileDirectory(
        SafeAccessTokenHandle token,
        StringBuilder? profileDir,
        ref uint size);
}
