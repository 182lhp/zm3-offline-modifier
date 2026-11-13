#if QA
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Media;
using System.Security.Cryptography;

namespace GmStudio;

public partial class MainWindow
{
    private async Task VerifyPetCreation(string folder)
    {
        var local = game!;
        string ArtHash(BitmapSource image)
        {
            var bitmap = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            byte[] bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(bytes, bitmap.PixelWidth * 4, 0);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        var families = PetGuideCatalog.Families(local);
        Require(families.All(family => family.Forms.Select(form => ArtHash(form.Entry.Icon!)).Distinct().Count() == family.Forms.Length),
            "Distinct named forms do not reuse the same portrait pixels.");
        Require(ReferenceEquals(local.Portrait("pets", "nian1"), local.Portrait("pets", "nian3")) &&
            new[] { "nian1", "nian4", "nian5" }.Select(code => ArtHash(local.Portrait("pets", code)!)).Distinct().Count() == 3,
            "Nian stages 1-3 share their verified baby sprite, and adult and great nian use different native artwork.");

        foreach (string code in local.PetNames.Select(pair => pair.Key))
        {
            foreach (bool extreme in new[] { false, true })
            {
                var generated = PetData.Create(local, code, extreme);
                string expectedCode=extreme ? PetData.FinalForm(local,code) : families.Single(f=>f.Code==PetData.Family(code)).Forms[0].Code;
                var rules = new PetRules(local, expectedCode);
                Require(generated.Fields[0] == expectedCode && generated.Fields[5] == (extreme ? "90" : "1") &&
                    generated.Fields[6] == "0" && generated.Fields[18] == "0", "Creation always targets the preset's first/final form, regardless of preview form.");
                if(extreme) Require(generated.Fields[19]==ExpectedFinalSkills[PetData.Family(code)],"Extreme creation learns ordered family talents and ultimate without stale skills.");
                else Require(generated.Fields[19]=="",$"{code}: a level-one full-growth baby has no learned skills, including passives, talents and ultimates.");
                Require(Enumerable.Range(9, 4).All(i => PetRules.Value(generated.Fields, i) == rules.QualityMaximum(i)),
                    "New pet qualifications use species-specific maxima.");
                Require(PetRules.Abilities.All(a => PetRules.Value(generated.Fields, a.Base) == rules.BaseMaximum(a.Key, a.MaxBase) &&
                    PetRules.Value(generated.Fields, a.Base) + PetRules.Value(generated.Fields, a.Bonus) == rules.TotalMaximum(a.Key) &&
                    generated.Fields[a.Progress] == "0"), "Base abilities and growth bonuses fill the valid species limits.");
                var learned = generated.Fields[19].Split('~', StringSplitOptions.RemoveEmptyEntries);
                var allowed = rules.Skills(generated.Fields, generated.Equipment).Where(skill => skill.Locked is null).Select(skill => skill.Code);
                Require(learned.Length <= rules.Capacity(generated.Fields) && learned.Except(allowed).Any() == false &&
                    generated.Equipment.Count == (extreme ? 5 : 0), "Each creation preset has compatible skills and the promised equipment.");
            }
        }
        Require(PetData.Create(local, "mouse4", true).Fields[19].Split('~').Contains("zsaoyi"),
            "The extreme preset equips the correct spirit before selecting skills.");
        Require(PetTabs.Items[0] == PetAttributesTab && PetTabs.Items[1] == PetCultivationTab && PetTabs.Items[2] == PetEquipmentTab,
            "Pet tabs are ordered attributes, cultivation, equipment.");
        Require(PetActions.Children.Count == 2 && PetActions.Children[0] == FightButton && PetActions.Children[1] == RestButton,
            "The pet footer contains only fight and rest actions.");
        var snapshot = (JsonObject)save!.Draft.DeepClone();
        string disk = System.IO.File.ReadAllText(save.Path);
        int selectedIndex = ((PetRow)PetTable.SelectedItem).Index;
        string key = player!.Key;
        double height = Height;
        try
        {
            ProbeSettingsDialog<PetCreateWindow>(AddPetButton, dialog =>
            {
                Require(dialog.BabyMode.IsChecked == true && dialog.ExtremeMode.IsChecked != true &&
                    dialog.FormBox.SelectedIndex == 0 && dialog.PreviewPet?.Fields[5] == "1" &&
                    VisualDescendants(dialog).OfType<RadioButton>().Count() == 2, "Exactly two generation presets, defaulting to a level-one baby.");
                foreach (var family in dialog.FamilyBox.Items.Cast<PetGuideFamily>().ToArray())
                {
                    dialog.FamilyBox.SelectedItem = family;
                    Require(dialog.FormBox.SelectedIndex == 0 && family.Entry.Icon is not null,
                        "Changing species selects the illustrated baby form.");
                    if (family.Code is "mouse" or "neat" or "roomhorse" or "terribletiger")
                        Require(dialog.FormBox.Items.Count == 2, "Two-form species have exactly two visible choices.");
                    foreach (var form in family.Forms)
                    {
                        dialog.FormBox.SelectedItem = form;
                        Require(ReferenceEquals(dialog.PetPortrait.Source, form.Entry.Icon) && dialog.PreviewPet?.Fields[0] == family.Forms[0].Code &&
                            dialog.PreviewPet.Fields[5]=="1" && dialog.PreviewPet.Fields[19]=="" && dialog.PetName.Text == form.Entry.Name &&
                            dialog.CreationTarget.Text==family.Forms[0].Entry.Name,
                            "Browsing every form changes its illustration/introduction while baby creation stays first form, level one.");
                        Require(dialog.FormSkillsPreview.Items.Cast<PetEntry>().All(entry=>entry.Hint is not null),"Every form introduction skill has a hover explanation.");
                    }
                    dialog.ExtremeMode.IsChecked=true;
                    foreach(var form in family.Forms)
                    {
                        dialog.FormBox.SelectedItem=form;
                        Require(dialog.PreviewPet?.Fields[0]==PetData.FinalForm(local,form.Code) && dialog.PreviewPet.Fields[5]=="90" &&
                            dialog.CreationTarget.Text==GameJson.Text(local.PetNames[dialog.PreviewPet.Fields[0]]),
                            "Every extreme preset ignores form browsing and always generates its final form.");
                    }
                    dialog.BabyMode.IsChecked=true;
                }
                dialog.FamilyBox.SelectedItem = dialog.FamilyBox.Items.Cast<PetGuideFamily>().Single(f => f.Code == "nian");
                for (int stage = 0; stage < 3; stage++)
                {
                    dialog.FormBox.SelectedIndex = stage;
                    Capture(folder, $"nian-form-{stage + 1}.png", dialog);
                }
                dialog.FamilyBox.SelectedItem = dialog.FamilyBox.Items.Cast<PetGuideFamily>().Single(f => f.Code == "turtle");
                dialog.UpdateLayout();
                Capture(folder, "pet-create-baby.png", dialog);
                foreach (var combo in new[] { dialog.FamilyBox, dialog.FormBox })
                {
                    combo.IsDropDownOpen = true;
                    dialog.UpdateLayout();
                    var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
                    ((FrameworkElement)popup.Child).UpdateLayout();
                    Require(VisualDescendants(popup.Child).OfType<Image>().Any(image => image.Source is BitmapImage),
                        "Dropdown rows render original pet icons.");
                    combo.IsDropDownOpen = false;
                }
                dialog.FormBox.SelectedIndex = dialog.FormBox.Items.Count - 1;
                dialog.ExtremeMode.IsChecked = true;
                Require(dialog.PreviewPet?.Fields[5] == "90" && dialog.PreviewPet.Equipment.Count == 5,
                    "Extreme mode shows the final form with its promised equipment.");
                Require(dialog.FindName("PreviewSummary") is null,"The redundant potential/skill-count creation annotation is removed.");
                Capture(folder, "pet-create-extreme.png", dialog);
                dialog.Width = dialog.MinWidth; dialog.Height = dialog.MinHeight;
                Capture(folder, "pet-create-compact.png", dialog, 144);
                var root = (FrameworkElement)dialog.Content;
                var applyBounds = dialog.ApplyButton.TransformToAncestor(root).TransformBounds(new Rect(dialog.ApplyButton.RenderSize));
                Require(applyBounds.Left >= 0 && applyBounds.Top >= 0 && applyBounds.Right <= root.ActualWidth && applyBounds.Bottom <= root.ActualHeight,
                    "Creation action stays inside the compact dialog.");
                ClickDialogButton(dialog, "取消");
            });
            Require(JsonNode.DeepEquals(save.Draft, snapshot), "Browsing species and cancelling creation does not change the draft.");
            int count = Inventory.Records(Character!, Inventory.Pets).Count;
            ProbeSettingsDialog<PetCreateWindow>(AddPetButton, dialog =>
            {
                dialog.FamilyBox.SelectedItem = dialog.FamilyBox.Items.Cast<PetGuideFamily>().Single(f => f.Code == "mouse");
                dialog.FormBox.SelectedIndex=dialog.FormBox.Items.Count-1;
                ClickDialogButton(dialog, "添加到存档");
            });
            var created = new PetData(Inventory.Records(Character!, Inventory.Pets)[^1]);
            Require(Inventory.Records(Character!, Inventory.Pets).Count == count + 1 && created.Fields[0] == "mouse1" &&
                created.Fields[5] == "1" && created.Fields[18] == "0" && created.Fields[19]=="" && created.Equipment.Count == 0 &&
                ((PetRow)PetTable.SelectedItem).Index == count, "Applying a baby creates exactly one pet and selects it.");
            ProbeSettingsDialog<PetCreateWindow>(AddPetButton, dialog =>
            {
                dialog.FamilyBox.SelectedItem = dialog.FamilyBox.Items.Cast<PetGuideFamily>().Single(f => f.Code == "mouse");
                dialog.FormBox.SelectedIndex = 0;
                dialog.ExtremeMode.IsChecked = true;
                ClickDialogButton(dialog, "添加到存档");
            });
            created = new PetData(Inventory.Records(Character!, Inventory.Pets)[^1]);
            Require(created.Fields[0] == "mouse4" && created.Fields[5] == "90" && created.Equipment.Count == 5 &&
                System.IO.File.ReadAllText(save.Path) == disk, "Applying extreme while viewing the baby creates a final-form pet and only stages the result.");
            Height = MinHeight;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            double compact = PetSelector.ActualHeight;
            Height = 950;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Require(Math.Abs(PetSelector.ActualHeight - compact) < 1 && Math.Abs(compact - 200) < 1 && PetTable.ActualWidth >= 170,
                "Pet portrait and selector keep a fixed height at compact and large window sizes.");
            await CaptureWhenReady(folder, "pets-expanded.png");
        }
        finally
        {
            Height = height;
            save.Draft = snapshot;
            ShowSlot(key);
            PetTable.SelectedItem = PetTable.Items.Cast<PetRow>().Single(row => row.Index == selectedIndex);
            UpdateLayout();
        }
    }
}
#endif
