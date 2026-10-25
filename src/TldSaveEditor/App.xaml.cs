using System.IO;
using System.Windows;
using System.Windows.Threading;
using TldSave.Core;

namespace TldSaveEditor;

public partial class App : Application
{
    public App()
    {
        // Last safety net: an unexpected error shows a message instead of closing the editor,
        // so unsaved edits are not lost. Async handlers' exceptions also end up here.
        DispatcherUnhandledException += OnUnhandledException;
        // Exceptions from background tasks nobody awaited: don't let them crash the app later
        TaskScheduler.UnobservedTaskException += (_, e) => e.SetObserved();
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        var log = WriteErrorLog(e.Exception);
        MessageBox.Show(
            $"Unexpected error:\n\n{e.Exception.GetType().Name}: {e.Exception.Message}\n\n" +
            "The editor stays open and your edits are still there. Check them, then save as usual." +
            (log is null ? "" : $"\n\nDetails were written to:\n{log}"),
            "TLD Save Editor", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>Appends the full error to Documents\TldSaveEditor\error.log, for bug reports.</summary>
    private static string? WriteErrorLog(Exception exception)
    {
        try
        {
            var folder = Path.GetDirectoryName(SaveLocator.BackupFolder)!;
            Directory.CreateDirectory(folder);
            var log = Path.Combine(folder, "error.log");
            File.AppendAllText(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}\n\n");
            return log;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
