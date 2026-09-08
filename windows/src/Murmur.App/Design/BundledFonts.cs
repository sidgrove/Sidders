using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace Murmur.App.Design;

/// <summary>
/// Registers the three bundled faces as a font collection.
/// </summary>
/// <remarks>
/// An <c>avares:</c> folder is not, by itself, something the font manager will search — the
/// Inter package works because it registers an <see cref="EmbeddedFontCollection"/> under a
/// <c>fonts:</c> key. This does the same for <c>Assets/Fonts</c>, so
/// <see cref="Tokens.Fonts"/> can name the families the way the CSS does.
/// </remarks>
public static class BundledFonts
{
    /// <summary>The collection key the tokens refer to.</summary>
    public static Uri Key { get; } = new("fonts:Sidders");

    /// <summary>Where the files live inside the assembly.</summary>
    public static Uri Source { get; } = new("avares://Sidders/Assets/Fonts");

    /// <summary>Adds the collection. Safe to call once per process.</summary>
    public static void Register() =>
        FontManager.Current.AddFontCollection(new EmbeddedFontCollection(Key, Source));
}
