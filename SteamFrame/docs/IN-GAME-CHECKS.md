# In-game checks

What still needs trying in the headset, newest first. Tick items off (or tell Claude) as you go; anything that
misbehaves, describe it and the next session reads the logs for the details: every game session since 0.3.3 is in
`BepInEx/SessionLogs/<start time>.log` (`LogOutput.log` only has the latest).

## New in 0.3.3 (installed 2026-11-10, 23:25)

**Log first**
- [ ] `Steam Frame for VHVR 0.3.3: 10/10 modules active (all on).`
- [ ] `SessionLogs: this session is also saved to BepInEx/SessionLogs/<start time>.log (keeping the last 50).`
- [ ] That file starts with BepInEx's own first lines (the preloader), like `LogOutput.log`.
- [ ] After quitting the game, the file ends with the same lines as `LogOutput.log`.
- [ ] Start the game a second time: there are two session files, and the first one is unchanged.

**Also installed: a VHVR test build** (not part of this plugin)
`BepInEx/plugins/ValheimVRMod.dll` is VHVR from Oct 19 (commit `7cde6f5a`, the build that was installed) plus a fix
for the wrist quick bar's cursor, on VHVR branch `WristQuickBarCursorBehind`. The original DLL is in
`BepInEx/vhvr-backup/`.

First try (installed 2026-11-10, 23:20), cursor behind the bar:
- [x] On sit and recenter the red cursor is tinted under the gray square and hidden behind the picture, like the
      radial wheel.
- [x] Not good enough on the map: its picture is a solid square filling the tile, so the cursor and the hover
      highlight were both completely hidden. Same for the black chat square. The cursor also looked set back from the
      hand.

Second try (installed 2026-11-11, 00:17): the hovered wrist-bar picture is drawn about 60% opaque, and the cursor is
pushed back along the line of sight so it stays under the hand.
- [ ] Hover the map: the map picture goes see-through, and the red cursor and the lighter-gray hover square show
      through it. Moving off it, the map is solid again.
- [ ] Same on the chat square, power, sit and recenter.
- [ ] The red cursor sits under the hand, not shifted to one side.
- [ ] The radial wheel looks the same as before, and its icons never go see-through.
- [ ] Close the quick menu while hovering a wrist-bar button: that picture is solid again next time the bar shows.

## New in 0.3.2 (installed 2026-11-10, 23:04)

**Log first**
- [ ] `Steam Frame for VHVR 0.3.2: 9/9 modules active (all on).` (0.3.3 is installed now; check its line instead.)

**Steam Frame tab: page buttons** (Settings > VHVR > Steam Frame)
- [ ] The three page buttons (Settings, Button guide, Recenter & height) sit fully below VHVR's tab row, readable and
      clickable. In 0.3.1 they were about 80% hidden behind it.
- [ ] Under them, in order: the green "Steam Frame controllers detected" line, then the badge legend, then the page.
      Nothing touches or overlaps.
- [ ] Each page fits without a scroll bar: Settings ends with the saved-passwords note; Button guide's lists and
      "Wrist map angle" end above the panel's bottom edge; Recenter & height's picture isn't clipped at the top and
      the how-to text ends above the bottom edge.

## New in 0.3.1 (installed 2026-11-10, 22:41)

**Log first**
- [x] `Steam Frame for VHVR 0.3.1: 9/9 modules active (all on).` (22:48 session)

**Big map hints** (open the big map)
- [ ] Zoom shows D-pad down / up, not LT / RT.
- [ ] Remove pin shows the bumper.
- [ ] Cross off pin shows the trigger.
- [ ] Ping (right stick click) and Add pin (Grip + trigger) are unchanged.
- [ ] "Toggle pins" shows the trigger (point at the pin icon and pull). Say so if that isn't how it works.

**Seen in the 0.3.0 session's log, needs your word on how it felt**
- [ ] The message "Punch: thumb-on-stick can't be sensed..." appeared once, around a moment when one controller
      dropped out. Does punching with grip + thumb on the stick still work, and still refuse without the thumb? If yes,
      it was a false alarm (the 30 s wait before warning is too short for a controller dropout).
      It showed again in the 0.3.1 session (22:49), soon after the left controller first appeared: at the main menu
      the log had "left: none", and both Frame controllers were only detected at 22:49:04, after the SteamVR dashboard
      was opened and closed. Two-hand punch worked in that session.
- [ ] A seated recenter measured 1.27 m and kept your standing height (1.61 m). Did your in-game height look right?

**For whoever troubleshoots next:** the 0.3.0 session's log (2026-11-10, 20:38 to about 22:30) had no warnings or errors
from this plugin: VHVR 0.11.1 recognised, 9/9 modules, Steam Frame detected, tab added, binding left untouched, saved
server password used on join. Starting Valheim overwrites `LogOutput.log`, so read it before the next launch.

## New in 0.3.0 (installed 2026-11-10)

0.3.0 reorganised the code without changing gameplay, so the main question is "does everything still work as in 0.2.0?".
The 0.2.0 list below is still open and doubles as that check.

**Log first** (`BepInEx\LogOutput.log`, after starting the game once; no headset needed)
- [ ] `VHVR 0.11.1 (tested with this plugin).`
- [ ] `Steam Frame for VHVR 0.3.0: 9/9 modules active (all on).`
- [ ] The next line groups them:
      `Modules: [all headsets] Prompts, MenuStickClicks, Crouch, Punch, Posture, FrameTab [Steam Frame] Bindings [extras] MapSharing, ServerPasswords`.
      A module that failed shows `(off)` after its name.
- [ ] `Steam Frame detected (left: frame_controller, right: frame_controller).`
- [ ] `FrameTab: Steam Frame tab added to VHVR's settings.` (after opening Settings > VHVR)
- [ ] `Bindings: Frame binding already up to date.` (0.3.0 must not rewrite the binding)

**Steam Frame tab: scope badges** (Settings > VHVR > Steam Frame)
- [ ] Under the status line there's a one-line legend: ALL HEADSETS (blue), STEAM FRAME (amber), EXTRA (grey). It's
      readable and doesn't touch the status line above or the first header below.
- [ ] Settings page, left column: Punch, Crouch and Prompts, each header with a blue ALL HEADSETS badge on the same line.
- [ ] Settings page, right column: "Frame bindings" with an amber STEAM FRAME badge (5 rows + "Steam Frame detection"),
      then "Servers and map" with a grey EXTRA badge (3 rows + the saved-passwords note).
- [ ] No header wraps onto two lines, nothing overlaps, nothing is cut off at the bottom or the right edge.
- [ ] Button guide page: title "Steam Frame buttons" with a STEAM FRAME badge; the right-hand notes are titled
      "Map and chests" with an ALL HEADSETS badge. The left and right controller lists still line up.
- [ ] Recenter & height page: the picture isn't cut off at the top; "Seated / standing" has an ALL HEADSETS badge.

**Map sharing row** (new)
- [ ] "Share map position on join" is ticked. Untick it, press OK, restart Valheim: still unticked, and joining a world
      doesn't turn public position on.
- [ ] Tick it again, OK, join a world: other players see you on the map again.

**Same as before?** (quick pass; details in the 0.2.0 list)
- [ ] Prompts show Frame buttons.
- [ ] Right stick click crouches in the world, not in a chest.
- [ ] Punch with grip + thumb on the stick works.
- [ ] Recenter pose keeps the right height.

## New in 0.2.0 (installed 2026-11-09)

**Steam Frame tab** (Settings > VHVR)
- [ ] Three page buttons along the top (Settings, Button guide, Recenter & height) switch pages; nothing overlaps or
      runs off the panel edges. (0.3.1: they were hidden behind VHVR's tab row; moved down in 0.3.2, see above.)
- [ ] Recenter & height page shows the recenter-pose picture.
- [ ] Change a setting, press OK, restart Valheim: it stuck. Change one and press Back: it didn't.

**Seated / standing**
- [ ] Sit, do the recenter pose: "Seated: keeping your standing height" and normal height. The page then shows a
      seated eye height (learned) and "Split at ..." between your two heights.
- [ ] Stand, recenter: normal height, standing height updated.
- [ ] Optional: set the standing/seated heights by hand on that page and check Auto still picks the right one.

**Crouch**
- [ ] In a chest: right stick click does its menu job (move item / place stacks) without crouching you.
- [ ] On the big map: right stick click pings without crouching you.
- [ ] Optional: turn on "Double-click to crouch" and check one click does nothing, two quick clicks crouch, one click stands up.

**Big map hints** (open the map from the right wrist)
- [ ] Hints show D-pad up/down for zoom, right-stick-click for ping, grip + trigger for pins, bumper for remove,
      instead of LT/RT/X. If any hint still shows an Xbox button, say which. The log line starting
      "Prompts: big map open" lists exactly what the hints asked for, so it can be fixed precisely.

**Server password**
- [ ] "Forget saved passwords" + OK: next join asks again.

## Still open from 0.1.0
- [ ] Steam-button recenter while seated keeps your standing height. The log already shows the Steam button sends
      SteamVR's StandingZeroPoseReset and the plugin re-measures on it; what's left is how it feels in the headset.
- [ ] Build mode, "tilt": hold right grip, push the right stick up / down. The piece should turn to face away from /
      toward you (rotate left/right is grip + stick left/right, which already works).

## Confirmed working
- Steam Frame detected; tab appears (0.1.0).
- Recenter pose (hands to eyes) fixes height seated and standing.
- Build placement, rotation (grip + stick left/right) and removing pieces.
- One-hand punch needs grip + thumb on stick: the log shows "needs thumb on stick" refusals and punches when held.
  (The 0.1.0 "thumb-on-stick can't be sensed" message was a false alarm at startup; fixed in 0.2.0.)
- Prompts, Place stacks / Take all, bow, map sharing.
- Confirmed in 0.3.1 (2026-11-10):
  - Tab button reads "Steam / Frame" on two lines.
  - Out in the world, right stick click crouches; clicking again or sprinting stands you up.
  - Server password: typed once, "Server password saved for next time"; on the next join, no password box.
  - Two-hand punch (both fists, nothing held).

## Not a plugin issue
- Left middle finger stays half curled: VHVR copies finger poses straight from SteamVR's hand skeleton, the same code
  for both hands, so this comes from the controller/driver. Check SteamVR > Settings > Controllers > Test Controller:
  if that hand model's middle finger won't straighten either, it's the Frame controller's finger sensing.
