# Modern UI: status window and tray popup

Date: 2026-10-20
Branch: `feature/work-schedule` (the schedule logic this builds on lives there)

## Intent and why

The owner uses Auto Mouse Mover for one reason: keep their Microsoft Teams
status green during work hours instead of it turning yellow ("Away"). The
current WinForms window is dated, makes the state hard to read at a glance,
and has no notion of work hours.

The new UI should answer "am I green right now, and until when?" at a glance,
keep all existing options reachable, and stay out of the way in the tray.

## Goals

1. A main window whose top is a large **status card** (green Active / grey
   Paused / grey Outside work hours, plus "until HH:mm" when known).
2. One main button that toggles **Pause / Resume** (replaces Start and Stop).
3. All settings visible below the card, in two groups:
   - **Schedule:** work hours (start, end), work days, schedule on/off,
     Start with Windows.
   - **Movement:** interval (s), distance (px), click after moving.
4. A **tray popup** used while the window is minimized: status line, then
   Pause for 30 min, Pause until tomorrow, Open window, Close.
5. Windows 11 look (cards, toggle switches), following the system light/dark
   theme.
6. Wire in the existing `Logic/WorkSchedule.cs` (step 1) so the app starts
   and stops by itself at the edges of work hours.

## Non-goals

- Overnight work hours (e.g. 22:00-06:00). `WorkSchedule` rejects them.
- More than one schedule per week, or per-day hours.
- Keep-awake mode, global hotkey, automatic releases, smaller exe: separate
  improvements, not part of this work.
- Staying mergeable with the upstream repo. This is the owner's fork.

## Anti-goals (failure even if it "works")

- Teams goes yellow during work hours because the app silently stopped
  (e.g. after sleep, lock, or a schedule bug).
- The app can't be closed without Task Manager.
- Saved settings are lost on upgrade from 1.3.0.
- Regressions in the 1.3.0 sleep / lock / monitor-dock handling (those
  fixed real crashes; see `docs/Diagnosing-Crashes.md`).

## Decided by the owner

- **Toolkit:** WPF with the **WPF-UI** library (NuGet `WPF-UI` and
  `WPF-UI.Tray`, version 4.3.0, MIT, supports `net8.0-windows`).
- **Layout:** status card on top, Pause/Resume button, Schedule group,
  Movement group (mockup approved in chat).
- **Tray popup contents:** status line, Pause for 30 min, Pause until
  tomorrow, Open window, Close.
- **Language** moves to a small icon/menu in the window corner; About goes
  into that menu too.
- **"Show tray icon" option is dropped.** The tray icon is always visible
  while the app runs.
- Schedule is **off by default**; with it off, the app behaves like today
  (manual Pause/Resume only).

## Behaviour rules

Status the card shows, in priority order:

| Situation | Card |
|---|---|
| Running, schedule on | Active, "until <end time> today" |
| Running, schedule off | Active |
| Paused for 30 min | Paused, "until HH:mm" |
| Paused until tomorrow | Paused, "until <next work-day start>" |
| Paused manually | Paused |
| Schedule on, outside work hours | Outside work hours, "starts <next start>" |

Rules:

1. **Manual control wins until the next crossing.** Pausing at 14:00 stays
   paused until the next work-day start. Resuming at 20:00 runs until the
   user pauses. (`WorkSchedule.CheckTransition` already implements this.)
2. **Pause for 30 min** resumes automatically after 30 minutes, but only if
   it would be running then (schedule off, or inside work hours).
3. **Pause until tomorrow** stops now and lets the schedule start it at the
   next work-day start. Shown only when the schedule is on.
4. **Lock / sleep:** keep the existing behaviour (stop on lock or suspend,
   resume on unlock or resume), with one change: after unlock or resume,
   do not resume if the schedule is on and it is outside work hours.
5. **Schedule checks** run on their own timer (about every 30 s) and on
   unlock / resume, calling `WorkSchedule.CheckTransition(DateTime.Now)`.
6. **Start with Windows** writes or removes a value under
   `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pointing at the exe
   with `--autostart` (per-user, no admin). The registry is the source of
   truth: the toggle reads it on open.
7. **`--autostart`:** start minimized to the tray. With the schedule off,
   start running immediately (today's behaviour). With it on, let the
   schedule decide.
8. **`--debug-log`** keeps working (it lives in `Utils/CrashLogger.cs`).
9. **Close** (tray or window close button) saves settings and exits.
   Minimize hides to the tray.

## Constraints

- Target stays `net8.0-windows`. The project needs both `UseWPF` and
  `UseWindowsForms`: `Logic/AutomaticMouseMover.cs` uses
  `System.Windows.Forms.Screen`. Watch for name clashes between the two
  (`Application`, `MessageBox`, `Timer`, ...).
- Keep the single-file self-contained publish working
  (`dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`).
- **Reuse unchanged:** `Logic/AutomaticMouseMover.cs`, `WinHelper/*`,
  `Utils/CrashLogger.cs`. `Logic/WorkSchedule.cs` keeps its existing
  behaviour and tests but may gain small additions (next-start and end-time
  calculations).
- **Settings:** keep `Properties/Settings.settings` and `Utils/SettingsHelper.cs`;
  add the new settings (schedule on/off, start time, end time, work days).
  Existing users' saved values must load. Drop `ShowTrayBarIcon` and
  `MinimizeToTrayBar` from the UI (minimize always goes to the tray).
- **Languages:** English and Italian. All UI strings move to
  `Properties/lang_en.resx` and `Properties/lang_it.resx` (today most live in
  the WinForms form's own `.resx` files, which go away with the form).
- Keep the repo's code style: `m` prefix for fields, `c` prefix for
  parameters, `#region` blocks, a comment on every method.
- The owner is learning: prefer plain, readable code over clever patterns.

## Decisions after review

Decided by the owner:

- **A pause survives a restart.** Every pause is saved as "paused until
  <time>", so a reboot, crash or sign-out does not undo it.
- **Tray popup while paused:** the two pause items are replaced by a single
  **Resume** item. While running: Pause for 30 min, Pause until tomorrow.
  Always: status line, Open window, Close.

Author's calls (the owner can still change them):

- **Pause model.** The app tracks two things separately: whether it *wants*
  to run (set by schedule crossings, by Resume, and by `--autostart` with
  the schedule off) and an optional "paused until" time laid on top. It
  moves the mouse only when it wants to run, is not paused, and the session
  is not locked. This settles the edge cases:
  - Pause (main button) with schedule on: paused until the next work-day
    start. With schedule off: paused until Resume.
  - Pause for 30 min: when the 30 minutes end, the app does whatever it
    would have done without the pause. A 17:00 crossing during the pause
    means it stays stopped; a manual run at 20:00 continues at 20:30.
  - Pause until tomorrow means what it says: paused until the first work-day
    start on a later date, even if pressed at 08:00 before today's start.
  - Lock/unlock and sleep/resume clarify rule 4: unlocking resumes only if
    the app still wants to run. A schedule stop that happened while locked
    is applied; a manual run outside hours is kept.
- **Changing the schedule** (hours, days, or turning it on) re-applies it
  immediately: inside the new hours it wants to run, outside it doesn't.
  A saved pause is not touched.
- **Single instance:** a second copy exits immediately (named mutex), so a
  leftover Startup-folder shortcut plus the new Run-key entry can't start
  two movers. The owner deletes the old shortcut once the toggle works.
- **Settings upgrade:** call `Settings.Default.Upgrade()` once when a
  `SettingsUpgradeRequired` flag is set, so settings from 1.3.0 carry over.
  The exe keeps its place (`%LOCALAPPDATA%\AutoMouseMover`) and the company
  name stays, so the settings folder does not move.
- Defaults 09:00-17:00, Monday to Friday. Settings are always editable and
  movement changes apply live. Running outside work hours shows plain
  "Active". The chosen language is saved. Validation rejects end before
  start and no work days. The Run-key value points at
  `Environment.ProcessPath`.

## Left to the builder

- Exact visual details (spacing, icons, colours within the WPF-UI theme).
- How state is held between the window and the timers. The author's
  suggestion: one small UI-free class (e.g. `Logic/MoverController.cs`) that
  owns running / paused / pause-until state and the schedule, raises a
  "state changed" event, and is unit-tested; the window only displays it and
  forwards clicks. Use a different shape if it's clearly simpler.
- Whether the tray popup is a styled context menu or a small borderless
  window, as long as left-click and right-click on the tray icon both open it
  and double-click opens the main window.
- Time and day pickers' exact control types.

## How we'll know it works

- Unit tests (xUnit, `AutoMouseMover.Tests`) for the state rules above:
  pause 30 min and resume, pause until tomorrow, unlock outside hours,
  schedule off behaves like today, `--autostart` with schedule on/off.
- Existing 12 `WorkSchedule` tests still pass.
- Manual check list: open, pause, resume, minimize, tray popup items,
  close; lock and unlock; toggle Start with Windows and sign out/in;
  switch language; light and dark Windows theme; settings survive restart;
  1.3.0 settings carry over.
