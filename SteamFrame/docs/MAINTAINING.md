# Maintaining the fork

A short guide for looking after this plugin, written for someone new to Git and GitHub.

## Words you'll see

| Word | Meaning |
|---|---|
| **Repository (repo)** | The project folder plus its full history |
| **Fork** | Your own copy of someone else's repository on GitHub. Yours is `matthewariel/vhvr-mod-steamframe` |
| **Upstream** | The original: `brandonmousseau/vhvr-mod` (VHVR) |
| **Clone** | The copy of your fork on this PC: `C:\Users\matt\Documents\GitHub\vhvr-mod-steamframe` |
| **Commit** | A save point, with a message saying what changed |
| **Push** | Upload your commits to GitHub |
| **Pull** | Download commits from GitHub to this PC |

## The one rule

**Only change files inside `SteamFrame/` (and `.github/workflows/steamframe.yml`).**

Why: VHVR's maintainers change VHVR's files. If you never touch those, pulling their updates can never conflict with
your work, and GitHub's **Sync fork** button always just works.

## Running the scripts

Open PowerShell in the `SteamFrame` folder. Windows blocks scripts by default, so run them like this:

```bash
powershell -ExecutionPolicy Bypass -File scripts\Build.ps1
```

| Script | What it does | Needs the game? |
|---|---|---|
| `scripts\Build.ps1` | Builds the plugin DLL | Yes (Valheim + VHVR installed) |
| `scripts\Test.ps1` | Runs the unit tests and the VHVR-compatibility build | No |
| `scripts\Install.ps1` | Copies the DLL into Valheim, after backing up the old one | Yes, and Valheim must be closed |
| `scripts\Check-VhvrCompat.ps1` | Checks the installed VHVR and Valheim still have what the plugin needs | Yes |
| `scripts\Package.ps1` | Makes the zip for players in `dist\` | After a build |

Game somewhere else? Add `-ValheimDir "D:\Games\Valheim"` to Build, Install or Check-VhvrCompat.

Backups of replaced DLLs are in `%LOCALAPPDATA%\VhvrSteamFrame\backups\`. To go back, close Valheim and copy one to
`BepInEx\plugins\VhvrSteamFrame.dll`.

## Everyday loop: change something

1. Edit files under `SteamFrame/`.
2. `scripts\Test.ps1`, then `scripts\Build.ps1`.
3. Close Valheim, `scripts\Install.ps1`, start Valheim, check `BepInEx\LogOutput.log`.
4. Save and upload:

```bash
git status
```

Shows which files changed. They should all be under `SteamFrame/`.

```bash
git add SteamFrame
```

Picks the changes to include in the save point.

```bash
git commit -m "Say what changed and why"
```

Makes the save point (on this PC only).

```bash
git push
```

Uploads it to GitHub. **Anything pushed is public.**

### Never commit these

`SteamFrame/.gitignore` already blocks them, but know the list:
- any `*.cfg` (VHVR's config holds an API key);
- `server-passwords.dat`, `LogOutput.log`;
- `backup/` folders, DLLs, zips.

Before a push, this should print nothing worrying:

```bash
git grep -n -i -E "groq|apikey|api_key|BEGIN .*KEY" -- SteamFrame
```

## Getting VHVR's updates into the fork

1. On the fork's GitHub page, click **Sync fork**, then **Update branch**. This copies VHVR's new commits into your
   fork on GitHub.
2. Bring them to this PC:

```bash
git pull
```

3. `scripts\Test.ps1`. The tests re-check the binding tweaks against VHVR's new Frame binding file.

If Sync fork ever reports a conflict, a file outside `SteamFrame/` was changed in the fork. Don't force anything; find
that file with `git diff --stat upstream/master` and undo the change.

## When VHVR releases a new version

Syncing the fork updates VHVR's *source*. What you play with is VHVR's released *DLL* in the game folder. Do this before
playing on a new VHVR:

1. **Sync fork** and `git pull` (above).
2. Install the new VHVR into Valheim yourself, the usual way.
3. `scripts\Check-VhvrCompat.ps1`. It checks every VHVR and Valheim member the plugin uses.
   - `ALL OK`: go on.
   - `BROKEN`: VHVR renamed or removed something. `scripts\Check-VhvrCompat.ps1 -Describe <TypeName>` lists what the
     type contains now. Fix the entry in `src\VhvrSteamFrame\Core\VhvrApi.cs` and the hook that uses it.
   - `RISKY-PATCH`: a VHVR method the plugin patches now touches a private Valheim member. It must be called through a
     delegate instead (see [ARCHITECTURE.md](ARCHITECTURE.md), "The Harmony traps").
4. `scripts\Test.ps1`.
5. `scripts\Build.ps1`: the plugin must be rebuilt against the new VHVR.
6. `scripts\Install.ps1`, play, and go through [IN-GAME-CHECKS.md](IN-GAME-CHECKS.md).
7. Add the version to `TestedVhvrVersions` in `VhvrApi.cs`, so the startup warning goes away.
8. Commit, push, and make a release if you publish one.

Even if you skip all this, a module whose VHVR internals changed switches itself off and says so in the log. The rest
keeps working.

**Right now:** you play on VHVR 0.11.1 and VHVR 0.11.2 is out. The steps above are how to vet 0.11.2 before updating.

## Automatic tests on GitHub (CI)

`.github/workflows/steamframe.yml` runs the unit tests and the compatibility build on GitHub after every push that
touches `SteamFrame/`. It can't build the plugin itself, because that needs the game's files.

Forks have workflows switched off until you allow them: open the fork's **Actions** tab once and click the button to
enable workflows. A green tick next to a commit means the tests passed.

## Changing the version

The version is written in two places that must match (a unit test checks):
- `SteamFrame/Directory.Build.props`: `<SteamFrameVersion>`;
- `src/VhvrSteamFrame/Core/Plugin.cs`: `Version`.

Add a section to [CHANGELOG.md](CHANGELOG.md) too.

## Making a release (only when you want one)

1. `scripts\Test.ps1`, `scripts\Build.ps1`, then `scripts\Package.ps1`. The zip appears in `SteamFrame\dist\`.
2. On GitHub: **Releases > Draft a new release**.
3. Tag: `steamframe-v0.3.0`. The `steamframe-` prefix keeps it apart from VHVR's own `v0.x` tags.
4. Attach the zip and paste the version's section from the changelog.
5. **Save draft** keeps it private. **Publish** makes it public.

Before a first public release, go through "Before a public release" in [FEATURES.md](FEATURES.md).
