# SessionLogs → VHVR (no, keep local)

A development convenience, not a VR feature: it keeps every session's BepInEx log so problems can be looked at after
the game has been restarted. It belongs to BepInEx's logging rather than VHVR. Anyone who wants it without this plugin
can set `[Logging.Disk] AppendLog = true` in `BepInEx/config/BepInEx.cfg`, which keeps every session in one growing
`LogOutput.log` instead of separate files.
