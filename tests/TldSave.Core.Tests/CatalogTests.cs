using Newtonsoft.Json.Linq;

namespace TldSave.Core.Tests;

public class CatalogTests
{
    [Fact]
    public void Catalog_loads_and_excludes_notes_and_keys()
    {
        Assert.True(ItemCatalog.Items.Count > 400);
        Assert.All(ItemCatalog.Items, i => Assert.StartsWith(PrefabNames.GearPrefix, i.Name));
        Assert.DoesNotContain(ItemCatalog.Items, i => i.Category is ItemCategory.NotesAndCollectibles or ItemCategory.KeysAndQuest);
        Assert.Equal(ItemCatalog.Items.Count, ItemCatalog.Items.Select(i => i.Name).Distinct().Count());
    }

    [Theory]
    [InlineData("GEAR_InsulatedFlask_A")]
    [InlineData("GEAR_Softwood")]
    [InlineData("GEAR_Rifle")]
    [InlineData("GEAR_BallisticVest")]
    public void Common_items_have_a_real_template(string prefab)
    {
        var item = ItemCatalog.Find(prefab);
        Assert.NotNull(item);
        Assert.NotNull(item.Template);
        Assert.Equal(CatalogSource.Survival, item.Source);
    }

    [Theory]
    [InlineData("GEAR_Softwood", "{\"m_StackableItemSerialized\":\"{\\\"m_UnitsProxy\\\":1}\"}", ItemCategory.FireStarting)]
    [InlineData("GEAR_Jeans", "{\"m_ClothingItemSerialized\":\"{}\"}", ItemCategory.Clothing)]
    [InlineData("GEAR_CannedBeans", "{\"m_FoodItemSerialized\":\"{}\"}", ItemCategory.FoodAndDrink)]
    [InlineData("GEAR_HeavyBandage", "{}", ItemCategory.FirstAid)]
    [InlineData("GEAR_Rifle", "{\"m_WeaponItemSerialized\":\"{}\"}", ItemCategory.Tools)]
    [InlineData("GEAR_ArrowHead", "{}", ItemCategory.Materials)]
    [InlineData("GEAR_Arrow", "{}", ItemCategory.Tools)]
    [InlineData("GEAR_VisorNoteAC1", "{}", ItemCategory.NotesAndCollectibles)]
    [InlineData("GEAR_BIKey1", "{}", ItemCategory.KeysAndQuest)]
    public void Items_are_classified_like_the_game_tabs(string prefab, string gear, ItemCategory expected)
    {
        Assert.Equal(expected, ItemCategories.Classify(prefab, JObject.Parse(gear)));
    }

    [Theory]
    [InlineData("GEAR_Softwood", "Softwood")]
    [InlineData("CONTAINER_Box", "CONTAINER_Box")]
    public void Short_names_drop_the_gear_prefix(string prefab, string expected)
    {
        Assert.Equal(expected, PrefabNames.Short(prefab));
    }
}
