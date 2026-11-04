using Spectre.Console;
using Spectre.Console.Rendering;

namespace TuiDo.Presentation.Documents;

/// <summary>Shows a window of <paramref name="visible"/> lines of <paramref name="inner"/>, starting at line <paramref name="scroll"/>.</summary>
internal sealed class ScrollView(IRenderable inner, int visible, int scroll) : Renderable
{
    /// <summary>Total number of lines of the inner content (known after <see cref="Prepare"/> or the first render).</summary>
    public int Total { get; private set; }

    public int Visible => visible;

    public int Offset { get; private set; }

    /// <summary>Measures the content at the given width so <see cref="Total"/> is available before drawing.</summary>
    public void Prepare(IAnsiConsole console, int width)
    {
        Total = Segment.SplitLines(inner.Render(RenderOptions.Create(console), width)).Count;
        Offset = ClampedOffset();
    }

    protected override Measurement Measure(RenderOptions options, int maxWidth) => new(maxWidth, maxWidth);

    protected override IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        var lines = Segment.SplitLines(inner.Render(options with { Height = null }, maxWidth));
        Total = lines.Count;
        Offset = ClampedOffset();

        var window = lines.Skip(Offset).Take(Math.Max(1, visible)).ToList();
        for (var i = 0; i < window.Count; i++)
        {
            foreach (var segment in window[i]) yield return segment;
            if (i < window.Count - 1) yield return Segment.LineBreak;
        }
    }

    private int ClampedOffset() => Math.Clamp(scroll, 0, Math.Max(0, Total - Math.Max(1, visible)));
}
