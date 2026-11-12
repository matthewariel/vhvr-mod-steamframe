# Changelog

What changed in each version of Steam Frame for VHVR. The full feature list, compared with plain VHVR, is in
[FEATURES.md](FEATURES.md). Headset testing status is in [IN-GAME-CHECKS.md](IN-GAME-CHECKS.md).

## 0.5.3 (2026-11-11): installed 2026-11-12, not yet tested in the headset

### Changed
- **Button guide picture redrawn from your reference drawing** (`Assets/frame_controllers_reference.svg`).
  `draw_frame_controllers.ps1` copies its shapes as they are and only places and scales them.
  - **Main picture:** both controllers from above, with their handles. The left one has the D-pad, View, stick and
    Steam button; the right one has Menu, stick, A B X Y and Steam button. Each handle shows its grip button on the
    inner neck and its lanyard loop. The faces are 172 units across.
  - **Side views:** a small cropped side view under each handle shows its bumper (upper) and trigger (lower). Each
    one faces out towards its boxes, so the left one is mirrored from the reference.
  - **Box order:** face buttons, stick click, stick, then **Grip**, whose line runs across the handle to the grip
    button. Then Bumper and Trigger, whose lines go into the side view. Grip used to be last.
  - **Two-button combinations:** they now sit between the two handles, under the grip buttons, 210 units wide.
  - **Picture size:** it now runs the full height of the page (y = 165 to -240).
  - **Colours:** the reference drawing's (blue-grey outlines on dark grey), with light leader lines and orange dots.

## 0.5.2 (2026-11-11): installed, not yet tested in the headset

### Fixed
- **Big map:** the D-pad cross next to the pin icons is really hidden now. 0.5.1 only looked at Valheim's gamepad hint
  pictures, and the cross isn't one: it's a plain picture. Pictures on the big map named "dpad" / "d-pad" (object or
  sprite name) are switched off while the map shows controller labels, and back on with Button labels = Keyboard. The
  log names what it hid, or, if it found nothing, lists the pictures around the pin icon picker.

## 0.5.1 (2026-11-11): tested 2026-11-11, seated / standing confirmed

Fixes from the 0.4.0 headset test (00:44 session). Includes 0.5.0, which was never installed on its own.

### Changed
- **Seated / standing reworked.** In the test SteamVR's floor was about 0.5 m off: the Set buttons read 2.04 m
  standing and 1.55 m seated (other days ~1.52 / 1.27). 0.4.0 used the 2.04 m raw, without VHVR's
  `PlayerMaxEyeHeight` clamp (1.90), so seated you were much taller than other vikings. Standing up then added your
  real rise on top, until a Steam recenter put you back at VHVR's 1.90. Now (`PostureTracker`):
  - your in-game eyes stay at VHVR's own clamped standing height whether you sit or stand. Only the gap between your
    two heights is used for that, so a floor that's off doesn't matter;
  - posture follows your head between recenters: standing up or sitting down switches after about a second, and the
    view eases over about a quarter of a second. A quick duck or VHVR's roomscale sneak doesn't count. New setting
    `[Posture] SwitchWhenYouSitOrStand` (on);
  - the Set buttons also save SteamVR's floor reference (`[Posture] FloorReference`); when it changes (between sessions
    or at a Steam recenter), or the headset jumps at a Steam recenter, the heights are moved with it and the log says
    by how much;
  - `[Posture] SeatedBelowPercent` is gone: Auto needs both heights.
- **Button labels: new "Keyboard" choice** (page I). It forces no gamepad prompts, so Valheim shows its own keyboard
  and mouse prompts, as without the plugin (flat-screen play). No big-map changes either.

### Fixed
- **Big map:** the D-pad picture next to the pin icons is hidden (it's a gamepad hint picture, so 0.4.0's relabelling
  couldn't change it; pin icons are picked by pointing and pulling the trigger). The log lists every hint picture on
  the map and which were hidden.

### Not a plugin issue
- Crouch to sprint delay: flat-screen Valheim has the same half-second pause (the stand-up animation).

## 0.5.0 (2026-11-11): installed with 0.5.1

### Changed
- **Page II Button guide is a picture now, and buttons can be remapped.** Both Steam Frame controllers are drawn
  from above, the way you look down at them (after photos of a real pair): round face, D-pad / View / stick / Steam
  button on the left, Menu / stick / A B X Y / Steam button on the right, with the trigger tip showing. Under each
  face, a much smaller side view shows the bumper and the trigger clearly. (`Assets/frame_controllers.png`, drawn by
  `draw_frame_controllers.ps1`.) A line runs from every button out to a box:
  - **Layout:** left controller's 10 boxes in a column at x = -440..-250, right controller's at 250..440, the
    picture between them, all between y = 165 and -240. Face buttons first (top), then stick, bumper, trigger and
    grip (pointing into the side view). Each box gives the button's name (12 pt) and, under it, what
    it does now (13 pt), read live from the binding file.
  - **Under the picture:** the two-button combinations, one line each (for example "Grip + Trigger: Add map pin /
    Quick-move item").
  - **Bottom row:** **Reset to default** under the left column, a status note under the right one.
  - **Remapping:** click a box and the picture is replaced by a picker of 15 actions in 3 columns, with the
    current action highlighted, and **Cancel**. The change is saved straight away and used after a restart. A changed
    box's name turns orange with "- changed".
  - **Fixed buttons:** the trigger, the grip and stick movement are marked "(fixed)" and can't be changed: VHVR's
    laser, grabbing and moving need them.
  - **Menu stays reachable:** Menu has to stay on some button, so you can always get back to this page.
  - **Reset to default** needs two clicks. It clears every remap; the page I tweaks stay as they are.
  - **Gone from the page:** the big map, chest and wrist-map notes, and raw entries like "Laser click" on their own.
- **New setting** `[Bindings] ButtonRemaps` (empty by default), for example `right/a=dodge;left/bumper=none`.
  Remaps are applied after the binding tweaks, only while `ManageFrameBindings` is on.
- Prompt labels follow a remapped stick click (sprint / crouch glyphs).
- Shorter action names on the guide: "Sprint", "Crouch", "Quick switch", "Quick actions", "Middle click (ping)",
  "Scroll up / zoom in".

## 0.4.0 (2026-11-11): installed, not yet tested in the headset

Fixes from the 2026-11-10 headset test (23:40 session).

### Changed
- **Seated / standing heights are set with buttons, not learned.** The III Height page has **Set standing height**
  and **Set seated height**, which save the headset's current height (readings that can't be right are refused).
  Recenters only pick seated or standing; they never change the heights. Until now every recenter could overwrite
  them, and in the test one 1.98 m reading became the standing height, so a 1.54 m standing recenter counted as
  seated from then on.
- **New setting names** `[Posture] StandingHeight` / `SeatedHeight`. The old learned `StandingEyeHeight` /
  `SeatedEyeHeight` are not carried over (the log says what they were, once) and are removed from the config file:
  set both heights once with the buttons.

### Fixed
- **Tab page buttons were blank.** They now read **I Settings**, **II Button guide**, **III Height**, with their own
  labels (the copied VHVR button's text didn't show). The log lists what the button prefab's texts were.
- **Text ran outside the panel** on the Button guide and Height pages. All pages now stay inside x = ±440 and above
  y = -240 (the panel's edges, measured from headset screenshots, are about ±450 and -252). The guide is 12 pt in three
  280-300 wide columns; its fit test uses the measured sizes.
- **Big map:** remove pin is the **right** bumper (RB), not LB. The pin icon picker and the side-panel toggles showed
  D-pad / Y glyphs; they now show the trigger (point at one and pull). A **"Close map" (Menu)** hint is added to the
  hint bar.
- **Button guide:** grip + stick up/down is "Flip piece front/back" (there's no tilt in Valheim); the map notes list
  pin icons, right bumper and Menu to close.

## 0.3.3 (2026-11-10): installed and tested in the headset

### Added
- **A log file per game session** (new SessionLogs module, Extra). BepInEx overwrites `LogOutput.log` each launch, so
  each session is now also saved to `BepInEx/SessionLogs/<start time>.log`, with the last 50 kept
  (`[SessionLogs] Enabled`, `Keep`). `LogOutput.log` is unchanged. The startup summary is now `10/10 modules active`.

## 0.3.2 (2026-11-10): installed, not yet tested in the headset

### Fixed
- **Steam Frame tab page buttons** (Settings, Button guide, Recenter & height) were about 80% hidden behind VHVR's tab
  row, so they couldn't be read or clicked. They now sit just below it, with the status line, the badge legend and
  each page moved down by 35 px to match. The button guide's columns are 30 px shorter so they still end at the bottom
  of VHVR's panel, and a unit test checks the guide still fits that height.

## 0.3.1 (2026-11-10): installed, partly tested in the headset (big map hints not yet checked)

### Fixed
- **Big map hints:** three labels were wrong, found from the first 0.3.0 play session's log.
  - Zoom showed LT / RT; it now shows D-pad down / up (out / in).
  - Remove pin and Cross off pin were swapped; Remove pin now shows the bumper and Cross off pin the trigger.

## 0.3.0 (2026-11-10): installed, not yet tested in the headset

**No gameplay changes.** Every 0.2.0 feature, setting name and default is the same. This version is about where the code
lives and how it's organised.

### Changed
- **Moved into the GitHub fork** `matthewariel/vhvr-mod-steamframe`, as its own `SteamFrame/` folder. VHVR's files are
  untouched, so the fork syncs with VHVR without conflicts. Players install official VHVR plus this plugin's DLL.
- **Code split by who it's for:** All headsets / Steam Frame / Extras. All-headset code no longer names the Steam Frame;
  it asks the active controller profile. The Steam Frame is the only profile so far, and all-headset features are
  tested on Steam Frame only.
- **Settings tab grouped by scope:** every section header has an **ALL HEADSETS**, **STEAM FRAME** or **EXTRA** badge,
  with a legend under the status line. All-headset settings are on the left, Frame bindings and detection on the right,
  extras below them. The button guide is titled after the controller in use.
- **Startup log:** shows the VHVR version (with a warning when it isn't a tested one) and a `Modules:` line grouped by
  scope.

### Added
- **"Share map position on join" row** on the settings tab. The setting (`[Map] SharePositionOnJoin`) already existed;
  it just had no row.
- **Unit tests** for all the pure rules (77 tests), run automatically on GitHub without game files.
- **VHVR compatibility checker** (`scripts\Check-VhvrCompat.ps1`): checks an installed VHVR and Valheim against
  everything the plugin reaches into, without starting the game.
- **Scripts** to build, test, install (with a backup of the previous DLL) and package the plugin.
- **Docs:** [ARCHITECTURE.md](ARCHITECTURE.md), [MAINTAINING.md](MAINTAINING.md), [UPSTREAM-NOTES.md](UPSTREAM-NOTES.md).

## 0.2.0 (2026-11-09): installed, not yet tested in the headset

### Added
- **Crouch guard:** the crouch button is ignored while a chest, inventory, store, the big map, a text box or the pause
  menu is open. The Frame's right stick click is also VHVR's "middle click" there, so it used to crouch you on every
  click.
- **Double-click to crouch** (optional, off by default): going down needs two quick clicks; standing up is one.
- **Big map hints** show the Frame controls that work with VHVR's laser:
  - zoom: left D-pad up/down;
  - ping: right stick click;
  - add pin: grip + trigger;
  - remove pin: bumper.

  This replaces Valheim's Xbox map controls. The first map open each session logs the exact hint keys.
- **Saved server passwords:** remembered after a successful join (Windows DPAPI-encrypted) and filled in next time.
  A rejected saved password is forgotten, and there's a "Forget saved passwords" option.
- **Seated eye height:** learned from seated recenters, with sliders for both standing and seated heights. With both
  known, the seated/standing split is halfway between them instead of a fixed 80%.
- **Steam Frame tab pages:** Settings, Button guide and Recenter & height. The guide page adds the big map and chest
  controls, and a pointer to VHVR's wrist minimap angle setting.
- **Recenter pose picture** with instructions, plus the last height measurement and the current seated/standing split.

### Changed
- Tab renamed from "Frame" to "Steam Frame", on two lines like "Motion Control".
- More settings on the tab: crouch, server passwords, prompt style, map hints and Frame detection.
- The crouch prompt shows "2x" when double-click crouch is on.

### Fixed
- The "thumb-on-stick can't be sensed" message no longer appears at startup. SteamVR reports no binding for a moment
  while it starts; the message now waits until thumb sensing has been missing for 30 s.

## 0.1.0 (2026-11-08): first combined plugin

Replaced the earlier local "VR Gamepad Prompts" plugin and hand edits to the Frame binding, with every feature split into
its own module. It imports settings from VR Gamepad Prompts once.

### Added
- **Steam Frame detection** through SteamVR.
- **Frame button prompts:** gamepad glyphs in prompts and menu hints, labelled with the Frame button VHVR uses. This
  carries over from VR Gamepad Prompts.
- **Chest buttons:** right / left stick click = Place stacks / Take all. Carried over.
- **Map position sharing** turned on at each join. Carried over.
- **Punch checkboxes:** require grip / trigger / thumb on stick, with a looseness warning. One rule for creatures and
  objects; diagnostics option.
- **Seated / standing:** Auto mode keeps your standing height when you recenter seated; it learns the standing height.
  VHVR's `PlayerMinEyeHeight` workaround is no longer needed.
- **Steam button recenter** also runs VHVR's recenter and height measurement.
- **Frame binding tweaks** applied by the plugin from VHVR's untouched copy, each with its own switch. They take effect
  after a restart:
  - left stick click = sprint;
  - right stick click = crouch;
  - no right-stick flick sprint/crouch;
  - thumb touch sensing.
- **Frame tab** in VHVR's settings with these settings and a live button guide.

### Fixed
- **Build-piece tilt:** grip + right stick up/down (face piece away/toward) works again. The earlier hand edit that
  removed stick-flick sprint/crouch had also deleted the up/down directions those chords need.

## Before 0.1.0: VR Gamepad Prompts (local only)
- A local plugin that switched Valheim's prompts to gamepad glyphs under VR, added the chest stick-click buttons and
  public map position, plus early punch and height experiments.
- The Frame binding was edited by hand. VHVR's original is kept as the base copy.
