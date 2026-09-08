using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Murmur.Abstractions;
using Murmur.App.Controls;
using Murmur.App.Design;

namespace Murmur.App.Views;

/// <summary>The about box, which is also where the model's attribution lives.</summary>
/// <remarks>
/// The Parakeet weights are CC-BY-4.0: credit NVIDIA, name the model, say it was modified.
/// </remarks>
public sealed class AboutWindow : ShellWindow
{
    /// <summary>Builds the box.</summary>
    public AboutWindow()
    {
        Title = "About";
        IsSheet = true;
        Width = Tokens.Layout.DialogWidth;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var ok = new SgButton("Done", SgButton.Kind.Primary) { HorizontalAlignment = HorizontalAlignment.Right };
        ok.Click += (_, _) => Close();

        var body = Panels.Column(Tokens.Space.Roomy,
            Card.Standard(Panels.Column(Tokens.Space.Base,
                Text.Title(AppPaths.ProductName),
                Text.Body("Push-to-talk dictation for Windows. Hold a key, talk, release, and the text lands wherever you were typing. Speech recognition runs on this machine; nothing leaves it unless you switch on AI clean-up."),
                Text.Eyebrow("Speech model"),
                Text.Muted("NVIDIA Parakeet TDT 0.6B v2, converted to ONNX and quantised to int8 by the sherpa-onnx project. Model weights CC-BY-4.0, © NVIDIA, modified. sherpa-onnx Apache-2.0. ONNX Runtime MIT."),
                Text.Eyebrow("Interface"),
                Text.Muted("Avalonia UI, MIT. Inter, SIL Open Font License. Built by Sidgrove."))),
            ok);
        body.Margin = new Thickness(Tokens.Space.Wide, Tokens.Space.Snug, Tokens.Space.Wide, Tokens.Space.Wide);

        Content = Frame("About", body);
    }
}
