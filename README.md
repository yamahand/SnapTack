# SnapTack

**English** | [日本語](README.ja.md)

[![CI](https://github.com/yamahand/SnapTack/actions/workflows/ci.yml/badge.svg)](https://github.com/yamahand/SnapTack/actions/workflows/ci.yml)

A Windows tray utility that captures any region of your screen and pins it to the desktop as a sticky note.
It aims to be a successor to SETUNA2, a freeware tool that is no longer developed.

Clip part of a document, an error message, or a reference image in an instant, pin it to your screen, and keep it there to compare against or copy into another app.

![Screenshot](docs/images/screenshot.png)

## Features

- Press **Ctrl+Shift+Z** to freeze the screen, drag to select a region, and the selection stays on your desktop as a sticky note
- Notes are always on top. Drag to move, `Ctrl+C` to copy, `Ctrl+S` to save, middle-click to close
- Scroll the mouse wheel to change opacity, double-click to fold a note into a small tile to save space
- **Save as PNG, JPEG, or BMP** (JPEG quality is adjustable), or use **quick save** to drop a timestamped file into a folder of your choice with no dialog at all. Optionally copies the saved path to the clipboard
- **Make notes from images you already have** — paste from the clipboard via the tray menu or `Ctrl+V` on a note, or drop image files onto the scrap list
- **Edit notes after you pin them** — scale from 25% to 400%, rotate, flip, and trim away the parts you don't need. Editing is non-destructive, so **Reset edits** always brings the original back
- **Scrap list** — open it with **Ctrl+Shift+L** to browse every scrap as a thumbnail. Show or hide notes, copy or save them, and recover closed ones from the trash
- **Scraps persist across restarts.** Pinned notes come back where you left them next time you launch. Closing a note sends it to the trash instead of losing it, and the trash auto-clears after a configurable number of days
- Multi-monitor support. Captures at 1:1 physical pixels, with no positional drift even across mixed DPI scaling such as 125% / 150% (Per-Monitor V2 aware)
- Lives in the system tray. Hotkeys, retention limits, and startup restore can be changed from the settings window
- Available in English and Japanese. Follows your Windows display language by default, and can be switched manually from the settings window
- Runs portably — settings are saved to `settings.json` next to the executable, and scraps to a `scraps/` folder beside it, falling back to `%APPDATA%\SnapTack` if that location isn't writable

## Requirements

- Windows 10 / 11 (x64)

## Installation

### Portable

Download a zip from [Releases](../../releases), extract it anywhere, and run `SnapTack.exe`. Two builds are available:

| File | Description |
|---|---|
| `SnapTack-vX.X.X-portable-win-x64.zip` | **Runtime included. Pick this one if you're unsure.** Runs as-is even without .NET installed (larger download) |
| `SnapTack-vX.X.X-portable-win-x64-fd.zip` | Lightweight build. Requires [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) to be installed separately |

### Installer

Download `SnapTack-vX.X.X-setup.exe` from [Releases](../../releases) and run it.

### If Windows blocks the app

The binaries are not code-signed, so SmartScreen shows a "Windows protected your PC" dialog the first time you run them.
Click **More info** and then **Run anyway** to start the app.

## Usage

| Action | Result |
|---|---|
| `Ctrl+Shift+Z` (configurable) / double-click the tray icon | Start a capture |
| Left-drag | Select a region (releasing confirms it and leaves a note in place) |
| `Esc` / right-click | Cancel the capture |

### Working with notes

| Action | Result |
|---|---|
| Left-drag | Move |
| `Ctrl+C` | Copy the image to the clipboard |
| `Ctrl+V` | Turn the clipboard image into a new note, offset from this one |
| `Ctrl+S` | Save to a file (choose PNG / JPEG / BMP in the dialog) |
| `Ctrl+Shift+S` | Quick save — no dialog, straight to your quick save folder |
| Mouse wheel | Change opacity (20–100%) |
| `Alt+↑` / `Alt+↓` | Scale up / down by 10% (25–400%) |
| `Alt+Shift+↑` / `Alt+Shift+↓` | Scale by 1% for fine adjustment |
| `R` / `Shift+R` | Rotate 90° right / left |
| `T` | Trim — drag to select, `Enter` to apply, `Esc` to cancel |
| `↑↓←→` / `Shift+↑↓←→` | Move by 1px / 50px, showing the position as you go |
| Double-click | Fold into a tile / restore |
| Middle-click | Close |
| Right-click | Menu (copy / paste / save / quick save / scale / rotate / flip / trim / reset edits / opacity / fold / close / hide to list) |

You can pin as many notes as you like at once. Closing them all leaves the app running in the tray — quit from the tray menu's **Exit**.

### Editing notes

Scaling, rotating, flipping, and trimming are **non-destructive** — the original image is kept, so repeated scaling never degrades it and **Reset edits** always restores the original. Copying and saving give you the edited result, exactly as it appears on screen. Edits persist across restarts.

Notes you never edit look and behave exactly as they did before: 1:1 physical pixels, unscaled.

### The scrap list

Open the scrap list with **Ctrl+Shift+L** (configurable) or the tray menu. It shows every scrap as a thumbnail, with a separate tab for the trash.

| Action | Result |
|---|---|
| Double-click / `Enter` | Show the scrap on screen and bring it to the front |
| `Ctrl+C` | Copy the selected scrap image |
| `Delete` | Move to the trash (or permanently delete, with confirmation, when already in the trash) |
| Right-click | Menu (show / hide / copy / save / quick save / move to trash — or restore / delete permanently in the trash) |

Closing a note sends it to the trash rather than discarding it, so an accidental middle-click is recoverable. Scraps and the trash are capped (200 and 50 by default), and trash older than 30 days is cleared automatically; all three limits are configurable in the settings window, along with whether pinned notes are restored on startup.

### Making notes without capturing

You don't have to capture to create a scrap:

| How | Where |
|---|---|
| **Paste from clipboard** | Tray menu, or `Ctrl+V` while a note has focus |
| **Drag and drop** | Drop image files anywhere on the scrap list window |

Pasting accepts a copied image, copied files, or text that happens to be an image file path. Supported formats are PNG, JPEG, BMP, GIF, and TIFF — plus WebP and AVIF when the matching Windows codec is installed (they ship with Windows 11 but can be removed; files are skipped rather than failing if the codec is missing).

## Building

Requires the .NET 10 SDK.

```powershell
dotnet build SnapTack.slnx

# Build the portable releases (single-file)
# Outputs two zips to artifacts/: runtime-included and lightweight
pwsh scripts/publish.ps1

# Build the installer (requires Inno Setup 6; run after publish)
# Picks up the executable from artifacts/publish (the runtime-included build)
iscc installer\SnapTack.iss
```

The version number is defined once, in `Directory.Build.props`. The commands above use that
value; for a release the Git tag takes precedence and is passed in by CI.

```powershell
# Run the tests
dotnet test SnapTack.slnx
```

### Releasing

Releases are automated. Pushing a `v*` tag builds both portable zips and the installer,
and attaches them to a **draft** GitHub Release:

```powershell
# 1. Bump <Version> in Directory.Build.props, then commit it
# 2. Tag and push — this is what triggers the release workflow
git tag v1.4.0
git push origin v1.4.0
```

Then open [Releases](../../releases), check the attached files, write the release notes,
and publish the draft manually.

### Development docs

Specifications, milestones, and the CI policy live in [docs/](docs/README.md).

## License

[MIT License](LICENSE)

An independent implementation inspired by SETUNA2; it contains none of the original code.
