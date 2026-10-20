# Automatic Mouse Mover

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://raw.githubusercontent.com/ebellocchia/auto_mouse_mover/master/LICENSE)

## Introduction

This small GUI application simulates periodic user input by automatically moving the mouse cursor. It is written in C#.\
It may seem useless at first glance, but I often use it on company laptops where the power options are locked and cannot be modified. My typical use case is running demos at fairs or in front of customers, when you cannot move the mouse and you don’t want the laptop to automatically lock the screen. This way, you don’t have to remember to move the mouse every now and then, and you can focus entirely on the demo.\
Or, you can use it to keep alive some applications.

## How it works

Once started, the application is minimized to the tray bar and runs in background. The user can choose if showing the tray bar icon to make it completely invisible.\
The application periodically checks the mouse position and simulates a mouse moving. In order to avoid disturbing the user if he's using the PC, the mouse is moved only if its position remained the same since the last time.\
The cursor is moved back and forth, so it always remains in the same spot and does not go outside the screen.\
After the mouse is moved, the application can also perform a left-click if needed to keep an application active.\
In order to move the mouse automatically while in background, the application directly imports and calls the Windows APIs for sending input to the operating system and getting the cursor position, since there is no native function in C# for doing this (at least when I developed it).

The app supports multiple languages via localization (a different form is created for each language and automatically selected by Windows based on the system language). The currently supported languages are English and Italian, but more can be added easily.

## Building

Open the Visual Studio solution and build the project in either Debug or Release mode.\
The output folder is *AutoMouseMover\bin*.

## Usage

Open the application. The card at the top shows whether the mouse is being moved right now (*Active*), and until when. Use the button below it to pause or resume.\
Minimizing the window hides it to the tray. Click the tray icon for quick actions: *Pause for 30 min*, *Pause until tomorrow*, *Resume*, *Open window* and *Close*. Double-click the icon to open the window.\
Pauses are remembered, so a restart does not undo them.

### Schedule

Turn on *Follow work hours* and set your hours and days. Inside your hours the application starts moving by itself; outside them it stops. Pausing during work hours lasts until the next work day starts; resuming outside work hours keeps it running until you pause it.

### Start with Windows

Turn on *Start with Windows* to start the application in the tray when you sign in (no admin rights needed). If you previously added a shortcut to the Windows Startup folder, delete it. Only one copy of the application can run at a time.\
The `--autostart` argument still works: it starts the application in the tray and, without a schedule, starts moving immediately.

### Movement

Optionally adjust how many idle seconds pass before the cursor is moved and by how many pixels (the default of 5 pixels is barely noticeable), and whether to left-click after moving.\
All settings are saved automatically, including the language (globe icon in the title bar).

## License

This software is available under the MIT license.
