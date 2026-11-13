#if QA
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GmStudio;

public partial class MainWindow
{
    private async Task VerifyPetProfile(string folder)
    {
        var local = game!;
        var pet = PetData.New("turtle4");
        pet.Fields[15] = "3"; pet.Fields[16] = "1"; pet.Fields[27] = "1";
        pet.Fields[17] = "2"; pet.Fields[26] = "1";
        pet.Fields[19] = "tsml~zrsh~smzf~mfby";
        string raw = pet.Encode();
        var profile = new PetProfile(local, pet);
        Require(profile.Hp == 1600 && profile.Mp == 1200 && profile.Attack == 156 && profile.Defense == 42 &&
            profile.HealthText == "1,600 / 1,600" && profile.Metrics.Length == 8 && pet.Encode() == raw,
            "Pet static projection restores permanent passives without changing stored base fields.");
        pet.Equipment.Add("p_cyst|11|0|10|0|0|0|0|0|0|0|20|0");
        profile = new PetProfile(local, pet);
        Require(profile.Attack == 170 && profile.Hp == 1611, "Pet strengthening uses rarity progress and the local coefficient.");
        pet.Fields[19] = "";
        pet.Equipment.Clear();
        foreach (string code in new[] { "p_cyst", "p_cymz", "p_cyyf", "p_cyxz", "p_cygsjl" })
            pet.Equipment.Add(code + "|0|0|0|0|0|0|0|0|0|0|0|0");
        profile = new PetProfile(local, pet);
        Require(profile.Hp == 2500 && profile.MagicDefense == .1m && profile.Worn.All(tile => tile.Index >= 0),
            "A full suit and the matching spirit add their independent bonuses.");
        pet.Fields[0] = "mouse4";
        Require(new PetProfile(local, pet).Hp == 2000, "A foreign spirit does not activate the species bonus.");

        var grow = PetData.New("dragon4");
        grow.Fields[15] = "1"; grow.Fields[28] = "4"; grow.Fields[24] = "2"; grow.Fields[31] = "99";
        Require(grow.Cultivate(local, 15) && grow.Fields[25] == "1" && grow.Fields[28] == "0" &&
            grow.Fields[24] == "1" && grow.Fields[31] == "100", "Filled cultivation progress raises ability and resets progress.");
        Require(grow.Cultivate(local, 15) && grow.Fields[28] == "1" && grow.Fields[31] == "100" &&
            !grow.Cultivate(local, 15), "Cultivation consumes points and clamps potential limit at one hundred.");
        grow.Fields[24] = "10"; grow.Fields[15] = "5"; grow.Fields[25] = "3";
        raw = grow.Encode();
        Require(!grow.Cultivate(local, 15) && grow.Encode() == raw, "A capped ability cannot consume potential.");

        var snapshot = (JsonObject)save!.Draft.DeepClone();
        string diskBefore = System.IO.File.ReadAllText(save.Path);
        var selected = (PetRow)PetTable.SelectedItem;
        string key = player!.Key;
        double width = Width, height = Height;
        try
        {
            PetTabs.SelectedItem = PetCultivationTab;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Require(PetMetricsScroll.ScrollableHeight < 1 && PetCultivationScroll.ScrollableHeight < 1 && !PetCultivationHelp.IsExpanded,
                "The default window fully displays pet metrics and compact cultivation without scrolling.");
            var growthCards = VisualDescendants(PetGrowthRows).OfType<Button>().Select(button => (FrameworkElement)((FrameworkElement)button.Parent).Parent).ToArray();
            Require(growthCards.Length == 3 && growthCards.All(card => card.ActualHeight <= 48),
                "Cultivation cards keep a compact single-line height.");

            var legacyRecords = Inventory.Records(Character!, Inventory.Pets);
            string originalLegacy = legacyRecords[selected.Index];
            var legacy = new PetData(originalLegacy);
            legacy.Fields[31] = "999";
            legacyRecords[selected.Index] = legacy.Encode();
            Character![Inventory.Pets] = string.Join('}', legacyRecords);
            ShowPets();
            PetTabs.SelectedItem = PetCultivationTab;
            Require(PetPotentialText.Text == "100 / 100%" &&
                new PetData(Inventory.Records(Character!, Inventory.Pets)[selected.Index]).Fields[31] == "100" &&
                System.IO.File.ReadAllText(save.Path) == diskBefore,
                "Legacy potential is repaired in the editing copy without silently saving the file.");
            await CaptureWhenReady(folder, "pet-legacy-limit.png");
            legacyRecords[selected.Index] = originalLegacy;
            Character![Inventory.Pets] = string.Join('}', legacyRecords);
            ShowPets();
            Require(!PetPotentialText.Text.Contains("999") &&
                PetEntry.Skill(local, "tsml").Description.Length > 0,
                "Valid potential hides the notice and pet skills carry offline descriptions.");
            PetTabs.SelectedItem = PetEquipmentTab;
            await CaptureWhenReady(folder, "pet-equipment.png");
            var worn = PetGearList.Items.Cast<PetTile>().First(tile => tile.Index >= 0);
            PetGearList.SelectedItem = worn;
            Require(PetReturnButton.IsEnabled && PetTuneButton.IsEnabled && !PetWearButton.IsEnabled,
                "Equipment actions follow the selected worn slot.");
            string originalPet = Inventory.Records(Character!, Inventory.Pets)[selected.Index];
            var originalBag = Inventory.Records(Character!, Inventory.PetGear);
            PetReturnButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var returned = new PetData(Inventory.Records(Character!, Inventory.Pets)[selected.Index]);
            Require(returned.Equipment.Count == 4 && Inventory.Records(Character!, Inventory.PetGear).Count == originalBag.Count + 1,
                "Main pet equipment page returns a worn item into the shared backpack draft.");
            PetBackpackList.SelectedItem = PetBackpackList.Items.Cast<PetTile>().Last(tile => tile.Raw == worn.Raw);
            PetWearButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var restored = new PetData(Inventory.Records(Character!, Inventory.Pets)[selected.Index]);
            Require(restored.Equipment.Order().SequenceEqual(new PetData(originalPet).Equipment.Order()) &&
                Inventory.Records(Character!, Inventory.PetGear).SequenceEqual(originalBag), "Wearing the returned item preserves every equipment field.");
            var bag = Inventory.Records(Character!, Inventory.PetGear);
            bag.Add(worn.Raw);
            Character![Inventory.PetGear] = string.Join('}', bag);
            ShowPets();
            PetBackpackList.SelectedItem = PetBackpackList.Items.Cast<PetTile>().Last();
            PetWearButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(new PetData(Inventory.Records(Character!, Inventory.Pets)[selected.Index]).Equipment.Count == 5 &&
                Inventory.Records(Character!, Inventory.PetGear).Count == bag.Count, "Replacing a filled slot swaps rather than deleting or duplicating gear.");
            VerifyPetEquipmentDeletion(folder);

            var records = Inventory.Records(Character!, Inventory.Pets);
            var editable = new PetData(records[selected.Index]);
            editable.Fields[15] = "3"; editable.Fields[25] = "0"; editable.Fields[28] = "0";
            editable.Fields[24] = "2"; editable.Fields[31] = "99";
            records[selected.Index] = editable.Encode();
            Character![Inventory.Pets] = string.Join('}', records);
            ShowPets();
            PetTabs.SelectedItem = PetCultivationTab;
            await CaptureWhenReady(folder, "pet-cultivation.png");
            var button = VisualDescendants(PetGrowthRows).OfType<Button>().Single(b => b.Tag is 15);
            Require(button.IsEnabled, "Available cultivation points enable the corresponding action.");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            editable = new PetData(Inventory.Records(Character!, Inventory.Pets)[selected.Index]);
            Require(editable.Fields[28] == "1" && editable.Fields[24] == "1" && editable.Fields[31] == "100",
                "Cultivation action updates the selected pet draft and refreshes the panel.");
            Require(System.IO.File.ReadAllText(save.Path) == diskBefore && Dirty,
                "Pet equipment and cultivation only change the in-memory draft.");
            PetTabs.SelectedItem = PetAttributesTab;
            UpdateLayout();
            ProbeSettingsDialog<PetEditorWindow>(EditPetSkillsButton, dialog =>
            {
                Require(dialog.EditorTabs.SelectedItem == dialog.SkillsTab, "The skill shortcut opens the skill tab.");
                ClickDialogButton(dialog, "取消");
            });

            Width = MinWidth; Height = MinHeight;
            foreach (var (tab, name) in new[] { (PetEquipmentTab, "equipment"), (PetCultivationTab, "cultivation") })
            {
                PetTabs.SelectedItem = tab;
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Require(PetPortraitImage.IsVisible && PetActions.IsVisible, "Persistent portrait and actions stay visible across tabs.");
                foreach (var control in name == "equipment" ? new FrameworkElement[] { PetWearButton, PetTuneButton, PetDeleteGearButton } : [PetGrowthRows])
                {
                    Rect bounds = control.TransformToAncestor(this).TransformBounds(new Rect(control.RenderSize));
                    Require(bounds.Right <= ActualWidth && bounds.Bottom <= ActualHeight, "Pet page controls fit the minimum window.");
                }
                Capture(folder, $"pet-{name}-compact.png", dpi: 144);
            }
        }
        finally
        {
            save.Draft = snapshot;
            Width = width; Height = height;
            ShowSlot(key);
            PetTable.SelectedItem = PetTable.Items.Cast<PetRow>().Single(row => row.Index == selected.Index);
            PetTabs.SelectedItem = PetAttributesTab;
            UpdateLayout();
        }
        await CaptureWhenReady(folder, "pets.png");
    }
}
#endif
