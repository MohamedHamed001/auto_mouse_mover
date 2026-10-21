# macOS port: menu-bar app with a shared core

Date: 2026-10-20
Builds on: `docs/superpowers/specs/2026-10-20-modern-ui-design.md` (the Windows
2.x design; its behaviour rules apply here unless this document says otherwise)

## Intent and why

Auto Mouse Mover keeps Teams and Slack showing "Available" (green) while the
user is away from the keyboard, by nudging the mouse when the PC is idle. It
works on Windows today. The owner wants the same app on macOS, published on
their GitHub fork for **anyone in the community** who needs it, not only for
themselves.

Research (third-party tools and blogs, to be confirmed on a real Mac):
Teams and Slack on macOS decide "Away" from the system's time since the last
mouse/keyboard input (`HIDIdleTime`). Synthetic mouse moves posted with
`CGEventPost` reset it. Power assertions (`caffeinate`, Amphetamine,
`IOPMAssertionDeclareUserActivity`) keep the Mac awake but do **not** keep the
status green. So the macOS app moves the mouse, exactly like the Windows one.

## Goals

1. A macOS **menu-bar app** (no Dock icon) with the same features as Windows 2.x:
   - Status card: Active / Paused / Outside work hours, plus "until HH:mm" /
     "starts ddd HH:mm".
   - One Pause/Resume button.
   - Schedule: on/off, start and end time, work days, Start at login.
   - Movement: interval (2-30 s), distance (1-200 px), click after moving.
   - Menu-bar menu: status line; while running Pause for 30 min and Pause until
     tomorrow (only with schedule on); while paused Resume; always Open window
     and Quit.
   - Settings menu: English / Italiano, System / Light / Dark theme, About.
2. **Same rules as Windows**, by reusing `MoverController` and `WorkSchedule`
   unchanged in a shared core library.
3. **Visible permission state:** if macOS has not granted the permission to
   post input events, the app says so on the status card, with a button that
   opens the right System Settings page. It never shows "Active" while it
   cannot move the mouse.
4. **Released from the existing workflow:** pushing a `v*` tag also builds the
   macOS app and attaches it to the same GitHub release as the Windows exe.
5. README section "macOS" covering download, first launch, permission, updates
   and building from source.

## Non-goals

- Linux (dropped by the owner).
- Code signing with an Apple Developer ID, notarization, Mac App Store.
- Changing the Windows app's UI or behaviour. It stays on WPF.
- Syncing settings between machines.
- A single UI codebase for both platforms.

## Anti-goals (failure even if it "works")

- The status shows Active but the mouse is not moving (missing permission,
  locked screen, failed event posting).
- The app moves the mouse while the screen is locked or the Mac is asleep.
- A Dock icon or a window that pops up at every login.
- The Windows app, its 69 tests or its release break because of the port.

## Decided by the owner

- **Platforms:** macOS only, besides the existing Windows app.
- **Audience:** the community, via the owner's GitHub fork; free, open source.
- **Distribution:** unsigned. Users accept the first-launch steps, or build
  from source. Few releases are expected.
- **Architecture:** Windows stays on WPF. macOS gets its own **Avalonia** app.
  Both use a **shared core library** containing `MoverController` and
  `WorkSchedule`.

## Behaviour rules

All Windows 2.x behaviour rules (pause model, schedule crossings, pause
survives restart, `--autostart`) apply unchanged; they live in
`MoverController`. macOS-specific rules:

1. **Permission.** At start and every time the app is about to move, check
   `CGPreflightPostEventAccess()`. If not granted: tell the controller the
   mouse must stay still (same effect as locked) and show "Needs permission"
   on the card with an "Open System Settings" button
   (`CGRequestPostEventAccess()` and/or the
   `x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility`
   URL). macOS may only report a new grant after a relaunch: the card says so.
2. **Moving.** Same algorithm as `AutomaticMouseMover`: move only if the cursor
   has not moved since the last tick; alternate back and forth by the chosen
   distance; stay on the cursor's screen; optional left click. Post with
   `CGEventCreateMouseEvent` + `CGEventPost(kCGHIDEventTap, ...)`.
3. **Locked screen and sleep.** Never move while the screen is locked or the
   Mac sleeps. After unlock/wake, apply missed schedule crossings (the
   controller already does this on unlock).
4. **Menu-bar only.** `LSUIElement` in Info.plist: no Dock icon. Opening the
   app shows the window; closing the window only hides it (macOS convention);
   **Quit** in the menu exits.
5. **Start at login.** A per-user LaunchAgent plist in `~/Library/LaunchAgents`
   that starts the app with `--autostart`. The file's existence is the source
   of truth for the toggle. Works without signing.
6. **`--autostart`:** start hidden in the menu bar (same rule as Windows).
7. **Single instance:** a second launch shows the running copy's window.
8. **Settings** in a JSON file under
   `~/Library/Application Support/AutoMouseMover/`. Same fields and defaults as
   Windows. A damaged file falls back to defaults, never a crash.
9. **Theme:** System (default, follows macOS appearance), Light or Dark.

## Constraints

- .NET 8. The shared core targets plain `net8.0` (no Windows or macOS APIs) so
  it builds and tests on both.
- The Windows project keeps `net8.0-windows`, WPF and its single-file publish.
  It references the core instead of holding `MoverController` and
  `WorkSchedule` itself. Its tests keep passing.
- Avalonia 11.x stable, pinned to an exact version. Fluent theme.
- macOS builds for Apple Silicon (`osx-arm64`) and Intel (`osx-x64`),
  self-contained, packaged as `.app` bundles in zip files. Apple Silicon
  requires at least an ad-hoc signature (`codesign -s -`) to run: apply it.
- Packaging and the ad-hoc signature run on a GitHub-hosted macOS runner in the
  existing `release.yml` workflow (new job). The Windows job is unchanged.
- English and Italian strings: reuse the Windows wording (same keys) so the
  two apps read the same.
- Repo code style: `m` prefix for fields, `c` prefix for parameters,
  `#region` blocks, a comment on every method.
- The owner is learning: plain, readable code over clever patterns.

## Decisions after review (author's calls)

Settled during the builder check; the owner can still change them.

- **Moving the app breaks start-at-login paths.** Unsigned apps launched
  straight from Downloads run from a hidden, temporary copy ("App
  Translocation", path contains `/AppTranslocation/`). The app detects that
  path and asks the user to move it to Applications first (README says the
  same). When Start at login is on, the app rewrites the LaunchAgent with its
  current path on every launch, so moving the app later doesn't break it.
- **Tests are split by platform.** `MoverController` and `WorkSchedule` tests
  move to a new `net8.0` test project for the core, which runs on both
  runners. Windows-only tests (registry, Windows settings) stay in
  `AutoMouseMover.Tests` (`net8.0-windows`) and run on Windows only.
- **One release, no race.** The workflow builds Windows and macOS in separate
  jobs that upload build artifacts; a final job (needs both) creates the
  release once and attaches all files: `AutoMouseMover.exe`,
  `AutoMouseMover-macos-arm64.zip`, `AutoMouseMover-macos-x64.zip`. If either
  build or its tests fail, nothing is released.
- **Same version everywhere.** One `v*` tag releases both platforms; the
  macOS bundle's `CFBundleShortVersionString` comes from the same `<Version>`.
- **Updates need the permission again.** Each build has a new ad-hoc
  signature, so macOS treats it as a new app: the README's update steps say to
  remove the old entry in Accessibility and grant the new one.

## Left to the builder

- Project names and folders (suggestion: `AutoMouseMover.Core`,
  `AutoMouseMover.Mac`), and whether the pure text/format helpers in
  `SettingsHelper` (times, days, pause, theme) move into the core so both apps
  share them.
- How locked-screen and sleep are detected on macOS (polling the session state
  on each tick is acceptable).
- How single instance is implemented.
- Exact Avalonia layout and icons, within the Windows 2.x layout.
- Whether the macOS strings come from the Windows `.resx` files (linked) or a
  copy.

## How we'll know it works

- All existing tests pass. Core tests run on both a Windows and a macOS
  GitHub runner.
- Unit tests for the macOS settings file (round-trip, damaged file) and the
  LaunchAgent file (enable writes the plist with `--autostart`, disable
  removes it, using a temporary folder).
- Manual check on the owner's MacBook:
  - First launch from the downloaded zip (right-click Open), permission flow,
    card shows "Needs permission" until granted.
  - With permission: idle 5 s → cursor nudges;
    `ioreg -c IOHIDSystem | grep HIDIdleTime` resets.
  - Teams and Slack stay green for 15+ minutes away.
  - Lock (Ctrl+Cmd+Q) → no movement; unlock → resumes.
  - Schedule, pauses, menu-bar menu, Start at login (log out/in), theme,
    language, close vs Quit.
