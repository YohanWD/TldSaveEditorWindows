using System.Text.RegularExpressions;

namespace TldSave.Core;

public sealed record SaveSlotInfo(string FilePath, string FileName, string DisplayName, string GameMode, string Timestamp);

public static partial class SaveLocator
{
    public static string SaveFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hinterland", "TheLongDark");

    /// <summary>Current game versions keep Survival mode saves here.</summary>
    public static string SurvivalFolder { get; } = Path.Combine(SaveFolder, "Survival");

    public static string BackupFolder { get; } =
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

    /// <summary>Copies the save to Documents\TldSaveEditor\Backups and returns the backup path.</summary>
    public static string Backup(string savePath)
    {
        Directory.CreateDirectory(BackupFolder);
        var backup = Path.Combine(BackupFolder, $"{Path.GetFileName(savePath)}_{DateTime.Now:yyyyMMdd_HHmmss}");
        File.Copy(savePath, backup, overwrite: true);
        return backup;
    }
}
