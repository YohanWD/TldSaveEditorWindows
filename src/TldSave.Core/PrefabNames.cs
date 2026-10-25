namespace TldSave.Core;

public static class PrefabNames
{
    /// <summary>Every inventory item prefab starts with this, e.g. GEAR_Softwood.</summary>
    public const string GearPrefix = "GEAR_";

    /// <summary>Prefab name without the GEAR_ prefix, for lists: GEAR_Softwood -> Softwood.</summary>
    public static string Short(string prefabName) =>
        prefabName.StartsWith(GearPrefix) ? prefabName[GearPrefix.Length..] : prefabName;
}
