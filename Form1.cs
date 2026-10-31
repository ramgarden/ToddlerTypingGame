namespace ToddlerTypingGame;

using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public partial class Form1 : Form
{
    private readonly List<char> _letters = new();
    private int _currentLetterIndex = 0;
    private readonly Random _random = new();
    private bool _gameStarted = false;
    private int _escapePressCount = 0;
    private DateTime _lastEscapePress = DateTime.MinValue;
    private DateTime _lastActionTime = DateTime.MinValue;
    private bool _isShowingWrongKey = false;
    private bool _modeIsFreeTyping = false;
    private string _typingLog = "";
    private int _typingLineCount = 0;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_TAB = 0x09;
    private const int VK_F4 = 0x73;
    private const int VK_MENU = 0x12;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;
    private const int VK_E = 0x45;
    private const int VK_Q = 0x51;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private static LowLevelKeyboardProc? _keyboardProc;
    private static IntPtr _hookId = IntPtr.Zero;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hInstance, uint threadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    public Form1()
    {
        InitializeComponent();
        SetupKioskMode();
        InitializeLetters();
        SetupKeyboardHook();
    }

    private void SetupKioskMode()
    {
        this.FormBorderStyle = FormBorderStyle.None;
        this.WindowState = FormWindowState.Maximized;
        this.TopMost = true;
        this.BackColor = Color.Black;
        this.DoubleBuffered = true;
        this.Cursor = null;

        var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        this.Bounds = screen.Bounds;
    }

    private void InitializeLetters()
    {
        _letters.Clear();
        for (char c = 'A'; c <= 'Z'; c++)
        {
            _letters.Add(c);
        }
        _letters.Shuffle(_random);
        _currentLetterIndex = 0;
        _isShowingWrongKey = false;
    }

    private void SetupKeyboardHook()
    {
        _keyboardProc = KeyboardHookCallback;
        using (var curProcess = Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule!)
        {
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            int vkCode = Marshal.ReadInt32(lParam);

            bool isAltDown = (GetKeyState(VK_MENU) & 0x8000) != 0;
            bool isCtrlDown = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
            bool isShiftDown = (GetKeyState(VK_SHIFT) & 0x8000) != 0;

            if (vkCode == VK_LWIN || vkCode == VK_RWIN)
            {
                return (IntPtr)1;
            }

            if (isAltDown && (vkCode == VK_TAB || vkCode == VK_F4))
            {
                return (IntPtr)1;
            }

            if (vkCode == VK_ESCAPE)
            {
                HandleEscapePress();
                return (IntPtr)1;
            }

            if (vkCode == 0x78) // F9 key
            {
                _modeIsFreeTyping = !_modeIsFreeTyping;
                if (_modeIsFreeTyping)
                {
                    _typingLog = "";
                    _typingLineCount = 0;
                }
                else
                {
                    InitializeLetters();
                    _currentLetterIndex = 0;
                }
                this.Invalidate();
                return (IntPtr)1;
            }

            // Normal game key: route directly to Form1KeyDown via BeginInvoke and consume the event
            if (this.IsHandleCreated && !this.IsDisposed)
            {
                this.BeginInvoke((Action)(() =>
                {
                    var keyArgs = new KeyEventArgs((Keys)vkCode);
                    Form1_KeyDown(this, keyArgs);
                }));
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void HandleEscapePress()
    {
        var now = DateTime.Now;
        if ((now - _lastEscapePress).TotalSeconds < 1.0)
        {
            _escapePressCount++;
            if (_escapePressCount >= 3)
            {
                this.BeginInvoke((Action)ShowExitDialog);
            }
        }
        else
        {
            _escapePressCount = 1;
        }
        _lastEscapePress = now;
    }

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private void Form1_Load(object? sender, EventArgs e)
    {
        this.Invalidate();
    }

    private void Form1_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!_gameStarted)
        {
            _gameStarted = true;
            _currentLetterIndex = 0;
            this.Invalidate();
            return;
        }

        char pressedChar = char.ToUpper(e.KeyCode.ToString()[0]);

        if (_modeIsFreeTyping)
        {
            HandleFreeTypingKey(e, pressedChar);
            return;
        }

        char targetChar = _letters[_currentLetterIndex];

        if (pressedChar == targetChar)
        {
            SystemSounds.Asterisk.Play();
            _lastActionTime = DateTime.Now;
            _currentLetterIndex++;
            if (_currentLetterIndex >= _letters.Count)
            {
                InitializeLetters();
                _currentLetterIndex = 0;
            }
            this.Invalidate();
        }
        else
        {
            SystemSounds.Hand.Play();
            _isShowingWrongKey = true;
            this.Invalidate();
            Task.Delay(300).ContinueWith(_ => {
                _isShowingWrongKey = false;
                this.BeginInvoke((Action)this.Invalidate);
            });
        }
    }

    private void Form1_KeyUp(object? sender, KeyEventArgs e)
    {
    }

    private void HandleFreeTypingKey(KeyEventArgs e, char pressedChar)
    {
        string keyText = e.KeyCode.ToString();

        if (e.KeyCode == Keys.Space)
        {
            _typingLog += " ";
            SystemSounds.Asterisk.Play();
        }
        else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return)
        {
            _typingLog += "\n";
            _typingLineCount++;
            if (_typingLineCount >= 12)
            {
                _typingLog = _typingLog.Substring(_typingLog.IndexOf('\n') + 1);
                _typingLineCount--;
            }
            SystemSounds.Beep.Play();
            this.Invalidate();
        }
        else if (e.KeyCode == Keys.Back)
        {
            if (_typingLog.Length > 0)
                _typingLog = _typingLog.Substring(0, _typingLog.Length - 1);
            SystemSounds.Asterisk.Play();
        }
        else if (e.KeyCode == Keys.Tab)
        {
            _typingLog += "    ";
            SystemSounds.Asterisk.Play();
        }
        else if (e.KeyCode == Keys.OemPeriod || e.KeyCode == Keys.OemQuestion)
        {
            _typingLog += ".";
            SystemSounds.Asterisk.Play();
        }
        else if (keyText.Length == 1 && !char.IsControl(pressedChar))
        {
            char actualChar = e.Shift ? pressedChar : char.ToLower(pressedChar);
            _typingLog += actualChar;
            SystemSounds.Hand.Play();
        }

        this.Invalidate();
    }

    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private void ExitGame()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        Application.Exit();
    }

    private void ShowExitDialog()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        using var dialog = new ExitDialog();
        var result = dialog.ShowDialog(this);

        if (result == DialogResult.OK)
        {
            if (dialog.SwitchMode)
            {
                _modeIsFreeTyping = !_modeIsFreeTyping;
                if (_modeIsFreeTyping)
                {
                    _typingLog = "";
                    _typingLineCount = 0;
                }
                else
                {
                    InitializeLetters();
                    _currentLetterIndex = 0;
                }
                this.Invalidate();
                SetupKeyboardHook();
            }
            else
            {
                ExitGame();
            }
        }
        else if (_hookId == IntPtr.Zero)
        {
            SetupKeyboardHook();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        int centerX = this.ClientSize.Width / 2;
        int centerY = this.ClientSize.Height / 2;

        if (!_gameStarted)
        {
            DrawStartScreen(g, centerX, centerY);
        }
        else
        {
            DrawGameScreen(g, centerX, centerY);
        }
    }

    private void DrawStartScreen(Graphics g, int centerX, int centerY)
    {
        string title = "TODDLER TYPING GAME";
        string subtitle = "Press any key to start";

        using var titleFont = new Font("Segoe UI", 72, FontStyle.Bold);
        using var subtitleFont = new Font("Segoe UI", 36, FontStyle.Regular);
        using var brush = new SolidBrush(Color.White);

        var titleSize = g.MeasureString(title, titleFont);
        var subtitleSize = g.MeasureString(subtitle, subtitleFont);

        g.DrawString(title, titleFont, brush,
            centerX - titleSize.Width / 2,
            centerY - titleSize.Height - 50);

        using var pulseBrush = new SolidBrush(Color.FromArgb(
            (int)(128 + 127 * Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 3)),
            Color.White));
        g.DrawString(subtitle, subtitleFont, pulseBrush,
            centerX - subtitleSize.Width / 2,
            centerY + 50);
    }

    private void DrawGameScreen(Graphics g, int centerX, int centerY)
    {
        if (_modeIsFreeTyping)
        {
            DrawFreeTypingScreen(g, centerX, centerY);
        }
        else
        {
            DrawLetterGameScreen(g, centerX, centerY);
        }
    }

    private void DrawLetterGameScreen(Graphics g, int centerX, int centerY)
    {
        char currentLetter = _letters[_currentLetterIndex];

        using var letterFont = new Font("Segoe UI", 300, FontStyle.Bold);
        using var infoFont = new Font("Segoe UI", 28, FontStyle.Regular);
        using var hintFont = new Font("Segoe UI", 14, FontStyle.Regular);
        using var brush = new SolidBrush(Color.White);

        string letterStr = currentLetter.ToString();
        var letterSize = g.MeasureString(letterStr, letterFont);

        bool celebrate = (DateTime.Now - _lastActionTime).TotalMilliseconds < 600;
        bool showWrong = _isShowingWrongKey;

        var displayColor = showWrong ? Color.FromArgb(255, 80, 80) : Color.White;
        using var displayFont = new Font("Segoe UI", showWrong ? 280 : 300, FontStyle.Bold);
        var displaySize = g.MeasureString(letterStr, displayFont);
        var displayRect = new RectangleF(
            centerX - displaySize.Width / 2 - 40,
            centerY - displaySize.Height / 2 - 40,
            displaySize.Width + 80,
            displaySize.Height + 80);

        using (var path2 = new GraphicsPath())
        {
            path2.AddEllipse(displayRect);
            using var pathGradient = new PathGradientBrush(path2)
            {
                CenterColor = showWrong ? Color.FromArgb(80, 40, 40) : Color.FromArgb(50, 50, 80),
                SurroundColors = new[] { Color.Black }
            };
            g.FillPath(pathGradient, path2);
        }

        g.DrawString(letterStr, displayFont, new SolidBrush(displayColor),
            centerX - displaySize.Width / 2,
            centerY - displaySize.Height / 2);

        // Dynamically position text below the letter so nothing overlaps or clips.
        // Compute positions from the bottom up: hint is pinned to bottom margin,
        // examples sit above hint, progress sits above examples.
        float infoFontHeight = infoFont.GetHeight(g);
        float hintFontHeight = hintFont.GetHeight(g);

        // Hint: pinned to bottom of screen with margin (using its own smaller font)
        float hintY = this.ClientSize.Height - hintFontHeight - 60;

        // Examples: above hint, with gap
        float examplesY = hintY - infoFontHeight - 20;

        // Progress: above examples, with gap
        float progressY = examplesY - infoFontHeight - 15;

        // Ensure progress stays below the letter; if not, shift everything down
        float letterBottom = centerY + displaySize.Height / 2;
        float overflow = (letterBottom + 50) - progressY;
        if (overflow > 0)
        {
            progressY += overflow;
            examplesY += overflow;
            hintY += overflow;
        }

        // Safety: clamp hint so it never goes off the bottom edge of the screen
        float maxHintY = this.ClientSize.Height - hintFontHeight - 30;
        if (hintY > maxHintY)
        {
            float clampOver = hintY - maxHintY;
            hintY -= clampOver;
            examplesY -= clampOver;
            progressY -= clampOver;
        }

        string progress = $"Letter {_currentLetterIndex + 1} of {_letters.Count}";
        var progressSize = g.MeasureString(progress, infoFont);
        g.DrawString(progress, infoFont, brush,
            centerX - progressSize.Width / 2,
            progressY);

        string hint = "Press Esc x3 to exit";
        var hintSize = g.MeasureString(hint, hintFont);
        g.DrawString(hint, hintFont, brush,
            centerX - (int)(hintSize.Width / 2),
            hintY);

        DrawLetterExamples(g, currentLetter, centerX, (int)examplesY);
    }

    private void DrawFreeTypingScreen(Graphics g, int centerX, int centerY)
    {
        using var paperFont = new Font("Consolas", 32, FontStyle.Regular);
        using var titleFont = new Font("Segoe UI", 24, FontStyle.Bold);
        using var hintFont = new Font("Segoe UI", 14, FontStyle.Regular);
        using var brush = new SolidBrush(Color.FromArgb(40, 30, 20));
        using var titleBrush = new SolidBrush(Color.FromArgb(80, 60, 40));
        using var textBrush = new SolidBrush(Color.FromArgb(20, 15, 10));
        using var hintBrush = new SolidBrush(Color.FromArgb(100, 80, 60));

        int paperWidth = Math.Min(this.ClientSize.Width - 200, 1000);
        int paperHeight = this.ClientSize.Height - 200;
        int paperX = centerX - paperWidth / 2;
        int paperY = 100;

        using (var paperPath = new GraphicsPath())
        {
            paperPath.AddRectangle(new Rectangle(paperX, paperY, paperWidth, paperHeight));
            using var paperGradient = new PathGradientBrush(paperPath)
            {
                CenterColor = Color.FromArgb(245, 235, 215),
                SurroundColors = new[] { Color.FromArgb(210, 195, 170) }
            };
            g.FillPath(paperGradient, paperPath);
        }

        using (var pen = new Pen(Color.FromArgb(160, 140, 110), 2))
        {
            g.DrawRectangle(pen, paperX, paperY, paperWidth, paperHeight);
        }

        string title = "TYPEWRITER MODE";
        var titleSize = g.MeasureString(title, titleFont);
        g.DrawString(title, titleFont, titleBrush,
            centerX - titleSize.Width / 2,
            40);

        string hint = "Press F9 to return to Letter Game  |  Esc 3x to exit";
        var hintSize = g.MeasureString(hint, hintFont);
        g.DrawString(hint, hintFont, hintBrush,
            centerX - hintSize.Width / 2,
            paperY + paperHeight + 20);

        int lineHeight = 48;
        int charWidth = 19;
        int maxCharsPerLine = (paperWidth - 80) / charWidth;

        string[] lines = _typingLog.Split('\n');
        int startLine = Math.Max(0, lines.Length - 12);
        for (int i = startLine; i < lines.Length; i++)
        {
            string line = lines[i];
            int y = paperY + 40 + (i - startLine) * lineHeight;

            for (int j = 0; j < line.Length; j += maxCharsPerLine)
            {
                int len = Math.Min(maxCharsPerLine, line.Length - j);
                string segment = line.Substring(j, len);
                g.DrawString(segment, paperFont, textBrush, paperX + 40, y);
            }
        }

        if (lines.Length > 0)
        {
            string lastLine = lines[lines.Length - 1];
            int lineIndex = lines.Length - 1 - startLine;
            int y = paperY + 40 + lineIndex * lineHeight;
            int cursorX = paperX + 40 + (lastLine.Length % maxCharsPerLine) * charWidth;

            if (DateTime.Now.Millisecond < 500)
            {
                g.DrawString("_", paperFont, textBrush, cursorX, y);
            }
        }
    }

    private void DrawLetterExamples(Graphics g, char letter, int centerX, int startY)
    {
        using var font = new Font("Segoe UI", 48, FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(180, 180, 180));

        string examples = GetLetterExamples(letter);
        var size = g.MeasureString(examples, font);
        g.DrawString(examples, font, brush,
            centerX - size.Width / 2,
            startY);
    }

    private string GetLetterExamples(char letter)
    {
        var examples = new Dictionary<char, string>
        {
            {'A', "Apple   Ant   Airplane"},
            {'B', "Bear   Banana   Balloon"},
            {'C', "Cat   Carrot   Car"},
            {'D', "Dog   Duck   Dinosaur"},
            {'E', "Elephant   Egg   Eagle"},
            {'F', "Fish   Flower   Frog"},
            {'G', "Giraffe   Grapes   Guitar"},
            {'H', "House   Hamster   Hat"},
            {'I', "Ice Cream   Iguana   Island"},
            {'J', "Jellyfish   Juice   Jet"},
            {'K', "Kangaroo   Key   Kite"},
            {'L', "Lion   Lemon   Leaf"},
            {'M', "Monkey   Moon   Music"},
            {'N', "Butterfly   Nest   Note"},
            {'O', "Owl   Orange   Ocean"},
            {'P', "Pig   Pizza   Penguin"},
            {'Q', "Queen   Puzzle   Quiet"},
            {'R', "Rabbit   Rainbow   Rocket"},
            {'S', "Sun   Snake   Star"},
            {'T', "Tiger   Tree   Train"},
            {'U', "Umbrella   Unicorn   Up"},
            {'V', "Violin   Volcano   Veggie"},
            {'W', "Whale   Wave   Web"},
            {'X', "Xylophone   Box   Fox"},
            {'Y', "Yoyo   Yarn   Yacht"},
            {'Z', "Zebra   Zoo   Zap"}
        };

        return examples.TryGetValue(letter, out var example) ? example : $"{letter} for ...";
    }
}

public class ExitDialog : Form
{
    private readonly TextBox _pinBox;
    private readonly Label _messageLabel;
    private readonly Button _exitButton;
    private readonly Button _switchButton;
    private readonly Button _cancelButton;

    private const string DefaultPin = "1234";

    // Public so Form1 can check if user wants to switch mode after PIN
    public bool SwitchMode { get; private set; }

    public ExitDialog()
    {
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.TopMost = true;
        this.BackColor = Color.Black;
        this.ForeColor = Color.White;
        this.Size = new Size(520, 250);
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ShowInTaskbar = false;
        this.ControlBox = false;

        var titleLabel = new Label
        {
            Text = "EXIT",
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(100, 20)
        };

        _messageLabel = new Label
        {
            Text = "Enter PIN to exit:",
            Font = new Font("Segoe UI", 14),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(50, 80)
        };

        _pinBox = new TextBox
        {
            Font = new Font("Segoe UI", 24),
            PasswordChar = '●',
            MaxLength = 4,
            Size = new Size(120, 50),
            Location = new Point(140, 110),
            TextAlign = HorizontalAlignment.Center
        };
        _pinBox.KeyDown += PinBox_KeyDown;

        _exitButton = new Button
        {
            Text = "Exit",
            Font = new Font("Segoe UI", 14),
            Size = new Size(100, 40),
            Location = new Point(90, 180),
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(200, 60, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _exitButton.Click += ExitButton_Click;

        _switchButton = new Button
        {
            Text = "Switch Mode",
            Font = new Font("Segoe UI", 14),
            Size = new Size(100, 40),
            Location = new Point(210, 180),
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(60, 120, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _switchButton.Click += SwitchButton_Click;

        _cancelButton = new Button
        {
            Text = "Cancel",
            Font = new Font("Segoe UI", 14),
            Size = new Size(100, 40),
            Location = new Point(330, 180),
            DialogResult = DialogResult.Cancel,
            BackColor = Color.FromArgb(60, 60, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        this.Controls.Add(titleLabel);
        this.Controls.Add(_messageLabel);
        this.Controls.Add(_pinBox);
        this.Controls.Add(_exitButton);
        this.Controls.Add(_switchButton);
        this.Controls.Add(_cancelButton);

        this.AcceptButton = _exitButton;
        this.CancelButton = _cancelButton;

        this.Shown += (s, e) => _pinBox.Focus();
    }

    private void ExitButton_Click(object? sender, EventArgs e)
    {
        SwitchMode = false;
        CheckPinAndClose();
    }

    private void SwitchButton_Click(object? sender, EventArgs e)
    {
        SwitchMode = true;
        CheckPinAndClose();
    }

    private void PinBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            CheckPinAndClose();
        }
    }

    private void CheckPinAndClose()
    {
        if (_pinBox.Text == DefaultPin)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        else
        {
            _messageLabel.Text = "Incorrect PIN. Try again.";
            _messageLabel.ForeColor = Color.Red;
            _pinBox.Clear();
            _pinBox.Focus();
        }
    }
}

public static class ListExtensions
{
    public static void Shuffle<T>(this List<T> list, Random rng)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            (list[n], list[k]) = (list[k], list[n]);
        }
    }
}