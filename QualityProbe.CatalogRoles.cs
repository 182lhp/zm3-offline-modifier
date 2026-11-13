#if QA
using System.Text.Json.Nodes;
using System.Windows;

namespace GmStudio;

public partial class MainWindow
{
    private void VerifyCatalogRoles()
    {
        var originalDraft=save!.Draft.DeepClone();var originalPlayer=player;
        Require(CatalogRoleBox.Items.Cast<string>().SequenceEqual(["全部角色","通用","孙悟空","唐僧","猪八戒","沙僧"]),
            "The catalog offers All, Universal and four independent hero filters.");
        foreach(var (role,shortName) in new[] {("孙悟空","悟空"),("唐僧","唐僧"),("猪八戒","八戒"),("沙僧","沙僧")})
        {
            CatalogRoleBox.SelectedItem=role;
            Require(catalogMatches.Any(item=>item.User==shortName) && catalogMatches.Any(item=>item.Code=="wpsmd1") &&
                catalogMatches.All(item=>string.IsNullOrWhiteSpace(item.User) || item.User==shortName),
                $"{role} sees the matching dedicated equipment and universal items without other heroes' equipment.");
        }
        CatalogRoleBox.SelectedItem="通用";
        Require(catalogMatches.Length>0 && catalogMatches.All(item=>string.IsNullOrWhiteSpace(item.User)),
            "Universal-only excludes all hero-specific equipment.");
        ClearSearch(this,new RoutedEventArgs());CatalogSizeBox.SelectedValue=20;
        NextCatalogPage(this,new RoutedEventArgs());Require(catalogPage==2,"Prepare a later catalog page.");
        CatalogRoleBox.SelectedItem="孙悟空";Require(catalogPage==1,"Role changes return to page one.");
        var equipment=game!.Catalog.First(item=>item.User=="悟空" && item.Group=="武器防具");
        CategoryBox.SelectedItem=equipment.Group;QualityBox.SelectedItem=equipment.Quality;SearchBox.Text=equipment.Code;
        Require(catalogMatches.Length==1 && catalogMatches[0].Code==equipment.Code,"Role, category, quality and code search combine.");
        CatalogRoleBox.SelectedItem="唐僧";
        Require(catalogMatches.Length==0 && !GiveButton.IsEnabled,"A mismatched hero produces an empty, non-grantable result.");
        CatalogClearButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Require(CatalogRoleBox.SelectedIndex==0 && CategoryBox.SelectedIndex==0 && QualityBox.SelectedIndex==0 &&
            SearchBox.Text=="" && catalogMatches.Length==450 && catalogPage==1,"Clear resets every filter together.");
        CatalogSizeBox.SelectedValue=0;CatalogTable.SelectedIndex=0;
        Require(ReferenceEquals(player,originalPlayer) && JsonNode.DeepEquals(originalDraft,save.Draft),
            "Catalog role filtering neither changes the editing character nor edits save data.");
    }
}
#endif
