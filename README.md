# Automatic Mouse Mover

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Latest release](https://img.shields.io/github/v/release/MohamedHamed001/auto_mouse_mover)](https://github.com/MohamedHamed001/auto_mouse_mover/releases/latest)

A small Windows tray app that keeps your PC "active" by nudging the mouse cursor while you're away: no screen lock, no "Away" status in chat apps, and it never gets in your way while you're working.

<p align="center">
  <img src="docs/images/main-window.png" alt="Main window while active" width="360">
  &nbsp;&nbsp;
  <img src="docs/images/main-window-paused.png" alt="Main window while paused" width="360">
</p>

This is a fork of [ebellocchia/auto_mouse_mover](https://github.com/ebellocchia/auto_mouse_mover). Version 2.0 adds a new window, a work schedule and a tray menu.

## Features

- **Status at a glance:** a card shows whether the mouse is being moved right now and until when.
- **Work schedule:** starts by itself when your work hours begin and stops when they end, on the days you choose.
- **Quick pauses from the tray:** pause for 30 minutes, pause until tomorrow, or resume. Pauses survive a restart.
- **Start with Windows:** one switch, no admin rights needed.
- **Never in your way:** the cursor only moves if you haven't touched it since the last check, and it moves back and forth on the same spot.
- **Follows Windows light and dark theme.** English and Italian.

## Download

Get `AutoMouseMover.exe` from the [latest release](https://github.com/MohamedHamed001/auto_mouse_mover/releases/latest).\
It's a single file: no installer, no .NET install and no admin rights needed. Put it anywhere (for example `%LOCALAPPDATA%\AutoMouseMover`) and run it.

The file is not code-signed, so Windows SmartScreen may warn on first run: choose *More info*, then *Run anyway*.

## Usage

Open the application. The card at the top shows whether the mouse is being moved right now (*Active*), and until when. Use the button below it to pause or resume.\
Minimizing the window hides it to the tray. Click the tray icon for quick actions: *Pause for 30 min*, *Pause until tomorrow*, *Resume*, *Open window* and *Close*. Double-click the icon, or open the exe again, to show the window.

### Schedule

Turn on *Follow work hours* and set your hours and days. Inside your hours the application starts moving by itself; outside them it stops.\
Pausing during work hours lasts until the next work day starts; resuming outside work hours keeps it running until you pause it.

### Start with Windows

Turn on *Start with Windows* to start the application in the tray when you sign in. If you previously added a shortcut to the Windows Startup folder, delete it. Only one copy of the application runs at a time.\
The `--autostart` argument still works: it starts the application in the tray and, without a schedule, starts moving immediately.

### Movement

Optionally adjust how many idle seconds pass before the cursor is moved and by how many pixels (the default of 5 pixels is barely noticeable), and whether to left-click after moving.\
All settings are saved automatically, including the language (icon in the title bar).

## How it works

The application periodically checks the mouse position. If the cursor hasn't moved since the last check, it moves it a few pixels back and forth, so it always stays in the same spot and never leaves the screen. It can also left-click after moving, to keep a specific application active.\
To move the mouse while running in the background, it calls the Windows input APIs directly.\
Locking the PC or putting it to sleep pauses the movement; it resumes when you unlock.

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows).

```powershell
dotnet test
dotnet publish AutoMouseMover/AutoMouseMover.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The single-file exe is written to `AutoMouseMover/bin/Release/net8.0-windows/win-x64/publish/`.\
To diagnose crashes, see [docs/Diagnosing-Crashes.md](docs/Diagnosing-Crashes.md).

## License

This software is available under the MIT license.
