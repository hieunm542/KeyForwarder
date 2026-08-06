namespace KeyForwarder.Ui;

internal sealed class Card : Panel
{
    public int CornerRadius { get; set; } = 8;

    public Card()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint,
            true);
        BackColor = Theme.Card;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Theme.PaintSurface(
            e.Graphics,
            ClientRectangle,
            Parent?.BackColor ?? Theme.Bg,
            Theme.Card,
            Theme.Border,
            CornerRadius);
    }
}

/// <summary>Rounded display of a hotkey combination, highlighted while capturing.</summary>
internal sealed class KeyCap : Control
{
    private const TextFormatFlags Format =
        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
        | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    private bool _listening;

    public KeyCap()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint | ControlStyles.Selectable,
            true);
        TabStop = false;
        Font = Theme.KeyCapFont;
        ForeColor = Theme.TextPrimary;
    }

    public bool Listening
    {
        get => _listening;
        set
        {
            if (_listening == value)
            {
                return;
            }

            _listening = value;
            Invalidate();
        }
    }

    /// <summary>Every key must reach KeyDown so capture can read Tab, Enter, arrows, etc.</summary>
    protected override bool IsInputKey(Keys keyData) => true;

    protected override void OnPaint(PaintEventArgs e)
    {
        var fill = _listening ? Theme.AccentSoft : Theme.FieldBg;
        var border = _listening ? Theme.Accent : Theme.Border;

        Theme.PaintSurface(e.Graphics, ClientRectangle, Parent?.BackColor ?? Theme.Card, fill, border, 6);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            _listening ? Theme.Accent : ForeColor,
            Format);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }
}

internal sealed class FlatButton : Button
{
    private const TextFormatFlags Format =
        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
        | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    private bool _hover;

    public bool Primary { get; init; }

    public FlatButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint,
            true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = Theme.Body;
        Cursor = Cursors.Hand;
        AutoSize = false;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Color fill;
        Color text;
        Color? border;

        if (Primary)
        {
            fill = _hover ? Theme.AccentDark : Theme.Accent;
            text = Color.White;
            border = null;
        }
        else
        {
            fill = _hover ? Color.FromArgb(238, 243, 246) : Color.White;
            text = Focused ? Theme.Accent : Theme.TextPrimary;
            border = Focused ? Theme.Accent : Theme.Border;
        }

        Theme.PaintSurface(e.Graphics, ClientRectangle, Parent?.BackColor ?? Theme.Bg, fill, border, 6);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Primary ? Theme.BodyBold : Font,
            ClientRectangle,
            text,
            Format);
    }
}

/// <summary>Accent header strip with a subtle horizontal gradient.</summary>
internal sealed class HeaderBar : Panel
{
    public HeaderBar()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint,
            true);
        BackColor = Theme.Accent;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var bounds = ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            bounds,
            Theme.Accent,
            Theme.AccentDark,
            System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(brush, bounds);
    }
}
