using System;
using System.Configuration;
using System.Data;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace AMICUS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        private const int ATTACH_PARENT_PROCESS = -1;

        public static IServiceProvider ServiceProvider { get; private set; } = null!;
        public static ILogger Logger { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Attach to parent console if launched from terminal (PowerShell/CMD)
            // If no parent console, allocate a new one
            if (!AttachConsole(ATTACH_PARENT_PROCESS))
            {
                // Optional: Uncomment to always show console even when double-clicked
                // AllocConsole();
            }

            // Configure logging and services
            var services = new ServiceCollection();
            services.AddLogging(configure =>
            {
                configure.AddConsole();
                configure.SetMinimumLevel(LogLevel.Debug);
            });

            ServiceProvider = services.BuildServiceProvider();
            Logger = ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AMICUS");

            Logger.LogInformation("Application starting...");

            // Global exception handlers
            this.DispatcherUnhandledException += (s, args) =>
            {
                Logger.LogError(args.Exception, "Unhandled exception occurred");
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                Logger.LogCritical(ex, "Fatal unhandled exception occurred");
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger?.LogInformation("Application shutting down...");
            (ServiceProvider as IDisposable)?.Dispose();
            base.OnExit(e);
        }
    }

}
