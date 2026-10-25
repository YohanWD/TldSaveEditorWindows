using Newtonsoft.Json.Linq;

namespace TldSave.Core.Tests;

public class SaveFileTests : IDisposable
{
    private readonly TestFiles files = new();
    public void Dispose() => files.Dispose();

    private static JObject RawSections(string path) => (JObject)SaveFile.Load(path).Slot["m_Dict"]!;

    [Theory]
    [InlineData(TestFiles.Small, "GoDLC", 7)]
    [InlineData(TestFiles.Large, "1000Days", 169)]
    public void Fixture_saves_load(string fixture, string displayName, int sections)
    {
        var save = SaveFile.Load(TestFiles.Fixture(fixture));
        Assert.True(save.IsSurvival);
        Assert.Equal(displayName, save.DisplayName);
        Assert.Equal(sections, save.Sections.Count);
        Assert.True(save.HoursPlayed > 0);
    }

    [Theory]
    [MemberData(nameof(TestFiles.BothSaves), MemberType = typeof(TestFiles))]
    public void Saving_without_edits_keeps_every_section_byte_for_byte(string fixture)
    {
        var path = files.Copy(fixture);
        var save = SaveFile.Load(path);
        save.Save(path);

        Assert.Empty(save.LastSaveChangedSections);
        var original = RawSections(TestFiles.Fixture(fixture));
        var written = RawSections(path);
        Assert.All(original.Properties(), p => Assert.True(JToken.DeepEquals(p.Value, written[p.Name]), p.Name));
    }

    [Theory]
    [MemberData(nameof(TestFiles.BothSaves), MemberType = typeof(TestFiles))]
    public void An_edit_rewrites_only_the_edited_sections(string fixture)
    {
        var path = files.Copy(fixture);
        var save = SaveFile.Load(path);
        new PlayerState(save).Condition = 50;   // global + its copy in info
        save.Save(path);

        Assert.Equal(["global", "info"], save.LastSaveChangedSections.Order());
        var original = RawSections(TestFiles.Fixture(fixture));
        var written = RawSections(path);
        var untouched = original.Properties().Where(p => p.Name is not ("global" or "info"));
        Assert.All(untouched, p => Assert.True(JToken.DeepEquals(p.Value, written[p.Name]), p.Name));

        var reloaded = SaveFile.Load(path);
        Assert.Equal(50, new PlayerState(reloaded).Condition);
        Assert.Equal(50, (double)reloaded.Info!["m_Condition"]!);
    }

    [Fact]
    public void Saving_twice_rewrites_nothing_the_second_time()
    {
        var path = files.Copy(TestFiles.Large);
        var save = SaveFile.Load(path);
        new PlayerState(save).Thirst = 10;
        save.Save(path);
        save.Save(path);
        Assert.Empty(save.LastSaveChangedSections);
    }

    [Fact]
    public void A_failed_save_is_retried_in_full_by_the_next_one()
    {
        var path = files.Copy(TestFiles.Small);
        var save = SaveFile.Load(path);
        new PlayerState(save).Thirst = 42;

        // Fails: the folder doesn't exist
        Assert.ThrowsAny<IOException>(() => save.Save(Path.Combine(files.Folder, "missing", "sandbox1")));

        save.Save(path);
        Assert.Equal(["global"], save.LastSaveChangedSections);
        Assert.Equal(42, new PlayerState(SaveFile.Load(path)).Thirst);
    }

    [Fact]
    public void Corrupt_data_never_replaces_the_save()
    {
        var path = files.Copy(TestFiles.Small);
        var before = File.ReadAllBytes(path);

        Assert.ThrowsAny<Exception>(() => SaveFile.WriteVerified(path, temp => File.WriteAllBytes(temp, [1, 2, 3])));

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void A_crash_while_writing_never_replaces_the_save()
    {
        var path = files.Copy(TestFiles.Small);
        var before = File.ReadAllBytes(path);

        Assert.Throws<IOException>(() => SaveFile.WriteVerified(path, temp =>
        {
            File.WriteAllBytes(temp, before[..1000]);
            throw new IOException("simulated disk full");
        }));

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void A_file_written_by_someone_else_after_loading_is_detected()
    {
        var path = files.Copy(TestFiles.Small);
        var save = SaveFile.Load(path);
        Assert.False(save.ChangedOnDisk);

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5)); // the game saving meanwhile
        Assert.True(save.ChangedOnDisk);

        save.Save(path);
        Assert.False(save.ChangedOnDisk);
    }

    [Theory]
    [InlineData("global", false)]
    [InlineData("screenshot", false)]
    [InlineData("LakeRegion", true)]
    public void Meta_sections_are_not_scenes(string key, bool isScene)
    {
        Assert.Equal(!isScene, SaveFile.IsMetaSection(key));
    }
}
