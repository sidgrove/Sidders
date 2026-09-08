using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Murmur.App.Design;

namespace Murmur.App.Controls;

/// <summary>
/// A brushed-metal panel.
/// </summary>
/// <remarks>
/// The grain is drawn, not an image: it stays sharp at any scale, follows the silver/black
/// face automatically, and adds nothing to the download.
/// </remarks>
public sealed class BrushedPanel : Decorator
{
    /// <summary>Corner radius of the panel.</summary>
    public static readonly StyledProperty<double> CornerRadiusProperty =
        AvaloniaProperty.Register<BrushedPanel, double>(nameof(CornerRadius), Tokens.Radius.Panel);

    /// <summary>Whether to draw a screw in each corner.</summary>
    public static readonly StyledProperty<bool> HasScrewsProperty =
        AvaloniaProperty.Register<BrushedPanel, bool>(nameof(HasScrews));

    /// <inheritdoc cref="CornerRadiusProperty"/>
    public double CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <inheritdoc cref="HasScrewsProperty"/>
    public bool HasScrews
    {
        get => GetValue(HasScrewsProperty);
        set => SetValue(HasScrewsProperty, value);
    }

    static BrushedPanel() => AffectsRender<BrushedPanel>(CornerRadiusProperty, HasScrewsProperty);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var shape = new RoundedRect(bounds, CornerRadius);
        context.DrawRectangle(Tokens.Brushes.Panel, null, shape);

        using (context.PushClip(bounds))
        {
            // Fine horizontal striations — the direction a rolled aluminium sheet is brushed.
            var light = new SolidColorBrush(Avalonia.Media.Colors.White, Tokens.Material.GrainLight);
            var dark = new SolidColorBrush(Avalonia.Media.Colors.Black, Tokens.Material.GrainDark);
            var alternate = false;

            for (var y = 0.0; y < bounds.Height; y += Tokens.Material.GrainPitch)
            {
                context.FillRectangle(
                    alternate ? dark : light,
                    new Rect(0, y, bounds.Width, Tokens.Material.GrainPitch / 2));
                alternate = !alternate;
            }
        }

        // Top bevel catches the light; the whole edge carries a seam.
        var bevel = new Pen(new SolidColorBrush(Tokens.Colors.PanelHighlight, Tokens.Opacity.Seam), Tokens.Border.Bevel);
        context.DrawLine(bevel, bounds.TopLeft, bounds.TopRight);

        var seam = new Pen(new SolidColorBrush(Tokens.Colors.Seam, Tokens.Opacity.Faint), Tokens.Border.Seam);
        context.DrawRectangle(null, seam, shape);

        if (!HasScrews) return;

        var inset = Tokens.Material.ScrewInset;
        Screw.Draw(context, new Point(inset, inset));
        Screw.Draw(context, new Point(bounds.Width - inset, inset));
        Screw.Draw(context, new Point(inset, bounds.Height - inset));
        Screw.Draw(context, new Point(bounds.Width - inset, bounds.Height - inset));
    }
}

/// <summary>
/// A panel screw: a countersunk head with a single slot.
/// </summary>
/// <remarks>
/// Drawn by <see cref="BrushedPanel"/> into its corners. Also usable standalone for the
/// caption strip. The slot angles differ slightly per screw, as they do on a real unit —
/// nobody lines them up.
/// </remarks>
public sealed class Screw : Control
{
    /// <summary>Creates a screw at the token size.</summary>
    public Screw()
    {
        Width = Tokens.Material.ScrewSize;
        Height = Tokens.Material.ScrewSize;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context) =>
        Draw(context, new Point(Bounds.Width / 2, Bounds.Height / 2));

    /// <summary>Draws a screw head centred on <paramref name="centre"/>.</summary>
    public static void Draw(DrawingContext context, Point centre)
    {
        var radius = Tokens.Material.ScrewSize / 2;

        // Countersink: a shade ring, then the head slightly lighter than the panel.
        context.DrawEllipse(new SolidColorBrush(Tokens.Colors.PanelShade), null, centre, radius, radius);
        context.DrawEllipse(new SolidColorBrush(Tokens.Colors.PanelHighlight), null,
            new Point(centre.X, centre.Y - Tokens.Border.Hairline / 2), radius - Tokens.Border.Hairline, radius - Tokens.Border.Hairline);

        // The slot. Angle derived from position so each screw sits differently.
        var angle = ((centre.X * 7) + (centre.Y * 13)) % 180 * Math.PI / 180;
        var dx = Math.Cos(angle) * (radius - Tokens.Border.Hairline * 1.5);
        var dy = Math.Sin(angle) * (radius - Tokens.Border.Hairline * 1.5);
        var slot = new Pen(new SolidColorBrush(Tokens.Colors.Seam, Tokens.Opacity.Dim), Tokens.Border.Hairline);
        context.DrawLine(slot, new Point(centre.X - dx, centre.Y - dy), new Point(centre.X + dx, centre.Y + dy));
    }
}

/// <summary>
/// A silkscreened panel label: small, uppercase, tightly tracked.
/// </summary>
/// <remarks>
/// The uppercasing happens here rather than at the call site so a label can never be
/// half-styled — the look depends on all three of size, tracking and case.
/// </remarks>
public sealed class Silkscreen : TextBlock
{
    /// <summary>Uses the larger silkscreen size.</summary>
    public static readonly StyledProperty<bool> IsLargeProperty =
        AvaloniaProperty.Register<Silkscreen, bool>(nameof(IsLarge));

    /// <inheritdoc cref="IsLargeProperty"/>
    public bool IsLarge
    {
        get => GetValue(IsLargeProperty);
        set => SetValue(IsLargeProperty, value);
    }

    /// <summary>Creates an empty label.</summary>
    public Silkscreen()
    {
        FontFamily = Tokens.Fonts.Grotesque;
        FontSize = Tokens.Fonts.Silkscreen;
        FontWeight = FontWeight.Medium;
        LetterSpacing = Tokens.Fonts.SilkscreenTracking;
        Foreground = Tokens.Brushes.Silkscreen;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty && Text is { } text)
        {
            var upper = text.ToUpperInvariant();
            if (!string.Equals(text, upper, StringComparison.Ordinal)) Text = upper;
        }
        else if (change.Property == IsLargeProperty)
        {
            FontSize = IsLarge ? Tokens.Fonts.SilkscreenLarge : Tokens.Fonts.Silkscreen;
        }
    }
}

/// <summary>
/// An indicator lamp behind a lens.
/// </summary>
/// <remarks>
/// A lit lamp gets a specular dot, not a bloom. The brief rules out glow, and real lamps read
/// as lit because of the highlight on the lens rather than light spilling past it.
/// </remarks>
public sealed class Lamp : Control
{
    /// <summary>Whether the lamp is lit.</summary>
    public static readonly StyledProperty<bool> IsLitProperty =
        AvaloniaProperty.Register<Lamp, bool>(nameof(IsLit));

    /// <summary>The lamp's colour when lit.</summary>
    public static readonly StyledProperty<Color> LampColorProperty =
        AvaloniaProperty.Register<Lamp, Color>(nameof(LampColor), Tokens.Colors.Record);

    /// <inheritdoc cref="IsLitProperty"/>
    public bool IsLit
    {
        get => GetValue(IsLitProperty);
        set => SetValue(IsLitProperty, value);
    }

    /// <inheritdoc cref="LampColorProperty"/>
    public Color LampColor
    {
        get => GetValue(LampColorProperty);
        set => SetValue(LampColorProperty, value);
    }

    static Lamp() => AffectsRender<Lamp>(IsLitProperty, LampColorProperty);

    /// <summary>Creates a lamp at the token size.</summary>
    public Lamp()
    {
        Width = Tokens.Material.LampSize;
        Height = Tokens.Material.LampSize;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;

        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = size / 2;

        var lens = new SolidColorBrush(LampColor, IsLit ? 1 : Tokens.Material.LampUnlitOpacity);
        context.DrawEllipse(lens, null, centre, radius, radius);

        var rim = new Pen(new SolidColorBrush(Tokens.Colors.Seam, 0.7), Tokens.Border.Hairline);
        context.DrawEllipse(null, rim, centre, radius, radius);

        if (!IsLit) return;

        var specular = new SolidColorBrush(Avalonia.Media.Colors.White, Tokens.Material.LampSpecular);
        var dot = size * 0.15;
        context.DrawEllipse(
            specular, null,
            new Point(centre.X - (radius * 0.3), centre.Y - (radius * 0.32)),
            dot, dot);
    }
}

/// <summary>
/// A transport key: rectangular, chunky, with real travel.
/// </summary>
/// <remarks>
/// Pressed means <i>pressed</i> — the cap sinks and its bevel inverts — rather than merely
/// tinted. That distinction is most of what separates equipment from software.
/// </remarks>
public sealed class TransportKey : Button
{
    /// <summary>Whether this key is latched down.</summary>
    public static readonly StyledProperty<bool> IsEngagedProperty =
        AvaloniaProperty.Register<TransportKey, bool>(nameof(IsEngaged));

    /// <summary>Label colour when engaged.</summary>
    public static readonly StyledProperty<Color> EngagedColorProperty =
        AvaloniaProperty.Register<TransportKey, Color>(nameof(EngagedColor), Tokens.Colors.Record);

    /// <inheritdoc cref="IsEngagedProperty"/>
    public bool IsEngaged
    {
        get => GetValue(IsEngagedProperty);
        set => SetValue(IsEngagedProperty, value);
    }

    /// <inheritdoc cref="EngagedColorProperty"/>
    public Color EngagedColor
    {
        get => GetValue(EngagedColorProperty);
        set => SetValue(EngagedColorProperty, value);
    }

    static TransportKey() => AffectsRender<TransportKey>(IsEngagedProperty, IsPressedProperty);

    /// <summary>Creates a key at the token dimensions.</summary>
    public TransportKey()
    {
        MinWidth = Tokens.Material.KeyMinWidth;
        Height = Tokens.Material.KeyHeight;
        Padding = new Thickness(Tokens.Space.Base, 0);
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
        Background = null;
        BorderBrush = null;
        FontFamily = Tokens.Fonts.Grotesque;
        FontSize = Tokens.Fonts.Silkscreen;
        FontWeight = FontWeight.Medium;
        Foreground = Tokens.Brushes.Ink;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var down = IsPressed || IsEngaged;

        // The cap sinks by the travel distance while held or latched; the shadow it cast
        // collapses with it.
        var sunk = down ? bounds.Translate(new Vector(0, Tokens.Material.KeyTravel)) : bounds;
        var shape = new RoundedRect(sunk, Tokens.Radius.Control);

        var shadowRect = new RoundedRect(
            bounds.Translate(new Vector(0, down ? Tokens.Border.Hairline : Tokens.Material.KeyTravel + Tokens.Border.Hairline)),
            Tokens.Radius.Control);
        context.DrawRectangle(new SolidColorBrush(Avalonia.Media.Colors.Black, down ? 0.22 : 0.35), null, shadowRect);

        context.DrawRectangle(Tokens.Brushes.Cap, null, shape);

        // Bevel: highlight on top when proud, shade on top when pressed.
        var bevelColor = down ? Tokens.Colors.PanelShade : Tokens.Colors.PanelHighlight;
        var bevelPen = new Pen(new SolidColorBrush(bevelColor), Tokens.Border.Bevel);
        context.DrawLine(bevelPen, sunk.TopLeft + new Vector(Tokens.Radius.Control, Tokens.Border.Bevel / 2), sunk.TopRight + new Vector(-Tokens.Radius.Control, Tokens.Border.Bevel / 2));

        var seam = new Pen(new SolidColorBrush(Tokens.Colors.Seam, Tokens.Opacity.Seam), Tokens.Border.Hairline);
        context.DrawRectangle(null, seam, shape);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Foreground is updated HERE, not in Render. Assigning a property during the render
        // pass invalidates the visual mid-pass, and Avalonia throws "Visual was invalidated
        // during the render pass" rather than merely logging it. Render must be a pure
        // function of current state.
        if (change.Property == IsEngagedProperty || change.Property == EngagedColorProperty)
        {
            Foreground = new SolidColorBrush(IsEngaged ? EngagedColor : Tokens.Colors.Ink);
        }
    }
}

/// <summary>
/// A window caption key — minimise, maximise, close — drawn as a small square cap on the
/// chassis rather than the OS's glyph buttons.
/// </summary>
public sealed class CaptionKey : Button
{
    /// <summary>Which glyph the key carries.</summary>
    public enum Glyph
    {
        /// <summary>A short bar.</summary>
        Minimise,

        /// <summary>A hollow square.</summary>
        Maximise,

        /// <summary>A cross.</summary>
        Close,
    }

    /// <summary>The glyph.</summary>
    public static readonly StyledProperty<Glyph> KindProperty =
        AvaloniaProperty.Register<CaptionKey, Glyph>(nameof(Kind));

    /// <inheritdoc cref="KindProperty"/>
    public Glyph Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    static CaptionKey() => AffectsRender<CaptionKey>(KindProperty, IsPressedProperty, IsPointerOverProperty);

    /// <summary>Creates a caption key at the token size.</summary>
    public CaptionKey()
    {
        Width = Tokens.Material.CaptionKeySize;
        Height = Tokens.Material.CaptionKeySize;
        Background = null;
        BorderBrush = null;
        Padding = new Thickness(0);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var sunk = IsPressed ? bounds.Translate(new Vector(0, Tokens.Border.Hairline)) : bounds;
        var shape = new RoundedRect(sunk.Deflate(Tokens.Border.Hairline), Tokens.Radius.Chip);

        var cap = new SolidColorBrush(Tokens.Colors.Cap, IsPointerOver ? 1 : 0.85);
        context.DrawRectangle(cap, null, shape);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Tokens.Colors.Seam, Tokens.Opacity.Dim), Tokens.Border.Hairline), shape);

        var ink = new Pen(new SolidColorBrush(Tokens.Colors.Ink), Tokens.Border.Hairline);
        var c = new Point(sunk.Width / 2, sunk.Height / 2 + (IsPressed ? Tokens.Border.Hairline : 0));
        var r = Tokens.Material.CaptionKeySize * 0.18;

        switch (Kind)
        {
            case Glyph.Minimise:
                context.DrawLine(ink, new Point(c.X - r, c.Y), new Point(c.X + r, c.Y));
                break;
            case Glyph.Maximise:
                context.DrawRectangle(null, ink, new Rect(c.X - r, c.Y - r, 2 * r, 2 * r));
                break;
            case Glyph.Close:
                context.DrawLine(ink, new Point(c.X - r, c.Y - r), new Point(c.X + r, c.Y + r));
                context.DrawLine(ink, new Point(c.X - r, c.Y + r), new Point(c.X + r, c.Y - r));
                break;
        }
    }
}

/// <summary>
/// A VU meter with a real needle and a printed scale.
/// </summary>
/// <remarks>
/// <para>
/// The needle is damped rather than driven straight from the signal. A physical VU movement
/// takes ~300 ms to reach a step and overshoots slightly before settling, and that lag is the
/// instrument's character.
/// </para>
/// <para>
/// The physics live in plain fields stepped by a timer, deliberately kept out of the property
/// system: a styled property invalidated 60 times a second would push a full layout pass each
/// frame for a value only this control's <c>Render</c> ever reads.
/// </para>
/// </remarks>
public sealed class VuMeter : Control
{
    /// <summary>Current input level, 0…1.</summary>
    public static readonly StyledProperty<double> LevelProperty =
        AvaloniaProperty.Register<VuMeter, double>(nameof(Level));

    /// <summary>Whether the meter lamp is lit.</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<VuMeter, bool>(nameof(IsActive));

    /// <summary>Whether to print the scale numbers. Off for the small overlay face.</summary>
    public static readonly StyledProperty<bool> ShowScaleProperty =
        AvaloniaProperty.Register<VuMeter, bool>(nameof(ShowScale), true);

    /// <inheritdoc cref="LevelProperty"/>
    public double Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    /// <inheritdoc cref="IsActiveProperty"/>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <inheritdoc cref="ShowScaleProperty"/>
    public bool ShowScale
    {
        get => GetValue(ShowScaleProperty);
        set => SetValue(ShowScaleProperty, value);
    }

    /// <summary>The scale printing, as fraction of sweep and label. -20 … +3 VU.</summary>
    private static readonly (double At, string Label)[] Scale =
    [
        (0.00, "-20"), (0.22, "-10"), (0.40, "-7"), (0.52, "-5"), (0.63, "-3"),
        (Tokens.Material.MeterZeroPoint, "0"), (0.86, "+3"),
    ];

    private double _needle;
    private double _velocity;
    private DispatcherTimer? _ticker;

    static VuMeter() => AffectsRender<VuMeter>(IsActiveProperty, ShowScaleProperty);

    /// <summary>Creates a meter at the front-panel size.</summary>
    public VuMeter()
    {
        Width = Tokens.Material.MeterWidth;
        Height = Tokens.Material.MeterHeight;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _ticker = new DispatcherTimer(DispatcherPriority.Render) { Interval = Tokens.Motion.Frame };
        _ticker.Tick += (_, _) => { AdvanceNeedle(); InvalidateVisual(); };
        _ticker.Start();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _ticker?.Stop();
        _ticker = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Steps the movement one frame toward the current level.</summary>
    private void AdvanceNeedle()
    {
        var target = Math.Clamp(Level, 0, 1);
        var rising = target > _needle;
        var time = rising ? Tokens.Motion.NeedleAttackSeconds : Tokens.Motion.NeedleReleaseSeconds;

        var stiffness = 1 / time;
        _velocity += (target - _needle) * stiffness * 0.16;
        _velocity *= 0.72;
        _needle = Math.Clamp(_needle + _velocity, 0, 1 + Tokens.Motion.NeedleOvershoot);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var face = new RoundedRect(bounds, Tokens.Radius.Chip);
        context.DrawRectangle(Tokens.Brushes.MeterFace, null, face);

        if (IsActive)
        {
            context.DrawRectangle(new SolidColorBrush(Tokens.Colors.MeterLamp, Tokens.Opacity.MeterWash), null, face);
        }

        // The pivot sits just below the face, so the needle sweeps across it like a real
        // moving-coil movement rather than rotating about its centre.
        var pivot = new Point(bounds.Width / 2, bounds.Height * 1.08);
        var radius = Math.Min(bounds.Width * 0.47, bounds.Height * 0.98);
        var sweep = Tokens.Material.NeedleSweepDegrees * Math.PI / 180;
        var zeroAngle = -sweep / 2 + (sweep * Tokens.Material.MeterZeroPoint);

        // The arc, black to 0 VU and red beyond it — printed, not lit.
        var arcPen = new Pen(new SolidColorBrush(Tokens.Colors.MeterNeedle), Tokens.Border.Hairline);
        var overPen = new Pen(new SolidColorBrush(Tokens.Colors.MeterRed), Tokens.Border.Hairline * 2);
        DrawArc(context, arcPen, pivot, radius * 0.86, -sweep / 2, zeroAngle);
        DrawArc(context, overPen, pivot, radius * 0.86, zeroAngle, sweep / 2);

        // Ticks, and the printed numbers.
        var typeface = new Typeface(Tokens.Fonts.Grotesque, weight: FontWeight.Medium);
        foreach (var (at, label) in Scale)
        {
            var angle = -sweep / 2 + (sweep * at);
            var over = at >= Tokens.Material.MeterZeroPoint;
            var pen = over ? new Pen(new SolidColorBrush(Tokens.Colors.MeterRed), Tokens.Border.Hairline) : arcPen;
            context.DrawLine(pen, Polar(pivot, angle, radius * 0.86), Polar(pivot, angle, radius * 0.94));

            if (!ShowScale) continue;

            var text = new FormattedText(
                label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface,
                Tokens.Fonts.MeterScale, new SolidColorBrush(over ? Tokens.Colors.MeterRed : Tokens.Colors.MeterNeedle));
            var at2 = Polar(pivot, angle, radius * 1.0);
            context.DrawText(text, new Point(at2.X - text.Width / 2, at2.Y - text.Height / 2));
        }

        // Minor ticks between the printed ones.
        for (var tick = 0.05; tick < 1.0; tick += 0.1)
        {
            var angle = -sweep / 2 + (sweep * tick);
            var over = tick >= Tokens.Material.MeterZeroPoint;
            context.DrawLine(over ? overPen : arcPen, Polar(pivot, angle, radius * 0.86), Polar(pivot, angle, radius * 0.90));
        }

        if (ShowScale)
        {
            var vu = new FormattedText(
                "VU", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(Tokens.Fonts.Grotesque, weight: FontWeight.Bold),
                Tokens.Fonts.SilkscreenLarge, new SolidColorBrush(Tokens.Colors.MeterNeedle, Tokens.Opacity.Dim));
            context.DrawText(vu, new Point(bounds.Width / 2 - vu.Width / 2, bounds.Height * 0.56));
        }

        // Needle, and the pivot cover it disappears into. Both clipped to the face: the
        // pivot is below the bezel, and the movement must not draw on the panel.
        using (context.PushClip(bounds))
        {
            var needleAngle = -sweep / 2 + (sweep * _needle);
            var needlePen = new Pen(new SolidColorBrush(Tokens.Colors.MeterNeedle), Tokens.Material.NeedleWidth);
            context.DrawLine(needlePen, pivot, Polar(pivot, needleAngle, radius * 0.97));

            var coverRadius = bounds.Height * 0.16;
            context.DrawEllipse(new SolidColorBrush(Tokens.Colors.MeterNeedle), null, pivot, coverRadius, coverRadius);
        }

        // Bezel: seam outside, a hairline of shade inside so the face reads as recessed.
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Tokens.Colors.Seam), Tokens.Border.Hairline), face);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Avalonia.Media.Colors.Black, Tokens.Opacity.Faint), Tokens.Border.Hairline),
            new RoundedRect(bounds.Deflate(Tokens.Border.Hairline), Tokens.Radius.Chip));
    }

    private static void DrawArc(DrawingContext context, IPen pen, Point pivot, double radius, double from, double to)
    {
        const int steps = 24;
        var previous = Polar(pivot, from, radius);
        for (var i = 1; i <= steps; i++)
        {
            var angle = from + ((to - from) * i / steps);
            var next = Polar(pivot, angle, radius);
            context.DrawLine(pen, previous, next);
            previous = next;
        }
    }

    private static Point Polar(Point origin, double angle, double distance) =>
        new(origin.X + (Math.Sin(angle) * distance), origin.Y - (Math.Cos(angle) * distance));
}

/// <summary>
/// A seven-segment readout, the tape counter.
/// </summary>
/// <remarks>
/// Digits are drawn, not typeset: seven bars per digit with a slight forward slant, unlit
/// segments left faintly visible the way an LCD's are. Accepts digits, a colon, a space and
/// a dash; anything else renders as blank.
/// </remarks>
public sealed class SegmentReadout : Control
{
    /// <summary>The text to show.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<SegmentReadout, string>(nameof(Text), "00:00");

    /// <inheritdoc cref="TextProperty"/>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // Segment order: a top, b top-right, c bottom-right, d bottom, e bottom-left, f top-left, g middle.
    private static readonly Dictionary<char, byte> Glyphs = new()
    {
        ['0'] = 0b0111111, ['1'] = 0b0000110, ['2'] = 0b1011011, ['3'] = 0b1001111,
        ['4'] = 0b1100110, ['5'] = 0b1101101, ['6'] = 0b1111101, ['7'] = 0b0000111,
        ['8'] = 0b1111111, ['9'] = 0b1101111, ['-'] = 0b1000000, [' '] = 0,
    };

    static SegmentReadout() => AffectsMeasure<SegmentReadout>(TextProperty);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0;
        foreach (var ch in Text)
        {
            width += ch == ':' ? Tokens.Material.SegmentColonAdvance : Tokens.Material.SegmentDigitAdvance;
        }
        return new Size(width + (Tokens.Material.SegmentDigitHeight * Tokens.Material.SegmentSlant), Tokens.Material.SegmentDigitHeight);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var lit = new SolidColorBrush(Tokens.Colors.InkOnDeck);
        var ghost = new SolidColorBrush(Tokens.Colors.InkOnDeck, Tokens.Material.SegmentGhostOpacity);

        var x = Tokens.Material.SegmentDigitHeight * Tokens.Material.SegmentSlant;
        foreach (var ch in Text)
        {
            if (ch == ':')
            {
                DrawColon(context, lit, x);
                x += Tokens.Material.SegmentColonAdvance;
                continue;
            }

            var mask = Glyphs.GetValueOrDefault(ch, (byte)0);
            DrawDigit(context, lit, ghost, x, mask);
            x += Tokens.Material.SegmentDigitAdvance;
        }
    }

    private static void DrawDigit(DrawingContext context, IBrush lit, IBrush ghost, double x, byte mask)
    {
        var h = Tokens.Material.SegmentDigitHeight;
        var w = Tokens.Material.SegmentDigitWidth;
        var t = Tokens.Material.SegmentThickness;
        var g = Tokens.Material.SegmentGap;
        var slant = Tokens.Material.SegmentSlant;

        // Each segment as a rectangle in unslanted digit space, then sheared.
        (Rect rect, int bit)[] segments =
        [
            (new Rect(t + g, 0, w - 2 * (t + g), t), 0),                     // a
            (new Rect(w - t, t + g, t, h / 2 - t - 2 * g), 1),               // b
            (new Rect(w - t, h / 2 + g, t, h / 2 - t - 2 * g), 2),           // c
            (new Rect(t + g, h - t, w - 2 * (t + g), t), 3),                 // d
            (new Rect(0, h / 2 + g, t, h / 2 - t - 2 * g), 4),               // e
            (new Rect(0, t + g, t, h / 2 - t - 2 * g), 5),                   // f
            (new Rect(t + g, h / 2 - t / 2, w - 2 * (t + g), t), 6),         // g
        ];

        foreach (var (rect, bit) in segments)
        {
            var on = (mask & (1 << bit)) != 0;
            var shear = (h - rect.Y - rect.Height / 2) * slant;
            context.DrawRectangle(on ? lit : ghost, null, new Rect(x + rect.X + shear, rect.Y, rect.Width, rect.Height));
        }
    }

    private static void DrawColon(DrawingContext context, IBrush lit, double x)
    {
        var h = Tokens.Material.SegmentDigitHeight;
        var t = Tokens.Material.SegmentThickness;
        var slant = Tokens.Material.SegmentSlant;
        var cx = x + (Tokens.Material.SegmentColonAdvance - t) / 2;

        context.DrawRectangle(lit, null, new Rect(cx + (h - h * 0.3) * slant, h * 0.3 - t / 2, t, t));
        context.DrawRectangle(lit, null, new Rect(cx + (h - h * 0.7) * slant, h * 0.7 - t / 2, t, t));
    }
}

/// <summary>
/// A scrolling trace of recent input level, drawn as bars mirrored about a centre line.
/// </summary>
/// <remarks>
/// Reads as a waveform on an oscilloscope rather than a progress bar: it has no end, it
/// scrolls, and it is drawn in readout ink on the deck window. Fed by <see cref="Push"/>.
/// </remarks>
public sealed class LevelTrace : Control
{
    private readonly double[] _history = new double[Tokens.Material.TraceLength];
    private int _head;

    /// <summary>Creates a trace at the token size.</summary>
    public LevelTrace()
    {
        Width = Tokens.Material.TraceLength * (Tokens.Material.TraceBarWidth + Tokens.Material.TraceBarGap);
        Height = Tokens.Material.TraceHeight;
    }

    /// <summary>Appends one reading, 0…1, and redraws.</summary>
    public void Push(double level)
    {
        _history[_head] = Math.Clamp(level, 0, 1);
        _head = (_head + 1) % _history.Length;
        InvalidateVisual();
    }

    /// <summary>Clears the trace.</summary>
    public void Clear()
    {
        Array.Clear(_history);
        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var mid = bounds.Height / 2;
        var pitch = Tokens.Material.TraceBarWidth + Tokens.Material.TraceBarGap;

        var baseline = new Pen(new SolidColorBrush(Tokens.Colors.InkOnDeck, Tokens.Material.SegmentGhostOpacity), Tokens.Border.Hairline);
        context.DrawLine(baseline, new Point(0, mid), new Point(bounds.Width, mid));

        var ink = new SolidColorBrush(Tokens.Colors.InkOnDeck, 0.85);
        for (var i = 0; i < _history.Length; i++)
        {
            var value = _history[(_head + i) % _history.Length];
            if (value <= 0) continue;

            // Square-root so quiet speech is visible; a linear trace is a flat line with
            // occasional spikes.
            var half = Math.Max(Tokens.Border.Hairline, Math.Sqrt(value) * mid);
            context.DrawRectangle(ink, null, new Rect(i * pitch, mid - half, Tokens.Material.TraceBarWidth, half * 2));
        }
    }
}

/// <summary>A run of ventilation slots.</summary>
/// <remarks>
/// Purely decorative, and deliberately so — real equipment has vents and fasteners, and their
/// absence is one of the things that makes software look like software.
/// </remarks>
public sealed class Vents : Control
{
    /// <summary>How many slots to draw.</summary>
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<Vents, int>(nameof(Count), 6);

    /// <inheritdoc cref="CountProperty"/>
    public int Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    static Vents() => AffectsRender<Vents>(CountProperty);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(
        (Count * Tokens.Material.VentSlotWidth) + ((Count - 1) * Tokens.Material.VentSlotGap),
        Tokens.Material.VentSlotHeight);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var brush = new SolidColorBrush(Tokens.Colors.Seam, Tokens.Opacity.Seam);
        var pitch = Tokens.Material.VentSlotWidth + Tokens.Material.VentSlotGap;

        for (var i = 0; i < Count; i++)
        {
            var slot = new Rect(
                i * pitch, 0,
                Tokens.Material.VentSlotWidth, Tokens.Material.VentSlotHeight);
            context.DrawRectangle(brush, null, new RoundedRect(slot, Tokens.Material.VentRadius));
        }
    }
}
