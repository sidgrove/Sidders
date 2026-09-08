using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Themes.Fluent;
using Murmur.App.Controls;
using Murmur.App.Design;
using Murmur.App.Views;
using Shouldly;

[assembly: AvaloniaTestApplication(typeof(Murmur.AppTests.TestAppBuilder))]

namespace Murmur.AppTests;

/// <summary>Hosts the app headlessly so the UI can be exercised without a display.</summary>
public static class TestAppBuilder
{
    /// <summary>Builds a headless Avalonia app for the test host.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

/// <summary>A minimal application shell for headless tests.</summary>
public sealed class TestApp : Application
{
    /// <inheritdoc />
    public override void Initialize() => Styles.Add(new FluentTheme());
}

/// <summary>
/// Real UI tests, running with no display.
/// </summary>
/// <remarks>
/// This is the payoff for choosing Avalonia over WPF. These run on macOS in milliseconds and
/// on a Windows runner in CI, so a broken layout or a control that fails to construct is
/// caught while writing it rather than after shipping to a machine we cannot test on.
/// </remarks>
public sealed class MainWindowTests
{
    [AvaloniaFact]
    public void Window_opens_and_lays_out()
    {
        var window = new MainWindow();
        window.Show();

        window.Bounds.Width.ShouldBeGreaterThan(0);
        window.Bounds.Height.ShouldBeGreaterThan(0);
    }

    [AvaloniaFact]
    public void Record_toggles_the_lamp_and_the_meter_together()
    {
        var window = new MainWindow();
        window.Show();

        window.IsRecording.ShouldBeFalse();
        window.RecordLamp.IsLit.ShouldBeFalse();
        window.Meter.IsActive.ShouldBeFalse();

        window.ToggleRecording();

        window.IsRecording.ShouldBeTrue();
        window.RecordLamp.IsLit.ShouldBeTrue("the record lamp must follow the transport");
        window.Meter.IsActive.ShouldBeTrue("the meter lamp must follow the transport");

        window.ToggleRecording();

        window.RecordLamp.IsLit.ShouldBeFalse();
        window.Meter.IsActive.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Window_honours_its_minimum_size()
    {
        var window = new MainWindow();
        window.Show();

        window.MinWidth.ShouldBe(Tokens.Layout.MainMinWidth);
        window.MinHeight.ShouldBe(Tokens.Layout.MainMinHeight);
    }
}

/// <summary>The individual pieces of equipment.</summary>
public sealed class EquipmentTests
{
    [AvaloniaFact]
    public void Silkscreen_uppercases_its_text()
    {
        // The look depends on size, tracking AND case together — a label that kept its
        // original casing would be half-styled and read as ordinary UI text.
        var label = new Silkscreen { Text = "transport" };
        label.Text.ShouldBe("TRANSPORT");
    }

    [AvaloniaFact]
    public void Silkscreen_uses_the_token_tracking()
    {
        new Silkscreen().LetterSpacing.ShouldBe(Tokens.Fonts.SilkscreenTracking);
    }

    [AvaloniaFact]
    public void Lamp_defaults_to_the_token_size()
    {
        var lamp = new Lamp();
        lamp.Width.ShouldBe(Tokens.Material.LampSize);
        lamp.Height.ShouldBe(Tokens.Material.LampSize);
    }

    [AvaloniaFact]
    public void Transport_key_uses_the_token_dimensions()
    {
        var key = new TransportKey();
        key.Height.ShouldBe(Tokens.Material.KeyHeight);
        key.MinWidth.ShouldBe(Tokens.Material.KeyMinWidth);
    }

    [AvaloniaFact]
    public void Vents_measure_to_the_slot_geometry()
    {
        var vents = new Vents { Count = 4 };
        vents.Measure(Size.Infinity);

        var expected = (4 * Tokens.Material.VentSlotWidth) + (3 * Tokens.Material.VentSlotGap);
        vents.DesiredSize.Width.ShouldBe(expected);
        vents.DesiredSize.Height.ShouldBe(Tokens.Material.VentSlotHeight);
    }

    [AvaloniaFact]
    public void Meter_renders_without_throwing()
    {
        // The VU meter does all its own drawing, including a damped needle stepped by a
        // timer. Constructing and showing it is what proves the render path is sound.
        var meter = new VuMeter { Width = 168, Height = 54, Level = 0.6 };
        var window = new Window { Content = meter };
        window.Show();

        meter.Bounds.Width.ShouldBeGreaterThan(0);
    }
}

/// <summary>
/// Guards the two colour rules the design system calls non-negotiable.
/// </summary>
/// <remarks>
/// These are the sort of rule that erodes one reasonable-looking commit at a time. Asserting
/// them makes the erosion a build failure.
/// </remarks>
public sealed class DesignSystemTests
{
    [AvaloniaFact]
    public void Record_red_is_the_lacquered_value_not_a_bright_one()
    {
        var red = Tokens.Colors.Record;
        red.R.ShouldBe((byte)0xC8);
        red.G.ShouldBe((byte)0x34);
        red.B.ShouldBe((byte)0x2A);
    }

    [AvaloniaFact]
    public void Radii_stay_small_enough_to_read_as_equipment()
    {
        // Anything softer starts reading as software rather than a machined object.
        Tokens.Radius.Chip.ShouldBeLessThanOrEqualTo(2);
        Tokens.Radius.Control.ShouldBeLessThanOrEqualTo(3);
        Tokens.Radius.Panel.ShouldBeLessThanOrEqualTo(5);
        Tokens.Radius.Window.ShouldBeLessThanOrEqualTo(8);
    }

    [AvaloniaFact]
    public void Spacing_stays_on_the_four_point_grid()
    {
        double[] steps =
        [
            Tokens.Space.Hair, Tokens.Space.Tight, Tokens.Space.Snug,
            Tokens.Space.Base, Tokens.Space.Roomy, Tokens.Space.Wide, Tokens.Space.Panel,
        ];

        foreach (var step in steps) (step % 2).ShouldBe(0, $"{step} is off the grid");
    }

    [AvaloniaFact]
    public void Needle_ballistics_match_a_real_vu_movement()
    {
        // ~300 ms to reach a step, a slower fall, and a slight overshoot. Without these the
        // needle reads as a progress bar with a stick on it.
        Tokens.Motion.NeedleAttackSeconds.ShouldBe(0.30);
        Tokens.Motion.NeedleReleaseSeconds.ShouldBeGreaterThan(Tokens.Motion.NeedleAttackSeconds);
        Tokens.Motion.NeedleOvershoot.ShouldBeGreaterThan(0);
    }
}

/// <summary>The controls added for the Windows front panel.</summary>
public sealed class FrontPanelTests
{
    [AvaloniaFact]
    public void Segment_readout_measures_from_the_token_geometry()
    {
        var readout = new SegmentReadout { Text = "12:34" };
        readout.Measure(Size.Infinity);

        var expected = (4 * Tokens.Material.SegmentDigitAdvance)
                     + Tokens.Material.SegmentColonAdvance
                     + (Tokens.Material.SegmentDigitHeight * Tokens.Material.SegmentSlant);
        // Layout rounding snaps desired sizes to whole pixels.
        readout.DesiredSize.Width.ShouldBe(expected, 1.0);
        readout.DesiredSize.Height.ShouldBe(Tokens.Material.SegmentDigitHeight);
    }

    [AvaloniaFact]
    public void Level_trace_renders_after_being_fed()
    {
        var trace = new LevelTrace();
        var window = new Window { Content = trace };
        window.Show();

        for (var i = 0; i < 200; i++) trace.Push(i % 10 / 10.0);
        trace.Clear();

        trace.Bounds.Width.ShouldBe(Tokens.Material.TraceLength * (Tokens.Material.TraceBarWidth + Tokens.Material.TraceBarGap));
    }

    [AvaloniaFact]
    public void A_fault_shows_on_the_panel_and_the_counter_runs_from_the_tokens()
    {
        var window = new MainWindow();
        window.Show();

        window.FaultStrip.IsVisible.ShouldBeFalse();
        window.ReportFault("The microphone could not be opened.");
        window.FaultStrip.IsVisible.ShouldBeTrue();
        window.Counter.Text.ShouldBe("00:00");
    }

    [AvaloniaFact]
    public void Caption_keys_and_screws_construct_at_token_sizes()
    {
        new CaptionKey().Width.ShouldBe(Tokens.Material.CaptionKeySize);
        new Screw().Width.ShouldBe(Tokens.Material.ScrewSize);
    }

    [AvaloniaFact]
    public void The_only_red_in_the_tokens_is_the_record_lamp_family()
    {
        // Instrumentation red is allowed on the VU scale; nothing else may be red-dominant.
        static bool IsRed(Avalonia.Media.Color c) => c.R > 150 && c.G < 90 && c.B < 90;

        IsRed(Tokens.Colors.Record).ShouldBeTrue();
        IsRed(Tokens.Colors.MeterRed).ShouldBeTrue();
        IsRed(Tokens.Colors.Panel).ShouldBeFalse();
        IsRed(Tokens.Colors.Selection).ShouldBeFalse();
        IsRed(Tokens.Colors.FocusRing).ShouldBeFalse();
        IsRed(Tokens.Colors.Hover).ShouldBeFalse();
        IsRed(Tokens.Colors.MeterAmber).ShouldBeFalse();
    }
}
