using KeyForwarder.Ui;

namespace KeyForwarder.Settings;

public sealed class SettingsForm : Form
{
    // Longest combination the key display must show without ellipsis.
    private const string WidestHotkeyText = "Ctrl+Alt+Shift+F12";

    private readonly int _em;
    private readonly int _pad;
    private readonly int _gap;
    private readonly int _controlHeight;
    private readonly int _keyCapWidth;
    private readonly int _buttonWidth;
    private readonly int _numericWidth;
    private readonly int _footerHeight;
    private readonly int _minContentWidth;

    private readonly TableLayoutPanel _root;
    private readonly HeaderBar _header;
    private readonly Panel _bodyScroll;
    private readonly TableLayoutPanel _bodyStack;
    private readonly Panel _footer;

    private readonly NumericUpDown _delay;
    private readonly NumericUpDown _warnLength;
    private readonly CheckBox _startWithWindows;
    private readonly CheckBox _enabled;
    private readonly CheckBox _lowLevelHook;
    private readonly KeyCap _typeKeyCap;
    private readonly KeyCap _cancelKeyCap;
    private readonly Label _hint;

    private HotkeyBinding _typeBinding;
    private HotkeyBinding _cancelBinding;

    private enum CaptureTarget { None, Type, Cancel }
    private CaptureTarget _capture = CaptureTarget.None;
    private bool _applied;

    public AppSettings ResultSettings { get; private set; }

    public SettingsForm(AppSettings current)
    {
        ArgumentNullException.ThrowIfNull(current);

        ResultSettings = Clone(current);
        _typeBinding = CloneBinding(current.TypeHotkey);
        _cancelBinding = CloneBinding(current.CancelHotkey);

        SuspendLayout();

        Text = "KeyForwarder Settings";
        Icon = AppIcon.Get();
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        // Sizes are measured from font metrics below, so WinForms must not rescale anything.
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Bg;
        Font = Theme.Body;
        ForeColor = Theme.TextPrimary;
        KeyPreview = true;

        _em = Metrics.LineHeight(Theme.Body);
        _pad = Metrics.Unit(Theme.Body, 0.95);
        _gap = Metrics.Unit(Theme.Body, 0.45);
        _controlHeight = Metrics.Unit(Theme.Body, 2.0);
        _keyCapWidth = Metrics.TextWidth(WidestHotkeyText, Theme.KeyCapFont) + _em;
        _buttonWidth = Metrics.TextWidth("Change", Theme.Body) + (int)(_em * 1.8);
        _numericWidth = Metrics.TextWidth("1000000", Theme.Body) + (int)(_em * 2.0);
        _footerHeight = _controlHeight + _pad + _gap;
        _minContentWidth = Metrics.Unit(Theme.Body, 27);

        _typeKeyCap = NewKeyCap(_typeBinding.ToString());
        _cancelKeyCap = NewKeyCap(_cancelBinding.ToString());

        _enabled = NewCheckBox("Enable typing hotkey", current.Enabled);
        _startWithWindows = NewCheckBox("Start with Windows", current.StartWithWindows);
        _lowLevelHook = NewCheckBox("Work inside remote desktop sessions", current.UseLowLevelHook);

        _delay = NewNumeric(0, 500, current.DelayMs);
        _warnLength = NewNumeric(0, 1_000_000, current.WarnLength, 500);

        _hint = new Label
        {
            Text = DefaultHint,
            AutoSize = true,
            ForeColor = Theme.TextMuted,
            BackColor = Theme.Card,
            Margin = new Padding(0, _gap, 0, 0)
        };

        _header = BuildHeader();
        (_bodyScroll, _bodyStack) = BuildBody();
        _footer = BuildFooter();

        _root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Bg,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        // Absolute: a docked Panel reports no useful preferred height for an AutoSize row.
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, _footerHeight));
        _root.Controls.Add(_header, 0, 0);
        _root.Controls.Add(_bodyScroll, 0, 1);
        _root.Controls.Add(_footer, 0, 2);

        Controls.Add(_root);

        KeyDown += OnFormKeyDown;
        FormClosing += OnFormClosing;

        ResumeLayout(performLayout: true);
    }

    private static string DefaultHint => "Click Change, then press your combination.";

    /// <summary>
    /// Sizes the window to the measured content once fonts and DPI are final.
    /// </summary>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        var work = Screen.FromPoint(Cursor.Position).WorkingArea;
        var headerSize = _header.PreferredSize;
        var stackSize = _bodyStack.PreferredSize;

        var bodyPaddingH = _bodyScroll.Padding.Horizontal;
        var bodyPaddingV = _bodyScroll.Padding.Vertical;

        var contentWidth = Math.Max(
            Math.Max(headerSize.Width, stackSize.Width + bodyPaddingH),
            _minContentWidth);
        var contentHeight = headerSize.Height + stackSize.Height + bodyPaddingV + _footerHeight;

        var chrome = new Size(Width - ClientSize.Width, Height - ClientSize.Height);
        var maxWidth = work.Width - chrome.Width - _em;
        var maxHeight = (int)(work.Height * 0.92) - chrome.Height;

        var width = Math.Min(contentWidth, maxWidth);
        var height = Math.Min(contentHeight, maxHeight);

        // Reserve room for the scrollbar when the content has to scroll.
        if (contentHeight > height)
        {
            width = Math.Min(width + SystemInformation.VerticalScrollBarWidth, maxWidth);
        }

        ClientSize = new Size(width, height);
        MinimumSize = new Size(
            width + chrome.Width,
            Math.Min(height, Metrics.Unit(Theme.Body, 16)) + chrome.Height);

        CenterToScreen();
    }

    private HeaderBar BuildHeader()
    {
        var header = new HeaderBar
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(_pad, _pad, _pad, _pad),
            Margin = Padding.Empty
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var logoSize = Metrics.Unit(Theme.Body, 2.2);
        var logo = new PictureBox
        {
            Image = AppIcon.Get().ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(logoSize, logoSize),
            Margin = new Padding(0, (int)(_gap * 0.5), _pad, 0),
            BackColor = Color.Transparent
        };
        grid.Controls.Add(logo, 0, 0);
        grid.SetRowSpan(logo, 2);

        grid.Controls.Add(new Label
        {
            Text = "KeyForwarder",
            Font = Theme.Title,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            AutoSize = true,
            Margin = Padding.Empty
        }, 1, 0);

        grid.Controls.Add(new Label
        {
            Text = "Types your clipboard into the focused window",
            Font = Theme.Subtitle,
            ForeColor = Color.FromArgb(198, 229, 229),
            BackColor = Color.Transparent,
            AutoSize = true,
            Margin = new Padding(0, (int)(_gap * 0.4), 0, 0)
        }, 1, 1);

        header.Controls.Add(grid);
        return header;
    }

    private (Panel scroll, TableLayoutPanel stack) BuildBody()
    {
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Theme.Bg,
            Padding = new Padding(_pad, _pad, _pad, _gap),
            Margin = Padding.Empty
        };

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Theme.Bg,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        stack.Controls.Add(BuildHotkeysCard());
        stack.Controls.Add(BuildTypingCard());
        stack.Controls.Add(BuildGeneralCard());

        scroll.Controls.Add(stack);
        return (scroll, stack);
    }

    private Control BuildHotkeysCard()
    {
        var (card, grid) = NewCard("Hotkeys");

        AddRow(grid,
            NewRowLabel("Type clipboard"),
            _typeKeyCap,
            NewChangeButton(() => BeginCapture(CaptureTarget.Type)));

        AddRow(grid,
            NewRowLabel("Cancel typing"),
            _cancelKeyCap,
            NewChangeButton(() => BeginCapture(CaptureTarget.Cancel)));

        var hintRow = AddRow(grid, _hint, null, null);
        grid.SetColumnSpan(_hint, 3);
        _ = hintRow;

        return card;
    }

    private Control BuildTypingCard()
    {
        var (card, grid) = NewCard("Typing");
        AddRow(grid, NewRowLabel("Delay between keys"), _delay, NewUnitLabel("ms"));
        AddRow(grid, NewRowLabel("Confirm above length"), _warnLength, NewUnitLabel("chars"));
        return card;
    }

    private Control BuildGeneralCard()
    {
        var (card, grid) = NewCard("General");

        AddRow(grid, _enabled, null, null);
        grid.SetColumnSpan(_enabled, 3);

        AddRow(grid, _startWithWindows, null, null);
        grid.SetColumnSpan(_startWithWindows, 3);

        AddRow(grid, _lowLevelHook, null, null);
        grid.SetColumnSpan(_lowLevelHook, 3);

        var note = new Label
        {
            Text = "Uses a keyboard hook so RDP and Citrix cannot steal the hotkey.\n"
                   + "Turn off only if another tool conflicts.",
            AutoSize = true,
            ForeColor = Theme.TextMuted,
            BackColor = Theme.Card,
            Margin = new Padding(_em, 0, 0, _gap)
        };
        AddRow(grid, note, null, null);
        grid.SetColumnSpan(note, 3);

        return card;
    }

    /// <summary>
    /// A card with a section heading. Every row shares the same three columns
    /// (label / control / trailing) so values line up across rows.
    /// </summary>
    private (Card card, TableLayoutPanel grid) NewCard(string section)
    {
        var card = new Card
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(_pad, (int)(_pad * 0.8), _pad, (int)(_pad * 0.85)),
            Margin = new Padding(0, 0, 0, _pad)
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Theme.Card,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = section.ToUpperInvariant(),
            Font = Theme.Section,
            ForeColor = Theme.Accent,
            BackColor = Theme.Card,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, _gap)
        };
        grid.Controls.Add(title, 0, 0);
        grid.SetColumnSpan(title, 3);

        card.Controls.Add(grid);
        return (card, grid);
    }

    private static int AddRow(TableLayoutPanel grid, Control? first, Control? second, Control? third)
    {
        var row = grid.RowCount;
        grid.RowCount = row + 1;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        if (first is not null)
        {
            grid.Controls.Add(first, 0, row);
        }

        if (second is not null)
        {
            grid.Controls.Add(second, 1, row);
        }

        if (third is not null)
        {
            grid.Controls.Add(third, 2, row);
        }

        return row;
    }

    private Panel BuildFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Bg,
            Padding = new Padding(_pad, _gap, _pad, _pad),
            Height = _controlHeight + _pad + _gap,
            Margin = Padding.Empty
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Theme.Bg,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var actionWidth = Math.Max(
            _buttonWidth,
            Metrics.TextWidth("Cancel", Theme.Body) + (int)(_em * 1.8));

        var save = new FlatButton
        {
            Text = "Save",
            Primary = true,
            Size = new Size(actionWidth, _controlHeight),
            Margin = new Padding(_gap, 0, 0, 0)
        };
        save.Click += (_, _) =>
        {
            if (_capture != CaptureTarget.None)
            {
                EndCapture();
            }

            ApplyToResult();
            _applied = true;
            DialogResult = DialogResult.OK;
            Close();
        };

        var cancel = new FlatButton
        {
            Text = "Cancel",
            Size = new Size(actionWidth, _controlHeight),
            Margin = new Padding(_gap, 0, 0, 0)
        };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        flow.Controls.Add(save);
        flow.Controls.Add(cancel);
        footer.Controls.Add(flow);
        return footer;
    }

    private KeyCap NewKeyCap(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Size = new Size(_keyCapWidth, _controlHeight),
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 0, _gap, _gap)
    };

    private FlatButton NewChangeButton(Action onClick)
    {
        var button = new FlatButton
        {
            Text = "Change",
            Size = new Size(_buttonWidth, _controlHeight),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 0, _gap)
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private Label NewRowLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Theme.TextPrimary,
        BackColor = Theme.Card,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 0, _pad, _gap)
    };

    private Label NewUnitLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Theme.TextMuted,
        BackColor = Theme.Card,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 0, 0, _gap)
    };

    private CheckBox NewCheckBox(string text, bool isChecked) => new()
    {
        Text = text,
        Checked = isChecked,
        AutoSize = true,
        FlatStyle = FlatStyle.System,
        BackColor = Theme.Card,
        ForeColor = Theme.TextPrimary,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 0, 0, _gap)
    };

    private NumericUpDown NewNumeric(decimal min, decimal max, int value, int increment = 1) => new()
    {
        Minimum = min,
        Maximum = max,
        Increment = increment,
        Value = Math.Clamp(value, (int)min, (int)max),
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Theme.FieldBg,
        ForeColor = Theme.TextPrimary,
        TextAlign = HorizontalAlignment.Center,
        Font = Theme.Body,
        Width = _numericWidth,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 0, _gap, _gap)
    };

    protected override bool ProcessDialogKey(Keys keyData)
    {
        // While capturing, no key may act as a dialog shortcut (Esc/Enter/Tab).
        if (_capture != CaptureTarget.None)
        {
            return false;
        }

        if (keyData == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return true;
        }

        return base.ProcessDialogKey(keyData);
    }

    private void BeginCapture(CaptureTarget target)
    {
        _capture = target;

        _typeKeyCap.Listening = target == CaptureTarget.Type;
        _cancelKeyCap.Listening = target == CaptureTarget.Cancel;

        var cap = target == CaptureTarget.Type ? _typeKeyCap : _cancelKeyCap;
        cap.Text = "Press keys…";
        cap.Focus();

        _hint.Text = target == CaptureTarget.Type
            ? "Listening for Type… Esc keeps the current one."
            : "Listening for Cancel… Esc keeps the current one.";
        _hint.ForeColor = Theme.Accent;
    }

    private void EndCapture()
    {
        _capture = CaptureTarget.None;
        _typeKeyCap.Listening = false;
        _cancelKeyCap.Listening = false;
        _typeKeyCap.Text = _typeBinding.ToString();
        _cancelKeyCap.Text = _cancelBinding.ToString();
        _hint.Text = DefaultHint;
        _hint.ForeColor = Theme.TextMuted;
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (_capture == CaptureTarget.None)
        {
            return;
        }

        e.SuppressKeyPress = true;
        e.Handled = true;

        if (e.KeyCode == Keys.Escape && !e.Control && !e.Alt && !e.Shift)
        {
            EndCapture();
            return;
        }

        if (IsModifierOnly(e.KeyCode))
        {
            return;
        }

        var binding = FromKeyEvent(e);
        if (_capture == CaptureTarget.Type)
        {
            _typeBinding = binding;
        }
        else
        {
            _cancelBinding = binding;
        }

        EndCapture();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK && !_applied)
        {
            ApplyToResult();
            _applied = true;
        }
    }

    private void ApplyToResult()
    {
        ResultSettings = new AppSettings
        {
            Enabled = _enabled.Checked,
            DelayMs = (int)_delay.Value,
            WarnLength = (int)_warnLength.Value,
            StartWithWindows = _startWithWindows.Checked,
            UseLowLevelHook = _lowLevelHook.Checked,
            TypeHotkey = CloneBinding(_typeBinding),
            CancelHotkey = CloneBinding(_cancelBinding)
        };
    }

    private static bool IsModifierOnly(Keys key) =>
        key is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey
            or Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey
            or Keys.Menu or Keys.LMenu or Keys.RMenu
            or Keys.LWin or Keys.RWin;

    private static HotkeyBinding FromKeyEvent(KeyEventArgs e) => new()
    {
        Control = e.Control,
        Alt = e.Alt,
        Shift = e.Shift,
        Win = false,
        VirtualKey = (int)(e.KeyCode & Keys.KeyCode)
    };

    private static AppSettings Clone(AppSettings s) => new()
    {
        Enabled = s.Enabled,
        DelayMs = s.DelayMs,
        WarnLength = s.WarnLength,
        StartWithWindows = s.StartWithWindows,
        UseLowLevelHook = s.UseLowLevelHook,
        TypeHotkey = CloneBinding(s.TypeHotkey),
        CancelHotkey = CloneBinding(s.CancelHotkey)
    };

    private static HotkeyBinding CloneBinding(HotkeyBinding b) => new()
    {
        Control = b.Control,
        Alt = b.Alt,
        Shift = b.Shift,
        Win = b.Win,
        VirtualKey = b.VirtualKey
    };
}
