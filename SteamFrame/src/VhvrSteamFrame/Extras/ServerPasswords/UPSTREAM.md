# ServerPasswords → probably not VHVR

Remembering server passwords isn't VR-specific, so it may fit better as its own small mod (or a Valheim suggestion)
than in VHVR. It only matters more in VR because typing on the virtual keyboard is slow.

How it works: Valheim's `ZNet.RPC_ClientHandshake` already submits `FejdStartup.ServerPassword` without showing the
password box (that's the `+password` launch option). A prefix fills it from the saved copy for `ZNet.GetServerString(true)`
and a finalizer clears it again; a prefix on `ZNet.OnPasswordEntered` notes what was submitted; when
`ZNet.GetConnectionStatus()` reaches `Connected` it's saved, and a saved password answered with `ErrorPassword` is
forgotten. Storage is Windows DPAPI (current user) in `BepInEx/config/VhvrSteamFrame/server-passwords.dat`; a
cross-platform version would need another store (Linux/Proton has no DPAPI).
