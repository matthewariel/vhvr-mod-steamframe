# Punch → VHVR (PR 4)

**Problem:** bare-hand punches need grip (creatures) or grip + trigger + laser off (everything else), hard-coded in
`FistCollision`. Frame users want grip + thumb resting on the stick; others may want looser/stricter.

**Change:**
- `VHVRConfig` (Motion Control): `PunchRequireGrip`, `PunchRequireTrigger`, `PunchRequireThumbOnStick`, with
  `PunchPolicy.Warning` text in the descriptions / settings tab.
- `FistCollision.canAttackWithCollision`: for a free hand, return the base conditions && `PunchPolicy.Allows(...)`
  (replaces the `canAttackWithCollision` postfix).
- `FistCollision.OnTriggerEnter`, non-creature branch: replace "trigger held && laser off" with "laser off" (the gate
  already applied the requirements). That removes `ThumbPunch` entirely.
- Thumb sensing: add an input action (e.g. `ThumbRest`, boolean) to `VRInputActions.cs`, `actions.json`, and the
  default bindings as stick `touch`, instead of borrowing the default set's `teleport`.
