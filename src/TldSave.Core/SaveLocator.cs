using System.Text.RegularExpressions;

namespace TldSave.Core;

public sealed record SaveSlotInfo(string FilePath, string FileName, string DisplayName, string GameMode, string Timestamp);

public static partial class SaveLocator
{
    // The setters exist only so tests can point these at temp folders instead of the user's real ones.
    public static string SaveFolder { get; internal set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hinterland", "TheLongDark");

    /// <summary>Current game versions keep Survival mode saves here.</summary>
    public static string SurvivalFolder => Path.Combine(SaveFolder, "Survival");

    public static string BackupFolder { get; internal set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TldSaveEditor", "Backups");

    // Survival mode saves are named sandbox1, sandbox2, ... and have m_GameMode = SANDBOX
    [GeneratedRegex(@"^sandbox[0-9]+$")]
    private static partial Regex SurvivalSaveNameRegex();

    public const string SurvivalGameMode = "SANDBOX";

    public static List<SaveSlotInfo> FindSurvivalSaves()
    {
        var result = new List<SaveSlotInfo>();
        if (!Directory.Exists(SurvivalFolder)) return result;

        foreach (var file in Directory.EnumerateFiles(SurvivalFolder).OrderBy(f => f))
        {
            var name = Path.GetFileName(file);
            if (!SurvivalSaveNameRegex().IsMatch(name)) continue;
            try
            {
                var header = SaveFile.ReadHeader(file);
                var mode = (string?)header["m_GameMode"] ?? "";
                if (mode != SurvivalGameMode) continue;
                result.Add(new SaveSlotInfo(file, name,
                    (string?)header["m_DisplayName"] ?? name,
                    mode,
                    (string?)header["m_Timestamp"] ?? ""));
            }
            catch (Exception)
            {
                result.Add(new SaveSlotInfo(file, name, $"{name} (unreadable)", "", ""));
            }
        }
        return result;
    }

    /// <summary>
    /// Copies the save to Documents\TldSaveEditor\Backups and returns the backup path:
    /// sandbox1_20261025_143000, or sandbox1_20261025_143000_manual with a label.
    /// Never overwrites an existing backup: a counter is added instead (..._manual_2).
    /// </summary>
    public static string Backup(string savePath, string? label = null)
    {
        Directory.CreateDirectory(BackupFolder);
        var suffix = string.IsNullOrEmpty(label) ? "" : "_" + label;
        var baseName = $"{Path.GetFileName(savePath)}_{DateTime.Now:yyyyMMdd_HHmmss}{suffix}";

        var backup = Path.Combine(BackupFolder, baseName);
        for (var n = 2; File.Exists(backup); n++)
            backup = Path.Combine(BackupFolder, $"{baseName}_{n}");

        File.Copy(savePath, backup, overwrite: false);
        return backup;
    }

    // ------------------------------------------------------------------ restore

    // sandbox1_20261025_143000, sandbox1_20261025_143000_manual, sandbox1_20261025_143000_manual_2
    [GeneratedRegex(@"^(?<save>sandbox[0-9]+)_(?<date>\d{8}_\d{6})(?:_(?<label>[A-Za-z][A-Za-z-]*))?(?:_(?<n>\d+))?$")]
    private static partial Regex BackupNameRegex();

    /// <summary>Survival save backups in the backup folder, newest first.</summary>
    public static List<BackupInfo> FindBackups()
    {
        var result = new List<BackupInfo>();
        if (!Directory.Exists(BackupFolder)) return result;

        foreach (var file in Directory.EnumerateFiles(BackupFolder))
        {
            var match = BackupNameRegex().Match(Path.GetFileName(file));
            if (!match.Success) continue;

            // The name pattern accepts any digits (e.g. month 13): skip files whose date isn't real
            if (!DateTime.TryParseExact(match.Groups["date"].Value, "yyyyMMdd_HHmmss", null,
                    System.Globalization.DateTimeStyles.None, out var created)) continue;
            var kind = match.Groups["label"].Value switch
            {
                "" => "Before edit",
                "manual" => "Manual",
                "before-restore" => "Before restore",
                var other => other,
            };

            string? displayName = null, gameTimestamp = null;
            var valid = false;
            try
            {
                var header = SaveFile.ReadHeader(file);
                displayName = (string?)header["m_DisplayName"];
                gameTimestamp = (string?)header["m_Timestamp"];
                valid = (string?)header["m_GameMode"] == SurvivalGameMode;
            }
            catch (Exception) { /* listed as unreadable */ }

            result.Add(new BackupInfo(file, match.Groups["save"].Value, created, kind, displayName, gameTimestamp, valid));
        }
        return result.OrderByDescending(b => b.Created).ThenByDescending(b => b.FilePath).ToList();
    }

    /// <summary>The save file a backup restores to, e.g. ...\Survival\sandbox1.</summary>
    public static string RestoreTarget(BackupInfo backup) => Path.Combine(SurvivalFolder, backup.SaveFileName);

    /// <summary>In-game name of a save file, or null if it doesn't exist or can't be read.</summary>
    public static string? ReadDisplayName(string savePath)
    {
        if (!File.Exists(savePath)) return null;
        try { return (string?)SaveFile.ReadHeader(savePath)["m_DisplayName"]; }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// True if the save the backup would replace is a different run (different in-game name). Happens when a run
    /// was deleted in game and a new one reused its slot (sandbox1), or when the backup came from a copy of
    /// another save that had the same file name.
    /// </summary>
    public static bool IsDifferentRun(BackupInfo backup)
    {
        var current = ReadDisplayName(RestoreTarget(backup));
        return current is not null && backup.DisplayName is not null && current != backup.DisplayName;
    }

    /// <summary>
    /// Restores a backup over its save file. If that file exists it is first backed up ("before-restore"),
    /// and that backup's path is returned. The caller must have asked the user before calling this.
    /// </summary>
    public static string? Restore(BackupInfo backup)
    {
        if (!backup.IsValid) throw new InvalidOperationException($"{backup.FileName} is not a readable survival save.");
        if (IsGameRunning()) throw new InvalidOperationException("The Long Dark is running. Quit the game completely first.");

        var target = RestoreTarget(backup);
        var safetyBackup = File.Exists(target) ? Backup(target, "before-restore") : null;

        Directory.CreateDirectory(SurvivalFolder);
        // Copy to a temp file, check it decodes, then swap it in: the save is never left half-written
        SaveFile.WriteVerified(target, temp => File.Copy(backup.FilePath, temp, overwrite: true));
        return safetyBackup;
    }

    /// <summary>True if The Long Dark (tld.exe) is running: it keeps the save in memory and would overwrite edits.</summary>
    public static bool IsGameRunning() => System.Diagnostics.Process.GetProcessesByName("tld").Length > 0;
}

public sealed record BackupInfo(
    string FilePath, string SaveFileName, DateTime Created, string Kind,
    string? DisplayName, string? GameTimestamp, bool IsValid)
{
    public string FileName => Path.GetFileName(FilePath);
    public string CreatedText => Created.ToString("yyyy-MM-dd HH:mm:ss");
    public string DisplayNameText => IsValid ? DisplayName ?? "" : "(unreadable)";
}
