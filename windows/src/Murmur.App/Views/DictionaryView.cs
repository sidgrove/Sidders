using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Murmur.App.Controls;
using Murmur.App.Design;
using Murmur.Core;
using Murmur.Dictionary;

namespace Murmur.App.Views;

/// <summary>
/// The dictionary: add, edit, delete, search.
/// </summary>
/// <remarks>
/// Both entry kinds live in one list — they are two shapes of the same idea, and you want to
/// see everything you have taught it at once. The kind is carried by a badge on each row.
/// </remarks>
public sealed class DictionaryView : UserControl
{
    private readonly DictionaryFile _file;
    private readonly TextBox _search;
    private readonly StackPanel _list;
    private readonly TextBlock _count;

    /// <summary>Builds the view over <paramref name="file"/>.</summary>
    public DictionaryView(DictionaryFile file)
    {
        _file = file;

        _search = Field.Search("Search dictionary");
        _search.TextChanged += (_, _) => Refresh();
        _search.Width = Tokens.Layout.ContentMaxWidth / 3;

        var add = new SgButton("Add word", SgButton.Kind.Primary);
        add.Click += (_, _) => ShowEditor(null);

        _list = new StackPanel { Spacing = Tokens.Space.Snug, Margin = new Thickness(0, Tokens.Space.Roomy, 0, Tokens.Space.Roomy) };
        _count = Text.Caption(string.Empty);

        var open = new SgButton("Open dictionary.txt", SgButton.Kind.Quiet, compact: true);
        open.Click += (_, _) => OpenInEditor(_file.FilePath);

        Content = new DockPanel
        {
            Children =
            {
                Panels.Docked(Gutter(Panels.Split(new Badge("Dictionary"), Panels.Row(Tokens.Space.Base, _search, add))), Dock.Top),
                Panels.Docked(Gutter(Panels.Split(_count, open)), Dock.Bottom),
                new ScrollViewer { Content = _list, Padding = new Thickness(Tokens.Layout.ScrollGutter * 2, 0, Tokens.Layout.ScrollGutter * 2, Tokens.Layout.ScrollGutter * 3) },
            },
        };

        _file.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    private static Control Gutter(Control control)
    {
        control.Margin = new Thickness(Tokens.Layout.ScrollGutter * 2, 0);
        return control;
    }

    /// <summary>Puts the caret in the search field.</summary>
    public void FocusSearch()
    {
        _search.Focus();
        _search.SelectAll();
    }

    /// <summary>Opens the editor on a blank entry.</summary>
    public void AddEntry() => ShowEditor(null);

    private void Refresh()
    {
        var entries = _file.Search(_search.Text ?? string.Empty);

        _list.Children.Clear();
        _count.Text = $"{_file.Entries.Count} {(_file.Entries.Count == 1 ? "entry" : "entries")}  ·  edit the file by hand if you like";

        if (entries.Count == 0)
        {
            _list.Children.Add(Panels.EmptyState(
                _file.Entries.Count == 0 ? "📖" : "🔍",
                _file.Entries.Count == 0 ? "Dictionary is empty" : "No matches",
                _file.Entries.Count == 0 ? "Add names, jargon and product names it keeps getting wrong." : "Try a different search."));
            return;
        }

        foreach (var entry in entries) _list.Children.Add(BuildRow(entry));
    }

    private Border BuildRow(DictionaryEntry entry)
    {
        var toggle = new Switch { IsChecked = entry.IsEnabled, VerticalAlignment = VerticalAlignment.Center };
        toggle.IsCheckedChanged += (_, _) => _file.Update(entry with { IsEnabled = toggle.IsChecked == true });

        var edit = new SgButton("Edit", SgButton.Kind.Ghost, compact: true);
        edit.Click += (_, _) => ShowEditor(entry);

        var delete = new SgButton("Delete", SgButton.Kind.Danger, compact: true);
        delete.Click += (_, _) => _file.Remove(entry.Id);

        var kind = entry.Kind == EntryKind.Correction ? Pill.Amber("Fix") : Pill.Brand("Word");
        Control text = entry.Kind == EntryKind.Correction
            ? Panels.Row(Tokens.Space.Snug, Text.Muted(entry.Hear), Text.Caption("→"), Text.BodyStrong(entry.Write))
            : Text.BodyStrong(entry.Write);

        var left = Panels.Row(Tokens.Space.Base, kind, text);
        var right = Panels.Row(Tokens.Space.Snug, edit, delete, toggle);

        var card = Card.Standard(Panels.Split(left, right), Tokens.Space.Roomy);
        card.CornerRadius = new CornerRadius(Tokens.Radius.CardLarge);
        card.BorderBrush = Tokens.Brushes.Line;
        card.BoxShadow = Tokens.Shadow.Soft;
        card.Opacity = entry.IsEnabled ? 1 : Tokens.Opacity.Disabled;
        return card;
    }

    private void ShowEditor(DictionaryEntry? entry)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var editor = new DictionaryEditorWindow(entry);
        editor.Saved += (_, saved) => { if (entry is null) _file.Add(saved); else _file.Update(saved); };
        _ = editor.ShowDialog(owner);
    }

    /// <summary>Opens the dictionary in the user's default text editor.</summary>
    private static void OpenInEditor(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, string.Empty);
            }

            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true };
            process.Start();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            Log.Warn($"could not open {path}: {e.Message}");
        }
    }
}

/// <summary>Add or edit one dictionary entry, with the false-positive warning shown live.</summary>
public sealed class DictionaryEditorWindow : ShellWindow
{
    private readonly Segmented _kind;
    private readonly TextBox _hear;
    private readonly TextBox _write;
    private readonly StackPanel _hearField;
    private readonly TextBlock _writeLabel;
    private readonly StackPanel _warnings;
    private readonly SgButton _save;
    private readonly Guid _id;
    private readonly bool _wasEnabled;

    private EntryKind _entryKind;

    /// <summary>Raised when the user saves.</summary>
    public event EventHandler<DictionaryEntry>? Saved;

    /// <summary>Creates the editor for a new or existing entry.</summary>
    public DictionaryEditorWindow(DictionaryEntry? entry)
    {
        _id = entry?.Id ?? Guid.NewGuid();
        _wasEnabled = entry?.IsEnabled ?? true;
        _entryKind = entry?.Kind ?? EntryKind.Term;

        Title = entry is null ? "New entry" : "Edit entry";
        IsSheet = true;
        Width = Tokens.Layout.DialogWidth;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _kind = new Segmented(["A word to know", "A correction"], _entryKind == EntryKind.Correction ? 1 : 0);
        _kind.Selected += (_, i) => SetKind(i == 1 ? EntryKind.Correction : EntryKind.Term);

        _hear = Field.Text("what it hears, e.g. cloud code", entry?.Hear);
        _write = Field.Text("what to write, e.g. Claude Code", entry?.Write);
        _hear.TextChanged += (_, _) => Revalidate();
        _write.TextChanged += (_, _) => Revalidate();

        _hearField = Panels.Labelled("When it hears", _hear);
        _writeLabel = Text.Eyebrow("Word or phrase");
        _warnings = new StackPanel { Spacing = Tokens.Space.Snug };

        var cancel = new SgButton("Cancel", SgButton.Kind.Ghost);
        cancel.Click += (_, _) => Close();
        _save = new SgButton("Save", SgButton.Kind.Primary);
        _save.Click += (_, _) => { if (IsValid) { Saved?.Invoke(this, Draft); Close(); } };

        var buttons = Panels.Row(Tokens.Space.Snug, cancel, _save);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;

        var body = Panels.Column(Tokens.Space.Roomy,
            _kind,
            _hearField,
            Panels.Column(Tokens.Space.Chip, _writeLabel, _write),
            _warnings,
            buttons);
        body.Margin = new Thickness(Tokens.Space.Wide, Tokens.Space.Snug, Tokens.Space.Wide, Tokens.Space.Wide);

        Content = Frame(Title, body);
        SetKind(_entryKind);
    }

    private DictionaryEntry Draft => new()
    {
        Id = _id,
        Kind = _entryKind,
        Write = (_write.Text ?? string.Empty).Trim(),
        Hear = _entryKind == EntryKind.Correction ? (_hear.Text ?? string.Empty).Trim() : string.Empty,
        IsEnabled = _wasEnabled,
    };

    private bool IsValid => Draft.Write.Length > 0 && (_entryKind == EntryKind.Term || Draft.Hear.Length > 0);

    private void SetKind(EntryKind kind)
    {
        _entryKind = kind;
        _hearField.IsVisible = kind == EntryKind.Correction;
        _writeLabel.Text = (kind == EntryKind.Correction ? "Write instead" : "Word or phrase").ToUpperInvariant();
        Revalidate();
    }

    private void Revalidate()
    {
        _warnings.Children.Clear();
        foreach (var warning in DictionaryWarning.Check(Draft))
        {
            var text = Text.Body(warning.Message);
            text.Foreground = Tokens.Brushes.Amber;
            _warnings.Children.Add(Card.Notice(text, Tokens.Brushes.AmberLight, new Avalonia.Media.SolidColorBrush(Tokens.Colors.AmberMid, Tokens.Opacity.FocusBorder)));
        }

        _save.IsEnabled = IsValid;
    }
}
