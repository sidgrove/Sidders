using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Murmur.App.Controls;
using Murmur.App.Design;
using Murmur.Core;

namespace Murmur.App.Views;

/// <summary>
/// Past transcriptions: searchable, each copyable and deletable.
/// </summary>
/// <remarks>
/// Rows show which dictionary corrections fired and whether the AI tier rewrote them.
/// Without that the dictionary is invisible and there is no way to tell a rule that works
/// from one that never matches.
/// </remarks>
public sealed class TranscriptionsView : UserControl
{
    private readonly TranscriptStore _store;
    private readonly TextBox _search;
    private readonly StackPanel _list;
    private readonly TextBlock _count;

    /// <summary>Builds the view over <paramref name="store"/>.</summary>
    public TranscriptionsView(TranscriptStore store)
    {
        _store = store;

        _search = Field.Search("Search transcriptions");
        _search.TextChanged += (_, _) => Refresh();
        _search.MaxWidth = Tokens.Layout.ContentMaxWidth / 2;
        _search.HorizontalAlignment = HorizontalAlignment.Right;

        _list = new StackPanel { Spacing = Tokens.Space.Base, Margin = new Thickness(0, Tokens.Space.Roomy, 0, Tokens.Space.Roomy) };
        _count = Text.Caption(string.Empty);

        var clear = new SgButton("Clear all", SgButton.Kind.Quiet, compact: true);
        clear.Click += (_, _) => { _store.Clear(); Refresh(); };

        Content = new DockPanel
        {
            Children =
            {
                Panels.Docked(Gutter(Panels.Split(new Badge("Recent"), _search)), Dock.Top),
                Panels.Docked(Gutter(Panels.Split(_count, clear)), Dock.Bottom),
                // Padding inside the scroll viewer, not margin outside it: the viewer clips to its
                // bounds, and without room the cards' shadows are cut off at the sides.
                new ScrollViewer { Content = _list, Padding = new Thickness(Tokens.Layout.ScrollGutter, 0) },
            },
        };

        // The store changes on the engine's thread when a dictation completes; the list
        // must only be rebuilt on the UI thread.
        _store.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    private static Control Gutter(Control control)
    {
        control.Margin = new Thickness(Tokens.Layout.ScrollGutter, 0);
        return control;
    }

    /// <summary>Puts the caret in the search field.</summary>
    public void FocusSearch()
    {
        _search.Focus();
        _search.SelectAll();
    }

    private void Refresh()
    {
        var records = _store.Search(_search.Text ?? string.Empty);

        _list.Children.Clear();
        _count.Text = $"{_store.Records.Count} recording{(_store.Records.Count == 1 ? "" : "s")}";

        if (records.Count == 0)
        {
            _list.Children.Add(Panels.EmptyState(
                _store.Records.Count == 0 ? "🎙️" : "🔍",
                _store.Records.Count == 0 ? "No recordings yet" : "No matches",
                _store.Records.Count == 0 ? "Hold the push-to-talk key and speak." : "Try a different search."));
            return;
        }

        foreach (var record in records) _list.Children.Add(BuildRow(record));
    }

    private Border BuildRow(TranscriptRecord record)
    {
        var copy = new SgButton("Copy", SgButton.Kind.Ghost, compact: true);
        copy.Click += async (_, _) =>
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null) await clipboard.SetTextAsync(record.Text).ConfigureAwait(true);

            copy.Content = "Copied";
            await Task.Delay(Tokens.Motion.Confirmation).ConfigureAwait(true);
            copy.Content = "Copy";
        };

        var delete = new SgButton("Delete", SgButton.Kind.Danger, compact: true);
        delete.Click += (_, _) => _store.Remove(record.Id);

        var meta = new WrapPanel { ItemSpacing = Tokens.Space.Base, LineSpacing = Tokens.Space.Tight };
        meta.Children.Add(MonoLabel.Make(record.At.ToLocalTime().ToString("HH:mm · d MMM", CultureInfo.CurrentCulture)));
        meta.Children.Add(MonoLabel.Make($"{record.AudioSeconds:0.0}s · {record.ProcessingSeconds * 1000:0} ms"));

        if (record.CleanedBy is { Length: > 0 } model) meta.Children.Add(Pill.Brand($"AI · {model}"));

        if (record.Corrections is { Count: > 0 } corrections)
        {
            foreach (var correction in corrections)
            {
                var label = correction.Count > 1
                    ? $"{correction.From} → {correction.To} ×{correction.Count}"
                    : $"{correction.From} → {correction.To}";
                meta.Children.Add(Pill.Amber(label));
            }
        }

        var actions = Panels.Row(Tokens.Space.Tight, copy, delete);
        actions.VerticalAlignment = VerticalAlignment.Top;

        var body = Panels.Column(Tokens.Space.Base, Text.Reading(record.Text), meta);
        body.Margin = new Thickness(0, 0, Tokens.Space.Roomy, 0);

        var card = Card.Lifting(Panels.Split(body, actions), Tokens.Space.Wide);
        card.CornerRadius = new CornerRadius(Tokens.Radius.CardLarge);
        card.BorderBrush = Tokens.Brushes.Line;
        card.BoxShadow = Tokens.Shadow.Soft;
        return card;
    }
}
