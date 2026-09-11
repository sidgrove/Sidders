using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Murmur.App.Views;
using Shouldly;

namespace Murmur.AppTests;

/// <summary>The listening preview stays compact and shows more than a single line.</summary>
public sealed class OverlayLayoutTests
{
    [AvaloniaFact]
    public void Send_feedback_is_nonactivating_and_new_recording_preempts_it()
    {
        var overlay = new OverlayWindow(null);
        try
        {
            overlay.ShowSendFeedback();
            overlay.IsShowingSendFeedback.ShouldBeTrue();
            overlay.ShowActivated.ShouldBeFalse();
            overlay.IsHitTestVisible.ShouldBeFalse();
            overlay.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Sent ↗").ShouldBeTrue();
            overlay.Sync(true, false, false, 0.5, "00:01", "next recording");
            overlay.IsShowingSendFeedback.ShouldBeFalse();
            overlay.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Listening").ShouldBeTrue();
        }
        finally { overlay.Close(); }
    }
    [AvaloniaFact]
    public void Presenting_reasserts_always_on_top_every_time()
    {
        var tweaks = new RecordingTweaks();
        var overlay = new OverlayWindow(tweaks);
        try
        {
            overlay.Present();
            tweaks.KeepOnTopCalls.ShouldBe(1, "a shown pill must be pushed back into the topmost band");
            overlay.Present();
            tweaks.KeepOnTopCalls.ShouldBe(2, "a pill that stays shown can still be demoted, so every sync re-asserts");
        }
        finally { overlay.Close(); }
    }

    private sealed class RecordingTweaks : Murmur.Abstractions.IWindowTweaks
    {
        public int KeepOnTopCalls { get; private set; }
        public void MakeNonActivating(nint handle) { }
        public void KeepOnTop(nint handle) => KeepOnTopCalls++;
        public (int X, int Y)? ActiveWindowCentre() => null;
    }

    [AvaloniaFact]
    public void Empty_preview_is_small_and_only_words_expand_it()
    {
        var overlay = new OverlayWindow(null);
        try
        {
            overlay.Sync(true, false, false, 0.5, "00:01", "");
            overlay.Show();
            overlay.UpdateLayout();
            var compact = overlay.Height;
            compact.ShouldBeLessThan(160);
            overlay.Sync(true, false, false, 0.5, "00:02", "These words should reveal the transcript area.");
            overlay.Height.ShouldBeGreaterThan(compact);
            overlay.Sync(true, false, false, 0.5, "00:03", "");
            overlay.Height.ShouldBe(compact);
        }
        finally { overlay.Close(); }
    }

    [AvaloniaFact]
    public void Long_preview_wraps_in_a_compact_nonactivating_window()
    {
        var overlay = new OverlayWindow(null);
        try
        {
            overlay.Sync(true, false, false, 0.1, "00:08", string.Join(" ", Enumerable.Repeat("These are the words being dictated into another application.", 6)));
            overlay.Show();
            overlay.UpdateLayout();
            overlay.UpdateLayout();
            var scroll = overlay.GetVisualDescendants().OfType<ScrollViewer>().Single();
            scroll.Offset.Y.ShouldBe(Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height), 0.1, "the newest words must stay in view");
            overlay.Bounds.Width.ShouldBeLessThan(480);
            overlay.ShowActivated.ShouldBeFalse();
            var preview = overlay.GetVisualDescendants().OfType<TextBlock>().MaxBy(t => t.Text?.Length ?? 0)!;
            preview.Text!.Length.ShouldBeGreaterThan(140);
            preview.Bounds.Height.ShouldBeGreaterThan(35);
        }
        finally { overlay.Close(); }
    }
}
