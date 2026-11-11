# FrameTab → VHVR (PR 5, discuss first)

**Target:** `ValheimVRMod/VRCore/UI/ConfigSettings.cs`. Add `CreateControllerTab(int tabCount)` next to
`CreateFullBodyTrackingTab`, count it in `createModSettings`' `tabCount`, and build it like `FullBodyTrackingTab`:
the controller-related VHVRConfig rows (punch, posture) via `createElement`, plus a guide label filled by
`BindingTable.Build/Format` (copy `BindingTable.cs`; it only needs the JSON library VHVR already ships).

The guide reads the binding file the game ships, so it stays right for any controller type, not just the Frame —
worth proposing as a general "Controls" tab. Ask on the VHVR Discord before building; it touches their UI layout.
Here it's injected with a prefix on `CreateTab` (tabCount + 1) and a postfix on `CreateFullBodyTrackingTab`, both of
which disappear upstream.
