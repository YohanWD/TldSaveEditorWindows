// Builds src/TldSave.Core/ItemCatalog.json: the list of items the editor can add.
//
// Item names come from every save on this machine (player inventory, containers and loose items in every
// visited scene) plus, optionally, the original TS editor's availableItems.ts. For items found in a save,
// a real sample is cleaned into a "fresh item" template so new items get correct component data.
//
// Usage: dotnet run --project tools/CatalogBuilder -- <output ItemCatalog.json> [path to availableItems.ts]

using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TldSave.Core;

var outputPath = args.Length > 0 ? args[0] : "src/TldSave.Core/ItemCatalog.json";
var tsListPath = args.Length > 1 ? args[1] : null;

var samples = new Dictionary<string, Sample>();
var copySuffix = new Regex(@"\s\(\d+\)$"); // Unity copies: "GEAR_Stick (12)"

void AddSample(JToken? nameToken, JToken? gearToken, int priority, bool survival)
{
    var name = (string?)nameToken;
    if (string.IsNullOrEmpty(name) || !name.StartsWith("GEAR_")) return;
    name = copySuffix.Replace(name, "");
    var gear = AsObject(gearToken) ?? new JObject();

    if (!samples.TryGetValue(name, out var existing))
    {
        samples[name] = new Sample(gear, priority, survival);
        return;
    }
    existing.SeenInSurvival |= survival;
    if (priority < existing.Priority) { existing.Gear = gear; existing.Priority = priority; }
}

var saveFiles = new[] { SaveLocator.SurvivalFolder, SaveLocator.SaveFolder }
    .Where(Directory.Exists)
    .SelectMany(Directory.EnumerateFiles)
    .Where(f => Regex.IsMatch(Path.GetFileName(f), @"^(ep[0-9])?(sandbox|challenge|story|relentless)[0-9]+$"));

foreach (var file in saveFiles)
{
    SaveFile save;
    try { save = SaveFile.Load(file); }
    catch (Exception e) { Console.WriteLine($"skip {file}: {e.Message}"); continue; }
    Console.WriteLine($"scan {file} ({save.GameMode})");
    var survival = save.IsSurvival;

    // Priority: 0 = player inventory, 1 = container, 2 = lying in the world
    foreach (var item in Inventory.Load(save).Items)
        AddSample(item.Entry["m_PrefabName"], item.Gear, 0, survival);

    foreach (var (key, section) in save.Sections)
    {
        if (SaveFile.IsMetaSection(key) || section is not JObject scene) continue;

        var containers = AsObject(scene["m_ContainerManagerSerialized"])?["m_SerializedContainers"] as JArray;
        foreach (var container in containers?.OfType<JObject>() ?? [])
        {
            var data = AsObject(container["m_SearializedContainer"]);
            foreach (var item in (data?["m_SerializedItems"] as JArray)?.OfType<JObject>() ?? [])
                AddSample(item["m_PrefabName"], item["m_SerializedGear"] ?? item["m_SearializedGear"], 1, survival);
        }

        var loose = AsObject(scene["m_GearManagerSerialized"])?["m_SerializedItems"] as JArray;
        foreach (var item in loose?.OfType<JObject>() ?? [])
            AddSample(item["m_PrefabName"], item["m_SearializedGear"] ?? item["m_SerializedGear"], 2, survival);
    }
}

var tsNames = new HashSet<string>();
if (tsListPath is not null)
{
    foreach (Match m in Regex.Matches(File.ReadAllText(tsListPath), @"create\w+\('(GEAR_[^']+)'"))
        tsNames.Add(m.Groups[1].Value);
    Console.WriteLine($"{tsNames.Count} names in {tsListPath}");
}

var entries = new List<JObject>();
foreach (var name in samples.Keys.Union(tsNames).Distinct())
{
    if (name.Contains("Placeholder") || name.Contains("Dummy")) continue; // internal objects, not real items

    var sample = samples.GetValueOrDefault(name);
    var template = sample is null ? null : BuildTemplate(sample.Gear);
    var category = ItemCategories.Classify(name, template ?? new JObject());
    if (category is ItemCategory.NotesAndCollectibles or ItemCategory.KeysAndQuest) continue;

    var entry = new JObject
    {
        ["name"] = name,
        ["category"] = category.ToString(),
        ["source"] = sample is null ? "list" : sample.SeenInSurvival ? "survival" : "other-modes",
    };
    if (template is not null) entry["template"] = template;
    entries.Add(entry);
}

entries = entries
    .OrderBy(e => Enum.Parse<ItemCategory>((string)e["category"]!))
    .ThenBy(e => (string)e["name"]!, StringComparer.OrdinalIgnoreCase)
    .ToList();

File.WriteAllText(outputPath, TldJson.Serialize(new JArray(entries), indented: true));
Console.WriteLine($"\nWrote {entries.Count} items to {Path.GetFullPath(outputPath)}");
foreach (var group in entries.GroupBy(e => (string)e["category"]!))
    Console.WriteLine($"  {group.Key,-14} {group.Count(),4}   ({group.Count(e => (string)e["source"]! == "survival")} seen in survival, {group.Count(e => e["template"] is null)} without template)");

// ------------------------------------------------------------------ helpers

static JObject? AsObject(JToken? token) => token switch
{
    JObject obj => obj,
    JValue { Type: JTokenType.String } value when ((string?)value)?.StartsWith('{') == true => TldJson.Parse((string)value!) as JObject,
    _ => null,
};

// Turns a real item into a "fresh, never touched" template: only component data is kept, reset to new.
static JObject BuildTemplate(JObject sample)
{
    string[] dropped =
    [
        "m_ObjectGuidSerialized", "m_MissionObjectSerialized", "m_OwnershipOverrideSerialized",
        "m_PlacePointGuidSerialized", "m_PlacePointNameSerialized", "m_InProgressItemSerialized",
    ];

    var template = new JObject();
    foreach (var property in sample.Properties())
    {
        // Older saves list every component with null; only keep the ones this item really has
        var keep = property.Value.Type != JTokenType.Null
                   && (property.Name is "m_WeightKG" or "m_GearItemSaveVersion"
                       || (property.Name.EndsWith("Serialized") && !dropped.Contains(property.Name)));
        if (keep) template[property.Name] = property.Value.DeepClone();
    }

    Reset(template, "m_StackableItemSerialized", c => { c.RemoveAll(); c["m_UnitsProxy"] = 1; });
    Reset(template, "m_FoodItemSerialized", c =>
    {
        if (c["m_CaloriesTotal"] is { } total) c["m_CaloriesRemainingProxy"] = total.DeepClone();
    });
    Reset(template, "m_ClothingItemSerialized", c =>
    {
        c.Remove("m_HasEquippedData");
        c.Remove("m_EquippedLayerIndex");
        foreach (var field in new[] { "m_PercentWet", "m_PercentFrozen" })
            if (c[field] is not null) c[field] = 0.0;
    });

    // Max HP = current HP / condition, so the new item is at 100%
    var condition = (double?)sample["m_NormalizedCondition"];
    var hp = (double?)sample["m_CurrentHPProxy"];
    template["m_NormalizedCondition"] = 1.0;
    if (condition > 0 && hp is not null) template["m_CurrentHPProxy"] = Math.Round(hp.Value / condition.Value, 3);

    return template;
}

static void Reset(JObject template, string field, Action<JObject> reset)
{
    if (AsObject(template[field]) is not { } component) return;
    reset(component);
    template[field] = TldJson.Serialize(component);
}

sealed class Sample(JObject gear, int priority, bool seenInSurvival)
{
    public JObject Gear { get; set; } = gear;
    public int Priority { get; set; } = priority;
    public bool SeenInSurvival { get; set; } = seenInSurvival;
}
