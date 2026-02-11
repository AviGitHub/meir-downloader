using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MeirDownloader.Desktop.Services;

namespace MeirDownloader.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ThemeManager.Initialize();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogFatalException(e.Exception, "DispatcherUnhandledException");
        MessageBox.Show("אירעה שגיאה. האפליקציה תמשיך לפעול.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogFatalException(e.Exception, "UnobservedTaskException");
        e.SetObserved();
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogFatalException(ex, "AppDomain.UnhandledException");
        else
            LogFatalException(new Exception(e.ExceptionObject?.ToString()), "AppDomain.UnhandledException");
    }

    private static void LogFatalException(Exception ex, string source)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MeirDownloader");
            Directory.CreateDirectory(dir);
            var logPath = Path.Combine(dir, "meir-downloader-errors.log");
            var entry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}] [{source}] {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(logPath, entry);
        }
        catch
        {
            // Logging should never throw
        }
    }
}
