# In-game checks

What still needs trying in the headset, newest first. Tick items off (or tell Claude) as you go; anything that
misbehaves, describe it and the next session reads the logs for the details: every game session since 0.3.3 is in
`BepInEx/SessionLogs/<start time>.log` (`LogOutput.log` only has the latest).

## New in 0.5.3 (installed 2026-11-12)

**Log first**
- [ ] `Steam Frame for VHVR 0.5.3: ... modules active ...`

The Button guide picture is redrawn from your reference drawing. What page II should look like:
[button-guide-preview.png](button-guide-preview.png) (drawn without the game, so fonts and the box look differ).

**Page II Button guide: the picture**
- [ ] Both controllers look like your reference drawing: seen from above, with handles, the grip button on each
      handle's inner neck, and a small side view of the bumper and trigger under each handle.
- [ ] The boxes go face buttons, Stick click, Stick, **Grip**, Bumper, Trigger, and each line ends at the edge of its
      own box. Grip's line crosses the handle to the button on the inner neck; Bumper's and Trigger's go into the side
      view.
- [ ] The A/B/X/Y letters are easy to read, and the X line passes between A and B without touching them.
- [ ] The side views show the bumper and trigger clearly enough to tell which is which. If they're too small, say so:
      they can only grow if the faces shrink a little.
- [ ] The "Grip + ..." combinations between the handles are easy to read and don't run into the handles or the grip
      dots.

## New in 0.5.2 (installed 2026-11-11)

**Big map**
- [ ] The D-pad cross next to the pin icons is gone (0.5.1 missed it). The log line
      `Prompts: big map D-pad pictures hidden in VR: ...` names it. If it says `none found` and the cross is still
      there, the next line, `Prompts: pictures around the pin icon picker ...`, lists the pictures there by name, so
      the next fix can pick the right one.
- [ ] With Button labels = Keyboard, the cross is back (Valheim's own map).

## New in 0.5.1 (installed 2026-11-11, tested in the 23:16 session; 0.5.0's new button guide below)

**Log first**
- [x] `Steam Frame for VHVR 0.5.1: 10/10 modules active (all on).`
- [x] `Posture: mode Auto, standing eye height 2.04 m, seated eye height 1.55 m, follow head on, VHVR eye height range
      1.20-1.90 m, SteamVR floor reference ...`, and a moment later `Height: SteamVR floor reference ...; heights today:
      ...`. (No "since the heights were set" yet: the floor reference is only saved from 0.5.1's Set buttons on.)

**Seated / standing, reworked** (page III Height). Start by pressing **Set standing height** standing up straight,
then **Set seated height** sitting, so both are saved against today's SteamVR floor.

Result (23:16 session): works. Eye level matched other vikings and objects seated and standing, and it switched as
you sat down and stood up (the log shows `you stood up` / `you sat down` at levels 0.95-1.01 / 0.11-0.26). The heights
weren't set again, so the floor reference isn't saved yet (startup log: `SteamVR floor reference NaN`).
- [x] Seated after a recenter: your eyes are level with other vikings' (about where the Steam recenter put you on
      2026-11-11), not taller.
- [x] Stand up and wait a second: you stay level with other vikings (the view follows within about a quarter of a
      second), instead of getting taller. "Standing." notice; the log says `Height: you stood up ...`.
- [x] Sit down again and wait a second: still level. "Seated: playing at your standing height." notice.
- [ ] A quick duck (under a second) doesn't switch anything.
- [ ] Recenter with the Steam button standing, then seated: same eye level each time. If the log shows
      `SteamVR's floor reference moved` or `the headset jumped ... at a SteamVR recenter`, the heights were moved with
      the floor: say whether your eye level stayed right afterwards.
- [ ] The page shows "Now: seated / standing" matching what you're doing, and a new row **Switch when you sit or
      stand** (on). Turned off, it only switches when you recenter.
- [ ] After pressing both Set buttons once, next session: the startup log says how far SteamVR's floor moved since you set the heights, and your eye level
      is still right without setting them again.

**Page I: Button labels = Keyboard**
- [ ] Set Button labels to **Keyboard**, OK: prompts show Valheim's keyboard keys ([E] Pick up) instead of controller
      buttons, and the big map shows its usual hints without the Close map one. Back to Auto: Frame buttons again.

**Big map**
- [x] ~~The D-pad picture next to the pin icons is gone.~~ Still there (23:16 session): it isn't a gamepad hint picture
      (the log listed only the two by "Visible to other players"). See 0.5.2.

## New in 0.5.0 (installed with 0.5.1)

What page II should look like: [button-guide-preview.png](button-guide-preview.png) (drawn without the game, so
fonts and the box look differ).

**Log first**
- [ ] (See 0.5.1's log line above.)

**Page II Button guide: the picture**
- [x] ~~Both controllers are drawn from above like the real ones (round face, buttons where yours are), with a small
      side view of the bumper and trigger under each. Do they read as Frame controllers?~~ No: "the images still
      aren't good". Redrawn from your reference drawing in 0.5.3.
- [ ] A line runs from each button to its box, and every line ends at the edge of its own box (if the lines and boxes
      are out of step, say which). The A/B/X/Y letters aren't covered by the dots.
- [ ] Nothing crosses the panel's edges or runs into the page buttons at the top.
- [ ] The text is easy to read in the headset: button name on top, what it does underneath. Is anything too small,
      clipped or wrapped? Say which box.
- [ ] The boxes say what your buttons do: A Jump, X Dodge, Y Sprint (hold), B Quick switch, left stick click
      Sprint, right stick click "Middle click (ping) + Crouch", View Wrist map, Menu Menu.
- [ ] Trigger, Grip and Stick are marked "(fixed)" and aren't clickable.
- [ ] ~~The four "Grip + ..." lines under the picture fit between the grip lines.~~ Moved between the handles in
      0.5.3.

**Page II: remapping**
- [ ] Click the **A** box: the picture is replaced by a picker of 15 actions with "Jump" highlighted, and Cancel.
      Cancel brings the picture back with nothing changed.
- [ ] Click A, pick **Dodge**: A's box says Dodge, its name is orange with "- changed", and the note under the right
      column says to restart. The log has `Bindings: right/a set to dodge.`
- [ ] Restart Valheim: A dodges, and the box still says Dodge. The log has `Bindings: button remaps: right/a=dodge.`
- [ ] Click **Menu**, pick Jump: refused in orange ("Menu has to stay on a button..."), and nothing changes.
- [ ] Set a button to **Nothing**: its box says Nothing and, after a restart, the button does nothing.
- [ ] **Reset to default**: the first click asks "Click again to reset", and the second click puts every box back.
      After a restart, A jumps again.
- [ ] With "Apply binding tweaks" off on page I, the note says changed buttons aren't used.

## New in 0.4.0 (installed 2026-11-11)

**Log first**
- [x] `Steam Frame for VHVR 0.4.0: 10/10 modules active (all on).` (00:44 session)
- [x] Once only: `Posture: heights are now set with buttons on the Steam Frame tab, not learned from recenters. The old
      learned values (standing 1.98 m, seated 1.48 m) are no longer used.` Then `Posture: mode Auto, standing eye height
      not set, seated eye height not set`.

**Steam Frame tab: page buttons** (Settings > VHVR > Steam Frame)
- [ ] The three page buttons read **I Settings**, **II Button guide**, **III Height**, and the open page's button is
      highlighted (bold, light gold). In 0.3.3 they were blank.
- [ ] If any button is still blank, say so: the log line `FrameTab: button prefab texts: ...` shows what the button
      had inside, so it can be fixed precisely.

**Page II Button guide** (only margins fixed; replaced by the picture in 0.5.0)
- [ ] No text crosses the panel's left or right edge, or runs below its bottom edge.
- [ ] The build lines read "Grip + Stick up / down: Flip piece front/back" (they said "Tilt" before).
- [ ] "Map and chests" lists "Pin icon: point at it, pull trigger", "Remove pin: right bumper" and "Close: Menu button".

**Page III Height: Set standing / Set seated** (replaces Auto learning)

Result (00:44 session): the buttons worked, but SteamVR's floor was about 0.5 m off that day (standing read 2.04 m,
seated 1.55 m; other days ~1.52 / 1.27). Seated you were far too tall, standing up made it worse until a Steam
recenter. Reworked in 0.5.1 (see above), so the items below are replaced by 0.5.1's.

- [ ] Layout: picture and how-to text on the left, settings and two buttons on the right; nothing crosses the panel
      edges (the how-to text did in 0.3.3).
- [ ] Stand up straight, press **Set standing height**: green "Standing height set: 1.5x m." under the buttons, a
      matching notice, and the Standing slider jumps to that value.
- [ ] Sit as you play, press **Set seated height**: same, with your seated height (about 1.2-1.3 m).
- [ ] The note under them shows both heights and "Below 1.4x m counts as seated" (halfway between them).
- [ ] Press **OK**, restart Valheim: both heights are still there (the startup `Posture:` line shows them).
- [ ] Sitting, recenter (hands to eyes or the Steam button): "Seated: playing at your standing height (1.5x m)", and
      you're at standing height in game.
- [ ] Standing, recenter: "Standing: height 1.5x m", normal height. Recentering never changes the heights you set
      (check the page or the log).
- [ ] Press Set standing while sitting: it's refused ("too low for standing") and nothing changes.

**Big map** (open it from the right wrist)
- [x] Remove pin shows **RB** (it showed LB).
- [x] A new **Menu "Close map"** hint at the end of the hint bar, not overlapping the others.
- [x] Point at a pin icon and pull the trigger: it selects it for the next pin. But the D-pad picture next to the
      icons was still there (it's a picture, not text); hidden in 0.5.1.

## New in 0.3.3 (installed 2026-11-10, 23:25)

**Log first**
- [x] `Steam Frame for VHVR 0.3.3: 10/10 modules active (all on).` (23:40 session)
- [x] `SessionLogs: this session is also saved to BepInEx/SessionLogs/<start time>.log (keeping the last 50).`
- [x] That file starts with BepInEx's own first lines (the preloader), like `LogOutput.log`.
- [x] After quitting the game, the file ends with the same lines as `LogOutput.log`.
- [x] Start the game a second time: there are two session files, and the first one is unchanged (23:40 and 00:20).

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

## Results from 0.3.2 / 0.3.3 (2026-11-10, 23:40 session)

- Page buttons: below VHVR's tab row now, but **blank** (no text). Fixed in 0.4.0, see above.
- Button guide and Recenter & height pages: text ran outside the panel's edges. Fixed in 0.4.0, see above.
- Big map hints: zoom, ping, add pin, cross off and toggle pins were right; **remove pin showed LB, it's RB**. The pin
  icon picker showed a D-pad, which doesn't change the icon. The right Menu button closes the map but had no hint.
  All fixed in 0.4.0, see above.
- Seated / standing (Auto): didn't set itself properly. The log shows why: one recenter measured 1.98 m and was saved as
  your standing height, so afterwards a 1.54 m standing recenter counted as seated. Replaced in 0.4.0 by heights you
  set with buttons, see above.
- Build "tilt" doesn't exist: grip + stick up/down flips the piece front to back. The guide says so now.

## Still open

- [ ] **Button guide redesign** (page II): done in 0.5.0, see its checks above.
- [ ] **"Punch: thumb-on-stick can't be sensed..."** still appears once a session (23:40 session too), while the
      log's punch lines show the thumb *is* sensed (`thumb=Y`). Does a one-hand punch still refuse without the thumb
      on the stick? It showed up both times soon after the left controller first appeared (at the menu the log has
      "left: none" until the SteamVR dashboard is opened and closed).
- [ ] Map sharing row: untick "Share map position on join", OK, restart: still unticked, and joining a world doesn't
      turn public position on. Tick it again: others see you again.
- [ ] Change a setting, press OK, restart Valheim: it stuck. Change one and press Back: it didn't.

## Confirmed working
- Steam Frame detected; tab appears (0.1.0); tab button reads "Steam / Frame" on two lines (0.3.1).
- Recenter pose (hands to eyes) recenters; build placement, rotation (grip + stick left/right), flipping (grip + stick
  up/down) and removing pieces.
- One-hand punch needs grip + thumb on stick (the log shows "needs thumb on stick" refusals and punches when held);
  two-hand punch (0.3.1).
- Prompts, Place stacks / Take all, bow, map sharing.
- Crouch, all checks (2026-11-10): right stick click crouches in the world; clicking again or sprinting stands you up;
  in a chest it does its menu job without crouching; on the big map it pings without crouching; double-click crouch.
- Server passwords, all three checks (2026-11-10): typed once and saved; no password box on the next join; "Forget
  saved passwords" + OK asks again.
- Big map hints: zoom (D-pad), ping (RS), add pin (grip + RT), cross off and toggle pins (RT).
- SessionLogs (0.3.3): one file per session, the last 50 kept.

## Not a plugin issue
- Crouch to sprint delay: crouched, pressing sprint stands you up at once but running starts about half a second
  later. Flat-screen Valheim does the same (checked 2026-11-11): it's the game's stand-up animation.
- Left middle finger stays half curled: VHVR copies finger poses straight from SteamVR's hand skeleton, the same code
  for both hands, so this comes from the controller/driver. Check SteamVR > Settings > Controllers > Test Controller:
  if that hand model's middle finger won't straighten either, it's the Frame controller's finger sensing.
