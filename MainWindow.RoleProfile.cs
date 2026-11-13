using System.Text.Json.Nodes;
using System.Windows;

namespace GmStudio;

public partial class MainWindow
{
    private void ShowRoleProfile()
    {
        if (game is null || save is null || slot is null || player is null || Character is not JsonObject character)
        {
            RoleProfileContent.Visibility = Visibility.Collapsed;
            RoleProfileEmpty.Visibility = Visibility.Visible;
            RolePortraitImage.Source = null;
            return;
        }

        var profile = new RoleProfile(game, save.Draft, character);
        RoleProfileName.Text = GameJson.Role(profile.Id);
        RoleProfileContext.Text = $"存档 {slot.Number} · 角色 {player.Key[6]}";
        RoleProfileLevel.Text = $"Lv. {profile.Level}";
        RoleProfilePower.Text = $"战力 {RoleProfile.Format(profile.Power)}";
        RoleProfileSoul.Text = $"灵魂 {RoleProfile.Format(GameJson.Number(character["lhValue"]))}";
        RoleProfileStage.Text = $"已解锁 {Value(save.Draft, "curBigStage", "1")}–{Value(save.Draft, "curBigLevel", "1")}";
        RoleProfileExperience.Text = $"经验 {RoleProfile.Format(profile.Experience)} / {RoleProfile.Format(profile.NextExperience)}";
        RoleExperienceBar.Maximum = profile.NextExperience;
        RoleExperienceBar.Value = Math.Min(profile.Experience, profile.NextExperience);
        RoleMetrics.ItemsSource = profile.Metrics;
        RoleWornSlots.ItemsSource = profile.Gear;
        RolePortraitImage.Source = game.RoleArtwork.Portrait(profile.Id, profile.Clothing, profile.Weapon, out bool fallback);
        RolePortraitPlaceholder.Visibility = RolePortraitImage.Source is null ? Visibility.Visible : Visibility.Collapsed;
        RolePortraitNote.Visibility = RolePortraitImage.Source is not null && fallback ? Visibility.Visible : Visibility.Collapsed;
        RoleProfileEmpty.Visibility = Visibility.Collapsed;
        RoleProfileContent.Visibility = Visibility.Visible;
    }
}
