using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GmStudio;

public partial class PetCreateWindow : Window
{
    private readonly GameFolder game;
    internal PetData? CreatedPet { get; private set; }
    internal PetData? PreviewPet { get; private set; }

    internal PetCreateWindow(Window owner, GameFolder game)
    {
        this.game = game;
        InitializeComponent();
        Owner = owner;
        Width = Math.Clamp(SystemParameters.WorkArea.Width, MinWidth, Width);
        Height = Math.Clamp(SystemParameters.WorkArea.Height, MinHeight, Height);
        BabyMode.IsChecked = true;
        FamilyBox.ItemsSource = PetGuideCatalog.Families(game);
        FamilyBox.SelectedIndex = 0;
    }

    private void FamilyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FamilyBox.SelectedItem is not PetGuideFamily family) return;
        FormBox.ItemsSource = family.Forms;
        FormBox.SelectedIndex = 0;
    }

    private void FormChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();
    private void ModeChanged(object sender, RoutedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        if (FormBox?.SelectedItem is not PetGuideForm form || FamilyBox.SelectedItem is not PetGuideFamily family) return;
        CreatedPet = PreviewPet = null;
        ApplyButton.IsEnabled = false;
        CreationError.Visibility = Visibility.Collapsed;
        PetPortrait.Source = form.Entry.Icon;
        PortraitInitial.Text = form.Entry.Initial;
        PortraitInitial.Visibility = form.Entry.Icon is null ? Visibility.Visible : Visibility.Collapsed;
        PetName.Text = form.Entry.Name;
        PetForm.Text = $"第 {form.Number} 形态 / 共 {family.Forms.Length} 种";
        var formRules=new PetRules(game,form.Code);
        FormSkillsPreview.ItemsSource=formRules.SkillDefinitions().Where(skill=>skill.Category!="基础" &&
            (form.IncludesStage(skill.FirstStage) || skill.Category=="奥义" && formRules.MatchesForm(skill)))
            .Select(skill=>PetEntry.Skill(game,skill.Code)).ToArray();
        try
        {
            var pet = PetData.Create(game, form.Code, ExtremeMode.IsChecked == true);
            var rules = new PetRules(game, pet.Fields[0]);
            string[] labels = ["生命资质", "魔法资质", "攻击资质", "防御资质"];
            QualityPreview.ItemsSource = Enumerable.Range(0, 4).Select(i => new PetMetric(labels[i], pet.Fields[9 + i])).ToArray();
            string[] abilities = ["悟性", "根骨", "灵力"];
            GrowthPreview.ItemsSource = PetRules.Abilities.Select((a, i) => new PetMetric(abilities[i],
                $"{PetRules.Value(pet.Fields, a.Base) + PetRules.Value(pet.Fields, a.Bonus)} / {rules.TotalMaximum(a.Key)}")).ToArray();
            PreviewLevel.Text = $"{pet.Fields[5]} 级 · 默认休息";
            CreationTarget.Text = GameJson.Text(game.PetNames[pet.Fields[0]]);
            CreationTargetImage.Source = game.Portrait("pets",pet.Fields[0]);
            PreviewPet = pet;
            ApplyButton.IsEnabled = true;
        }
        catch (InvalidDataException ex)
        {
            QualityPreview.ItemsSource = GrowthPreview.ItemsSource = null;
            PreviewLevel.Text = CreationTarget.Text = "";
            CreationTargetImage.Source = null;
            CreationError.Text = ex.Message;
            CreationError.Visibility = Visibility.Visible;
        }
    }

    private void ApplyClick(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
        if (PreviewPet is null) return;
        CreatedPet = PreviewPet;
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
