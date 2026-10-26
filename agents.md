# Toddler Typing Game - Agent Handoff Guide

## Project Structure
```
TodlerTypingGame/
├── ToddlerTypingGame.csproj      # .NET 10 project file
├── Program.cs                     # Entry point - first-run prompt, auto-start via registry
├── Form1.cs                       # Main form: kiosk mode, keyboard hook, game, free typing, adult exit
├── Form1.Designer.cs             # Windows Forms designer code (auto-generated, do not modify)
├── agents.md                      # This file
└── obj/                           # Build output (auto-generated)
```

## Technology Stack
- **.NET 10.0** with Windows Forms (net10.0-windows)
- C# for the typing game logic
- Windows Registry for auto-start (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`)
- JSON settings file for user prefs
- Low-level keyboard hook (WH_KEYBOARD_LL) for kiosk mode

## Build & Run Commands
```powershell
# Build the project
dotnet build

# Run the project
dotnet run

# Publish as self-contained single exe (for distribution)
dotnet publish -c Release -r win-x86 --self-contained true -o ./publish
# Output: C:\Users\ramga\Source\TodlerTypingGame\bin\Release\net10.0-windows\win-x86\ToddlerTypingGame.exe
```

## Features Implemented

### 1. Kiosk Mode (Form1.cs)
- Borderless, fullscreen (`FormBorderStyle.None`, `WindowState.Maximized`, `TopMost=true`)
- Bounds set to primary screen size
- Blocks via keyboard hook (WH_KEYBOARD_LL):
  - Win key: `VK_LWIN` (0x5B) / `VK_RWIN` (0x5C) -> eat
  - Alt+Tab / Alt+F4 -> eat when Alt held
  - Ctrl+Shift+E / Ctrl+Shift+Q -> opens adult exit dialog
  - Escape -> 3 rapid presses -> adult exit dialog

### 2. Toddler Typing Game (Letter Mode)
- `_modeIsFreeTyping` flag controls mode (default false = Letter Game)
- F9 key toggles mode (`VK = 0x78`)
- F9 in free mode: clears typing log, resets
- F9 in letter mode: resets to first shuffled letter

### 3. Letter Game Logic (DrawLetterGameScreen)
- `_letters` = shuffled list of A-Z
- `_currentLetterIndex` = current target
- Correct key -> `SystemSounds.Asterisk.Play()`, advance letter, invalidate
- Wrong key -> `SystemSounds.Hand.Play()`, set `_isShowingWrongKey = true`, invalidate, Task.Delay(300) reset via BeginInvoke
- Visual: wrong key -> letter drawn in red (255,80,80) with dark red ellipse background; font scaled to 280 when wrong else 300
- Success: `_lastActionTime = DateTime.Now` (used for "celebrate" window)

### 4. Free Typing Mode (Typewriter)
- `_typingLog` string accumulates typed chars
- `_typingLineCount` tracks line count; max 12 lines, oldest trimmed
- On key:
  - Space -> append " ", Asterisk.Play()
  - Enter/Return -> append "\n", Beep.Play(), increment line count, trim to 12
  - Back -> remove last char, Asterisk.Play()
  - Tab -> append "    "
  - OemPeriod/OemQuestion -> append "."
  - Single char & not control -> append char (shift-aware), Hand.Play()
- Draw: typewriter paper (cream gradient rect + border), title "TYPEWRITER MODE", scrolling text lines (maxCharsPerLine calc), cursor "_" blinking when last line being typed

### 5. First-Run Auto-Start + Settings (Program.cs)
- First run -> check `%APPDATA%/ToddlerTypingGame/settings.json`
- If file missing -> first run, save defaults (FirstRunShown=false, AutoStartWithWindows=false), return true
- If FirstRunShown == false -> first run, show prompt
- Prompt: MessageBox Yes/No "Would you like this game to start automatically with Windows every time it starts?"
- Save: FirstRunShown=true, AutoStartWithWindows = (Yes==Yes)
- Apply: if AutoStartWithWindows -> add to registry Run key:
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` -> SetValue("ToddlerTypingGame", Application.ExecutablePath)
  else -> DeleteValue("ToddlerTypingGame", false)

### 6. Adult Exit (PIN Dialog)
- Ctrl+Shift+E / Ctrl+Shift+Q -> `BeginInvoke(ShowAdultExitDialog)`
- Escape x3 -> `BeginInvoke(ShowAdultExitDialog)`
- `AdultExitDialog` (Form):
  - FixedDialog, CenterScreen, TopMost, BorderStyle.FixedDialog, MaximizeBox/MinimizeBox=false, ShowInTaskbar=false, ControlBox=false
  - BackColor Black, ForeColor White
  - Size 400x250
  - Labels: "ADULT EXIT" (24pt bold), "Enter PIN to exit:" (14pt)
  - TextBox `_pinBox`: Font SegoeUI 24, PasswordChar='●', MaxLength=4, Centered, KeyDown -> Enter -> CheckPinAndClose
  - Buttons: "Exit" (red, DialogResult.OK, Flat), "Cancel" (dark, DialogResult.Cancel, Flat)
  - AcceptButton=Exit, CancelButton=Cancel, Shown -> _pinBox.Focus
  - CheckPinAndClose: if `_pinBox.Text == DefaultPin ("1234")` -> DialogResult=OK, Close; else -> "Incorrect PIN. Try again.", Red text, clear+refocus

### 7. Game Start
- `_gameStarted` false -> DrawStartScreen; true -> DrawGameScreen
- Form1_KeyDown: if !_gameStarted -> set started, reset index, Invalidate, return
- DrawStartScreen: title "TODDLER TYPING GAME" (72pt), subtitle "Press any key to start" (36pt, pulsing alpha)
- DrawGameScreen: dispatch to DrawLetterGameScreen or DrawFreeTypingScreen

## Key Code Locations
- Kiosk: Form1.cs `SetupKioskMode` (line ~59)
- Keyboard hook: Form1.cs `KeyboardHookCallback` (line ~95), P/Invokes ~34-49
- Letter game: Form1.cs `DrawLetterGameScreen` (line ~363)
- Free typing: Form1.cs `DrawFreeTypingScreen`, `HandleFreeTypingKey` (line ~240)
- Adult exit: Form1.cs `ShowAdultExitDialog`, `AdultExitDialog` class (line ~379)
- Program: Program.cs `Main` -> CheckFirstRun / ShowAutoStartPrompt / ApplyAutoStartSetting / AddToWindowsRegistryRun / RemoveFromWindowsRegistryRun / LoadSettings / SaveSettings / Settings class

## Exit Strategy / Adult Knowledge
- Adult exit trigger: **Ctrl+Shift+E** (or Ctrl+Shift+Q) OR **Escape x3 within 1s**
- These all route to adult exit dialog requiring PIN (default **1234**)
- PIN NOT displayed in dialog (to avoid kid seeing it); adults know from docs/default config
- Cancel button dismisses dialog (stays in game)
- Hard fallback: restart machine / Ctrl+Alt+Del for true emergencies

## Testing Checklist
- [ ] `dotnet build` succeeds with 0 errors/warnings
- [ ] `dotnet publish -c Release -r win-x86 --self-contained true` succeeds -> exe at bin/Release/net10.0-windows/win-x86/ToddlerTypingGame.exe
- [ ] First run -> prompt shows -> saves settings -> registry updated per answer
- [ ] Kiosk: fullscreen borderless, no taskbar entry (ShowInTaskbar false)
- [ ] Kiosk: Win key blocked, Alt+Tab/F4 blocked, Escape gated
- [ ] Letter mode: correct advances, wrong flashes red + sound, cycles A-Z
- [ ] Free typing: F9 toggles, typing appends to log, Enter newline, scroll to 12 lines, cursor blinks
- [ ] Adult exit: Ctrl+Shift+E -> PIN dialog -> OK exits; Cancel stays

## Distribution Notes
- Published exe self-contained (includes .NET runtime)
- Target: win-x86 (32-bit)
- Settings in `%APPDATA%/ToddlerTypingGame/settings.json`
- Registry auto-start at `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\ToddlerTypingGame`
- Default adult PIN: **1234**
- Build outputs in `bin/` and `publish/` folders