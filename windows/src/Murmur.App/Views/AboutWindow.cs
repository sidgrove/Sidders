using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Murmur.App.Controls;
using Murmur.App.Design;

namespace Murmur.App.Views;

/// <summary>The about box, which is also where the model's attribution lives.</summary>
/// <remarks>
/// The Parakeet weights are CC-BY-4.0: credit NVIDIA, name the model, say it was modified.
/// This is that line.
/// </remarks>
public sealed class AboutWindow : UnitWindow
{
    /// <summary>Builds the box.</summary>
    public AboutWindow()
    {
        Title = "About Murmur";
        ModelNumber = "ABOUT";
        IsResizableUnit = false;
        Width = Tokens.Layout.EditorWidth;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var ok = new TransportKey { Content = "OK", EngagedColor = Tokens.Colors.Ink, HorizontalAlignment = HorizontalAlignment.Right };
        ok.Click += (_, _) => Close();

        Content = Frame("Murmur", new StackPanel
        {
            Margin = new Thickness(Tokens.Space.Panel),
            Spacing = Tokens.Space.Roomy,
            Children =
            {
                new BrushedPanel
                {
                    HasScrews = true,
                    Child = new StackPanel
                    {
                        Margin = new Thickness(Tokens.Space.Wide),
                        Spacing = Tokens.Space.Snug,
                        Children =
                        {
                            new Silkscreen { Text = "Murmur  ·  PD-26", IsLarge = true },
                            Line("Push-to-talk dictation for Windows. Hold a key, talk, release — the text lands in whatever has focus. Everything runs on this machine; nothing leaves it."),
                            new Silkscreen { Text = "SPEECH MODEL", Margin = new Thickness(0, Tokens.Space.Snug, 0, 0) },
                            Line("NVIDIA Parakeet TDT 0.6B v2, converted to ONNX and quantized to int8 by the sherpa-onnx project. Model weights CC-BY-4.0, © NVIDIA, modified. sherpa-onnx Apache-2.0. ONNX Runtime MIT."),
                            new Silkscreen { Text = "INTERFACE", Margin = new Thickness(0, Tokens.Space.Snug, 0, 0) },
                            Line("Avalonia UI, MIT. Bahnschrift typeface, Microsoft."),
                        },
                    },
                },
                ok,
            },
        });
    }

    private static TextBlock Line(string text) => new()
    {
        Text = text,
        FontFamily = Tokens.Fonts.Grotesque,
        FontSize = Tokens.Fonts.Label,
        Foreground = Tokens.Brushes.Ink,
        TextWrapping = TextWrapping.Wrap,
    };
}
