# Posture → VHVR (PR 3)

**Problem:** `VRPlayer.referencePlayerHeight` = measured eye height clamped to PlayerMin/MaxEyeHeight, and the view sits
that high above the character's feet. Recentering while seated (~1.2 m) makes you chest-height to other players; the
only workaround is raising `PlayerMinEyeHeight`. SteamVR's own recenter (Steam button) never re-measures.

**Change:**
- `VHVRConfig`: `PlayPosture` (Auto / Standing / Seated), `StandingEyeHeight` (learned), `SeatedBelowPercent`.
- `VRPlayer.referencePlayerHeight`: `PostureCalibrator.Classify` + `ReferenceOverride` (copy `PostureCalibrator.cs`).
- `VRPlayer.maybeCaliberatePlayerHeight`: after a measurement, `ShouldLearn` → save `StandingEyeHeight`; show a message.
- SteamVR backend: listen for `VREvent_StandingZeroPoseReset` / `SeatedZeroPoseReset` / `ChaperoneUniverseHasChanged`
  and call `RequestRecentering(true)` + `RequestPelvisCaliberation()` + `ResetRoomscaleCamera()` (what
  `SteamVRRecenter` does via a PollNextEvent postfix). Check the log line "SteamVR event seen" to confirm which event
  the Frame's recenter sends before proposing.
