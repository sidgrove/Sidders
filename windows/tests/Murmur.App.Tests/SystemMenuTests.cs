using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Murmur.App.Views;
using Shouldly;

namespace Murmur.AppTests;

public sealed class SystemMenuTests
{
    [AvaloniaFact]
    public void Alt_space_opens_window_menu_even_with_a_text_field_focused()
    {
        var window = new MenuWindow();
        var field = new TextBox();
        window.Content = field;
        window.Show();
        try
        {
            field.Focus();
            var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space, KeyModifiers = KeyModifiers.Alt };
            field.RaiseEvent(key);
            window.OpenedMenu.ShouldBeTrue();
            key.Handled.ShouldBeTrue();
        }
        finally { window.Close(); }
    }

    private sealed class MenuWindow : ShellWindow
    {
        public bool OpenedMenu { get; private set; }
        protected override void ShowSystemMenu() => OpenedMenu = true;
    }
}
