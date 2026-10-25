using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace TldSave.Core;

/// <summary>
/// A TLD save file.
///
///   file                 = LZF( JSON "SlotData" )         m_DisplayName, m_GameMode, m_Dict, ...
///   SlotData.m_Dict[key] = base64 -> LZF( JSON )          boot, global, info, screenshot, one per visited scene
///   inside sections      = many values are JSON strings containing more JSON (see TldJson.ReadNested)
/// </summary>
public sealed class SaveFile
{
    public string FilePath { get; }

    /// <summary>The outer SlotData object. Its m_Dict is rebuilt from <see cref="Sections"/> on save.</summary>
    public JObject Slot { get; }

    /// <summary>Decoded m_Dict sections, in file order.</summary>
    public IReadOnlyDictionary<string, JToken> Sections => sections;

    private readonly Dictionary<string, JToken> sections;

    /// <summary>
    /// SHA-256 of each section's JSON text, matching its encoded value currently in Slot.m_Dict.
    /// On save, a section whose JSON still has the same fingerprint keeps that encoded value as-is
    /// (the game's own bytes), so only edited sections are re-encoded.
    /// </summary>
    private readonly Dictionary<string, byte[]> fingerprints;

    /// <summary>Sections re-encoded by the last <see cref="Save"/> (the ones that changed).</summary>
    public IReadOnlyList<string> LastSaveChangedSections { get; private set; } = [];

    /// <summary>The file's last-write time when it was loaded (or last saved by us).</summary>
    public DateTime LoadedWriteTimeUtc { get; private set; }

    /// <summary>True if something else (usually the game) wrote the file after we loaded it.</summary>
    public bool ChangedOnDisk => File.Exists(FilePath) && File.GetLastWriteTimeUtc(FilePath) != LoadedWriteTimeUtc;

    private SaveFile(string filePath, JObject slot, Dictionary<string, JToken> sections,
        Dictionary<string, byte[]> fingerprints, DateTime writeTimeUtc)
    {
        FilePath = filePath;
        Slot = slot;
        this.sections = sections;
        this.fingerprints = fingerprints;
        LoadedWriteTimeUtc = writeTimeUtc;
    }

    public string DisplayName => (string?)Slot["m_DisplayName"] ?? "";
    public string GameMode => (string?)Slot["m_GameMode"] ?? "";
    public string Timestamp => (string?)Slot["m_Timestamp"] ?? "";
    public bool IsSurvival => GameMode == SaveLocator.SurvivalGameMode;

    /// <summary>True for the sections that aren't a visited scene (location).</summary>
    public static bool IsMetaSection(string key) => key is "boot" or "global" or "info" or "screenshot" or "sandbox_global";

    public JObject Global => (JObject)sections["global"];
    public JObject? Info => sections.GetValueOrDefault("info") as JObject;
    public JObject? Boot => sections.GetValueOrDefault("boot") as JObject;

    /// <summary>The game's main clock, in hours since the start of the run.</summary>
    public double HoursPlayed =>
        (double?)Global.ReadNested("m_TimeOfDay_Serialized")?["m_HoursPlayedNotPausedProxy"]
        ?? (double?)Info?["m_HoursSurvived"]
        ?? 0;

    public static SaveFile Load(string path)
    {
        var writeTime = File.GetLastWriteTimeUtc(path);
        var slot = (JObject)TldJson.Parse(Encoding.UTF8.GetString(Lzf.Decompress(File.ReadAllBytes(path))));

        var sections = new Dictionary<string, JToken>();
        var fingerprints = new Dictionary<string, byte[]>();
        if (slot["m_Dict"] is JObject dict)
        {
            foreach (var property in dict.Properties())
            {
                var json = Lzf.Decompress(ReadBytes(property.Value));
                fingerprints[property.Name] = SHA256.HashData(json);
                sections[property.Name] = TldJson.Parse(Encoding.UTF8.GetString(json));
            }
        }
        return new SaveFile(path, slot, sections, fingerprints, writeTime);
    }

    /// <summary>Reads only the outer SlotData (name, mode, timestamp). Sections are left undecoded.</summary>
    public static JObject ReadHeader(string path)
    {
        var slot = (JObject)TldJson.Parse(Encoding.UTF8.GetString(Lzf.Decompress(File.ReadAllBytes(path))));
        slot.Remove("m_Dict");
        return slot;
    }

    public void Save(string path)
    {
        var dict = new JObject();
        var changed = new List<string>();
        foreach (var (key, value) in sections)
        {
            var json = Encoding.UTF8.GetBytes(TldJson.Serialize(value));
            var fingerprint = SHA256.HashData(json);
            var original = Slot["m_Dict"]?[key];

            // Unchanged section: keep the encoded value from the file, byte for byte
            if (original is not null && fingerprints.TryGetValue(key, out var known) && known.AsSpan().SequenceEqual(fingerprint))
            {
                dict[key] = original.DeepClone();
                continue;
            }

            var compressed = Lzf.Compress(json);
            // The game writes base64 strings; older editors wrote number arrays. Keep whatever the file used.
            dict[key] = original?.Type == JTokenType.Array
                ? new JArray(compressed.Select(b => (int)b))
                : Convert.ToBase64String(compressed);
            fingerprints[key] = fingerprint;
            changed.Add(key);
        }
        Slot["m_Dict"] = dict;
        LastSaveChangedSections = changed;

        var bytes = Lzf.Compress(Encoding.UTF8.GetBytes(TldJson.Serialize(Slot)));
        WriteVerified(path, temp => File.WriteAllBytes(temp, bytes));

        if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(FilePath), StringComparison.OrdinalIgnoreCase))
            LoadedWriteTimeUtc = File.GetLastWriteTimeUtc(path);
    }

    /// <summary>
    /// Replaces <paramref name="path"/> safely: <paramref name="writeTemp"/> writes "path.tmp", which is loaded
    /// back as a save to prove it decodes, then swapped in with File.Replace. If anything fails (crash, full disk,
    /// bad data) the original file is untouched and the temp file is removed.
    /// </summary>
    internal static void WriteVerified(string path, Action<string> writeTemp)
    {
        var temp = path + ".tmp"; // the game only lists files named exactly sandbox<N>, so it never sees this
        try
        {
            writeTemp(temp);
            Load(temp);
            if (File.Exists(path)) File.Replace(temp, path, destinationBackupFileName: null);
            else File.Move(temp, path);
        }
        catch
        {
            if (File.Exists(temp)) File.Delete(temp);
            throw;
        }
    }

    private static byte[] ReadBytes(JToken value) => value.Type switch
    {
        JTokenType.String => Convert.FromBase64String((string)value!),
        JTokenType.Array => value.Select(b => (byte)b).ToArray(),
        _ => throw new InvalidDataException($"Unexpected m_Dict value type {value.Type}"),
    };
}
