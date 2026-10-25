using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TldSave.Core;

namespace TldSaveEditor;

/// <summary>
/// Lists backups and restores one over its save file. Never replaces a file without asking:
/// the user confirms, and the current save is backed up first ("before-restore").
/// </summary>
public partial class RestoreBackupWindow : Window
{
    private const string AllSaves = "All saves";

    private readonly List<BackupInfo> backups = SaveLocator.FindBackups();

    /// <summary>The save file that was restored, if any.</summary>
    public string? RestoredSavePath { get; private set; }

    public RestoreBackupWindow(string? preselectedSave)
    {
        InitializeComponent();
        FolderText.Text = SaveLocator.BackupFolder;

        var saves = backups.Select(b => b.SaveFileName).Distinct().Order().ToList();
        SaveFilter.ItemsSource = new[] { AllSaves }.Concat(saves).ToList();
        SaveFilter.SelectedItem = preselectedSave is not null && saves.Contains(preselectedSave) ? preselectedSave : AllSaves;

        if (backups.Count == 0) DetailsText.Text = "No backups yet. They are created when you Save or click Backup now.";
    }

    private void SaveFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var filter = SaveFilter.SelectedItem as string;
        BackupsList.ItemsSource = filter is null or AllSaves ? backups : backups.Where(b => b.SaveFileName == filter).ToList();
    }

    private void BackupsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var backup = BackupsList.SelectedItem as BackupInfo;
        RestoreButton.IsEnabled = backup?.IsValid == true;
        if (backup is null) { DetailsText.Text = ""; return; }
        if (!backup.IsValid) { DetailsText.Text = "This backup can't be read as a survival save, so it can't be restored."; return; }

        var target = SaveLocator.RestoreTarget(backup);
        DetailsText.Text = File.Exists(target)
            ? $"Restores to {target}.\nThat file exists and will be replaced. It is backed up first, so you can undo this."
            : $"Restores to {target}. No file has that name, so it will be created.";
        if (SaveLocator.IsDifferentRun(backup))
            DetailsText.Text = DifferentRunWarning(backup, target) + "\n" + DetailsText.Text;
    }

    private void BackupsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RestoreButton.IsEnabled) Restore_Click(sender, e);
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (BackupsList.SelectedItem is not BackupInfo { IsValid: true } backup) return;

        if (SaveLocator.IsGameRunning())
        {
            MessageBox.Show(this, "The Long Dark is running.\n\nQuit the game completely before restoring a save: " +
                "otherwise the game keeps its own copy and overwrites the restored file.",
                "Restore", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var target = SaveLocator.RestoreTarget(backup);
        if (!Confirm(backup, target)) return;

        try
        {
            var safetyBackup = SaveLocator.Restore(backup);
            RestoredSavePath = target;
            MessageBox.Show(this,
                $"Restored \"{backup.DisplayName}\" ({backup.CreatedText}) to {target}." +
                (safetyBackup is null ? "" : $"\n\nThe file it replaced was backed up as:\n{safetyBackup}"),
                "Restore", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Restore failed. Nothing was replaced unless stated below.\n\n{ex.Message}",
                "Restore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string DifferentRunWarning(BackupInfo backup, string target) =>
        $"⚠⚠ DIFFERENT RUN: this backup is \"{backup.DisplayName}\", but {Path.GetFileName(target)} is currently " +
        $"\"{SaveLocator.ReadDisplayName(target)}\". Restoring replaces that run with another one. " +
        "This happens when a run was deleted in game and a new one took its slot.";

    private bool Confirm(BackupInfo backup, string target)
    {
        string message;
        if (File.Exists(target))
        {
            string current;
            try
            {
                var header = SaveFile.ReadHeader(target);
                current = $"\"{(string?)header["m_DisplayName"]}\", game last saved {(string?)header["m_Timestamp"]}";
            }
            catch (Exception)
            {
                current = "unreadable file";
            }

            message = $"⚠ This will OVERWRITE an existing save file:\n\n{target}\n({current})\n\n" +
                      $"It will be replaced by the backup \"{backup.DisplayName}\" taken {backup.CreatedText}.\n\n" +
                      "The current file is backed up first (\"before restore\"), so you can undo this.\n\nContinue?";
            if (SaveLocator.IsDifferentRun(backup))
                message = DifferentRunWarning(backup, target) + "\n\n" + message;
        }
        else
        {
            message = $"No save file named {backup.SaveFileName} exists. Restoring will create:\n\n{target}\n\nContinue?";
        }

        return MessageBox.Show(this, message, "Confirm restore", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }
}
