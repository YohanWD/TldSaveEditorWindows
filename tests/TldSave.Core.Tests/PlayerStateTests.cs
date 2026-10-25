using Newtonsoft.Json.Linq;

namespace TldSave.Core.Tests;

public class PlayerStateTests : IDisposable
{
    private readonly TestFiles files = new();
    public void Dispose() => files.Dispose();

    [Fact]
    public void Status_values_survive_a_save()
    {
        var path = files.Copy(TestFiles.Large);
        var save = SaveFile.Load(path);
        var player = new PlayerState(save);
        player.WellFed = false;
        player.Condition = 60;
        player.Calories = 500;
        player.Thirst = 80;
        player.Fatigue = 0;
        player.Freezing = 12.5;
        save.Save(path);

        var reloaded = new PlayerState(SaveFile.Load(path));
        Assert.Equal(60, reloaded.Condition);
        Assert.Equal(500, reloaded.Calories);
        Assert.Equal(80, reloaded.Thirst);
        Assert.Equal(0, reloaded.Fatigue);
        Assert.Equal(12.5, reloaded.Freezing);
        Assert.False(reloaded.WellFed);
    }

    [Fact]
    public void Values_are_clamped_to_their_range()
    {
        var player = new PlayerState(SaveFile.Load(TestFiles.Fixture(TestFiles.Small)));
        player.Thirst = 150;
        player.Calories = -10;
        player.WellFed = false;
        player.Condition = 500;
        Assert.Equal(100, player.Thirst);
        Assert.Equal(0, player.Calories);
        Assert.Equal(100, player.Condition);
    }

    [Fact]
    public void Restore_all_gives_full_meters()
    {
        var player = new PlayerState(SaveFile.Load(TestFiles.Fixture(TestFiles.Large)));
        player.RestoreAll();
        Assert.Equal(player.ConditionMax, player.Condition);
        Assert.Equal(PlayerState.MaxCalories, player.Calories);
        Assert.Equal(0, player.Thirst);
        Assert.Equal(0, player.Fatigue);
        Assert.Equal(0, player.Freezing);
    }

    [Fact]
    public void Well_fed_sets_the_hours_even_when_the_field_is_missing()
    {
        var save = SaveFile.Load(TestFiles.Fixture(TestFiles.Small));
        save.Global.WriteNested("m_WellFedSerialized", new JObject());   // no m_ElapsedHoursNotStarving at all

        new PlayerState(save).WellFed = true;

        var wellFed = (JObject)save.Global.ReadNested("m_WellFedSerialized")!;
        Assert.True((bool)wellFed["m_Active"]!);
        Assert.Equal(72.0, (double)wellFed["m_ElapsedHoursNotStarving"]!);
    }

    [Theory]
    [InlineData("Cooking", 0, "Level 1")]
    [InlineData("Cooking", 80, "Level 3")]
    [InlineData("Cooking", 350, "Level 5 (max)")]
    [InlineData("Fire starting", 20, "Level 2")]
    [InlineData("Archery", 125, "Level 5 (max)")]
    public void Skill_levels_match_their_points(string skillName, int points, string level)
    {
        var player = new PlayerState(SaveFile.Load(TestFiles.Fixture(TestFiles.Small)));
        var skill = player.Skills.Single(s => s.Name == skillName);
        skill.Points = points;
        Assert.Equal(level, skill.LevelText);
    }

    [Fact]
    public void Max_all_skills_survives_a_save()
    {
        var path = files.Copy(TestFiles.Small);
        var save = SaveFile.Load(path);
        new PlayerState(save).MaxAllSkills();
        save.Save(path);

        var skills = new PlayerState(SaveFile.Load(path)).Skills.Where(s => s.Exists).ToList();
        Assert.NotEmpty(skills);
        Assert.All(skills, s => Assert.Equal(s.MaxPoints, s.Points));
    }

    [Fact]
    public void Curing_empties_the_wound_lists()
    {
        var save = SaveFile.Load(TestFiles.Fixture(TestFiles.Small));
        // A wolf-bite sprained wrist, in the exact shape a real save stores it
        save.Global.WriteNested("m_SprainedWristSerialized", JObject.Parse(
            """{"m_CausesLocIDs":["GAMEPLAY_WolfBite"],"m_Locations":[3],"m_ElapsedHoursList":[0.27],"m_DurationHoursList":[51.4],"m_ElapsedRestList":[0.0]}"""));
        var player = new PlayerState(save);
        var wrist = player.Afflictions.Single(a => a.Name == "Sprained wrist");
        Assert.True(wrist.IsActive);

        wrist.Cure();

        Assert.False(wrist.IsActive);
        var data = (JObject)save.Global.ReadNested("m_SprainedWristSerialized")!;
        Assert.All(data.Properties(), p => Assert.Empty((JArray)p.Value));
    }

    [Fact]
    public void Curing_a_flag_affliction_clears_the_flag()
    {
        var save = SaveFile.Load(TestFiles.Fixture(TestFiles.Small));
        save.Global.WriteNested("m_BurnsSerialized", JObject.Parse("""{"m_Active":true,"m_DurationHours":33.0}"""));
        var burns = new PlayerState(save).Afflictions.Single(a => a.Name == "Burns");
        Assert.True(burns.IsActive);

        burns.Cure();

        Assert.False(burns.IsActive);
    }

    [Theory]
    [MemberData(nameof(TestFiles.BothSaves), MemberType = typeof(TestFiles))]
    public void Fixture_players_have_no_active_affliction(string fixture)
    {
        var player = new PlayerState(SaveFile.Load(TestFiles.Fixture(fixture)));
        Assert.DoesNotContain(player.Afflictions, a => a.IsActive);
    }
}
