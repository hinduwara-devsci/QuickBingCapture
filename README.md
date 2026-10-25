# Quick Bing Capture

A tiny Windows tray app: capture a screen **region** or a **whole window**, and it is sent straight to
**Bing Visual Search** in your default browser. The capture is also left on the clipboard.

**Developer:** Heshan Induwara - https://github.com/hinduwara-devsci
**License:** MIT - free and open source, free to use and modify. **Privacy:** collects and stores nothing.

## Using it

| Action | How |
|---|---|
| Select an area | **Alt+Shift+S**, drag a rectangle |
| Capture one app's window only | **Alt+Shift+W**, click the window (it is highlighted as you hover) |
| Switch modes while selecting | press **W** |
| Cancel | **Esc** or right-click |

Everything is also in the tray icon's right-click menu, together with **Start with Windows** and **About**.
Window capture uses `PrintWindow`, so you get just that app even if other windows overlap it
(apps that refuse to render that way fall back to a normal screen copy).

## Installer (small, traditional setup.exe)

The app targets **.NET Framework 4.8**, which is already part of Windows 10 (1903+) and Windows 11, so nothing
is bundled and the installer stays tiny. The installer (Inno Setup):

- shows the license / privacy agreement the user must accept,
- installs per-user (no admin prompt) with Start Menu entry, optional desktop shortcut, and an uninstaller,
- starts the app automatically at every sign-in (checked by default; can be changed later in the tray menu),
- removes the startup entry and temp files on uninstall.

### Build it

1. Install the .NET SDK and Inno Setup 6: `winget install JRSoftware.InnoSetup`
2. In PowerShell, from this folder: `.\build.ps1`
3. Your installer is in `dist\QuickBingCapture-Setup-1.1.0.exe`

`build.ps1` also leaves the portable `publish\QuickBingCapture.exe` if you only want the exe.

## Notes

- Uploading uses Bing's image-upload endpoint through a temporary local HTML form (deleted automatically).
  If Microsoft changes that endpoint, the app falls back to opening Bing Images with the screenshot on
  the clipboard (click the page, press Ctrl+V).
- Protected (DRM) video, secure desktops and some elevated apps cannot be captured.
