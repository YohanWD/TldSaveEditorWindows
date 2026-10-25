using Newtonsoft.Json.Linq;

namespace TldSave.Core;

/// <summary>Display order matches the enum order.</summary>
public enum ItemCategory
{
    FireStarting,
    FirstAid,
    Clothing,
    FoodAndDrink,
    Tools,
    Materials,
    NotesAndCollectibles,
    KeysAndQuest,
    Other,
}

/// <summary>
/// The save does not store an item's category (the game takes it from the prefab), so it is guessed from
/// the item's serialized components first, then from its prefab name. Approximates the in-game inventory tabs.
/// </summary>
public static class ItemCategories
{
    public static string DisplayName(ItemCategory category) => category switch
    {
        ItemCategory.FireStarting => "Fire starting",
        ItemCategory.FirstAid => "First aid",
        ItemCategory.Clothing => "Clothing",
        ItemCategory.FoodAndDrink => "Food & drink",
        ItemCategory.Tools => "Tools",
        ItemCategory.Materials => "Materials",
        ItemCategory.NotesAndCollectibles => "Notes & collectibles",
        ItemCategory.KeysAndQuest => "Keys & quest items",
        _ => "Other",
    };

    private static readonly string[] NoteNames =
    [
        "Note", "PostCard", "Postcard", "RecipeCard", "BlueprintCard", "Logbook", "Journal", "ClipBoard",
        "Memo", "Papers", "Schematic", "Pamphlet", "SurvivalPath", "ForemanLog",
    ];

    private static readonly string[] FireNames =
    [
        "Matches", "Accelerant", "Coal", "Charcoal", "Tinder", "Newsprint", "Firestriker", "FlintAndSteel",
        "MagnifyingLens", "Softwood", "Hardwood", "ReclaimedWood", "Stick",
    ];

    private static readonly string[] FirstAidNames =
    [
        "Bandage", "Antibiotics", "PainKillers", "HydrogenPeroxide", "WaterPurification", "Dressing",
        "EmergencyStim", "Prepared",
    ];

    private static readonly string[] MaterialNames =
    [
        "ArrowHead", "ArrowShaft", "Cloth", "Gut", "Hide", "Pelt", "Leather", "Sapling", "ScrapMetal",
        "Feather", "Mushroom", "RoseHip", "Harvested", "Lead", "Birchbark", "Hardcase",
    ];

    private static readonly string[] KeyNames = ["Key", "Watch", "HiddenWallCache"];

    // Checked after MaterialNames, so ArrowHead / ArrowShaft stay materials
    private static readonly string[] ToolNames =
    [
        "Arrow", "Ammo", "BoltCutters", "HookAndLine", "SewingKit", "Backpack", "Travois", "Whetstone",
        "Prybar", "Hacksaw", "Hammer", "Crampons", "Rope", "Snowshoes", "Canteen", "Flask",
    ];

    private static readonly string[] ToolComponents =
    [
        "m_WeaponItemSerialized", "m_BowItemSerialized", "m_MillableSerialized", "m_SnareItemSerialized",
        "m_FlareItemSerialized", "m_FlashlightItemSerialized", "m_KeroseneLampItemSerialized",
        "m_TorchItemSerialized", "m_CookingPotItemSerialized", "m_SprayPaintCanSerialized",
        "m_RespiratorSerialized", "m_HandheldshortwaveSerialized", "m_BedSerialized",
        "m_ResearchItemSerialized", "m_LiquidItemSerialized",
    ];

    public static ItemCategory Classify(string prefabName, JObject gear)
    {
        var name = prefabName.StartsWith("GEAR_") ? prefabName["GEAR_".Length..] : prefabName;

        // Names first for paper items: some notes carry generic components (MissionObject, ObjectGuid)
        if (ContainsAny(name, NoteNames)) return ItemCategory.NotesAndCollectibles;

        if (Has(gear, "m_ClothingItemSerialized")) return ItemCategory.Clothing;
        if (Has(gear, "m_FoodItemSerialized") || Has(gear, "m_WaterSupplySerialized") || Has(gear, "m_BodyHarvestSerialized"))
            return ItemCategory.FoodAndDrink;
        if (Has(gear, "m_MatchesItemSerialized") || ContainsAny(name, FireNames)) return ItemCategory.FireStarting;
        if (ContainsAny(name, FirstAidNames)) return ItemCategory.FirstAid;
        if (ContainsAny(name, MaterialNames)) return ItemCategory.Materials;
        if (ContainsAny(name, KeyNames) || name.EndsWith("ID")) return ItemCategory.KeysAndQuest;
        if (ToolComponents.Any(c => Has(gear, c)) || ContainsAny(name, ToolNames)) return ItemCategory.Tools;
        if (Has(gear, "m_StackableItemSerialized")) return ItemCategory.Materials;

        return ItemCategory.Other;
    }

    private static bool ContainsAny(string name, string[] parts) => parts.Any(p => name.Contains(p, StringComparison.Ordinal));

    private static bool Has(JObject gear, string field) => gear[field] is { Type: not JTokenType.Null } value && value.ToString() != "";
}
