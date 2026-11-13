#if QA
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace GmStudio;

public partial class MainWindow
{
    private static readonly Dictionary<string, string> ExpectedFinalSkills = new()
    {
        ["monkey"]="xj~lj~lyq~jgaoyi", ["horse"]="sp~bd~bz~tmaoyi", ["ufo"]="pms~ss~kmsk",
        ["tigress"]="hy~sxhz~hsqj~bhaoyi", ["turtle"]="sld~txlj~sybh~xwaoyi",
        ["phoenix"]="np~bshn~dhly~zqaoyi", ["dragon"]="fs~sdcc~ltwj~qlaoyi",
        ["rabbit"]="yg~jf~bs~ysaoyi", ["nian"]="qxyl~bhjm~jhgy", ["mouse"]="sc~hxfb~zsaoyi",
        ["neat"]="mncz~mljt~cnaoyi", ["roomhorse"]="hybt~hhjt", ["terribletiger"]="hx~mhxs~yhaoyi"
    };

    private void VerifyPetFinalForms(string folder)
    {
        var local=game!;var rows=new List<string>();
        foreach (string code in local.PetNames.Select(pair=>pair.Key).Append("nea1"))
        {
            string family=PetData.Family(code);
            int finalStage=family=="nian" ? 5 : family=="ufo" ? 3 : 4;
            var pet=PetData.New(code);pet.Fields[18]="1";pet.Fields[19]="alien~tsml~qlaoyi~fs~fs";
            pet.Fields[20]="30";pet.Fields[21]="20";pet.Fields[24]="350";
            string oldGear="p_cyst|11|0|10|0|0|0|0|0|0|0|20|0|extra";
            pet.Equipment.Add(oldGear);
            var backpack=new List<string>();
            pet.MaximizeToFinal(local,backpack);
            string expected=ExpectedFinalSkills[family];
            Require(pet.Fields[0]==family+finalStage && pet.Fields[19]==expected,
                $"{code}: final evolution and complete ordered, deduplicated talents/ultimate, clearing every old skill.");
            Require(pet.Fields[5]=="90" && pet.Fields[18]=="1" && pet.Fields[20]=="30" && pet.Fields[21]=="20" &&
                pet.Fields[24]=="350" && pet.Fields[31]=="100" && backpack.SequenceEqual([oldGear]),
                $"{code}: final evolution preserves displaced gear, fight state, experience and excess potential.");
            VerifyMaximumPetEquipment(local,pet);
            var rules=new PetRules(local,pet.Fields[0]);
            Require(Enumerable.Range(9,4).All(i=>PetRules.Value(pet.Fields,i)==rules.QualityMaximum(i)) &&
                PetRules.Abilities.All(a=>PetRules.Value(pet.Fields,a.Base)+PetRules.Value(pet.Fields,a.Bonus)==rules.TotalMaximum(a.Key)) &&
                expected.Split('~').Length<=rules.Capacity(pet.Fields),$"{code}: final growth and skill count fit species limits.");
            string result=pet.Encode();pet.MaximizeToFinal(local,backpack);
            Require(pet.Encode()==result && backpack.SequenceEqual([oldGear]),$"{code}: repeated one-click does not duplicate equipment in the backpack.");
            rows.Add($"{code} -> {pet.Fields[0]}: {expected}");
        }
        var unknown=PetData.New("unknown1");string before=unknown.Encode();
        try { unknown.MaximizeToFinal(local);throw new Exception("Unknown species unexpectedly evolved."); }
        catch(InvalidDataException) { Require(unknown.Encode()==before,"Missing species reference makes no changes."); }
        File.WriteAllLines(Path.Combine(folder,"pet-final-forms.txt"),rows);
    }

    private void VerifyPetEditingScopes(string folder)
    {
        var local = game!;
        var catalog = local.PetNames.SelectMany(pair =>
            new PetRules(local, pair.Key).Skills(PetData.New(pair.Key).Fields, [])).Select(rule => rule.Code).ToHashSet();
        Require(catalog.SetEquals(((JsonObject)local.Official["petSkillNames"]!).Select(pair => pair.Key)),
            "Per-form rules still cover every locally named pet skill.");
        var baby = PetData.New("dragon1");
        Require(new PetRules(local, "dragon1").Skills(baby.Fields, []).All(rule => rule.Code is not ("sdcc" or "qlaoyi" or "bhaoyi")),
            "The skill editor keeps family and form restrictions.");
        Require(new PetRules(local, "dragon4").Skills(baby.Fields, []).Single(rule => rule.Code == "qlaoyi").Locked is not null &&
            new PetRules(local, "mouse4").Skills(baby.Fields, ["p_sscyzsjl"]).Single(rule => rule.Code == "zsaoyi").Locked is null,
            "Potential and matching-spirit requirements remain separate.");
        VerifyLegacyPetLimits(folder);
        VerifyPetFinalForms(folder);
        foreach(var code in local.PetNames.Select(pair=>pair.Key))
        {
            var sample=PetData.New(code);sample.Fields[18]="1";sample.Fields[19]="preserved-skill";
            sample.Fields[24]="350";sample.Fields[20]="30";sample.Fields[21]="20";sample.Equipment.Add("preserved-equipment");
            sample.MaximizeGrowth(local);var bounds=new PetRules(local,code);
            Require(sample.Fields[0]==code && sample.Fields[5]=="90" && sample.Fields[18]=="1" && sample.Fields[19]=="preserved-skill" &&
                sample.Fields[24]=="350" && sample.Fields[20]=="30" && sample.Fields[21]=="20" && sample.Fields[31]=="100" &&
                sample.Equipment.SequenceEqual(["preserved-equipment"]) && Enumerable.Range(9,4).All(i=>PetRules.Value(sample.Fields,i)==bounds.QualityMaximum(i)),
                $"Growth maximization respects {code}'s aptitude caps and preserves owned skills, equipment, experience and excess points.");
        }


        var current = Character!;
        var snapshot = (JsonObject)save!.Draft.DeepClone();
        string key = player!.Key;
        int selected = ((PetRow)PetTable.SelectedItem).Index;
        var growthButton=EditPetValuesButton;
        PetData Read() => new(Inventory.Records(Character!, Inventory.Pets)[selected]);
        TextBox Field(PetEditorWindow dialog, string name)
        {
            if (name.Contains("经验") || name.Contains("进度")) dialog.AdvancedGrowth.IsExpanded = true;
            dialog.UpdateLayout();
            return VisualDescendants(dialog.GrowthFields).OfType<TextBox>()
                .Single(box => AutomationProperties.GetName(box) == name);
        }
        void RequireScope(PetEditorWindow dialog, PetEditSection section)
        {
            Require(dialog.Section == section && dialog.EditorTabs.Items.Count == 1 &&
                dialog.EditorTabs.SelectedItem == (section == PetEditSection.Growth ? dialog.GrowthTab : dialog.SkillsTab),
                "Each pet entry exposes exactly one editing scope.");
            Require(section == PetEditSection.Growth || dialog.GrowthFields.Children.Count == 0,
                "Skills and gear have no hidden numeric editors.");
        }
        void Open(Button button, TabItem tab, Action<PetEditorWindow> interact)
        {
            PetTabs.SelectedItem = tab;
            UpdateLayout();
            ProbeSettingsDialog(button, interact);
        }
        static bool BlocksTyping(TextBox box, string text)
        {
            box.SelectAll();
            var input = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, box, text))
                { RoutedEvent = TextCompositionManager.PreviewTextInputEvent };
            box.RaiseEvent(input);
            return input.Handled;
        }
        static bool BlocksPaste(TextBox box, string text)
        {
            box.SelectAll();
            var input = new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, text), false, DataFormats.UnicodeText);
            box.RaiseEvent(input);
            return input.CommandCancelled;
        }
        try
        {
            var original = Read();
            var rules = new PetRules(local, original.Fields[0]);
            PetTabs.SelectedItem=PetCultivationTab;UpdateLayout();
            Require(EditPetValuesButton.IsVisible && EditPetValuesButton.IsEnabled && Equals(EditPetValuesButton.Content,"编辑具体数值"),
                "Manual pet editing has a visible, enabled button instead of unclickable instruction text.");
            PetTable.ScrollIntoView(PetTable.SelectedItem);UpdateLayout();
            var selectedContainer=(ListBoxItem)PetTable.ItemContainerGenerator.ContainerFromItem(PetTable.SelectedItem);
            var rowText=VisualDescendants(selectedContainer).OfType<TextBlock>().First();
            var doubleClickButton=new Button();
            doubleClickButton.Click+=(_,_)=>PetTable.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left)
                {RoutedEvent=Control.MouseDoubleClickEvent,Source=rowText});
            Open(doubleClickButton,PetCultivationTab,dialog=>
            {
                RequireScope(dialog,PetEditSection.Growth);
                Require(dialog.PetName.Text==GameJson.Text(local.PetNames[original.Fields[0]]),
                    "The list double-click route opens the intended pet's growth editor.");
                ClickDialogButton(dialog,"取消");
            });
            Open(growthButton, PetCultivationTab, dialog =>
            {
                RequireScope(dialog, PetEditSection.Growth);
                dialog.UpdateLayout();
                Capture(folder, "pet-editor-default.png", dialog);
                Require(!dialog.AdvancedGrowth.IsExpanded && dialog.GrowthScroll.ScrollableHeight < 1,
                    $"Default cultivation fit: window={dialog.ActualHeight}, viewport={dialog.GrowthScroll.ViewportHeight}, extent={dialog.GrowthScroll.ExtentHeight}, overflow={dialog.GrowthScroll.ScrollableHeight}; advanced starts collapsed.");
                var quality = Field(dialog, "生命资质");
                string tooHigh = (rules.QualityMaximum(9)+1).ToString(CultureInfo.InvariantCulture);
                Require(BlocksTyping(quality, tooHigh) && BlocksPaste(quality, tooHigh), "Quality limits reject typing and pasting.");
                var potential = Field(dialog, "潜力极限");
                Require(BlocksTyping(potential, "101") && BlocksPaste(potential, "999") && !BlocksPaste(potential, "100"),
                    "Potential input accepts one hundred and rejects values above it.");
                potential.Text = "999";
                ClickDialogButton(dialog, "应用到存档");
                Require(dialog.IsVisible && dialog.EditorError.Text.Contains("100"), "Apply rejects a bypassed potential limit.");
                potential.Text = "100";
                var hpExp = Field(dialog, "生命经验");
                var mpExp = Field(dialog, "魔法经验");
                hpExp.Text = "200";
                Require(BlocksPaste(mpExp, "51"), "Qualification experience retains its shared budget.");
                mpExp.Text = "51";
                ClickDialogButton(dialog, "应用到存档");
                Require(dialog.IsVisible && dialog.EditorError.IsVisible, "Apply checks the combined qualification experience budget.");
                hpExp.Text = original.Fields[20]; mpExp.Text = original.Fields[21];
                var progress = Field(dialog, "悟性进度");
                Require(progress.IsReadOnly && progress.ToolTip is null, "Capped progress is clearly read-only without a repetitive tooltip.");
                var perception = Field(dialog, "悟性");
                string beforePerception = perception.Text;
                perception.Text = "4";
                Require(!progress.IsReadOnly, "Progress becomes editable when the ability is below its cap.");
                perception.Text = beforePerception;
                dialog.AdvancedGrowth.IsExpanded = false;
                dialog.UpdateLayout();
                Capture(folder, "pet-editor-growth.png", dialog);
                dialog.AdvancedGrowth.IsExpanded = true;
                Capture(folder, "pet-editor-advanced.png", dialog);
                dialog.AdvancedGrowth.IsExpanded = false;
                dialog.Width = dialog.MinWidth; dialog.Height = dialog.MinHeight;
                Capture(folder, "pet-editor-compact.png", dialog, 144);
                ClickDialogButton(dialog, "取消");
            });
            Open(EditPetSkillsButton, PetAttributesTab, dialog =>
            {
                RequireScope(dialog, PetEditSection.Skills);
                dialog.UpdateLayout();
                VerifyRenderedSkillHint(dialog.LearnedSkills,((PetEntry)dialog.LearnedSkills.Items[0]).Code,folder,"pet-learned-tooltip.png");
                VerifyRenderedSkillHint(dialog.AvailableSkills,((PetSkillCandidate)dialog.AvailableSkills.Items[0]).Code,folder,"pet-candidate-tooltip.png");
                dialog.LearnedSkills.SelectedIndex = 0;
                dialog.RemoveSkillButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Capture(folder, "pet-editor-skills.png", dialog);
                ClickDialogButton(dialog, "取消");
            });
            Require(JsonNode.DeepEquals(save.Draft, snapshot), "Cancel in each scoped editor preserves the complete save.");

            var records = Inventory.Records(current, Inventory.Pets);
            var sample = new PetData(records[selected]);
            sample.Fields[6] = "2000000";
            records[selected] = sample.Encode();
            current[Inventory.Pets] = string.Join('}', records);
            ShowPets();
            Open(growthButton, PetCultivationTab, dialog =>
            {
                Field(dialog, "等级").Text = "89";
                ClickDialogButton(dialog, "应用到存档");
            });
            var grown = Read();
            Require(grown.Fields[5] == "89" && PetRules.Value(grown.Fields, 6) == rules.Range(6, grown.Fields, 10).Max &&
                grown.Fields[19] == original.Fields[19] && grown.Equipment.SequenceEqual(original.Equipment),
                "Growth changes level and dependent experience without editing skills or equipment.");
            string added = "";
            Open(EditPetSkillsButton, PetAttributesTab, dialog =>
            {
                var candidate = dialog.AvailableSkills.Items.Cast<PetSkillCandidate>().First(item => item.Rule.Locked is null);
                dialog.LearnedSkills.SelectedIndex = 0;
                dialog.RemoveSkillButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                dialog.SkillSearch.Text = candidate.Code;
                added = candidate.Code;
                dialog.AddSkillButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ClickDialogButton(dialog, "应用到存档");
            });
            var skilled = Read();
            Require(skilled.Fields.Where((_, index) => index != 19).SequenceEqual(grown.Fields.Where((_, index) => index != 19)) &&
                skilled.Fields[19].Split('~').Contains(added) && skilled.Equipment.SequenceEqual(grown.Equipment),
                "Skill apply preserves every growth and equipment field.");
            // Exercise the actual button starting from a baby with stale and duplicate skills.
            records = Inventory.Records(Character!, Inventory.Pets);
            skilled.Fields[0] = "dragon1";
            skilled.Fields[19] = "alien~tsml~qlaoyi~fs~fs";
            records[selected] = skilled.Encode();
            Character![Inventory.Pets] = string.Join('}', records);
            ShowPets();
            PetTable.SelectedItem = PetTable.Items.Cast<PetRow>().Single(row => row.Index == selected);
            var backpack = Inventory.Records(current,Inventory.PetGear);
            var expectedPet=new PetData(skilled.Encode());
            expectedPet.MaximizeToFinal(local,backpack);
            var beforeMaximum=save.Draft.DeepClone();
            PetTabs.SelectedItem=PetCultivationTab;UpdateLayout();
            ProbeSettingsDialog<PromptDialog>(MaximizePetButton,dialog=>
            {
                Capture(folder,"pet-max-confirm.png",dialog);ClickDialogButton(dialog,"取消");
            });
            Require(JsonNode.DeepEquals(save.Draft,beforeMaximum),"Canceling one-click pet growth preserves the complete save.");
            ProbeSettingsDialog<PromptDialog>(MaximizePetButton,dialog=>ClickDialogButton(dialog,"设为极品"));
            var maximum=Read();
            Require(maximum.Fields[5]=="90" && maximum.Fields[31]=="100" &&
                Enumerable.Range(9,4).All(i=>PetRules.Value(maximum.Fields,i)==rules.QualityMaximum(i)) &&
                PetRules.Abilities.All(a=>PetRules.Value(maximum.Fields,a.Item1)+PetRules.Value(maximum.Fields,a.Item2)==rules.TotalMaximum(a.Item4)),
                "One-click pet growth uses the current species' level, aptitude and total ability caps.");
            Require(maximum.Fields[0]=="dragon4" && maximum.Fields[18]==skilled.Fields[18] && maximum.Fields[19]=="fs~sdcc~ltwj~qlaoyi" &&
                maximum.Equipment.SequenceEqual(expectedPet.Equipment) && Inventory.Records(Character!,Inventory.PetGear).SequenceEqual(backpack),
                "The real one-click action evolves a baby, replaces skills, equips maximum matching gear and returns displaced equipment to the backpack.");
            VerifyMaximumPetEquipment(local,maximum);
            Require(PetTitle.Text == GameJson.Text(local.PetNames["dragon4"]), "The pet profile immediately displays its evolved form.");
            Require(FindName("PetGearButton") is null && typeof(PetData).Assembly.GetType("GmStudio.PetSuitWindow") is null,
                "The equipment-generating suit preset and its window are removed.");
            Capture(folder,"pet-cultivation-max.png");
            PetTabs.SelectedItem=PetEquipmentTab;UpdateLayout();
            Capture(folder,"pet-maximum-equipment.png");
            PetTabs.SelectedItem=PetAttributesTab;UpdateLayout();
            Capture(folder,"pet-final-skills.png");
            VerifyRenderedSkillHint(PetSkillList,"fs",folder,"pet-profile-tooltip.png");

            Require(PetTools.Children.Count==1 && PetTools.Children[0]==AddPetButton &&
                typeof(MainWindow).GetMethod("PetMore",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance) is null,
                "The pet More menu and its retired actions are removed.");

        }
        finally
        {
            save.Draft = snapshot;
            ShowSlot(key);
            PetTable.SelectedItem = PetTable.Items.Cast<PetRow>().Single(row => row.Index == selected);
            PetTabs.SelectedItem = PetAttributesTab;
        }
    }

    private void VerifyLegacyPetLimits(string folder)
    {
        string directory = Path.Combine(folder, "legacy-potential", "saves");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "slot0.json");
        var data = (JsonObject)save!.Draft.DeepClone();
        var pet = PetData.New("dragon4");
        pet.Fields[31] = "999";
        pet.Equipment.Add("p_cyst|11|0|10|0|0|0|0|0|0|0|20|0");
        ((JsonObject)data[player!.Key]!)[Inventory.Pets] = pet.Encode();
        pet.Fields[31] = "-4";
        data["petWareHouseSaveString"] = pet.Encode();
        string before = data.ToJsonString(GameJson.Format);
        File.WriteAllText(path, before);
        var editable = new EditableFile(path, repairPets: true);
        Require(editable.Dirty && editable.PetLimitRepairs == 2 && File.ReadAllText(path) == before,
            "Reading legacy pets fixes only the editing copy, including stored pets.");
        var expected = (JsonObject)data.DeepClone();
        pet.Fields[31] = "100";
        ((JsonObject)expected[player.Key]!)[Inventory.Pets] = pet.Encode();
        pet.Fields[31] = "0";
        expected["petWareHouseSaveString"] = pet.Encode();
        Require(JsonNode.DeepEquals(editable.Draft, expected), "Potential repair preserves all other pet and save data.");
        string? backup = editable.Save();
        Require(backup is not null && File.ReadAllText(backup) == before &&
            JsonNode.DeepEquals(GameJson.Read(path), expected) && !editable.Dirty && editable.PetLimitRepairs == 0,
            "Saving the repair writes a complete backup and clears the pending state.");
        File.WriteAllText(path, before);
        var raw = new EditableFile(path);
        raw.Save();
        Require(JsonNode.DeepEquals(GameJson.Read(path), expected), "Direct save cannot reintroduce out-of-range potential.");
        var template = ((JsonArray)game!.Extreme["pets"]!).OfType<JsonObject>().First();
        string old = GameJson.Text(template["raw"]);
        try
        {
            var oldPet = new PetData(old);
            oldPet.Fields[31] = "999";
            template["raw"] = oldPet.Encode();
            var reference = PetData.New(GameJson.Text(template["petName"]));
            reference.UseReference(game);
            Require(reference.Fields[31] == "100", "An old external reference template is capped before use.");
        }
        finally { template["raw"] = old; }
    }
}
#endif

