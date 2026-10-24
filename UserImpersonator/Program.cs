using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace UserImpersonator;

internal static class Program
{
    private static void Main(string[] args)
    {
        try
        {
            if (HasOption(args, "--install"))
            {
                ServiceInstaller.Install();
                return;
            }

            if (HasOption(args, "--uninstall"))
            {
                ServiceInstaller.Uninstall();
                return;
            }

            if (!WindowsServiceHelpers.IsWindowsService())
            {
                PrintUsage();
                return;
            }

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory
            });
            builder.Services.AddWindowsService(options => options.ServiceName = ImpersonationLog.EventSource);
            builder.Services.AddHostedService<ImpersonationWorker>();
            builder.Build().Run();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            Console.Error.WriteLine("Access is denied. Run --install or --uninstall from an elevated command prompt.");
            Environment.ExitCode = 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Environment.ExitCode = 1;
        }
    }

    private static void PrintUsage()
    {
        string name = Path.GetFileName(Environment.ProcessPath) ?? "UserImpersonator.exe";
        Console.WriteLine("User Impersonator is a Windows service.");
        Console.WriteLine("It runs as LocalSystem in session 0 and impersonates the user logged on at the console.");
        Console.WriteLine();
        Console.WriteLine($"  {name} --install");
        Console.WriteLine($"  {name} --uninstall");
        Console.WriteLine();
        Console.WriteLine("Install from an elevated command prompt. The report is written to:");
        Console.WriteLine($"  {ImpersonationLog.LatestReportPath}");
    }

    private static bool HasOption(string[] args, string option)
    {
        return args.Contains(option, StringComparer.OrdinalIgnoreCase);
    }
}
