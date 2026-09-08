using Avalonia;
using Avalonia.Media;

namespace Murmur.App.Design;

/// <summary>
/// The Sidgrove design system, as tokens.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors <c>lib/tokens.ts</c> and <c>globals.css</c> in Sidgrove Intelligence, and the
/// sidgrove-brand reference: lavender-blue brand, cool-slate neutrals with a blue undertone,
/// white cards on a washed background, DM Sans everywhere, sentence case, soft layered
/// shadows, radii on the tightened "crispness" scale (card 12, inner 10, control 8, pill 99).
/// </para>
/// <para>
/// <b>Views must not contain literal values.</b> If a control needs a number that isn't here,
/// add the token rather than inlining it.
/// </para>
/// <para>
/// Colour rules from the brand: brand blue for the primary action and anything "live";
/// rose only for negative or attention; amber only for warnings; no other hues.
/// </para>
/// </remarks>
public static class Tokens
{
    // ---- Colour ----

    /// <summary>The palette. Names follow the <c>T</c> object in the web apps.</summary>
    public static class Colors
    {
        /// <summary>Brand lavender-blue. Accent, active states, the listening bars.</summary>
        public static Color Brand => Rgb(0x6874B4);

        /// <summary>Very light brand tint, for hover and selected backgrounds.</summary>
        public static Color BrandLight => Rgb(0xEEF0FA);

        /// <summary>Mid tint, placeholder dots and idle bars.</summary>
        public static Color BrandMid => Rgb(0xA8B0D8);

        /// <summary>Dark brand, strong text and the far end of the primary gradient.</summary>
        public static Color BrandStrong => Rgb(0x3D4785);

        /// <summary>Muted rose: negative, attention, the recording dot in the tray.</summary>
        public static Color Rose => Rgb(0xB8456B);

        /// <summary>Rose tint background.</summary>
        public static Color RoseLight => Rgb(0xFDF2F6);

        /// <summary>Muted amber text.</summary>
        public static Color Amber => Rgb(0x7A5E1E);

        /// <summary>Amber mid, for warning badges.</summary>
        public static Color AmberMid => Rgb(0xC69B2D);

        /// <summary>Amber tint background.</summary>
        public static Color AmberLight => Rgb(0xFDF9EE);

        /// <summary>Primary text and headings.</summary>
        public static Color Ink => Rgb(0x1A1D2E);

        /// <summary>Secondary text.</summary>
        public static Color Muted => Rgb(0x525672);

        /// <summary>Tertiary text, placeholders. Clears AA on the wash.</summary>
        public static Color Faint => Rgb(0x646982);

        /// <summary>Borders and dividers.</summary>
        public static Color Line => Rgb(0xDFE1EE);

        /// <summary>Card borders, a touch stronger than a divider.</summary>
        public static Color CardBorder => Rgb(0xD8DEEC);

        /// <summary>Inner panel and hover background.</summary>
        public static Color Surface => Rgb(0xF6F7FC);

        /// <summary>The page wash behind everything.</summary>
        public static Color Wash => Rgb(0xF0F1F8);

        /// <summary>Card background.</summary>
        public static Color Card => Rgb(0xFFFFFF);

        /// <summary>Text on a brand-coloured surface.</summary>
        public static Color OnBrand => Rgb(0xFFFFFF);

        private static Color Rgb(uint hex) => Color.FromRgb(
            (byte)((hex >> 16) & 0xFF), (byte)((hex >> 8) & 0xFF), (byte)(hex & 0xFF));
    }

    /// <summary>Brushes for the colours above.</summary>
    public static class Brushes
    {
        /// <inheritdoc cref="Colors.Brand"/>
        public static IBrush Brand { get; } = new SolidColorBrush(Colors.Brand);

        /// <inheritdoc cref="Colors.BrandLight"/>
        public static IBrush BrandLight { get; } = new SolidColorBrush(Colors.BrandLight);

        /// <inheritdoc cref="Colors.BrandMid"/>
        public static IBrush BrandMid { get; } = new SolidColorBrush(Colors.BrandMid);

        /// <inheritdoc cref="Colors.BrandStrong"/>
        public static IBrush BrandStrong { get; } = new SolidColorBrush(Colors.BrandStrong);

        /// <inheritdoc cref="Colors.Rose"/>
        public static IBrush Rose { get; } = new SolidColorBrush(Colors.Rose);

        /// <inheritdoc cref="Colors.RoseLight"/>
        public static IBrush RoseLight { get; } = new SolidColorBrush(Colors.RoseLight);

        /// <inheritdoc cref="Colors.Amber"/>
        public static IBrush Amber { get; } = new SolidColorBrush(Colors.Amber);

        /// <inheritdoc cref="Colors.AmberMid"/>
        public static IBrush AmberMid { get; } = new SolidColorBrush(Colors.AmberMid);

        /// <inheritdoc cref="Colors.AmberLight"/>
        public static IBrush AmberLight { get; } = new SolidColorBrush(Colors.AmberLight);

        /// <inheritdoc cref="Colors.Ink"/>
        public static IBrush Ink { get; } = new SolidColorBrush(Colors.Ink);

        /// <inheritdoc cref="Colors.Muted"/>
        public static IBrush Muted { get; } = new SolidColorBrush(Colors.Muted);

        /// <inheritdoc cref="Colors.Faint"/>
        public static IBrush Faint { get; } = new SolidColorBrush(Colors.Faint);

        /// <inheritdoc cref="Colors.Line"/>
        public static IBrush Line { get; } = new SolidColorBrush(Colors.Line);

        /// <inheritdoc cref="Colors.CardBorder"/>
        public static IBrush CardBorder { get; } = new SolidColorBrush(Colors.CardBorder);

        /// <inheritdoc cref="Colors.Surface"/>
        public static IBrush Surface { get; } = new SolidColorBrush(Colors.Surface);

        /// <inheritdoc cref="Colors.Wash"/>
        public static IBrush Wash { get; } = new SolidColorBrush(Colors.Wash);

        /// <inheritdoc cref="Colors.Card"/>
        public static IBrush Card { get; } = new SolidColorBrush(Colors.Card);

        /// <inheritdoc cref="Colors.OnBrand"/>
        public static IBrush OnBrand { get; } = new SolidColorBrush(Colors.OnBrand);

        /// <summary>Transparent, for buttons that draw themselves.</summary>
        public static IBrush None { get; } = Avalonia.Media.Brushes.Transparent;

        /// <summary>The primary button: brand to brand-strong, 135°.</summary>
        public static IBrush PrimaryGradient { get; } = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Colors.Brand, 0), new GradientStop(Colors.BrandStrong, 1) },
        };

        /// <summary>Brand-tinted focus ring.</summary>
        public static IBrush FocusRing { get; } = new SolidColorBrush(Colors.Brand, Opacity.Ring);

        /// <summary>Brand at badge-background strength.</summary>
        public static IBrush BrandTint { get; } = new SolidColorBrush(Colors.Brand, Opacity.Tint);
    }

    /// <summary>Opacities used for tints, rings and washes.</summary>
    public static class Opacity
    {
        /// <summary>Badge and hover tint of a hue.</summary>
        public const double Tint = 0.08;

        /// <summary>A hover border in the brand hue.</summary>
        public const double Edge = 0.15;

        /// <summary>The focus ring.</summary>
        public const double Ring = 0.45;

        /// <summary>The top wash bloom on the page background.</summary>
        public const double WashTop = 0.07;

        /// <summary>The bottom wash bloom on the page background.</summary>
        public const double WashBottom = 0.05;

        /// <summary>Glass card fill.</summary>
        public const double Glass = 0.88;

        /// <summary>Disabled controls and rows.</summary>
        public const double Disabled = 0.45;

        /// <summary>Idle listening bars.</summary>
        public const double BarsIdle = 0.35;
    }

    // ---- Type ----

    /// <summary>
    /// Inter, bundled by Avalonia with every weight. One family for everything; numbers
    /// are tabular via <see cref="Tabular"/>.
    /// </summary>
    public static class Fonts
    {
        /// <summary>The one UI family, loaded from the bundled TTFs.</summary>
        public static FontFamily Sans { get; } = new("fonts:Inter#Inter");

        /// <summary>Tabular figures for anything that ticks or aligns.</summary>
        public static FontFeatureCollection Tabular { get; } = [new FontFeature { Tag = "tnum" }];

        /// <summary>Badges and eyebrow labels. Uppercase, tracked.</summary>
        public const double Badge = 10;

        /// <summary>Captions and metadata.</summary>
        public const double Caption = 11;

        /// <summary>Secondary body, table cells, helper text.</summary>
        public const double Small = 12.5;

        /// <summary>Body.</summary>
        public const double Body = 14;

        /// <summary>The transcript text — the thing you read most.</summary>
        public const double Reading = 15;

        /// <summary>Section headings.</summary>
        public const double Heading = 18;

        /// <summary>The window title.</summary>
        public const double Title = 22;

        /// <summary>The live counter while recording.</summary>
        public const double Counter = 28;

        /// <summary>Tracking for badges and eyebrows, in device-independent pixels at badge size.</summary>
        public const double BadgeTracking = 0.6;

        /// <summary>Tracking for headings. Tight, as <c>.display</c> is.</summary>
        public const double HeadingTracking = -0.4;
    }

    // ---- Geometry ----

    /// <summary>A 4pt grid.</summary>
    public static class Space
    {
        /// <summary>2</summary>
        public const double Hair = 2;

        /// <summary>4</summary>
        public const double Tight = 4;

        /// <summary>6</summary>
        public const double Chip = 6;

        /// <summary>8</summary>
        public const double Snug = 8;

        /// <summary>12</summary>
        public const double Base = 12;

        /// <summary>16</summary>
        public const double Roomy = 16;

        /// <summary>20</summary>
        public const double Card = 20;

        /// <summary>24</summary>
        public const double Wide = 24;

        /// <summary>32</summary>
        public const double Section = 32;

        /// <summary>40</summary>
        public const double Page = 40;
    }

    /// <summary>The crispness scale: card 12, inner 10, control 8, pill 99.</summary>
    public static class Radius
    {
        /// <summary>Chips, fields, badges with corners, small buttons.</summary>
        public const double Control = 8;

        /// <summary>Inner panels and nested boxes.</summary>
        public const double Inner = 10;

        /// <summary>Cards.</summary>
        public const double Card = 12;

        /// <summary>Pills and toggles.</summary>
        public const double Pill = 99;

        /// <summary>Listening bars.</summary>
        public const double Bar = 3;
    }

    /// <summary>Line weights.</summary>
    public static class Border
    {
        /// <summary>Card and field borders.</summary>
        public const double Hairline = 1;

        /// <summary>Focus ring.</summary>
        public const double Ring = 2;
    }

    /// <summary>
    /// Soft, layered, low-opacity. Cards sit at <see cref="Card"/> and lift on hover.
    /// </summary>
    public static class Shadow
    {
        /// <summary>Floating panels, popups.</summary>
        public static BoxShadows Soft => Layered((1, 2, 0.03), (4, 16, 0.04), (12, 32, 0.03));

        /// <summary>Standard card.</summary>
        public static BoxShadows Card => Layered((1, 2, 0.03), (4, 12, 0.04), (12, 28, 0.04));

        /// <summary>Hovered card, modal.</summary>
        public static BoxShadows Lift => Layered((2, 8, 0.04), (8, 24, 0.06), (20, 48, 0.06));

        /// <summary>The primary button's brand-tinted shadow.</summary>
        public static BoxShadows Primary => new(
            new BoxShadow { OffsetY = 2, Blur = 8, Color = Color.FromArgb((byte)(0.28 * 255), 0x3D, 0x47, 0x85) },
            [new BoxShadow { OffsetY = 1, Blur = 2, Color = Color.FromArgb((byte)(0.16 * 255), 0x3D, 0x47, 0x85) }]);

        /// <summary>No shadow.</summary>
        public static BoxShadows None => default;

        private static BoxShadows Layered(params (double OffsetY, double Blur, double Opacity)[] layers)
        {
            var first = ToShadow(layers[0]);
            var rest = layers.Skip(1).Select(ToShadow).ToArray();
            return new BoxShadows(first, rest);
        }

        private static BoxShadow ToShadow((double OffsetY, double Blur, double Opacity) l) => new()
        {
            OffsetY = l.OffsetY,
            Blur = l.Blur,
            Color = Color.FromArgb((byte)(l.Opacity * 255), 0, 0, 0),
        };
    }

    // ---- Layout ----

    /// <summary>Window and control dimensions.</summary>
    public static class Layout
    {
        /// <summary>Initial main window width.</summary>
        public const double MainWidth = 880;

        /// <summary>Initial main window height.</summary>
        public const double MainHeight = 660;

        /// <summary>Smallest main window.</summary>
        public const double MainMinWidth = 640;

        /// <summary>Smallest main window height.</summary>
        public const double MainMinHeight = 480;

        /// <summary>Settings dialog width.</summary>
        public const double SettingsWidth = 560;

        /// <summary>Dictionary editor and About width.</summary>
        public const double DialogWidth = 460;

        /// <summary>Content column max width, so text lines stay readable on a wide window.</summary>
        public const double ContentMaxWidth = 760;

        /// <summary>The caption strip that replaces the OS title bar.</summary>
        public const double CaptionHeight = 44;

        /// <summary>Caption glyph buttons.</summary>
        public const double CaptionButton = 28;

        /// <summary>Standard button height.</summary>
        public const double ButtonHeight = 36;

        /// <summary>Compact button height, for row actions.</summary>
        public const double ButtonHeightSmall = 28;

        /// <summary>Text field height.</summary>
        public const double FieldHeight = 38;

        /// <summary>Toggle switch width.</summary>
        public const double SwitchWidth = 38;

        /// <summary>Toggle switch height.</summary>
        public const double SwitchHeight = 22;

        /// <summary>Status dot diameter.</summary>
        public const double Dot = 8;

        /// <summary>Listening bars: count on the main card.</summary>
        public const int BarsCount = 28;

        /// <summary>Listening bars: count on the overlay.</summary>
        public const int BarsCountSmall = 12;

        /// <summary>Listening bars: width of one bar.</summary>
        public const double BarWidth = 4;

        /// <summary>Listening bars: gap.</summary>
        public const double BarGap = 4;

        /// <summary>Listening bars: height of the field on the main card.</summary>
        public const double BarsHeight = 56;

        /// <summary>Listening bars: height on the overlay.</summary>
        public const double BarsHeightSmall = 26;

        /// <summary>Listening bars: minimum bar height, as a fraction of the field.</summary>
        public const double BarMinFraction = 0.12;

        /// <summary>The overlay pill.</summary>
        public const double OverlayWidth = 260;

        /// <summary>Overlay height.</summary>
        public const double OverlayHeight = 56;

        /// <summary>Distance of the overlay from the bottom of the screen.</summary>
        public const double OverlayBottomMargin = 56;

        /// <summary>Gauge height for the model download.</summary>
        public const double GaugeHeight = 6;

        /// <summary>Widest a helper paragraph runs.</summary>
        public const double NoteMaxWidth = 440;

        /// <summary>Width of the logo tile in the header.</summary>
        public const double LogoTile = 28;
    }

    // ---- Motion ----

    /// <summary>Quick and quiet. Buttons press with a scale, cards lift, bars ease.</summary>
    public static class Motion
    {
        /// <summary>Hover and press transitions.</summary>
        public static TimeSpan Quick { get; } = TimeSpan.FromMilliseconds(150);

        /// <summary>Card lift.</summary>
        public static TimeSpan Lift { get; } = TimeSpan.FromMilliseconds(220);

        /// <summary>How often the panel polls the engine.</summary>
        public static TimeSpan PanelPoll { get; } = TimeSpan.FromMilliseconds(50);

        /// <summary>One frame of bar animation.</summary>
        public static TimeSpan Frame { get; } = TimeSpan.FromMilliseconds(16);

        /// <summary>How long "Copied" stays on a button.</summary>
        public static TimeSpan Confirmation { get; } = TimeSpan.FromMilliseconds(1400);

        /// <summary>Button press scale, as the brand's global rule.</summary>
        public const double PressScale = 0.93;

        /// <summary>How fast a bar rises toward the level, per frame.</summary>
        public const double BarAttack = 0.45;

        /// <summary>How fast a bar falls, per frame.</summary>
        public const double BarRelease = 0.12;

        /// <summary>Idle bars breathe at this fraction of the field.</summary>
        public const double BarIdleBreath = 0.06;
    }
}
