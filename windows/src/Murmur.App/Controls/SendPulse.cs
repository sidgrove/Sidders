using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Murmur.App.Design;

namespace Murmur.App.Controls;

/// <summary>A travelling multicoloured wave used only after auto-send succeeds.</summary>
public sealed class SendPulse : Control
{
    /// <summary>Elapsed animation time, advanced by the overlay while visible.</summary>
    public double Elapsed { get; set; }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var step = Bounds.Width / Tokens.SendFeedback.Bars;
        for (var i = 0; i < Tokens.SendFeedback.Bars; i++)
        {
            var wave = (Math.Sin(i * Tokens.SendFeedback.Phase - Elapsed * Tokens.SendFeedback.Speed) + 1) / 2;
            var height = Bounds.Height * (Tokens.SendFeedback.Floor + (1 - Tokens.SendFeedback.Floor) * wave);
            var brush = Tokens.SendFeedback.Palette[i * Tokens.SendFeedback.Palette.Count / Tokens.SendFeedback.Bars];
            context.DrawRectangle(brush, null,
                new Rect(i * step, (Bounds.Height - height) / 2, Math.Max(0, step - Tokens.SendFeedback.Gap), height),
                Tokens.SendFeedback.Radius, Tokens.SendFeedback.Radius);
        }
    }
}