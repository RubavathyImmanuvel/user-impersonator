using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace UserImpersonator;

internal static class ServiceInstaller
{
    private const uint ScManagerAllAccess = 0xF003F;
    private const uint ServiceAllAccess = 0xF01FF;
    private const uint ServiceWin32OwnProcess = 0x00000010;
    private const uint ServiceDemandStart = 0x00000003;
    private const uint ServiceErrorNormal = 0x00000001;
    private const uint ServiceConfigDescription = 1;
    private const int ErrorServiceDoesNotExist = 1060;
    private const int ErrorServiceAlreadyRunning = 1056;

    internal static void Install()
    {
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not locate this executable.");
        string binaryPath = $"\"{executable}\"";

        IntPtr manager = ServiceNative.OpenSCManager(null, null, ScManagerAllAccess);
        if (manager == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            EnsureStopped();
            EnsureCreated(manager, binaryPath);
            if (File.Exists(ImpersonationLog.LatestReportPath))
            {
                File.Delete(ImpersonationLog.LatestReportPath);
            }

            Start();
            string report = WaitForReport();
            Console.WriteLine($"Installed and started LocalSystem service {ImpersonationLog.EventSource}.");
            Console.WriteLine($"History: {ImpersonationLog.HistoryPath}");
            Console.WriteLine(report);
        }
        finally
        {
            ServiceNative.CloseServiceHandle(manager);
        }
    }

    internal static void Uninstall()
    {
        IntPtr manager = ServiceNative.OpenSCManager(null, null, ScManagerAllAccess);
        if (manager == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            EnsureStopped();
            IntPtr service = ServiceNative.OpenService(manager, ImpersonationLog.EventSource, ServiceAllAccess);
            if (service == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ErrorServiceDoesNotExist)
                {
                    Console.WriteLine($"Service {ImpersonationLog.EventSource} is not installed.");
                    return;
                }

                throw new Win32Exception(error);
            }

            try
            {
                if (!ServiceNative.DeleteService(service))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                ServiceNative.CloseServiceHandle(service);
            }

            Console.WriteLine($"Removed service {ImpersonationLog.EventSource}.");
            Console.WriteLine($"The log is still at {ImpersonationLog.HistoryPath}.");
        }
        finally
        {
            ServiceNative.CloseServiceHandle(manager);
        }
    }

    private static void EnsureCreated(IntPtr manager, string binaryPath)
    {
        IntPtr existing = ServiceNative.OpenService(manager, ImpersonationLog.EventSource, ServiceAllAccess);
        if (existing != IntPtr.Zero)
        {
            ServiceNative.CloseServiceHandle(existing);
            return;
        }

        int openError = Marshal.GetLastWin32Error();
        if (openError != ErrorServiceDoesNotExist)
        {
            throw new Win32Exception(openError);
        }

        IntPtr created = ServiceNative.CreateService(
            manager,
            ImpersonationLog.EventSource,
            "User Impersonator Demo",
            ServiceAllAccess,
            ServiceWin32OwnProcess,
            ServiceDemandStart,
            ServiceErrorNormal,
            binaryPath,
            null,
            IntPtr.Zero,
            null,
            null,
            null);
        if (created == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            var description = new ServiceNative.ServiceDescription
            {
                Description = "Demonstrates impersonating the interactive console user from a session 0 LocalSystem service."
            };
            ServiceNative.ChangeServiceConfig2(created, ServiceConfigDescription, ref description);
        }
        finally
        {
            ServiceNative.CloseServiceHandle(created);
        }
    }

    private static void EnsureStopped()
    {
        try
        {
            using var controller = new ServiceController(ImpersonationLog.EventSource);
            if (controller.Status == ServiceControllerStatus.Stopped)
            {
                return;
            }

            if (controller.Status != ServiceControllerStatus.StopPending)
            {
                controller.Stop();
            }

            controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void Start()
    {
        using var controller = new ServiceController(ImpersonationLog.EventSource);
        try
        {
            controller.Start();
            controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
        }
        catch (InvalidOperationException ex) when (ex.InnerException is Win32Exception win32 && win32.NativeErrorCode == ErrorServiceAlreadyRunning)
        {
        }
    }

    private static string WaitForReport()
    {
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(ImpersonationLog.LatestReportPath))
            {
                string report = File.ReadAllText(ImpersonationLog.LatestReportPath);
                if (report.Contains("--- demo complete ---", StringComparison.Ordinal))
                {
                    return report;
                }
            }

            Thread.Sleep(200);
        }

        throw new System.TimeoutException(
            "The service started, but it did not write a report. Check the Application event log and " +
            ImpersonationLog.HistoryPath + ".");
    }

    private static class ServiceNative
    {
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateService(
            IntPtr serviceControlManager,
            string serviceName,
            string displayName,
            uint desiredAccess,
            uint serviceType,
            uint startType,
            uint errorControl,
            string binaryPath,
            string? loadOrderGroup,
            IntPtr tagId,
            string? dependencies,
            string? serviceStartName,
            string? password);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr OpenService(IntPtr serviceControlManager, string serviceName, uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool DeleteService(IntPtr service);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool CloseServiceHandle(IntPtr handle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool ChangeServiceConfig2(
            IntPtr service,
            uint infoLevel,
            ref ServiceDescription description);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct ServiceDescription
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string Description;
        }
    }
}
