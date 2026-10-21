# macOS Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a macOS menu-bar version of Auto Mouse Mover (Avalonia) that shares its rules with the Windows app through a new core library, released from the same `v*` tag.

**Architecture:** `MoverController`, `WorkSchedule` and the pure settings text helpers move unchanged into `AutoMouseMover.Core` (`net8.0`), referenced by the Windows WPF app and a new `AutoMouseMover.Mac` Avalonia app. Everything macOS-specific (event posting, lock detection, LaunchAgent, JSON settings) sits behind small classes in the Mac project. The pieces that need no Mac are unit-tested on any OS; the CoreGraphics calls are verified by hand on the owner's MacBook.

**Tech Stack:** .NET 8, Avalonia 11.3.22 (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`), System.Text.Json, P/Invoke to CoreGraphics/CoreFoundation, xUnit, GitHub Actions (`windows-latest`, `macos-latest`).

**Spec:** `docs/superpowers/specs/2026-10-20-macos-port-design.md` (plus the Windows rules it builds on: `docs/superpowers/specs/2026-10-20-modern-ui-design.md`)

## Global Constraints

- Precondition: the 2.1.0 theme-picker work is committed and `main` contains it before Task 1 starts (it adds `eThemeChoice` and `SettingsHelper.Parse/FormatThemeChoice`, which Task 1 moves).
- Core targets `net8.0` with no Windows or macOS API. Windows app stays `net8.0-windows`, WPF, single-file publish unchanged.
- Avalonia packages pinned to `11.3.22`. Fluent theme. No other new packages.
- macOS builds: `osx-arm64` and `osx-x64`, self-contained, `.app` bundles, ad-hoc signed (`codesign --force --deep -s -`), zipped with `ditto -c -k --keepParent`.
- Release assets: `AutoMouseMover.exe`, `AutoMouseMover-macos-arm64.zip`, `AutoMouseMover-macos-x64.zip`. One release per tag; nothing released if any job fails.
- Bundle id `io.github.mohamedhamed001.automousemover`; `LSUIElement` = true; `CFBundleShortVersionString` = the csproj `<Version>`.
- Settings file: `~/Library/Application Support/AutoMouseMover/settings.json`. LaunchAgent: `~/Library/LaunchAgents/io.github.mohamedhamed001.automousemover.plist` with `--autostart` and `RunAtLoad`.
- Same defaults and limits as Windows: interval 2-30 s (5), distance 1-200 px (5), click off, schedule off, 09:00-17:00, Monday-Friday, theme System.
- Code style: `m` fields, `c` parameters, `#region` blocks, a comment above every method. Strings never hard-coded in XAML; English and Italian.
- Never show Active while the mouse cannot move (permission missing or screen locked).
- `dotnet` means `& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"` on the owner's Windows machine.
- No commits without the owner's go-ahead: each task ends with a checkpoint (diff + test output).

## Review Focus

1. **Permission revoked while running** (user removes the app in System Settings): the card must switch to "Needs permission" within one tick, no Active. Pinned by `PermissionLost_ShowsNeedsPermission` in Task 5.
2. **App launched from Downloads (App Translocation)** then Start at login turned on: no LaunchAgent pointing into `/AppTranslocation/`. Pinned by `Enable_TranslocatedPath_Refused` in Task 3.
3. **Damaged or hand-edited `settings.json`** (bad JSON, `"25:00"`, unknown day): app starts with defaults for the bad fields. Pinned by tests in Task 2.
4. **Cursor at a screen edge or on a second display**: the nudge flips direction instead of leaving the screen. Pinned by `MoveNear_RightEdge_FlipsDirection` in Task 4.
5. **Both release jobs racing to create the release**: exactly one release with three assets. Pinned by the workflow structure in Task 8 (single `release` job that `needs` both builds) and checked on the first tag.

---

### Task 1: Extract the shared core library

**Files:**
- Create: `AutoMouseMover.Core/AutoMouseMover.Core.csproj` (`net8.0`, no other packages)
- Move (git mv): `AutoMouseMover/Logic/WorkSchedule.cs`, `AutoMouseMover/Logic/MoverController.cs` → `AutoMouseMover.Core/Logic/`
- Create: `AutoMouseMover.Core/Utils/SettingsFormat.cs` (the eight pure helpers + `eThemeChoice`, moved out of `SettingsHelper`)
- Modify: `AutoMouseMover/Utils/SettingsHelper.cs` (call `SettingsFormat`), `AutoMouseMover/AutoMouseMover.csproj` (reference core), `AutoMouseMover.sln`
- Create: `AutoMouseMover.Core.Tests/AutoMouseMover.Core.Tests.csproj` (`net8.0`, xUnit as in the Windows tests)
- Move: `WorkScheduleTests.cs`, `MoverControllerTests.cs` → `AutoMouseMover.Core.Tests/`; `SettingsHelperTests.cs` → `AutoMouseMover.Core.Tests/SettingsFormatTests.cs` (class and calls renamed to `SettingsFormat`)

**Interfaces:**
- Produces: `public` classes `AutoMouseMover.Logic.WorkSchedule`, `MoverController`, `MoverStatus`, enums `eMoverState`, `eScheduleAction` (namespaces unchanged, only `internal` → `public`). `public static class AutoMouseMover.Utils.SettingsFormat` with `ParseWorkDays`, `FormatWorkDays`, `ParsePausedUntil`, `FormatPausedUntil`, `ParseTime`, `FormatTime`, `ParseThemeChoice`, `FormatThemeChoice` (same signatures as today in `SettingsHelper`), and `public enum eThemeChoice`.

- [ ] **Step 1:** Create the two projects and move the files and tests (behaviour unchanged; only access modifiers, namespaces of the moved helpers' class name, and project references change). `AutoMouseMover.Tests` keeps only `StartupHelperTests`.
- [ ] **Step 2: Run all tests.** `dotnet test`. Expected: `AutoMouseMover.Core.Tests` 65 passed, `AutoMouseMover.Tests` 4 passed (69 total, same as before the move).
- [ ] **Step 3: Windows publish still works.** `dotnet publish AutoMouseMover/AutoMouseMover.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`, then run the exe once. Expected: the window opens, and the publish folder contains only the exe (plus `.pdb` and `.config`).
- [ ] **Step 4: Checkpoint.**

---

### Task 2: macOS settings file

**Files:**
- Create: `AutoMouseMover.Mac/AutoMouseMover.Mac.csproj` (`net8.0`, `OutputType` WinExe, Avalonia 11.3.22 packages, references core), `AutoMouseMover.Mac/Services/MacSettings.cs`
- Create: `AutoMouseMover.Mac.Tests/AutoMouseMover.Mac.Tests.csproj` (`net8.0`, references the Mac project), `AutoMouseMover.Mac.Tests/MacSettingsTests.cs`

**Interfaces:**
- Consumes: Task 1 `SettingsFormat`, `eThemeChoice`, `WorkSchedule`.
- Produces: `class MacSettings` with `MacSettings(string cFolder)` (tests pass a temp folder; the app passes `~/Library/Application Support/AutoMouseMover`), `void Load()`, `void Save()`, `WorkSchedule CreateSchedule()` (null if off or invalid, same rule as Windows), and properties `MovingTime`, `MovingPixel`, `LeftClickAfterMoving`, `ScheduleEnabled`, `WorkStartTime` (TimeSpan), `WorkEndTime`, `WorkDays` (`List<DayOfWeek>`), `PausedUntil` (`DateTime?`), `Language` (string), `Theme` (`eThemeChoice`). JSON stores the text forms from `SettingsFormat`. `Save` writes a temp file, then replaces the real one.

- [ ] **Step 1: Write the failing tests:** `Load_NoFile_UsesDefaults` (5, 5, false, off, 09:00, 17:00, Mon-Fri, null, "", SYSTEM); `SaveThenLoad_RoundTrips` (every property, including `PausedUntil = DateTime.MaxValue`); `Load_BadJson_UsesDefaults` (file content `{not json`); `Load_BadFields_FallBackPerField` (`"WorkStartTime": "25:00"`, `"MovingTime": 99`, `"WorkDays": "Funday"` → 09:00, 5, empty list → `CreateSchedule()` null); `Save_CreatesFolder`.
- [ ] **Step 2: Run, expect a build failure.** `dotnet test AutoMouseMover.Mac.Tests`.
- [ ] **Step 3: Implement `MacSettings`.** A private DTO with string/int/bool fields, `System.Text.Json`. Out-of-range interval/distance → default.
- [ ] **Step 4: Run, expect 5 passed.**
- [ ] **Step 5: Checkpoint.**

---

### Task 3: Start at login (LaunchAgent)

**Files:**
- Create: `AutoMouseMover.Mac/Services/LoginItemHelper.cs`, `AutoMouseMover.Mac.Tests/LoginItemHelperTests.cs`

**Interfaces:**
- Produces: `class LoginItemHelper` with `const string LABEL = "io.github.mohamedhamed001.automousemover"`, `LoginItemHelper(string cLaunchAgentsFolder)`, `bool IsEnabled()` (the plist file exists), `bool Enable(string cExecutablePath)` (false, and writes nothing, if the path contains `/AppTranslocation/`), `void Disable()` (no error if missing), `void RefreshPath(string cExecutablePath)` (rewrite only if enabled), `static bool IsTranslocated(string cPath)`.

Plist content (exact):

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>io.github.mohamedhamed001.automousemover</string>
  <key>ProgramArguments</key>
  <array><string>{executable path, XML-escaped}</string><string>--autostart</string></array>
  <key>RunAtLoad</key><true/>
</dict>
</plist>
```

- [ ] **Step 1: Write the failing tests** (temp folder): `Enable_WritesPlistWithPathAndAutostart` (file `LABEL + ".plist"` exists; contains the path and `--autostart`); `Enable_PathWithAmpersand_IsEscaped` (`/Applications/A&B.app/...` → `A&amp;B`); `Enable_TranslocatedPath_Refused` (`/private/var/folders/x/AppTranslocation/y/Auto Mouse Mover.app/Contents/MacOS/AutoMouseMover` → returns false, no file); `Disable_WhenMissing_DoesNotThrow`; `RefreshPath_WhenDisabled_WritesNothing`; `RefreshPath_WhenEnabled_UpdatesPath`.
- [ ] **Step 2: Run, expect a build failure.**
- [ ] **Step 3: Implement** (plain string template plus `SecurityElement.Escape`).
- [ ] **Step 4: Run, expect 6 passed.**
- [ ] **Step 5: Checkpoint.**

---

### Task 4: Mouse nudging on macOS

**Files:**
- Create: `AutoMouseMover.Mac/Platform/ICursorDevice.cs`, `AutoMouseMover.Mac/Platform/MacCursorDevice.cs` (P/Invoke), `AutoMouseMover.Mac/Platform/NullCursorDevice.cs`, `AutoMouseMover.Mac/Logic/MacMouseMover.cs`
- Test: `AutoMouseMover.Mac.Tests/MacMouseMoverTests.cs` (with a fake `ICursorDevice`)

**Interfaces:**
- Produces:

```csharp
public interface ICursorDevice
{
    bool CanPostEvents();                         // CGPreflightPostEventAccess
    void RequestPostEvents();                     // CGRequestPostEventAccess
    bool IsScreenLocked();                        // CGSessionCopyCurrentDictionary["CGSSessionScreenIsLocked"]
    (double X, double Y) GetPosition();           // CGEventGetLocation(CGEventCreate(null))
    (double X, double Y, double Width, double Height)? GetScreenBounds(double cX, double cY); // CGGetDisplaysWithPoint + CGDisplayBounds
    void MoveTo(double cX, double cY);            // CGEventCreateMouseEvent(kCGEventMouseMoved=5) + CGEventPost(kCGHIDEventTap=0)
    void LeftClick(double cX, double cY);         // kCGEventLeftMouseDown=1 / Up=2
}

class MacMouseMover
{
    public MacMouseMover(ICursorDevice cDevice);
    public void Initialize(int cMovingPixel, bool cLeftClick);   // baseline = current position
    public void MoveMouse();                                     // same rule as Windows AutomaticMouseMover
}
```

`NullCursorDevice` (all no-ops, `CanPostEvents` true, never locked) lets the UI run on Windows during development: the app picks `MacCursorDevice` only when `OperatingSystem.IsMacOS()`. Mark it with a `ponytail:` comment ("dev-only stand-in; delete if Windows previews are no longer needed").

Algorithm (port of `Logic/AutomaticMouseMover.cs`): if the position equals the last one, move by `+d` then `-d` alternately on both axes; if the target would leave the cursor's screen bounds, flip that axis; if no screen contains the cursor, do nothing; click after the move if enabled; then store the new position as the baseline.

- [ ] **Step 1: Write the failing tests** with a fake device that records moves: `MoveMouse_CursorStill_MovesForwardThenBack` ((100,100) → (105,105), next tick → (100,100)); `MoveMouse_UserMovedCursor_DoesNothing`; `MoveNear_RightEdge_FlipsDirection` (bounds 0,0,1440,900; cursor (1438,450) → x goes to 1433); `MoveMouse_ClickEnabled_ClicksAfterMove`; `MoveMouse_NoScreen_DoesNothing`.
- [ ] **Step 2: Run, expect a build failure.**
- [ ] **Step 3: Implement** `MacMouseMover`, then `MacCursorDevice` (`[DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]` for CG*, CoreFoundation for `CFDictionaryGetValue`, `CFStringCreateWithCString`, `CFBooleanGetValue`, `CFRelease`; release every created CG/CF object).
- [ ] **Step 4: Run, expect 5 passed.** `MacCursorDevice` is checked by hand in Task 9.
- [ ] **Step 5: Checkpoint.**

---

### Task 5: Avalonia window: status card, Pause/Resume, permission and lock

**Files:**
- Create: `AutoMouseMover.Mac/Program.cs`, `App.axaml(.cs)`, `MainWindow.axaml(.cs)`, `Resources/lang_en.resx`, `Resources/lang_it.resx` (copy the Windows keys, add `StatusNeedsPermission` "Needs permission"/"Serve un permesso", `PermissionHint` "Allow Auto Mouse Mover in System Settings → Privacy & Security → Accessibility, then reopen it."/"Consenti Auto Mouse Mover in Impostazioni di Sistema → Privacy e sicurezza → Accessibilità, poi riaprila.", `OpenSystemSettings` "Open System Settings"/"Apri Impostazioni di Sistema", `MoveToApplications` "Move Auto Mouse Mover to the Applications folder, then open it again."/"Sposta Auto Mouse Mover nella cartella Applicazioni, poi riaprila.", `StartAtLogin` "Start at login"/"Avvia al login", `TrayQuit` "Quit"/"Esci")
- Create: `AutoMouseMover.Mac/Logic/MacStatus.cs`, test `AutoMouseMover.Mac.Tests/MacStatusTests.cs`

**Interfaces:**
- Consumes: Tasks 1–4.
- Produces: `static class MacStatus { static eMacCardState GetCardState(MoverStatus cStatus, bool cCanPostEvents, bool cScreenLocked) }` with `enum eMacCardState { ACTIVE, PAUSED, OUTSIDE_HOURS, NEEDS_PERMISSION }`. `NEEDS_PERMISSION` wins over everything; a locked screen shows the controller's state (the window isn't visible then anyway). `MainWindow(bool cAutoStart)` with `RefreshStatus()` and `ApplyTexts()` like the Windows window.

Window behaviour (mirrors Windows `MainWindow`): mouse timer (interval from settings) runs `controller.Tick(now)`, then computes `canMove = device.CanPostEvents() && !device.IsScreenLocked()` and calls `controller.SetLocked(!canMove, now)` only when it changes, then moves if `IsRunning`. Schedule timer 30 s. `StateChanged` → save `PausedUntil`, refresh. "Open System Settings" calls `RequestPostEvents()` and opens `x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility` with `open`. If `LoginItemHelper.IsTranslocated(Environment.ProcessPath)`, show the `MoveToApplications` text under the card.

- [ ] **Step 1: Write the failing tests:** `PermissionLost_ShowsNeedsPermission` (ACTIVE status, `cCanPostEvents=false` → NEEDS_PERMISSION); `Permission_Ok_PassesStateThrough` (each `eMoverState` maps to the same-named card state); `Locked_ShowsControllerState`.
- [ ] **Step 2: Run, expect a build failure.**
- [ ] **Step 3: Implement** `MacStatus`, the app shell and window (status card + button only in this task), with Fluent theme and `RequestedThemeVariant` from settings.
- [ ] **Step 4: Run all tests, expect them to pass.** `dotnet run --project AutoMouseMover.Mac` on Windows. Expected: the window shows "Paused" (NullCursorDevice), and Resume → Active.
- [ ] **Step 5: Checkpoint.**

---

### Task 6: Settings controls, language and theme menu

**Files:**
- Modify: `AutoMouseMover.Mac/MainWindow.axaml(.cs)`, resx files

**Interfaces:**
- Consumes: `MacSettings`, `LoginItemHelper`, `MoverController.SetSchedule`.

Same controls and validation as the Windows window (schedule toggle, `HH:mm` boxes accepting `9:00`, seven day toggles Monday first, Start at login, interval, distance, click; inline errors `ScheduleErrorTimeFormat`, `ScheduleErrorEndBeforeStart`, `ScheduleErrorNoDays`; an unchanged schedule must not stop a manual run). Gear button menu: English / Italiano / System theme / Light / Dark (tick on current) / About (version from the assembly). Start at login: `Enable(Environment.ProcessPath)`; if it returns false, show `MoveToApplications`. On every launch, `RefreshPath(Environment.ProcessPath)`.

- [ ] **Step 1: Build the controls.** `dotnet build`. Expected: 0 errors.
- [ ] **Step 2: Run all tests, expect them to pass.**
- [ ] **Step 3: Manual check on Windows (NullCursorDevice):** end before start → error; untick all days → error; switch to Italiano and Dark, restart → still Italian and dark; the settings file is written under the dev folder.
- [ ] **Step 4: Checkpoint.**

---

### Task 7: Menu bar, autostart, reopen and Quit

**Files:**
- Modify: `AutoMouseMover.Mac/App.axaml(.cs)`, `MainWindow.axaml.cs`, `Program.cs`; add `AutoMouseMover.Mac/Assets/menubar-icon.png` (monochrome 18×18 @1x and 36×36 @2x, derived from `app_icon.ico`) and `app.icns`

Behaviour:
- `TrayIcon` with a `NativeMenu`, rebuilt before it opens (`NativeMenu.Opening`). Items: disabled status line; while ACTIVE "Pause for 30 min" and "Pause until tomorrow" (schedule on only); otherwise "Resume"; then "Open window" and "Quit".
- Closing the window hides it. "Quit" saves and calls `desktop.Shutdown()`. `ShutdownMode = OnExplicitShutdown`.
- `--autostart` → don't show the window.
- Clicking the app again in Finder (macOS "reopen") shows the window: use Avalonia's `IActivatableLifetime` (`Activated` with `ActivationKind.Reopen`). Check that it exists in 11.3.22 first; if it doesn't, record a ruling and rely on LaunchServices keeping a single instance.

- [ ] **Step 1: Implement.** `dotnet build`. Expected: 0 errors.
- [ ] **Step 2: Run all tests, expect them to pass.**
- [ ] **Step 3: Checkpoint.** Menu-bar behaviour can only be checked on the Mac (Task 9).

---

### Task 8: Packaging and release workflow

**Files:**
- Create: `AutoMouseMover.Mac/Packaging/Info.plist` (template with `{VERSION}`), `scripts/package-macos.sh <rid> <version>`
- Modify: `.github/workflows/release.yml`

`package-macos.sh`:
1. `dotnet publish AutoMouseMover.Mac -c Release -r <rid> --self-contained -p:UseAppHost=true`
2. Build `Auto Mouse Mover.app/Contents/{MacOS,Resources}`: copy the publish output to `MacOS`, `app.icns` to `Resources`, and `Info.plist` with `{VERSION}` replaced.
3. Run `codesign --force --deep -s - "Auto Mouse Mover.app"`.
4. Run `ditto -c -k --keepParent "Auto Mouse Mover.app" AutoMouseMover-macos-<arch>.zip`.

`Info.plist` keys: `CFBundleName` "Auto Mouse Mover", `CFBundleIdentifier` (global), `CFBundleExecutable` "AutoMouseMover.Mac", `CFBundleIconFile` "app.icns", `CFBundleShortVersionString`/`CFBundleVersion` `{VERSION}`, `LSUIElement` true, `LSMinimumSystemVersion` "11.0", `NSHighResolutionCapable` true.

Workflow, replacing the single job:
- `windows` (`windows-latest`): run `dotnet test`, publish, upload the `AutoMouseMover.exe` artifact.
- `macos` (`macos-latest`): run `dotnet test AutoMouseMover.Core.Tests` and `AutoMouseMover.Mac.Tests`, then `package-macos.sh` for `osx-arm64` and `osx-x64`, and upload both zips.
- `release` (`ubuntu-latest`, `needs: [windows, macos]`): download the artifacts, build the notes (the existing CHANGELOG extraction, ported to bash), add each asset's SHA256, then create the release, or `upload --clobber` if it already exists.

- [ ] **Step 1: Write the script and workflow.** Validate the YAML (`python -c "import yaml..."`) and run `bash -n scripts/package-macos.sh`.
- [ ] **Step 2: Local dry-run of the notes extraction** against `CHANGELOG.md` for the current version.
- [ ] **Step 3: Checkpoint.** The real test is the next tag, run in Task 9.

---

### Task 9: Docs, version, release and MacBook check

**Files:**
- Modify: `README.md` (new "macOS" section: download the zip for your Mac type, move the app to Applications, first launch by right-clicking → Open, grant Accessibility, updates mean removing the old Accessibility entry and granting again, building from source), `CHANGELOG.md` (`# 2.2.0`), `AutoMouseMover/AutoMouseMover.csproj` and `AutoMouseMover.Mac/AutoMouseMover.Mac.csproj` `<Version>2.2.0</Version>`

- [ ] **Step 1: Update the docs and version.** Run all tests. Expected: all pass.
- [ ] **Step 2: Owner go-ahead, then commit, merge to `main`, and tag `v2.2.0`.** Expected: all three jobs green, and one release with three assets.
- [ ] **Step 3: Owner's MacBook check** (from the spec's "How we'll know it works"):
  - (a) First launch from the zip → card shows "Needs permission".
  - (b) Grant permission, reopen → Active, and the cursor nudges after the idle interval.
  - (c) `ioreg -c IOHIDSystem | grep HIDIdleTime` resets.
  - (d) Teams and Slack stay green for 15+ minutes.
  - (e) Lock → no movement; unlock → resumes.
  - (f) Menu-bar items, close vs Quit, Start at login across log out/in, theme, language.
- [ ] **Step 4: Fix what the MacBook check finds**, test-first where the logic is testable. Release a patch if needed.
