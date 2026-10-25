using Newtonsoft.Json.Linq;

namespace TldSave.Core;

/// <summary>Where a catalog item was found when the catalog was built (see tools/CatalogBuilder).</summary>
public enum CatalogSource
{
    /// <summary>Found in a Survival save: the safest choice.</summary>
    Survival,
    /// <summary>Only found in story / challenge saves.</summary>
    OtherModes,
    /// <summary>Only in the original TS editor's list, never seen in a save. May no longer exist in the game.</summary>
    List,
}

public sealed record CatalogItem(string Name, ItemCategory Category, CatalogSource Source, JObject? Template)
{
    public string ShortName => Name.StartsWith("GEAR_") ? Name["GEAR_".Length..] : Name;
    public string CategoryName => ItemCategories.DisplayName(Category);
    public bool IsStackable => Template?["m_StackableItemSerialized"] is { Type: JTokenType.String };

    public string SourceText => Source switch
    {
        CatalogSource.Survival => "",
        CatalogSource.OtherModes => "story/challenge only",
        _ => "unverified name",
    };
}

/// <summary>Items that can be added to the inventory. Notes, collectibles and keys are excluded.</summary>
public static class ItemCatalog
{
    private static readonly Lazy<IReadOnlyList<CatalogItem>> items = new(Load);

    public static IReadOnlyList<CatalogItem> Items => items.Value;

    public static CatalogItem? Find(string prefabName) => Items.FirstOrDefault(i => i.Name == prefabName);

    private static IReadOnlyList<CatalogItem> Load()
    {
        using var stream = typeof(ItemCatalog).Assembly.GetManifestResourceStream("TldSave.Core.ItemCatalog.json")
            ?? throw new InvalidOperationException("ItemCatalog.json is not embedded in TldSave.Core");
        using var reader = new StreamReader(stream);

        return ((JArray)TldJson.Parse(reader.ReadToEnd()))
            .OfType<JObject>()
            .Select(e => new CatalogItem(
                (string)e["name"]!,
                Enum.Parse<ItemCategory>((string)e["category"]!),
                (string?)e["source"] switch
                {
                    "survival" => CatalogSource.Survival,
                    "other-modes" => CatalogSource.OtherModes,
                    _ => CatalogSource.List,
                },
                e["template"] as JObject))
            .ToList();
    }
}
