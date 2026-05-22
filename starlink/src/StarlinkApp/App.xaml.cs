using System.Windows;
using StarlinkApp.Services;

namespace StarlinkApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            FileLogService.CreateDefault().Write("startup.unhandled_exception", args.Exception.ToString());
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            FileLogService.CreateDefault().Write("startup.domain_unhandled_exception", args.ExceptionObject?.ToString() ?? "Unknown error");
        };
    }
}
