# Steam Frame for VHVR: features and changes from VHVR

**Steam Frame for VHVR** is a BepInEx plugin that runs alongside [VHVR](https://github.com/brandonmousseau/vhvr-mod)
(Valheim VR). It makes the Valve Steam Frame controllers feel right in Valheim without changing VHVR itself or any
SteamVR setting. This file lists everything it adds or changes compared to plain VHVR. Version history is in
[CHANGELOG.md](CHANGELOG.md).

- **Version:** 0.3.1
- **Built against:** VHVR 0.11.1, BepInEx 5. `scripts\Check-VhvrCompat.ps1` checks any other VHVR version before you
  play on it.
- **Requires:** VHVR (hard dependency). Saved server passwords need Windows DPAPI.
- **Settings:** in game, Settings > VHVR > **Steam Frame** tab, saved with OK. Or edit
  `BepInEx/config/local.vhvr.steamframe.cfg`.

Status legend: **Confirmed** = tried in the headset and working; **Untested** = built and logic-tested, not yet tried in
the headset (see [IN-GAME-CHECKS.md](IN-GAME-CHECKS.md)).

Scope legend (the same badges appear on the in-game tab):

| Scope | Meaning |
|---|---|
| **All headsets** | Works with any VR controller VHVR supports. **Tested on Steam Frame only.** |
| **Steam Frame** | Only for Steam Frame controllers. |
| **Extra** | Not about VR; a convenience that lives in this plugin. |

## At a glance

| Area | Scope | Plain VHVR | With this plugin | Depends on | Status |
|---|---|---|---|---|---|
| Button prompts | All headsets (tested on Steam Frame only) | Keyboard prompts in VR (`[E] Pick up`) | Gamepad glyphs labelled with the button that does it (trigger, A, D-pad right...) | Button names come from the active controller profile (Steam Frame only so far); with no profile, Valheim's own labels | Confirmed |
| Big map hints | All headsets (tested on Steam Frame only) | Xbox map controls (LT/RT zoom, X ping) that don't work with the laser | The buttons that do work with the laser (D-pad zoom, stick-click ping, grip + trigger pin) | The profile's map labels | Untested |
| Chest buttons "Place stacks" / "Take all" | All headsets (tested on Steam Frame only) | Don't respond in VR | Right / left stick click | VHVR's crouch and sprint toggles must be on the stick clicks (the Frame binding tweaks do this) | Confirmed |
| Crouching by accident in chests / map | All headsets (tested on Steam Frame only) | Every crouch-button click there also crouched you | Crouch is ignored while a chest, menu, store or the big map is open; optional double-click | Nothing | Untested |
| Bare-hand punch | All headsets (tested on Steam Frame only) | Grip for creatures; grip + trigger + laser off for trees, rocks, bushes | Checkboxes: grip / trigger / thumb resting on the stick (default grip + thumb), same rule for everything, with a warning when too loose | Thumb-on-stick needs a stick-touch binding (Frame tweak `ThumbTouchSensing`); ignored otherwise | Confirmed (one hand) |
| Seated play | All headsets (tested on Steam Frame only) | Recentering while seated makes you chest-height to other players | Auto seated/standing: seated, you keep your standing height | Nothing | Confirmed |
| Steam button recenter | All headsets, SteamVR (tested on Steam Frame only) | Re-centres the view but doesn't re-measure your height | Also does VHVR's recenter and height measurement | Nothing | Confirmed (event seen) |
| Recenter pose | All headsets (tested on Steam Frame only) | Not explained anywhere in game | Picture and instructions on the settings tab | Nothing | Untested |
| Settings tab | All headsets UI (tested on Steam Frame only) | These settings don't exist | A "Steam Frame" tab in VHVR's settings: settings grouped by scope, button guide, recenter & height | The button guide reads the active profile's binding file | Confirmed (0.1 tab); 0.3.0 layout untested |
| Controller detection | All headsets mechanism (tested on Steam Frame only) | None | Asks SteamVR which controllers are in use and picks a profile | Profiles: Steam Frame | Confirmed |
| Sprint | **Steam Frame** | Right stick flick up (easy to trigger while turning), or hold Y | Left stick click toggles sprint; hold Y still works | Frame binding file | Confirmed |
| Crouch button | **Steam Frame** | Right stick flick down | Right stick click toggles crouch | Frame binding file | Confirmed (single click) |
| Thumb touch sensing | **Steam Frame** | Only "flick up" was sensed | Whole-stick touch is sensed, for the punch requirement | Frame binding file | Confirmed |
| Server passwords | **Extra** (not VR) | Typed with the virtual keyboard on every join | Remembered after a successful join (encrypted), filled in next time | Windows DPAPI | Untested |
| Map position sharing | **Extra** (not VR) | Off unless you tick it on the full map | Turned on each time you join a world | Nothing | Confirmed |

## All headsets vs Steam Frame

The code, the docs and the in-game tab all use the same three groups.

- **All headsets.** Prompts, big map hints, chest buttons, the crouch guard, punch requirements, seated/standing with
  Steam-button recenter, the settings tab and controller detection. None of this code names a headset. Where it needs
  something controller-specific (which button to show, which binding file to read) it asks the **active controller
  profile**. With no profile, it does nothing special: prompts fall back to Valheim's own labels.
  **All of it has only ever been tested on a Steam Frame**, because that's the only headset available. On another
  headset it should work, but nobody has tried.
- **Steam Frame.** The binding tweaks (stick clicks for sprint and crouch, no stick-flick sprint/crouch, thumb touch
  sensing) edit VHVR's Steam Frame binding file and nothing else. The Steam Frame profile holds the Frame's button
  names. On another headset this part stays idle.
- **Extra.** Saved server passwords and map position sharing have nothing to do with VR. They're here because they're
  tedious in a headset.

What another headset would need: one controller profile (button names + binding file name), and optionally its own
binding tweaks module. See [ARCHITECTURE.md](ARCHITECTURE.md).

## Details

### Steam Frame detection
*Scope: All headsets mechanism (tested on Steam Frame only). The only profile so far is Steam Frame.*

- Asks SteamVR for each hand's controller type and recognises `frame_controller`. It checks every 2 s until it knows,
  then every 15 s.
- The result appears at the top of the Steam Frame tab.
- `[General] FrameDetection`: Auto (default), Force or Off. It only affects Frame-specific labels; binding tweaks and
  other settings apply either way.

### Button prompts (Prompts module)
*Scope: All headsets (tested on Steam Frame only).*

- **The problem:** Valheim shows keyboard prompts under VR because it only shows gamepad glyphs after real gamepad
  input. VHVR feeds SteamVR actions in a way that doesn't count as gamepad input.
- **The fix:** only the prompt-text code paths are switched to gamepad mode. Valheim's ~70 other gamepad checks are left
  alone, so VHVR's laser pointer and menus behave as before. The prompt paths are:
  - hover text
  - interaction prompts
  - the crosshair
  - menu button icons
  - tame, saddle, pet, item stand and cooking station text
- With Frame controllers, prompts use the Frame button VHVR binds each action to:

  | Action | Frame button |
  |---|---|
  | Use / place | Trigger |
  | Jump | A |
  | Dodge | X |
  | Sprint | Left stick click (or Y if the sprint tweak is off) |
  | Crouch | Right stick click ("2x" when double-click crouch is on) |
  | Inventory | D-pad right |
  | Map | View |
  | Menu | Menu |
  | Build menu | Right bumper |
  | Remove piece | Grip + A |

- `ButtonLabels` = Auto / Frame / Xbox.
- `PromptOverrides` replaces any label by hand, for example `Use=Right A; AltPlace=Left Grip`.

### Big map hints (new in 0.2.0)
*Scope: All headsets (tested on Steam Frame only).*

- Valheim's map hints show its gamepad map controls, which don't apply in VR.
- While the big map is open, the hints show the Frame controls that work with VHVR's laser on the map:

  | Map action | Frame control |
  |---|---|
  | Zoom | Left D-pad up / down |
  | Ping | Right stick click |
  | Add pin | Grip + trigger |
  | Remove pin | Bumper |
  | Cross out a pin | Trigger on the pin |
  | Choose pin icons and filters | Point and pull the trigger |
  | Close | Map button |

- The first time the map opens each session, the log records the exact hint keys Valheim asked for, so any label that
  is still wrong can be fixed.
- `[Prompts] FrameMapHints` (on).

### Frame binding changes (Bindings module)
*Scope: Steam Frame.*

- **What is edited:** Valheim's copy of VHVR's Steam Frame binding,
  `valheim_Data/StreamingAssets/SteamVR/bindings_frame_controller.json`. The changes reach only Valheim, on Frame
  controllers.
- **Safe to switch off:** VHVR's untouched file is kept in `BepInEx/config/VhvrSteamFrame/`. Every write starts from that
  copy, so turning a tweak off restores VHVR's binding for that button.
- **VHVR updates:** if a VHVR update replaces the file, the new one becomes the base copy.
- **Restart:** Valheim needs one restart to load a change.

| Control | VHVR's Frame binding | With the tweaks (all on by default) | Setting |
|---|---|---|---|
| Left stick click | Middle click | Sprint toggle (and "Take all" in chests) | `SprintToggleOnLeftStickClick` |
| Right stick click | Middle click (move item, ping) | Middle click + crouch toggle (and "Place stacks" in chests) | `CrouchOnRightStickClick` |
| Right stick flick up / down (thumb touching) | Sprint / crouch | Nothing. The directions stay defined so the grip + stick up/down build chords keep working | `NoStickFlickRunCrouch` |
| Thumb resting anywhere on either stick | Only "flick up" was sensed (teleport, unused) | Whole-stick touch is sensed, for the thumb-on-stick punch requirement | `ThumbTouchSensing` |

`ManageFrameBindings` turns all of them off at once and puts VHVR's binding back.

### Chest buttons (MenuStickClicks module)
*Scope: All headsets (tested on Steam Frame only).*

- Valheim labels "Place stacks" and "Take all" with stick clicks, which never reach it under VR controls.
- While the inventory or a chest is open:
  - Right stick click = Place stacks
  - Left stick click = Take all
- `[Menus] StickClickMenuButtons` (on).

### Crouch guard (new in 0.2.0)
*Scope: All headsets (tested on Steam Frame only).*

- **Ignore crouch in menus:** on the Frame, the right stick click is both crouch and VHVR's "middle click", which moves
  items in chests and pings on the map. So every click there also crouched you. Now the crouch button is ignored while
  any of these is open:
  - inventory or chest
  - store
  - big map
  - text box
  - pause menu

  `[Crouch] BlockCrouchInMenus` (on).
- **Double-click to crouch** (off by default):
  - Going down needs two clicks within `DoubleClickWindow` (0.45 s), so a stray click in a fight doesn't crouch you.
  - Standing up is still one click, and sprinting still stands you up.
  - Physically ducking (VHVR's roomscale sneak) isn't affected.
- It cancels VHVR's crouch toggle after VHVR asks for it, and keeps VHVR's crouch-camera flag in step.

### Bare-hand punching (Punch module)
*Scope: All headsets (tested on Steam Frame only).*

- **VHVR's rule is fixed in code:**
  - creatures and training dummies: grip held;
  - trees, rocks, bushes and other objects: grip + trigger with the laser pointer off.
- **This plugin uses one configurable rule for everything.** A free hand punches when the ticked inputs are held:

  | Requirement | Setting | Default |
  |---|---|---|
  | Grip | `RequireGrip` | On |
  | Trigger | `RequireTrigger` | Off |
  | Thumb resting on that hand's stick | `RequireThumbOnStick` | On (Frame touch sensor) |

  The default, grip + thumb, means a fist you're actually making, without the trigger.
- **Looseness warning** (on the tab, in the setting descriptions, and once in game):
  - fewer than two requirements: "loose", meaning accidental punches when grabbing or touching things;
  - neither grip nor trigger required: "very loose".
- **Unchanged from VHVR:** swing speed, stamina, crafting-station hits and friendly-creature checks.
- `Diagnostics` (off) logs why each fist contact did or didn't become a punch.

### Seated / standing play (Posture module)
*Scope: All headsets (tested on Steam Frame only).*

- **The VHVR problem:** VHVR puts your view at your measured eye height, clamped to `PlayerMinEyeHeight` /
  `PlayerMaxEyeHeight`, above your character's feet. Recentering while seated makes you about 0.5 m short. The usual
  workaround is raising `PlayerMinEyeHeight`, but that floats your feet when standing.
- **Auto mode (default):**
  - Each VHVR height measurement is classed as standing or seated.
  - Seated, you play at your standing eye height.
  - Both heights are learned from recenters and can also be set by hand.
  - Once both are known, the split is halfway between them. Until then, below 80% (`SeatedBelowPercent`) of your
    standing height counts as seated.
- **Other modes:** Standing (VHVR's behaviour) and Seated (always use the standing height).
- **Steam button recenter:** when SteamVR recenters (`StandingZeroPoseReset`, `SeatedZeroPoseReset` or a chaperone
  change), VHVR's own recenter and height measurement also run. VHVR alone only measures at game start, on the Home key,
  or with its hands-to-eyes pose. Setting: `RecalibrateOnSteamVRRecenter` (on).
- **Feedback:** a short message after each recenter ("Seated: keeping your standing height (1.67 m)"), and the last
  measurement on the tab.
- VHVR's config file isn't modified. With this plugin, `PlayerMinEyeHeight` can stay at its normal value.

### Steam Frame settings tab (FrameTab module)
*Scope: All headsets UI (tested on Steam Frame only). The tab is named "Steam Frame" for now.*

- **Where:** a "Steam Frame" tab in VHVR's own settings dialog, built with VHVR's controls, so it looks the same and
  saves with OK (Back discards). The other tab buttons re-centre to make room.
- **Scope badges (new in 0.3.0):** every section header carries a coloured badge, **ALL HEADSETS** (blue),
  **STEAM FRAME** (amber) or **EXTRA** (grey), and a legend under the status line explains them.
- **Page: Settings**, grouped by scope:
  - left column, ALL HEADSETS: punch checkboxes with a live looseness warning, crouch, prompts (button labels, map
    hints);
  - right column, STEAM FRAME: the Frame binding tweaks and Steam Frame detection;
  - below that, EXTRA: server passwords and "Share map position on join" (new row in 0.3.0).
- **Page: Button guide**
  - what every button and chord of the active controller does on each hand, read live from the binding file in use;
  - the big map controls and chest controls;
  - where to change the wrist minimap angle.
- **Page: Recenter & height**
  - a picture of VHVR's recenter pose, with instructions;
  - posture mode;
  - standing and seated eye heights;
  - the Steam-button recenter option;
  - the last measurement and the current seated/standing split.
- A status line shows whether Frame controllers were detected and whether a binding change needs a restart.
- `[FrameTab] ShowFrameTab` (on).

### Saved server passwords (ServerPasswords module, new in 0.2.0)
*Scope: Extra (not VR).*

- **How it saves:** after you join a password-protected server, the password is saved and filled in automatically next
  time. It uses Valheim's own `+password` mechanism, so the password box doesn't appear.
- **Encryption:** Windows DPAPI, so only your Windows account on this PC can read the saved file.
- **Wrong passwords:** a saved password the server rejects is forgotten, and you're asked again. A wrong typed password
  is never saved.
- **Forget them all:** tick "Forget saved passwords" and press OK.
- Settings: `[Servers] RememberPasswords` (on), `ForgetSavedPasswords`.

### Map position sharing (MapSharing module)
*Scope: Extra (not VR).*

- Turns on Valheim's "public position" (other players see you on the map) each time you join a world.
- The checkbox is hard to reach in VR; you can still untick it on the full map.
- `[Map] SharePositionOnJoin` (on).

### Logging
- The VHVR version at startup, with one warning if it isn't a tested one.
- One line per module at startup (ready / disabled and why), then a summary line (`9/9 modules active`) and a
  `Modules:` line that groups them: `[all headsets] ... [Steam Frame] ... [extras] ...`.
- A one-time line when Frame controllers are detected.
- "Height:" lines for each measurement.
- Optional punch diagnostics.
- `[Posture] LogSteamVREvents` logs each kind of SteamVR event once per session.

## Settings reference

File: `BepInEx/config/local.vhvr.steamframe.cfg`. In game: Settings > VHVR > Steam Frame.
Section names, setting names and defaults are fixed: changing one would silently reset players' settings. "All headsets"
settings are tested on Steam Frame only.

| Section | Setting | Scope | Default | What it does |
|---|---|---|---|---|
| General | FrameDetection | All headsets | Auto | Auto / Force / Off for Frame-specific labels |
| General | ImportedOldSettings | All headsets | false | Internal: settings from the older "VR Gamepad Prompts" plugin were carried over |
| Prompts | ForceGamepadPrompts | All headsets | true | Gamepad glyphs in hover text and interaction prompts |
| Prompts | ForceGamepadMenuHints | All headsets | true | Gamepad icons next to menu buttons |
| Prompts | ButtonLabels | All headsets | Auto | Auto / Frame / Xbox labels |
| Prompts | PromptOverrides | All headsets | (empty) | `Action=Label; ...` custom labels |
| Prompts | FrameMapHints | All headsets | true | Big map hints show Frame laser controls |
| Menus | StickClickMenuButtons | All headsets | true | Stick clicks = Place stacks / Take all in chests |
| Crouch | BlockCrouchInMenus | All headsets | true | Ignore crouch in chests, menus and the map |
| Crouch | DoubleClickToCrouch | All headsets | false | Crouching needs a double click |
| Crouch | DoubleClickWindow | All headsets | 0.45 | Seconds between the two clicks (0.2–1.0) |
| Map | SharePositionOnJoin | Extra | true | Public map position on each join |
| Servers | RememberPasswords | Extra | true | Save server passwords after a successful join |
| Servers | ForgetSavedPasswords | Extra | false | Tick + OK deletes all saved passwords |
| Punch | RequireGrip | All headsets | true | Punch needs grip |
| Punch | RequireTrigger | All headsets | false | Punch needs trigger |
| Punch | RequireThumbOnStick | All headsets | true | Punch needs thumb resting on the stick |
| Punch | Diagnostics | All headsets | false | Log why each fist contact did or didn't punch |
| Posture | Mode | All headsets | Auto | Auto / Standing / Seated |
| Posture | StandingEyeHeight | All headsets | 0 (learned) | Standing eye height in m (0–2.5) |
| Posture | SeatedEyeHeight | All headsets | 0 (learned) | Seated eye height in m (0–2.0) |
| Posture | SeatedBelowPercent | All headsets | 80 | Seated threshold until the seated height is known (50–95) |
| Posture | RecalibrateOnSteamVRRecenter | All headsets | true | Steam button recenter also re-measures height |
| Posture | LogSteamVREvents | All headsets | true | Log each SteamVR event type once per session |
| Bindings | ManageFrameBindings | Steam Frame | true | Apply the tweaks below (off = VHVR's binding) |
| Bindings | ThumbTouchSensing | Steam Frame | true | Sense a thumb resting on either stick |
| Bindings | SprintToggleOnLeftStickClick | Steam Frame | true | Left stick click = sprint toggle |
| Bindings | CrouchOnRightStickClick | Steam Frame | true | Right stick click = crouch toggle |
| Bindings | NoStickFlickRunCrouch | Steam Frame | true | No sprint/crouch on right stick flick up/down |
| FrameTab | ShowFrameTab | All headsets | true | Add the Steam Frame tab |

## Files it writes

| File | When |
|---|---|
| `BepInEx/config/local.vhvr.steamframe.cfg` | Its settings (BepInEx) |
| `valheim_Data/StreamingAssets/SteamVR/bindings_frame_controller.json` | Only when the binding tweaks change; only if the content differs |
| `BepInEx/config/VhvrSteamFrame/bindings_frame_controller.vhvr.json` | VHVR's untouched Frame binding (the base copy) |
| `BepInEx/config/VhvrSteamFrame/bindings_frame_controller.written.sha256` | Fingerprint of the last write, to notice VHVR updates |
| `BepInEx/config/VhvrSteamFrame/server-passwords.dat` | Only once a password is saved (DPAPI-encrypted); deleted when emptied |

## What it does not change
- SteamVR's settings, bindings for other games, or anything outside the Valheim folder.
- VHVR's config file (`org.bepinex.plugins.valheimvrmod.cfg`) or VHVR's DLL.
- Other controllers: the binding edits only touch the Steam Frame binding, and the labels switch to Frame only when
  Frame controllers are detected (or `FrameDetection = Force`).

## Known limitations
- **Binding changes need one restart of Valheim:** SteamVR loads the binding at game start.
- **Map hint labels are partly guesses:** the map's hint layout is in compressed game assets, so the labels are matched
  to Valheim's gamepad map controls. The first map open logs the exact keys, so any mismatch can be fixed.
- **Saved passwords rely on Windows DPAPI.** The module tests it at startup and turns itself off if it isn't available
  (not tried under Linux/Proton).
- **Finger poses:** VHVR copies them straight from SteamVR's hand skeleton. A finger that won't straighten is the
  controller or driver, not something this plugin can fix.
- **Tab layout:** positions follow VHVR's settings layout. A VHVR UI change could make rows overlap; the tab then still
  works, and the logic is unaffected.

## Before a public release
- Change the plugin GUID from `local.vhvr.steamframe` to a real one (this resets everyone's settings file name, so do it
  before the first release).
- Licence: the plugin now lives in a fork of VHVR, so VHVR's GPL-3.0 `LICENSE` at the repository root covers it, and
  `scripts\Package.ps1` puts it in the zip. Credit VHVR in the release notes.
- Set the repository's GitHub "About" text (it still describes plain VHVR).
- Decide whether the all-headset modules become a generic tab or a separate plugin, and rename the tab if so (one
  constant, `TabName`).
- Decide public defaults:
  - `[Map] SharePositionOnJoin` should probably default to **off**, since sharing your position is a privacy choice;
  - `[Posture] LogSteamVREvents` should default to **off**.
- Remove the "VR Gamepad Prompts" settings import (`ConfigMigration`), which only matters on this PC.
- Confirm every "Untested" row above in the headset first.
- Contributing pieces to VHVR instead (not planned for now): [UPSTREAM-NOTES.md](UPSTREAM-NOTES.md).
