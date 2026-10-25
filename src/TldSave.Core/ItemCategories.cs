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
        "Note", "PostCard", "Postcard", "RecipeCard", "BlueprintCard", "Logbook", "LogBook", "Journal", "ClipBoard",
        "Memo", "Papers", "Schematic", "Pamphlet", "SurvivalPath", "ForemanLog", "Diary", "Letter", "Report",
        "Flyer", "Notice", "Manifest", "Bill", "Clipping", "Rumour", "Knowledge", "Collectible", "Poster", "Hymn",
        "Plans", "Thankyou", "Prescription", "Code", "_Dir", "WorkerLog", "SurvivalSchool",
    ];

    private static readonly string[] ClothingNames =
    [
        "Cloak", "Coat", "Parka", "Boots", "Mittens", "Gloves", "Balaclava", "Socks", "Pants", "Shirt", "Sweater",
        "Underwear", "AviatorCap", "ImprovisedHat",
    ];

    private static readonly string[] FoodNames =
    [
        "Flour", "Salt", "Oats", "Potato", "Carrot", "Pumpkin", "Soup", "Tea", "Raw", "Cooked", "Quarter",
        "Water500ml", "Water1000ml",
    ];

    private static readonly string[] FireNames =
    [
        "Matches", "Accelerant", "Coal", "Charcoal", "Tinder", "Newsprint", "Firestriker", "FlintAndSteel",
        "MagnifyingLens", "Softwood", "Hardwood", "ReclaimedWood", "Stick",
    ];

    private static readonly string[] FirstAidNames =
    [
        "Bandage", "Antibiotics", "PainKillers", "HydrogenPeroxide", "WaterPurification", "Dressing",
        "Stim", "Prepared", "TraumaKit", "HeatPad", "Insulin", "Medical",
    ];

    private static readonly string[] MaterialNames =
    [
        "ArrowHead", "ArrowShaft", "Cloth", "Gut", "Hide", "Pelt", "Leather", "Sapling", "ScrapMetal",
        "Feather", "Mushroom", "RoseHip", "Harvested", "Lead", "Birchbark", "Hardcase", "Sulfur", "Gunpowder",
        "Nylon", "SpearHead",
    ];

    // Story / quest objects: keys, IDs, machine parts
    private static readonly string[] KeyNames =
        ["Key", "Watch", "HiddenWallCache", "Crank", "Valve", "Parts", "HatchWheel", "Detonator"];

    // Checked after MaterialNames, so ArrowHead / ArrowShaft stay materials
    private static readonly string[] ToolNames =
    [
        "Arrow", "Ammo", "BoltCutters", "HookAndLine", "SewingKit", "Backpack", "Travois", "Whetstone",
        "Prybar", "Hacksaw", "Hammer", "Crampons", "Rope", "Snowshoes", "Canteen", "Flask", "Bow", "Axe",
        "Tools", "SharpeningStone", "CleaningKit", "CanOpener", "Lure", "TipUp", "FishingLine", "NoiseMaker",
        "Vice", "Canister", "CarBattery", "Shovel", "Flashlight", "StumpRemover", "Rifle", "Revolver", "Knife",
    ];

    private static readonly string[] ToolComponents =
    [
        "m_WeaponItemSerialized", "m_BowItemSerialized", "m_MillableSerialized", "m_SnareItemSerialized",
        "m_FlareItemSerialized", "m_FlashlightItemSerialized", "m_KeroseneLampItemSerialized",
        "m_TorchItemSerialized", "m_CookingPotItemSerialized", "m_SprayPaintCanSerialized",
        "m_RespiratorSerialized", "m_HandheldshortwaveSerialized", "m_BedSerialized",
        "m_ResearchItemSerialized", "m_LiquidItemSerialized", "m_CrankLampItemSerialized",
        "m_InsulatedFlaskSerialized",
    ];

    public static ItemCategory Classify(string prefabName, JObject gear)
    {
        var name = prefabName.StartsWith("GEAR_") ? prefabName["GEAR_".Length..] : prefabName;

        // Names first for paper items: some notes carry generic components (MissionObject, ObjectGuid)
        if (ContainsAny(name, NoteNames)) return ItemCategory.NotesAndCollectibles;

        if (Has(gear, "m_ClothingItemSerialized") || ContainsAny(name, ClothingNames)) return ItemCategory.Clothing;
        if (Has(gear, "m_FoodItemSerialized") || Has(gear, "m_WaterSupplySerialized") || Has(gear, "m_BodyHarvestSerialized"))
            return ItemCategory.FoodAndDrink;
        if (Has(gear, "m_MatchesItemSerialized") || ContainsAny(name, FireNames)) return ItemCategory.FireStarting;
        // Plain books (BookA, BookManual...) are fuel; skill books have research data and fall through to Tools
        if (name.StartsWith("Book") && !Has(gear, "m_ResearchItemSerialized") && gear.Count > 0) return ItemCategory.FireStarting;
        if (ContainsAny(name, FirstAidNames)) return ItemCategory.FirstAid;
        if (ContainsAny(name, FoodNames)) return ItemCategory.FoodAndDrink;
        if (name.EndsWith("Map")) return ItemCategory.NotesAndCollectibles;
        if (ContainsAny(name, MaterialNames)) return ItemCategory.Materials;
        if (ContainsAny(name, KeyNames) || name.EndsWith("ID")) return ItemCategory.KeysAndQuest;
        if (ToolComponents.Any(c => Has(gear, c)) || ContainsAny(name, ToolNames)) return ItemCategory.Tools;
        if (Has(gear, "m_StackableItemSerialized")) return ItemCategory.Materials;

        return ItemCategory.Other;
    }

    private static bool ContainsAny(string name, string[] parts) => parts.Any(p => name.Contains(p, StringComparison.Ordinal));

    private static bool Has(JObject gear, string field) => gear[field] is { Type: not JTokenType.Null } value && value.ToString() != "";
}
