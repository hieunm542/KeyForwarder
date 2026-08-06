using System.Drawing.Drawing2D;

namespace KeyForwarder.Ui;

internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(243, 246, 249);
    public static readonly Color Card = Color.White;
    public static readonly Color Accent = Color.FromArgb(15, 107, 107);
    public static readonly Color AccentDark = Color.FromArgb(11, 82, 82);
    public static readonly Color AccentSoft = Color.FromArgb(224, 240, 240);
    public static readonly Color TextPrimary = Color.FromArgb(26, 34, 39);
    public static readonly Color TextMuted = Color.FromArgb(107, 121, 130);
    public static readonly Color Border = Color.FromArgb(223, 230, 236);
    public static readonly Color FieldBg = Color.FromArgb(247, 250, 251);

    // Point-sized fonts scale with the display DPI, so every measurement derived from
    // them scales too. Cached to keep measurements consistent across the whole form.
    public static readonly Font Body = new("Segoe UI", 9.75f);
    public static readonly Font BodyBold = new("Segoe UI", 9.75f, FontStyle.Bold);
    public static readonly Font Title = new("Segoe UI", 15f, FontStyle.Bold);
    public static readonly Font Subtitle = new("Segoe UI", 9f);
    public static readonly Font Section = new("Segoe UI", 8.25f, FontStyle.Bold);
    public static readonly Font KeyCapFont = new("Segoe UI", 10.5f, FontStyle.Bold);

    public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Paints a rounded, anti-aliased surface. The parent background is filled first so the
    /// corners blend cleanly without Region clipping.
    /// </summary>
    public static void PaintSurface(
        Graphics g,
        Rectangle bounds,
        Color parentBg,
        Color fill,
        Color? border,
        int radius)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(parentBg);

        var r = bounds;
        r.Width -= 1;
        r.Height -= 1;

        using var path = RoundedRect(r, radius);
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, path);

        if (border is { } borderColor)
        {
            using var pen = new Pen(borderColor);
            g.DrawPath(pen, path);
        }
    }
}

/// <summary>
/// DPI-safe sizing helpers. All values are derived from rendered font metrics instead of
/// hard-coded pixels, so the layout holds at 100%, 150% and 200% display scaling.
/// </summary>
internal static class Metrics
{
    private static readonly TextFormatFlags MeasureFlags =
        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

    public static int TextWidth(string text, Font font) =>
        TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), MeasureFlags).Width;

    public static int LineHeight(Font font) =>
        TextRenderer.MeasureText("Ag", font, new Size(int.MaxValue, int.MaxValue), MeasureFlags).Height;

    /// <summary>Spacing unit: a multiple of the body line height.</summary>
    public static int Unit(Font font, double multiple) =>
        Math.Max(1, (int)Math.Round(LineHeight(font) * multiple));
}
