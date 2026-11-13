using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GmStudio;

public partial class PromptDialog : Window
{
    private PromptDialog(Window owner, string title, string message, string action, bool confirmation, bool caution)
    {
        InitializeComponent();
        Owner = owner;
        FontFamily = owner.FontFamily;
        FontSize = owner.FontSize;
        Title = Heading.Text = title;
        MessageBody.Text = message;
        AcceptButton.Content = action;
        if (caution)
        {
            IconTile.Background = new SolidColorBrush(Color.FromRgb(252, 239, 221));
            IconMark.Foreground = new SolidColorBrush(Color.FromRgb(143, 76, 25));
            IconMark.Text = "!";
            if (confirmation)
                AcceptButton.Background = AcceptButton.BorderBrush = new SolidColorBrush(Color.FromRgb(156, 65, 43));
        }
        if (!confirmation)
        {
            CancelButton.Visibility = Visibility.Collapsed;
            CancelButton.IsCancel = CancelButton.IsDefault = false;
            AcceptButton.IsCancel = AcceptButton.IsDefault = true;
        }
        FocusManager.SetFocusedElement(this, confirmation ? CancelButton : AcceptButton);
    }

    internal static bool Confirm(Window owner, string title, string message, string action, bool caution) =>
        new PromptDialog(owner, title, message, action, confirmation: true, caution).ShowDialog() == true;

    internal static void Alert(Window owner, string title, string message) =>
        new PromptDialog(owner, title, message, "知道了", confirmation: false, caution: true).ShowDialog();

    private void AcceptClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void DismissClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void DragHeading(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }
}
