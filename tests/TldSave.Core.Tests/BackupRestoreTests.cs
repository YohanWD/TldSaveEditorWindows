namespace TldSave.Core.Tests;

/// <summary>Tests that change SaveLocator's (static) folders run alone, never in parallel with each other.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SaveLocatorFolders
{
    public const string Name = "SaveLocator folders";
}

/// <summary>
/// Backup and restore rules, on temp folders standing in for the TLD save folder and Documents\...\Backups.
/// Restore refuses while the game runs, so these tests fail if The Long Dark is open.
/// </summary>
[Collection(SaveLocatorFolders.Name)]
public sealed class BackupRestoreTests : IDisposable
{
    private readonly TestFiles files = new();
    private readonly string originalSaveFolder = SaveLocator.SaveFolder;
    private readonly string originalBackupFolder = SaveLocator.BackupFolder;

    public BackupRestoreTests()
    {
        SaveLocator.SaveFolder = Path.Combine(files.Folder, "TheLongDark");
        SaveLocator.BackupFolder = Path.Combine(files.Folder, "Backups");
        Directory.CreateDirectory(SaveLocator.SurvivalFolder);
    }

    public void Dispose()
    {
        SaveLocator.SaveFolder = originalSaveFolder;
        SaveLocator.BackupFolder = originalBackupFolder;
        files.Dispose();
    }

    /// <summary>Puts a fixture in the fake Survival folder under a slot name such as sandbox1.</summary>
    private static string PutSave(string fixture, string slot)
    {
        var path = Path.Combine(SaveLocator.SurvivalFolder, slot);
        File.Copy(TestFiles.Fixture(fixture), path, overwrite: true);
        return path;
    }

    [Fact]
    public void Backups_never_overwrite_each_other()
    {
        var save = PutSave(TestFiles.Small, "sandbox1");
        var backups = Enumerable.Range(0, 3).Select(_ => SaveLocator.Backup(save, "manual")).ToList();

        Assert.Equal(3, backups.Distinct().Count());
        Assert.All(backups, b => Assert.Equal(File.ReadAllBytes(save), File.ReadAllBytes(b)));
        Assert.All(backups, b => Assert.Matches(@"^sandbox1_\d{8}_\d{6}_manual(_\d+)?$", Path.GetFileName(b)));
    }

    [Fact]
    public void FindBackups_lists_valid_backups_and_skips_bad_names()
    {
        var save = PutSave(TestFiles.Small, "sandbox1");
        var edit = SaveLocator.Backup(save);
        var manual = SaveLocator.Backup(save, "manual");
        Directory.CreateDirectory(SaveLocator.BackupFolder);
        File.WriteAllText(Path.Combine(SaveLocator.BackupFolder, "sandbox1_20261399_999999"), "month 13");  // N4
        File.WriteAllText(Path.Combine(SaveLocator.BackupFolder, "notes.txt"), "not a backup");
        File.WriteAllText(Path.Combine(SaveLocator.BackupFolder, "sandbox2_20261025_120000"), "garbage");

        var found = SaveLocator.FindBackups();

        Assert.Equal(3, found.Count);   // the two real backups + the unreadable one; bad date and notes.txt skipped
        Assert.Contains(found, b => b.FilePath == edit && b.Kind == "Before edit" && b.IsValid && b.DisplayName == "GoDLC");
        Assert.Contains(found, b => b.FilePath == manual && b.Kind == "Manual" && b.IsValid);
        Assert.Contains(found, b => b.SaveFileName == "sandbox2" && !b.IsValid);
        Assert.Equal(found.OrderByDescending(b => b.Created).Select(b => b.Created), found.Select(b => b.Created));
    }

    [Fact]
    public void Restore_replaces_the_save_and_backs_up_the_one_it_replaces()
    {
        var save = PutSave(TestFiles.Small, "sandbox1");
        var original = File.ReadAllBytes(save);
        var backupPath = SaveLocator.Backup(save, "manual");

        var edited = SaveFile.Load(save);                 // change the save after the backup
        new PlayerState(edited).Thirst = 99;
        edited.Save(save);
        var editedBytes = File.ReadAllBytes(save);

        var safety = SaveLocator.Restore(SaveLocator.FindBackups().Single(b => b.FilePath == backupPath));

        Assert.Equal(original, File.ReadAllBytes(save));
        Assert.NotNull(safety);
        Assert.EndsWith("_before-restore", Path.GetFileName(safety));
        Assert.Equal(editedBytes, File.ReadAllBytes(safety));
        Assert.False(File.Exists(save + ".tmp"));
    }

    [Fact]
    public void Restore_creates_a_save_that_no_longer_exists()
    {
        var save = PutSave(TestFiles.Small, "sandbox1");
        var backupPath = SaveLocator.Backup(save);
        File.Delete(save);

        var safety = SaveLocator.Restore(SaveLocator.FindBackups().Single(b => b.FilePath == backupPath));

        Assert.Null(safety);
        Assert.Equal(File.ReadAllBytes(backupPath), File.ReadAllBytes(save));
    }

    [Fact]
    public void Restore_refuses_an_unreadable_backup()
    {
        var save = PutSave(TestFiles.Small, "sandbox1");
        var before = File.ReadAllBytes(save);
        Directory.CreateDirectory(SaveLocator.BackupFolder);
        File.WriteAllText(Path.Combine(SaveLocator.BackupFolder, "sandbox1_20261025_120000"), "garbage");

        var bad = SaveLocator.FindBackups().Single();
        Assert.Throws<InvalidOperationException>(() => SaveLocator.Restore(bad));
        Assert.Equal(before, File.ReadAllBytes(save));
    }

    [Fact]
    public void A_backup_of_another_run_with_the_same_slot_is_detected()
    {
        // "1000Days" was backed up from sandbox1, then deleted in game; a new run "GoDLC" took the slot
        var oldRun = PutSave(TestFiles.Large, "sandbox1");
        var oldBackup = SaveLocator.Backup(oldRun);
        PutSave(TestFiles.Small, "sandbox1");
        var sameRunBackup = SaveLocator.Backup(Path.Combine(SaveLocator.SurvivalFolder, "sandbox1"), "manual");

        var backups = SaveLocator.FindBackups();
        Assert.True(SaveLocator.IsDifferentRun(backups.Single(b => b.FilePath == oldBackup)));
        Assert.False(SaveLocator.IsDifferentRun(backups.Single(b => b.FilePath == sameRunBackup)));

        // Nothing to compare with when the slot is empty
        File.Delete(Path.Combine(SaveLocator.SurvivalFolder, "sandbox1"));
        Assert.False(SaveLocator.IsDifferentRun(backups.Single(b => b.FilePath == oldBackup)));
    }

    [Fact]
    public void Survival_saves_are_found_by_slot_name_and_game_mode()
    {
        PutSave(TestFiles.Small, "sandbox1");
        PutSave(TestFiles.Large, "sandbox3");
        File.WriteAllText(Path.Combine(SaveLocator.SurvivalFolder, "profile_survival.123"), "not a save");

        var saves = SaveLocator.FindSurvivalSaves();

        Assert.Equal(["GoDLC", "1000Days"], saves.Select(s => s.DisplayName));
    }
}
