namespace TldSave.Core.Tests;

/// <summary>Fixture saves and a temp folder per test. Tests never touch the real TLD save folder.</summary>
public sealed class TestFiles : IDisposable
{
    public const string Small = "sandbox1_GoDLC";      // 0.5 MB, day 2.8, 7 sections
    public const string Large = "sandbox3_1000Days";   // 13 MB, day 323, 169 sections

    public static IEnumerable<object[]> BothSaves => [[Small], [Large]];

    public static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    public string Folder { get; } = Directory.CreateTempSubdirectory("TldSaveTests_").FullName;

    /// <summary>Copies a fixture into the temp folder, so a test can modify it.</summary>
    public string Copy(string fixture)
    {
        var path = Path.Combine(Folder, fixture);
        File.Copy(Fixture(fixture), path, overwrite: true);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Folder, recursive: true); } catch (IOException) { }
    }
}
