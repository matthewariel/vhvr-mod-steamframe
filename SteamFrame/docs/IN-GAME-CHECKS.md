# In-game checks

What still needs trying in the headset, newest first. Tick items off (or tell Claude) as you go; anything that
misbehaves, describe it and the next session reads `BepInEx/LogOutput.log` for the details.

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
