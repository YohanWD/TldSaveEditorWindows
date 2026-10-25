using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TldSave.Core;

namespace TldSaveEditor;

public partial class MainWindow : Window
{
    private SaveFile? save;
    private Inventory? inventory;
    private PlayerState? player;
    private readonly ObservableCollection<InventoryItem> items = new();
    private InventoryItem? selectedItem;

    private bool isDirty;
    private bool ignoreSaveSelection;
    private SaveSlotInfo? currentSlot;

    public MainWindow()
    {
        InitializeComponent();
        ItemsList.ItemsSource = items;

        var view = CollectionViewSource.GetDefaultView(items);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(InventoryItem.CategoryName)));
        view.SortDescriptions.Add(new SortDescription(nameof(InventoryItem.Category), ListSortDirection.Ascending));
        view.SortDescriptions.Add(new SortDescription(nameof(InventoryItem.ShortName), ListSortDirection.Ascending));
        view.Filter = o => o is InventoryItem item && MatchesFilters(item);

        CategoryFilter.ItemsSource = new[] { AllCategories }
            .Concat(Enum.GetValues<ItemCategory>().Select(ItemCategories.DisplayName))
            .ToList();
        CategoryFilter.SelectedIndex = 0;

        RefreshSaveList();
    }

    private const string AllCategories = "All categories";

    private bool MatchesFilters(InventoryItem item) =>
        item.PrefabName.Contains(FilterBox.Text, StringComparison.OrdinalIgnoreCase)
        && (CategoryFilter.SelectedItem is not string category || category == AllCategories || item.CategoryName == category);

    // ------------------------------------------------------------ save list / loading

    private void RefreshSaveList()
    {
        try
        {
            ignoreSaveSelection = true;
            SavesList.ItemsSource = SaveLocator.FindSurvivalSaves();
            SavesList.SelectedItem = (SavesList.ItemsSource as List<SaveSlotInfo>)?
                .FirstOrDefault(s => s.FilePath == save?.FilePath);
            SetStatus($"Survival saves in {SaveLocator.SurvivalFolder}");
        }
        catch (Exception e)
        {
            ShowError("Could not list saves", e);
        }
        finally
        {
            ignoreSaveSelection = false;
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (ConfirmDiscardChanges()) RefreshSaveList();
    }

    private async void SavesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ignoreSaveSelection || SavesList.SelectedItem is not SaveSlotInfo slot || slot == currentSlot) return;

        if (!ConfirmDiscardChanges())
        {
            ignoreSaveSelection = true;
            SavesList.SelectedItem = currentSlot;
            ignoreSaveSelection = false;
            return;
        }
        currentSlot = slot;
        await LoadSaveAsync(slot.FilePath);
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges()) return;
        var dialog = new OpenFileDialog { Title = "Open a survival save (e.g. a copy of sandbox1)", InitialDirectory = SaveLocator.SurvivalFolder };
        if (dialog.ShowDialog(this) != true) return;
        currentSlot = null;
        SavesList.SelectedItem = null;
        await LoadSaveAsync(dialog.FileName);
    }

    private async Task LoadSaveAsync(string path)
    {
        SetStatus($"Loading {path}…");
        Tabs.IsEnabled = false;
        try
        {
            var loaded = await Task.Run(() => SaveFile.Load(path));
            if (!loaded.IsSurvival)
            {
                MessageBox.Show(this, $"This is a {loaded.GameMode} save. This editor only edits Survival (SANDBOX) saves.",
                    "Not a survival save", MessageBoxButton.OK, MessageBoxImage.Warning);
                ClearSave();
                return;
            }

            save = loaded;
            inventory = Inventory.Load(save);
            ShowOverview();
            ShowPlayer();
            ShowInventory();
            SectionCombo.ItemsSource = save.Sections.Keys.ToList();
            SectionCombo.SelectedIndex = SectionCombo.Items.IndexOf("global");
            SetDirty(false);
            Tabs.IsEnabled = true;
            SetStatus($"Loaded {path}");
        }
        catch (Exception e)
        {
            ClearSave();
            ShowError($"Could not load {path}", e);
        }
    }

    private void ClearSave()
    {
        save = null;
        inventory = null;
        ShowPlayer();
        items.Clear();
        SectionCombo.ItemsSource = null;
        RawJsonBox.Text = "";
        SetDirty(false);
        SetStatus("Ready");
    }

    // ------------------------------------------------------------ overview

    private void ShowOverview()
    {
        if (save is null) return;
        var info = save.Info;
        var condition = (double?)save.Global.ReadNested("m_Condition_Serialized")?["m_CurrentHPProxy"] ?? (double?)info?["m_Condition"];

        NameText.Text = save.DisplayName;
        FileText.Text = save.FilePath;
        XpModeText.Text = (string?)info?["m_XPMode"] ?? "?";
        RegionText.Text = $"{(string?)info?["m_Region"] ?? "?"} / {(string?)save.Boot?["m_SceneName"] ?? "?"}";
        DaysText.Text = $"{save.HoursPlayed / 24:0.0}  ({save.HoursPlayed:0.#} hours)";
        ConditionText.Text = condition is { } c ? $"{c:0.#}%" : "?";
        TimestampText.Text = save.Timestamp;
        ScenesText.Text = (save.Sections.Count - save.Sections.Keys.Count(IsMetaSection)).ToString();
    }

    private static bool IsMetaSection(string key) => key is "boot" or "global" or "info" or "screenshot" or "sandbox_global";

    // ------------------------------------------------------------ inventory

    private void ShowInventory()
    {
        foreach (var item in items) item.PropertyChanged -= Item_PropertyChanged;
        items.Clear();
        foreach (var item in inventory!.Items)
        {
            item.PropertyChanged += Item_PropertyChanged;
            items.Add(item);
        }
        SelectItem(null);
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshItemsView();

    private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshItemsView();

    private void RefreshItemsView() => CollectionViewSource.GetDefaultView(items).Refresh();

    private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        SelectItem(ItemsList.SelectedItem as InventoryItem);

    private void SelectItem(InventoryItem? item)
    {
        selectedItem = item;
        ItemPanel.DataContext = item;
        ItemPanel.IsEnabled = item is not null;
        GearJsonBox.Text = item is null ? "" : TldJson.Serialize(item.Gear, indented: true);
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        SetDirty(true);
        if (sender == selectedItem && !GearJsonBox.IsKeyboardFocusWithin)
            GearJsonBox.Text = TldJson.Serialize(selectedItem!.Gear, indented: true);
    }

    private void IncreaseUnits_Click(object sender, RoutedEventArgs e)
    {
        if (selectedItem is { IsStackable: true }) selectedItem.Units++;
    }

    private void DecreaseUnits_Click(object sender, RoutedEventArgs e)
    {
        if (selectedItem is { IsStackable: true }) selectedItem.Units--;
    }

    private void UnitsBox_KeyDown(object sender, KeyEventArgs e)
    {
        // Apply the typed quantity on Enter instead of waiting for the box to lose focus
        if (e.Key == Key.Enter && sender is TextBox box)
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    private void ApplyJson_Click(object sender, RoutedEventArgs e)
    {
        if (selectedItem is null) return;
        try
        {
            if (TldJson.Parse(GearJsonBox.Text) is not JObject gear)
                throw new JsonException("Gear data must be a JSON object { … }");
            var item = selectedItem;
            item.ReplaceGear(gear);
            // New gear data can change the category, so re-group and keep the item selected
            RefreshItemsView();
            ItemsList.SelectedItem = item;
            SetStatus($"Updated {item.PrefabName}");
        }
        catch (JsonException ex)
        {
            MessageBox.Show(this, ex.Message, "Invalid JSON", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        if (inventory is null) return;
        var dialog = new AddItemWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;

        var prefab = dialog.SelectedItem?.Name ?? dialog.CustomPrefabName!;
        var item = inventory.Add(prefab, dialog.SelectedItem?.Template);
        if (item.IsStackable) item.Units = dialog.Quantity;
        item.PropertyChanged += Item_PropertyChanged;
        items.Add(item);
        ItemsList.SelectedItem = item;
        ItemsList.ScrollIntoView(item);
        SetDirty(true);
        SetStatus(item.IsStackable ? $"Added {prefab} ×{item.Units}" : $"Added {prefab}");
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (inventory is null || selectedItem is null) return;
        var item = selectedItem;
        if (MessageBox.Show(this, $"Remove {item.PrefabName} from the inventory?", "Remove item",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        item.PropertyChanged -= Item_PropertyChanged;
        inventory.Remove(item);
        items.Remove(item);
        SetDirty(true);
    }

    // ------------------------------------------------------------ player

    private void ShowPlayer()
    {
        if (player is not null) player.PropertyChanged -= Player_PropertyChanged;
        player = save is null ? null : new PlayerState(save);
        if (player is not null) player.PropertyChanged += Player_PropertyChanged;
        PlayerPanel.DataContext = player;
    }

    private void Player_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        SetDirty(true);
        ShowOverview(); // condition is shown there too
    }

    private void RestoreAll_Click(object sender, RoutedEventArgs e) => player?.RestoreAll();

    private void MaxAllSkills_Click(object sender, RoutedEventArgs e) => player?.MaxAllSkills();

    private void CureAll_Click(object sender, RoutedEventArgs e) => player?.CureAll();

    private void MaxSkill_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Skill skill) skill.Points = skill.MaxPoints;
    }

    private void Cure_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Affliction affliction) affliction.Cure();
    }

    // ------------------------------------------------------------ raw JSON

    private async void SectionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (save is null || SectionCombo.SelectedItem is not string key) return;
        RawJsonBox.Text = "Loading…";
        var section = save.Sections[key];
        var text = await Task.Run(() => TldJson.Serialize(TldJson.Expand(section), indented: true));
        if (SectionCombo.SelectedItem as string == key) RawJsonBox.Text = text;
    }

    // ------------------------------------------------------------ saving

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (save is null || inventory is null) return;
        try
        {
            SaveButton.IsEnabled = false;
            SetStatus("Saving…");
            inventory.Commit();
            var target = save;
            var backup = await Task.Run(() =>
            {
                var backupPath = SaveLocator.Backup(target.FilePath);
                target.Save(target.FilePath);
                return backupPath;
            });
            SetDirty(false);
            SetStatus($"Saved. Backup: {backup}");
        }
        catch (Exception ex)
        {
            SetDirty(true);
            ShowError("Save failed — the original file was backed up first if the error happened while writing", ex);
        }
    }

    // ------------------------------------------------------------ helpers

    private void SetDirty(bool dirty)
    {
        isDirty = dirty;
        SaveButton.IsEnabled = dirty && save is not null;
        Title = "TLD Survival Save Editor" + (save is null ? "" : $" — {save.DisplayName}") + (dirty ? " *" : "");
    }

    private bool ConfirmDiscardChanges() =>
        !isDirty || MessageBox.Show(this, "You have unsaved changes. Discard them?", "Unsaved changes",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!ConfirmDiscardChanges()) e.Cancel = true;
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private void ShowError(string message, Exception e) =>
        MessageBox.Show(this, $"{message}\n\n{e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
}
