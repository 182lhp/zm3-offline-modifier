using System.Windows;
using System.Windows.Controls;

namespace GmStudio;

public partial class MainWindow
{
    private bool? compactRoleLayout;
    private void RoleWorkspaceSizeChanged(object sender,SizeChangedEventArgs e)
    {
        if(BagListCard is null || BagDetailCard is null) return;
        bool compact=e.NewSize.Width<650;
        if(compactRoleLayout==compact) return;
        compactRoleLayout=compact;
        BagColumnGap.Width=new GridLength(compact ? 0 : 12);
        BagDetailsColumn.Width=new GridLength(compact ? 0 : 280);
        BagListRow.Height=new GridLength(compact ? 1.15 : 1,GridUnitType.Star);
        BagRowGap.Height=new GridLength(compact ? 10 : 0);
        BagDetailRow.Height=compact ? new GridLength(1,GridUnitType.Star) : new GridLength(0);
        Grid.SetColumnSpan(BagListCard,compact ? 3 : 1);
        Grid.SetColumn(BagDetailCard,compact ? 0 : 2);
        Grid.SetColumnSpan(BagDetailCard,compact ? 3 : 1);
        Grid.SetRow(BagDetailCard,compact ? 2 : 0);
    }
}
