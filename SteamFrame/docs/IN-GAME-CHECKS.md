# In-game checks

What still needs trying in the headset, newest first. Tick items off (or tell Claude) as you go; anything that
misbehaves, describe it and the next session reads `BepInEx/LogOutput.log` for the details.

## New in 0.3.1 (installed 2026-11-10, 22:41)

**Log first**
- [ ] `Steam Frame for VHVR 0.3.1: 9/9 modules active (all on).`

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
- [ ] Tab button reads "Steam / Frame" on two lines, like "Motion Control".
- [ ] Three page buttons along the top (Settings, Button guide, Recenter & height) switch pages; nothing overlaps or
      runs off the panel edges (the top row and the right-hand column are the likeliest to be cut off).
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
- [ ] Out in the world: right stick click still crouches; clicking again or sprinting stands you up.
- [ ] Optional: turn on "Double-click to crouch" and check one click does nothing, two quick clicks crouch, one click stands up.

**Big map hints** (open the map from the right wrist)
- [ ] Hints show D-pad up/down for zoom, right-stick-click for ping, grip + trigger for pins, bumper for remove,
      instead of LT/RT/X. If any hint still shows an Xbox button, say which. The log line starting
      "Prompts: big map open" lists exactly what the hints asked for, so it can be fixed precisely.

**Server password**
- [ ] Join your password server once (type it): message "Server password saved for next time" after spawning.
- [ ] Quit to menu, join again: no password box.
- [ ] "Forget saved passwords" + OK: next join asks again.

## Still open from 0.1.0
- [ ] Two-hand punch (both fists, nothing held). The log only shows right-hand punches so far.
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

## Not a plugin issue
- Left middle finger stays half curled: VHVR copies finger poses straight from SteamVR's hand skeleton, the same code
  for both hands, so this comes from the controller/driver. Check SteamVR > Settings > Controllers > Test Controller:
  if that hand model's middle finger won't straighten either, it's the Frame controller's finger sensing.
