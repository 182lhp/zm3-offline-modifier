using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GmStudio;
public partial class LocalGiftItemWindow:Window
{
    private readonly GameFolder game;
    internal CatalogItem? Selected {get;private set;}
    internal LocalGiftItemWindow(Window owner,GameFolder game)
    {
        this.game=game;InitializeComponent();Owner=owner;Width=Math.Min(Width,SystemParameters.WorkArea.Width);Height=Math.Min(Height,SystemParameters.WorkArea.Height);FilterChanged(this,new TextChangedEventArgs(TextBox.TextChangedEvent,UndoAction.None));
    }
    private void FilterChanged(object sender,TextChangedEventArgs e)
    {
        if(Items is null || game is null)return;string term=Search.Text.Trim();Items.ItemsSource=game.Catalog.Where(i=>term.Length==0 || (i.Name+" "+i.Code).Contains(term,StringComparison.OrdinalIgnoreCase)).OrderBy(i=>i.Name).ToArray();if(Items.Items.Count>0)Items.SelectedIndex=0;
    }
    private void ChooseClick(object sender,RoutedEventArgs e){if(Items.SelectedItem is CatalogItem item){Selected=item;DialogResult=true;}}
    private void ChooseDoubleClick(object sender,MouseButtonEventArgs e)
    {
        if(e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(Items,source) is ListBoxItem)ChooseClick(sender,e);
    }
}
