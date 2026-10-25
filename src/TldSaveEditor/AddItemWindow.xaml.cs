using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using TldSave.Core;

namespace TldSaveEditor;

/// <summary>
/// Picks an item from <see cref="ItemCatalog"/>. A full prefab name typed in the search box (GEAR_...)
/// can also be added even if it is not in the catalog.
/// </summary>
public partial class AddItemWindow : Window
{
    private const string AllCategories = "All categories";

    /// <summary>Catalog entry to add, or null for <see cref="CustomPrefabName"/>.</summary>
    public CatalogItem? SelectedItem { get; private set; }
    public string? CustomPrefabName { get; private set; }
    public int Quantity { get; private set; } = 1;

    private readonly ListCollectionView view;

    public AddItemWindow()
    {
        InitializeComponent();

        view = new ListCollectionView(ItemCatalog.Items.ToList());
        ItemsList.ItemsSource = view;
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CatalogItem.CategoryName)));
        view.Filter = o => o is CatalogItem item && Matches(item);

        CategoryFilter.ItemsSource = new[] { AllCategories }
            .Concat(ItemCatalog.Items.Select(i => i.Category).Distinct().Order().Select(ItemCategories.DisplayName))
            .ToList();
        CategoryFilter.SelectedIndex = 0;

        UpdateSelection();
    }

    private bool Matches(CatalogItem item) =>
        item.Name.Contains(SearchBox.Text.Trim(), StringComparison.OrdinalIgnoreCase)
        && (SurvivalOnlyBox.IsChecked != true || item.Source == CatalogSource.Survival)
        && (CategoryFilter.SelectedItem is not string category || category == AllCategories || item.CategoryName == category);

    private void Filters_Changed(object sender, RoutedEventArgs e)
    {
        if (view is null) return; // events fired while InitializeComponent runs
        view.Refresh();
        UpdateSelection();
    }

    private void ItemsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateSelection();

    private void ItemsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsList.SelectedItem is CatalogItem) Add_Click(sender, e);
    }

    private string? TypedPrefabName
    {
        get
        {
            var text = SearchBox.Text.Trim();
            return text.StartsWith("GEAR_") && text.Length > "GEAR_".Length && !text.Contains(' ') ? text : null;
        }
    }

    private void UpdateSelection()
    {
        var item = ItemsList.SelectedItem as CatalogItem;
        var custom = item is null ? TypedPrefabName : null;

        AddButton.IsEnabled = item is not null || custom is not null;
        // Quantity only means something for stackable items; for a custom name we can't know
        QuantityPanel.Visibility = item?.IsStackable == true ? Visibility.Visible : Visibility.Hidden;

        HintText.Text = item switch
        {
            { Template: null } => "No sample of this item was found in a save, so it is added with basic data only.",
            { Source: CatalogSource.OtherModes } => "Only found in story/challenge saves: it may not work in Survival.",
            not null => "",
            null when custom is not null => $"\"{custom}\" is not in the catalog: it will be added as typed, with basic data only.",
            _ => "Select an item, or type a full prefab name (GEAR_...) to add one that is not listed.",
        };
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        SelectedItem = ItemsList.SelectedItem as CatalogItem;
        CustomPrefabName = SelectedItem is null ? TypedPrefabName : null;
        if (SelectedItem is null && CustomPrefabName is null) return;

        if (SelectedItem?.IsStackable == true)
        {
            if (!int.TryParse(QuantityBox.Text, out var quantity) || quantity < 1)
            {
                MessageBox.Show(this, "Quantity must be a whole number of at least 1.", "Add item");
                return;
            }
            Quantity = quantity;
        }

        DialogResult = true;
    }
}
