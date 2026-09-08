using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Murmur.App.Design;

namespace Murmur.App.Controls;

/// <summary>
/// The page background: the wash with two soft brand blooms, never a flat colour.
/// </summary>
public sealed class WashPanel : Decorator
{
    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        context.FillRectangle(Tokens.Brushes.Wash, bounds);

        // radial-gradient(ellipse 80% 50% at 65% -10%, brand 7%) and (50% 55% at -5% 95%, strong 5%).
        context.FillRectangle(Bloom(Tokens.Colors.Brand, Tokens.Opacity.WashTop, new RelativePoint(0.65, -0.10, RelativeUnit.Relative), 0.80, 0.50), bounds);
        context.FillRectangle(Bloom(Tokens.Colors.BrandStrong, Tokens.Opacity.WashBottom, new RelativePoint(-0.05, 0.95, RelativeUnit.Relative), 0.50, 0.55), bounds);
    }

    private static RadialGradientBrush Bloom(Color colour, double opacity, RelativePoint centre, double rx, double ry) => new()
    {
        Center = centre,
        GradientOrigin = centre,
        RadiusX = new RelativeScalar(rx, RelativeUnit.Relative),
        RadiusY = new RelativeScalar(ry, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromArgb((byte)(opacity * 255), colour.R, colour.G, colour.B), 0),
            new GradientStop(Color.FromArgb(0, colour.R, colour.G, colour.B), 1),
        },
    };
}

/// <summary>Card surfaces.</summary>
public static class Card
{
    /// <summary>A white card with the standard shadow.</summary>
    public static Border Standard(Control content, double? padding = null) => new()
    {
        Background = Tokens.Brushes.Card,
        BorderBrush = Tokens.Brushes.CardBorder,
        BorderThickness = new Thickness(Tokens.Border.Hairline),
        CornerRadius = new CornerRadius(Tokens.Radius.Card),
        BoxShadow = Tokens.Shadow.Card,
        Padding = new Thickness(padding ?? Tokens.Space.Card),
        Child = content,
    };

    /// <summary>A card that lifts under the pointer, for rows in a list.</summary>
    public static Border Lifting(Control content, double? padding = null)
    {
        var card = Standard(content, padding);
        card.Transitions =
        [
            new BoxShadowsTransition { Property = Border.BoxShadowProperty, Duration = Tokens.Motion.Lift },
            new BrushTransition { Property = Border.BorderBrushProperty, Duration = Tokens.Motion.Lift },
        ];
        card.PointerEntered += (_, _) =>
        {
            card.BoxShadow = Tokens.Shadow.Lift;
            card.BorderBrush = new SolidColorBrush(Tokens.Colors.Brand, Tokens.Opacity.Edge);
        };
        card.PointerExited += (_, _) =>
        {
            card.BoxShadow = Tokens.Shadow.Card;
            card.BorderBrush = Tokens.Brushes.CardBorder;
        };
        return card;
    }

    /// <summary>An inner panel: surface fill, no shadow, tighter radius.</summary>
    public static Border Subtle(Control content, double? padding = null) => new()
    {
        Background = Tokens.Brushes.Surface,
        BorderBrush = Tokens.Brushes.Line,
        BorderThickness = new Thickness(Tokens.Border.Hairline),
        CornerRadius = new CornerRadius(Tokens.Radius.Inner),
        Padding = new Thickness(padding ?? Tokens.Space.Roomy),
        Child = content,
    };

    /// <summary>A tinted notice: rose for attention, amber for warning, brand for information.</summary>
    public static Border Notice(Control content, IBrush background, IBrush edge) => new()
    {
        Background = background,
        BorderBrush = edge,
        BorderThickness = new Thickness(Tokens.Border.Hairline),
        CornerRadius = new CornerRadius(Tokens.Radius.Inner),
        Padding = new Thickness(Tokens.Space.Roomy, Tokens.Space.Base),
        Child = content,
    };
}

/// <summary>Text at the type scale.</summary>
public static class Text
{
    /// <summary>The window title.</summary>
    public static TextBlock Title(string text) => Make(text, Tokens.Fonts.Title, FontWeight.Bold, Tokens.Brushes.Ink, Tokens.Fonts.HeadingTracking);

    /// <summary>A section heading.</summary>
    public static TextBlock Heading(string text) => Make(text, Tokens.Fonts.Heading, FontWeight.Bold, Tokens.Brushes.Ink, Tokens.Fonts.HeadingTracking);

    /// <summary>Body copy.</summary>
    public static TextBlock Body(string text) => Make(text, Tokens.Fonts.Body, FontWeight.Normal, Tokens.Brushes.Ink);

    /// <summary>Body copy, emphasised.</summary>
    public static TextBlock BodyStrong(string text) => Make(text, Tokens.Fonts.Body, FontWeight.SemiBold, Tokens.Brushes.Ink);

    /// <summary>The transcript itself.</summary>
    public static TextBlock Reading(string text) => Make(text, Tokens.Fonts.Reading, FontWeight.Normal, Tokens.Brushes.Ink, lineHeight: Tokens.Fonts.Reading * 1.5);

    /// <summary>Secondary copy.</summary>
    public static TextBlock Muted(string text) => Make(text, Tokens.Fonts.Small, FontWeight.Normal, Tokens.Brushes.Muted);

    /// <summary>Metadata and captions.</summary>
    public static TextBlock Caption(string text) => Make(text, Tokens.Fonts.Caption, FontWeight.Normal, Tokens.Brushes.Faint);

    /// <summary>An eyebrow label: small, uppercase, tracked.</summary>
    public static TextBlock Eyebrow(string text) => Make(text.ToUpperInvariant(), Tokens.Fonts.Badge, FontWeight.SemiBold, Tokens.Brushes.Faint, Tokens.Fonts.BadgeTracking);

    /// <summary>A number that ticks: tabular figures.</summary>
    public static TextBlock Number(string text, double size, IBrush brush)
    {
        var block = Make(text, size, FontWeight.SemiBold, brush);
        block.FontFeatures = Tokens.Fonts.Tabular;
        return block;
    }

    private static TextBlock Make(string text, double size, FontWeight weight, IBrush brush, double tracking = 0, double? lineHeight = null)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = Tokens.Fonts.Sans,
            FontSize = size,
            FontWeight = weight,
            Foreground = brush,
            LetterSpacing = tracking,
            TextWrapping = TextWrapping.Wrap,
        };
        if (lineHeight is { } lh) block.LineHeight = lh;
        return block;
    }
}

/// <summary>A badge: coloured text on the same hue at 8%, pill-shaped, uppercase.</summary>
public static class Pill
{
    /// <summary>Brand: active, positive, the AI tier.</summary>
    public static Border Brand(string text) => Make(text, Tokens.Brushes.BrandStrong, Tokens.Brushes.BrandLight);

    /// <summary>Amber: flagged, warning.</summary>
    public static Border Amber(string text) => Make(text, Tokens.Brushes.AmberMid, Tokens.Brushes.AmberLight);

    /// <summary>Rose: negative, off.</summary>
    public static Border Rose(string text) => Make(text, Tokens.Brushes.Rose, Tokens.Brushes.RoseLight);

    /// <summary>Neutral.</summary>
    public static Border Neutral(string text) => Make(text, Tokens.Brushes.Muted, Tokens.Brushes.Surface);

    private static Border Make(string text, IBrush foreground, IBrush background) => new()
    {
        Background = background,
        CornerRadius = new CornerRadius(Tokens.Radius.Pill),
        Padding = new Thickness(Tokens.Space.Chip, Tokens.Space.Hair),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = Tokens.Fonts.Sans,
            FontSize = Tokens.Fonts.Badge,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = Tokens.Fonts.BadgeTracking,
            Foreground = foreground,
        },
    };
}

/// <summary>
/// A button in one of the brand's three shapes. Presses with a scale, as every Sidgrove
/// button does.
/// </summary>
public sealed class SgButton : Button
{
    /// <summary>The visual variants.</summary>
    public enum Kind
    {
        /// <summary>Brand gradient, white text. One per view.</summary>
        Primary,

        /// <summary>Surface fill with a line border.</summary>
        Ghost,

        /// <summary>No fill until hovered. Row actions.</summary>
        Quiet,

        /// <summary>Rose text, rose tint on hover. Delete.</summary>
        Danger,
    }

    /// <summary>The variant.</summary>
    public static readonly StyledProperty<Kind> VariantProperty =
        AvaloniaProperty.Register<SgButton, Kind>(nameof(Variant), Kind.Ghost);

    /// <summary>Compact height, for row actions.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<SgButton, bool>(nameof(IsCompact));

    /// <inheritdoc cref="VariantProperty"/>
    public Kind Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    private readonly Border _skin = new();
    private readonly ScaleTransform _scale = new(1, 1);

    /// <summary>Creates a button with a text label.</summary>
    public SgButton(string label, Kind variant = Kind.Ghost, bool compact = false)
    {
        Variant = variant;
        IsCompact = compact;
        Content = label;
        FontFamily = Tokens.Fonts.Sans;
        FontSize = compact ? Tokens.Fonts.Small : Tokens.Fonts.Body;
        FontWeight = variant == Kind.Primary ? FontWeight.Bold : FontWeight.SemiBold;
        Height = compact ? Tokens.Layout.ButtonHeightSmall : Tokens.Layout.ButtonHeight;
        Padding = new Thickness(compact ? Tokens.Space.Base : Tokens.Space.Roomy, 0);
        Background = Tokens.Brushes.None;
        BorderThickness = new Thickness(0);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        RenderTransformOrigin = RelativePoint.Center;
        RenderTransform = _scale;
        Transitions =
        [
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = Tokens.Motion.Quick },
        ];

        _skin.CornerRadius = new CornerRadius(Tokens.Radius.Control);
        _skin.BorderThickness = new Thickness(Tokens.Border.Hairline);
        _skin.Transitions =
        [
            new BrushTransition { Property = Border.BackgroundProperty, Duration = Tokens.Motion.Quick },
            new BrushTransition { Property = Border.BorderBrushProperty, Duration = Tokens.Motion.Quick },
        ];

        Template = new FuncControlTemplate<SgButton>((button, scope) =>
        {
            _skin.Child = new ContentPresenter
            {
                Name = "PART_ContentPresenter",
                [!ContentPresenter.ContentProperty] = button[!ContentProperty],
                [!ContentPresenter.PaddingProperty] = button[!PaddingProperty],
                [!ContentPresenter.ForegroundProperty] = button[!ForegroundProperty],
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            }.RegisterInNameScope(scope);
            return _skin;
        });

        Paint();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsPressedProperty)
        {
            var pressed = IsPressed;
            RenderTransform = new ScaleTransform(pressed ? Tokens.Motion.PressScale : 1, pressed ? Tokens.Motion.PressScale : 1);
        }

        if (change.Property == IsPointerOverProperty || change.Property == VariantProperty || change.Property == IsEnabledProperty)
        {
            Paint();
        }
    }

    private void Paint()
    {
        var over = IsPointerOver;
        switch (Variant)
        {
            case Kind.Primary:
                _skin.Background = Tokens.Brushes.PrimaryGradient;
                _skin.BorderBrush = Tokens.Brushes.None;
                _skin.BoxShadow = over ? Tokens.Shadow.Lift : Tokens.Shadow.Primary;
                Foreground = Tokens.Brushes.OnBrand;
                break;
            case Kind.Ghost:
                _skin.Background = over ? Tokens.Brushes.BrandLight : Tokens.Brushes.Surface;
                _skin.BorderBrush = over ? new SolidColorBrush(Tokens.Colors.Brand, Tokens.Opacity.Ring) : Tokens.Brushes.Line;
                _skin.BoxShadow = Tokens.Shadow.None;
                Foreground = over ? Tokens.Brushes.BrandStrong : Tokens.Brushes.Muted;
                break;
            case Kind.Quiet:
                _skin.Background = over ? Tokens.Brushes.Surface : Tokens.Brushes.None;
                _skin.BorderBrush = Tokens.Brushes.None;
                _skin.BoxShadow = Tokens.Shadow.None;
                Foreground = over ? Tokens.Brushes.Ink : Tokens.Brushes.Muted;
                break;
            case Kind.Danger:
                _skin.Background = over ? Tokens.Brushes.RoseLight : Tokens.Brushes.None;
                _skin.BorderBrush = Tokens.Brushes.None;
                _skin.BoxShadow = Tokens.Shadow.None;
                Foreground = Tokens.Brushes.Rose;
                break;
        }

        Opacity = IsEnabled ? 1 : Tokens.Opacity.Disabled;
    }
}

/// <summary>A minimal window control: a thin glyph that gains a surface tint on hover.</summary>
public sealed class CaptionGlyph : Button
{
    /// <summary>What the glyph shows.</summary>
    public enum Glyph
    {
        /// <summary>A short bar.</summary>
        Minimise,

        /// <summary>A hollow square.</summary>
        Maximise,

        /// <summary>A cross.</summary>
        Close,
    }

    private readonly Glyph _kind;

    /// <summary>Creates a glyph button.</summary>
    public CaptionGlyph(Glyph kind)
    {
        _kind = kind;
        Width = Tokens.Layout.CaptionButton;
        Height = Tokens.Layout.CaptionButton;
        Background = Tokens.Brushes.None;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        CornerRadius = new CornerRadius(Tokens.Radius.Control);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPointerOverProperty || change.Property == IsPressedProperty) InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var hover = IsPointerOver;
        var close = _kind == Glyph.Close;

        if (hover)
        {
            context.DrawRectangle(close ? Tokens.Brushes.RoseLight : Tokens.Brushes.Surface, null,
                new RoundedRect(bounds, Tokens.Radius.Control));
        }

        var pen = new Pen(hover && close ? Tokens.Brushes.Rose : Tokens.Brushes.Muted, Tokens.Border.Hairline * 1.25);
        var c = new Point(bounds.Width / 2, bounds.Height / 2);
        var r = Tokens.Layout.CaptionButton * 0.17;

        switch (_kind)
        {
            case Glyph.Minimise:
                context.DrawLine(pen, new Point(c.X - r, c.Y), new Point(c.X + r, c.Y));
                break;
            case Glyph.Maximise:
                context.DrawRectangle(null, pen, new RoundedRect(new Rect(c.X - r, c.Y - r, 2 * r, 2 * r), Tokens.Space.Hair));
                break;
            case Glyph.Close:
                context.DrawLine(pen, new Point(c.X - r, c.Y - r), new Point(c.X + r, c.Y + r));
                context.DrawLine(pen, new Point(c.X - r, c.Y + r), new Point(c.X + r, c.Y - r));
                break;
        }
    }
}

/// <summary>A toggle switch, drawn.</summary>
public sealed class Switch : ToggleButton
{
    /// <summary>Creates a switch.</summary>
    public Switch()
    {
        Width = Tokens.Layout.SwitchWidth;
        Height = Tokens.Layout.SwitchHeight;
        Background = Tokens.Brushes.None;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        Template = new FuncControlTemplate<Switch>((_, _) => new Panel());
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty || change.Property == IsPointerOverProperty) InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var on = IsChecked == true;
        var bounds = new Rect(Bounds.Size);
        var track = new RoundedRect(bounds, Tokens.Radius.Pill);

        context.DrawRectangle(on ? Tokens.Brushes.Brand : (IsPointerOver ? Tokens.Brushes.BrandMid : Tokens.Brushes.Line), null, track);

        var pad = Tokens.Space.Hair + Tokens.Border.Hairline;
        var d = bounds.Height - 2 * pad;
        var x = on ? bounds.Width - pad - d / 2 : pad + d / 2;
        context.DrawEllipse(Tokens.Brushes.Card, null, new Point(x, bounds.Height / 2), d / 2, d / 2);
    }
}

/// <summary>A small status dot, optionally breathing when live.</summary>
public sealed class StatusDot : Control
{
    /// <summary>The colour.</summary>
    public static readonly StyledProperty<IBrush> FillProperty =
        AvaloniaProperty.Register<StatusDot, IBrush>(nameof(Fill), Tokens.Brushes.BrandMid);

    /// <summary>Whether the dot pulses.</summary>
    public static readonly StyledProperty<bool> IsLiveProperty =
        AvaloniaProperty.Register<StatusDot, bool>(nameof(IsLive));

    /// <inheritdoc cref="FillProperty"/>
    public IBrush Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <inheritdoc cref="IsLiveProperty"/>
    public bool IsLive
    {
        get => GetValue(IsLiveProperty);
        set => SetValue(IsLiveProperty, value);
    }

    private DispatcherTimer? _ticker;
    private double _phase;

    static StatusDot() => AffectsRender<StatusDot>(FillProperty, IsLiveProperty);

    /// <summary>Creates a dot at the token size.</summary>
    public StatusDot()
    {
        Width = Tokens.Layout.Dot * 2;
        Height = Tokens.Layout.Dot * 2;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _ticker = new DispatcherTimer(DispatcherPriority.Render) { Interval = Tokens.Motion.Frame };
        _ticker.Tick += (_, _) => { if (IsLive) { _phase += 0.08; InvalidateVisual(); } };
        _ticker.Start();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _ticker?.Stop();
        _ticker = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var r = Tokens.Layout.Dot / 2;

        if (IsLive)
        {
            var halo = r + (Tokens.Layout.Dot / 2) * (0.5 + 0.5 * Math.Sin(_phase));
            context.DrawEllipse(new SolidColorBrush(((ISolidColorBrush)Fill).Color, Tokens.Opacity.Edge), null, c, halo, halo);
        }

        context.DrawEllipse(Fill, null, c, r, r);
    }
}

/// <summary>
/// The listening bars: a row of rounded bars that move with the input level.
/// </summary>
/// <remarks>
/// Each bar has its own weight and phase so the row reads as a waveform rather than a
/// level meter, rises quickly and falls slowly, and breathes gently while idle so the
/// card never looks dead. The physics live in plain fields stepped by a timer, outside the
/// property system, because sixty invalidations a second of a styled property would cost a
/// layout pass each.
/// </remarks>
public sealed class LevelBars : Control
{
    /// <summary>Current input level, 0…1.</summary>
    public static readonly StyledProperty<double> LevelProperty =
        AvaloniaProperty.Register<LevelBars, double>(nameof(Level));

    /// <summary>Whether recording — bars go full brand and follow the level.</summary>
    public static readonly StyledProperty<bool> IsLiveProperty =
        AvaloniaProperty.Register<LevelBars, bool>(nameof(IsLive));

    /// <inheritdoc cref="LevelProperty"/>
    public double Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    /// <inheritdoc cref="IsLiveProperty"/>
    public bool IsLive
    {
        get => GetValue(IsLiveProperty);
        set => SetValue(IsLiveProperty, value);
    }

    private readonly int _count;
    private readonly double[] _heights;
    private readonly double[] _weights;
    private readonly double[] _phases;
    private DispatcherTimer? _ticker;
    private double _time;

    static LevelBars() => AffectsRender<LevelBars>(IsLiveProperty);

    /// <summary>Creates a row of bars.</summary>
    /// <param name="count">How many bars.</param>
    /// <param name="height">Height of the field.</param>
    public LevelBars(int count, double height)
    {
        _count = count;
        _heights = new double[count];
        _weights = new double[count];
        _phases = new double[count];

        // Weights peak in the middle, like a centred waveform; phases scatter so bars
        // never move in lockstep.
        for (var i = 0; i < count; i++)
        {
            var x = (i + 0.5) / count;
            _weights[i] = 0.45 + 0.55 * Math.Sin(x * Math.PI);
            _phases[i] = (i * 2.399) % (2 * Math.PI);   // golden-angle scatter
        }

        Width = count * (Tokens.Layout.BarWidth + Tokens.Layout.BarGap) - Tokens.Layout.BarGap;
        Height = height;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _ticker = new DispatcherTimer(DispatcherPriority.Render) { Interval = Tokens.Motion.Frame };
        _ticker.Tick += (_, _) => { Step(); InvalidateVisual(); };
        _ticker.Start();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _ticker?.Stop();
        _ticker = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void Step()
    {
        _time += 0.05;
        var level = Math.Clamp(Math.Sqrt(Math.Clamp(Level, 0, 1)) * 1.6, 0, 1);

        for (var i = 0; i < _count; i++)
        {
            var wobble = 0.65 + 0.35 * Math.Sin(_time * 3.1 + _phases[i]);
            var target = IsLive
                ? Tokens.Layout.BarMinFraction + (1 - Tokens.Layout.BarMinFraction) * level * _weights[i] * wobble
                : Tokens.Layout.BarMinFraction + Tokens.Motion.BarIdleBreath * (0.5 + 0.5 * Math.Sin(_time * 1.2 + _phases[i]));

            var rate = target > _heights[i] ? Tokens.Motion.BarAttack : Tokens.Motion.BarRelease;
            _heights[i] += (target - _heights[i]) * rate;
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var pitch = Tokens.Layout.BarWidth + Tokens.Layout.BarGap;
        var brush = IsLive ? Tokens.Brushes.Brand : new SolidColorBrush(Tokens.Colors.BrandMid, Tokens.Opacity.BarsIdle);

        for (var i = 0; i < _count; i++)
        {
            var h = Math.Max(Tokens.Layout.BarWidth, _heights[i] * bounds.Height);
            var rect = new Rect(i * pitch, (bounds.Height - h) / 2, Tokens.Layout.BarWidth, h);
            context.DrawRectangle(brush, null, new RoundedRect(rect, Tokens.Radius.Bar));
        }
    }
}

/// <summary>The Sidgrove mark on a brand tile.</summary>
public sealed class LogoTile : Control
{
    /// <summary>Creates the tile at the token size.</summary>
    public LogoTile()
    {
        Width = Tokens.Layout.LogoTile;
        Height = Tokens.Layout.LogoTile;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var size = Bounds.Width;
        context.DrawRectangle(Tokens.Brushes.PrimaryGradient, null, new RoundedRect(new Rect(Bounds.Size), size * 0.22));

        // The favicon geometry in a 100-unit space, filling 76% of the tile.
        var u = size / 100.0;
        using (context.PushTransform(Matrix.CreateTranslation(-48.1 * u, -49.15 * u) * Matrix.CreateScale(0.76, 0.76) * Matrix.CreateTranslation(50 * u, 50 * u)))
        {
            using (context.PushTransform(Matrix.CreateRotation(-44.6 * Math.PI / 180) * Matrix.CreateTranslation(39.5 * u, 49.15 * u)))
            {
                context.DrawRectangle(Tokens.Brushes.OnBrand, null, new RoundedRect(new Rect(-43.2 * u, -9.5 * u, 86.4 * u, 19 * u), 6.5 * u));
            }

            context.DrawEllipse(Tokens.Brushes.OnBrand, null, new Point(82.3 * u, 72.3 * u), 11.8 * u, 11.8 * u);
        }
    }
}

/// <summary>A segmented control: one active segment in brand-strong, the rest quiet.</summary>
public sealed class Segmented : Border
{
    private readonly List<(Button Button, TextBlock Label)> _segments = [];

    /// <summary>Raised with the index of the chosen segment.</summary>
    public event EventHandler<int>? Selected;

    /// <summary>Builds the control.</summary>
    public Segmented(IEnumerable<string> labels, int selected = 0)
    {
        Background = new SolidColorBrush(Tokens.Colors.Line, 0.6);
        CornerRadius = new CornerRadius(Tokens.Radius.Control);
        Padding = new Thickness(Tokens.Space.Hair + Tokens.Border.Hairline);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Tokens.Space.Hair };
        var index = 0;
        foreach (var label in labels)
        {
            var i = index++;
            var text = new TextBlock
            {
                Text = label,
                FontFamily = Tokens.Fonts.Sans,
                FontSize = Tokens.Fonts.Small,
                FontWeight = FontWeight.SemiBold,
            };
            var button = new Button
            {
                Content = text,
                Background = Tokens.Brushes.None,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(Tokens.Radius.Control - Tokens.Space.Hair),
                Padding = new Thickness(Tokens.Space.Base, Tokens.Space.Tight + Tokens.Space.Hair),
                Height = Tokens.Layout.ButtonHeightSmall,
            };
            button.Click += (_, _) => { Select(i); Selected?.Invoke(this, i); };
            _segments.Add((button, text));
            row.Children.Add(button);
        }

        Child = row;
        Select(selected);
    }

    /// <summary>Sets the active segment without raising <see cref="Selected"/>.</summary>
    public void Select(int index)
    {
        for (var i = 0; i < _segments.Count; i++)
        {
            var active = i == index;
            _segments[i].Button.Background = active ? Tokens.Brushes.BrandStrong : Tokens.Brushes.None;
            _segments[i].Label.Foreground = active ? Tokens.Brushes.OnBrand : Tokens.Brushes.Muted;
        }
    }
}

/// <summary>Text fields on the brand's terms.</summary>
public static class Field
{
    /// <summary>A single-line text field.</summary>
    public static TextBox Text(string placeholder, string? initial = null, bool secret = false)
    {
        var box = new TextBox
        {
            Text = initial ?? string.Empty,
            Watermark = placeholder,
            FontFamily = Tokens.Fonts.Sans,
            FontSize = Tokens.Fonts.Body,
            Foreground = Tokens.Brushes.Ink,
            Background = Tokens.Brushes.Card,
            BorderBrush = Tokens.Brushes.Line,
            BorderThickness = new Thickness(Tokens.Border.Hairline),
            CornerRadius = new CornerRadius(Tokens.Radius.Control),
            Padding = new Thickness(Tokens.Space.Base, 0),
            Height = Tokens.Layout.FieldHeight,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        if (secret) box.PasswordChar = '•';
        return box;
    }

    /// <summary>A search field: the same, on the surface tint, with a leading glyph.</summary>
    public static TextBox Search(string placeholder)
    {
        var box = Text(placeholder);
        box.Background = Tokens.Brushes.Surface;
        box.InnerLeftContent = new TextBlock
        {
            Text = "⌕",
            FontSize = Tokens.Fonts.Heading,
            Foreground = Tokens.Brushes.Faint,
            Margin = new Thickness(Tokens.Space.Base, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        return box;
    }
}

/// <summary>A thin brand gauge for the model download.</summary>
public sealed class Gauge : Control
{
    /// <summary>How far along, 0…1.</summary>
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<Gauge, double>(nameof(Fraction));

    /// <inheritdoc cref="FractionProperty"/>
    public double Fraction
    {
        get => GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    static Gauge() => AffectsRender<Gauge>(FractionProperty);

    /// <summary>Creates a gauge at the token height.</summary>
    public Gauge() => Height = Tokens.Layout.GaugeHeight;

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.DrawRectangle(Tokens.Brushes.Line, null, new RoundedRect(bounds, Tokens.Radius.Pill));
        var fill = bounds.WithWidth(bounds.Width * Math.Clamp(Fraction, 0, 1));
        context.DrawRectangle(Tokens.Brushes.Brand, null, new RoundedRect(fill, Tokens.Radius.Pill));
    }
}
