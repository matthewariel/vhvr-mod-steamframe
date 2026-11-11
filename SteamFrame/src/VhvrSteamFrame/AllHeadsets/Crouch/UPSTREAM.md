# Crouch → VHVR (small PR, pairs with the Frame bindings PR)

**Problem:** the Frame binding gives the right stick click two jobs: `middleclick` (move items in chests, ping on the
map) and, with the stick-click crouch tweak, `togglecrouch`. `Player_SetControls_SneakPatch.handleControllerOnlySneak`
toggles crouch on every press, so clicking in a chest or on the map also crouches you.

**Change** (`ValheimVRMod/Patches/Player_SetControls_SneakPatch.cs`):
- In `handleControllerOnlySneak`, ignore the crouch press while `InventoryGui.IsVisible() || Minimap.IsOpen() ||
  StoreGui.IsVisible() || TextInput.IsVisible() || Menu.IsVisible()` (`CrouchGate.Decide`'s `BlockInMenu`).
- Optional `VHVRConfig.DoubleClickToCrouch`: only the press that crouches you needs a second click within ~0.45 s;
  standing up stays one click. Copy `CrouchPolicy.cs` and call `CrouchGate.Decide` where the press is detected.

Here it's a lower-priority `Player.SetControls` prefix ordered after VHVR's (`after = com.valheimvrmod.patches`) that
clears `crouch`; upstream it's a few lines inside VHVR's own prefix.
