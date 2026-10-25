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

    private SaveFile(string filePath, JObject slot, Dictionary<string, JToken> sections)
    {
        FilePath = filePath;
        Slot = slot;
        this.sections = sections;
    }

    public string DisplayName => (string?)Slot["m_DisplayName"] ?? "";
    public string GameMode => (string?)Slot["m_GameMode"] ?? "";
    public string Timestamp => (string?)Slot["m_Timestamp"] ?? "";
    public bool IsSurvival => GameMode == SaveLocator.SurvivalGameMode;

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
        var slot = (JObject)TldJson.Parse(Encoding.UTF8.GetString(Lzf.Decompress(File.ReadAllBytes(path))));

        var sections = new Dictionary<string, JToken>();
        if (slot["m_Dict"] is JObject dict)
        {
            foreach (var property in dict.Properties())
                sections[property.Name] = TldJson.Parse(Encoding.UTF8.GetString(Lzf.Decompress(ReadBytes(property.Value))));
        }
        return new SaveFile(path, slot, sections);
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
        foreach (var (key, value) in sections)
        {
            var compressed = Lzf.Compress(Encoding.UTF8.GetBytes(TldJson.Serialize(value)));
            // The game writes base64 strings; older editors wrote number arrays. Keep whatever the file used.
            dict[key] = Slot["m_Dict"]?[key]?.Type == JTokenType.Array
                ? new JArray(compressed.Select(b => (int)b))
                : Convert.ToBase64String(compressed);
        }
        Slot["m_Dict"] = dict;

        File.WriteAllBytes(path, Lzf.Compress(Encoding.UTF8.GetBytes(TldJson.Serialize(Slot))));
    }

    private static byte[] ReadBytes(JToken value) => value.Type switch
    {
        JTokenType.String => Convert.FromBase64String((string)value!),
        JTokenType.Array => value.Select(b => (byte)b).ToArray(),
        _ => throw new InvalidDataException($"Unexpected m_Dict value type {value.Type}"),
    };
}
