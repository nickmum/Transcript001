---
name: verify
description: Build, launch, and drive the Transcript001 WPF app for runtime verification.
---

# Verifying Transcript001 (WPF, net8.0-windows)

## Build
```powershell
dotnet build Transcript001\Transcript001.csproj -v q
```

## Launch
```powershell
Start-Process Transcript001\bin\Debug\net8.0-windows\Transcript001.exe
```
- Main window UIA Name/title: `Video Transcript Processor`.
- If `ANTHROPIC_API_KEY` is unset, a blocking `Missing API Key` MessageBox appears at startup — dismiss its OK button first.
- Kill when done: `Stop-Process -Name Transcript001 -Force -Confirm:$false`.

## Drive (UI Automation from Windows PowerShell 5.1)
`Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes` then find the window by Name at RootElement children scope. WPF `x:Name` becomes the UIA `AutomationId` (e.g. `ScreenshotButton`, `StatusText`, `UrlTextBox`); unnamed buttons are findable by UIA Name (e.g. `Process Video`). Invoke buttons via `InvokePattern`. Invoking a disabled button throws `ElementNotEnabledException`.

## Flows worth driving
- **Load a video**: URL box is pre-filled at startup; invoke `Process Video`, then poll `StatusText` (its UIA Name) until it matches `complete|loaded|error|failed` (~10–30 s, needs network). Player is a WebView2 (`VideoPlayer`) showing a YouTube IFrame embed.
- **Screenshot-to-clipboard**: invoke `ScreenshotButton`, then check `[System.Windows.Forms.Clipboard]::ContainsImage()` / `GetImage()` (powershell.exe is STA, so this works directly). Button label swaps to "✓ Copied" for 1.5 s; `StatusText` shows "Screenshot copied to clipboard".

## Gotchas
- Clipboard assertions and window screenshots (`Graphics.CopyFromScreen` on the UIA `BoundingRectangle`) need the window visible on a real desktop — fine on this machine.
- "AI summary failed" after video load just means the Claude API call failed; video/player features still work without it.
