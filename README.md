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

For basic usage, simply open the application, choose whether you want it to perform a left-click, and click the Start button while leaving the default settings.\
Optionally (not necessary in most cases, added just for completeness), you can adjust the time interval in seconds and the number of pixels to move the cursor. Additionally, you can choose whether to minimize the application to the tray (recommended) and whether to display the tray icon. If the tray icon is hidden, the only way to close the application after starting it is through the Task Manager.\
When minimized to the tray, you can reopen the application by double-clicking the icon. You can also close it directly by right-clicking the icon and selecting Close.\
If you don’t want to see the cursor move visibly, select a small movement value. The default of 5 pixels should be sufficient (and barely noticeable, especially on modern high-resolution screens).\
All settings are saved when the application is closed and automatically restored when it starts, so you don’t need to reconfigure them every time.\
To start moving automatically when the application opens (e.g. from a shortcut in the Windows Startup folder), launch it with the `--autostart` argument: `AutoMouseMover.exe --autostart`.

## License

This software is available under the MIT license.
