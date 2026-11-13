using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GmStudio;

public partial class MainWindow
{
    private SidebarPreferences sidebar = null!;
    private Dictionary<string, FrameworkElement> sidebarElements = null!;

    private void InitializeSidebar()
    {
        var controls = SidebarItems.Children.Cast<FrameworkElement>().ToArray();
        sidebarElements = controls.ToDictionary(element => element.Name);
        var defaults = controls.Select(element => element is RadioButton button
            ? new SidebarDefinition(element.Name, (string)button.Content, (string)button.Tag)
            : new SidebarDefinition(element.Name, ((TextBlock)element).Text, null)).ToArray();
        string directory = ((App)Application.Current).PreferencesDirectory;
        sidebar = new SidebarPreferences(Path.Combine(directory, "navigation.json"), defaults);
        ApplySidebar();
    }

    private void ApplySidebar()
    {
        for (int index = 0; index < sidebar.Current.Count; index++)
        {
            var entry = sidebar.Current[index];
            var element = sidebarElements[entry.Id];
            element.ToolTip = entry.Name;
            if (element is RadioButton button)
                button.Content = entry.Name;
            else if (element is TextBlock heading)
            {
                heading.Text = entry.Name;
                heading.Margin = index == 0 ? new Thickness(12, 0, 0, 8) : new Thickness(12, 11, 0, 7);
            }
            int previous = SidebarItems.Children.IndexOf(element);
            if (previous != index)
            {
                SidebarItems.Children.RemoveAt(previous);
                SidebarItems.Children.Insert(index, element);
            }
        }
        RefreshNavigationState();
    }

    internal void EditSidebar(Window owner)
    {
        var editor = new SidebarSettingsWindow(owner, sidebar, active.Name);
        if (editor.ShowDialog() == true)
            ApplySidebar();
    }
}
