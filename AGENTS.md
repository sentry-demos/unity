# Sentaur Survivors — agent guide

Sentry-themed Vampire Survivors clone. Unity **6000.3.21f1** (pinned in
`ProjectSettings/ProjectVersion.txt`), URP, new Input System.

## Read first

- **Some bugs here are on purpose** — Sentry demo faults gated behind `DemoConfiguration`
  (`Assets/Scripts/Config/DemoConfiguration.cs`), live only with `-demo` or `SENTRY_DEMO`.
- Reachable with the demo config **off** → real bug, fix it. **On** only → instrumentation, leave it.
- Renaming a Unity-serialised field drops its value in every prefab and scene that uses it.
- Full rules, coding style and folder notes: `CONTRIBUTING.md`.

## Layout

| Path | Assembly |
| --- | --- |
| `Assets/Scripts/` | `Sentaur.Runtime` |
| `Assets/Editor/` | `Sentaur.Editor` |
| `Assets/Tests/EditMode/` | `Sentaur.Tests.EditMode` |
| `Assets/Tests/PlayMode/` | `Sentaur.Tests.PlayMode` — asmdef only, no tests yet |

- Runtime is grouped by concern: `Characters`, `Config`, `Debug`, `Managers`, `Pickups`,
  `SceneManagers`, `Telemetry`, `UI`, `Upgrades`, `Weapons`.
- `Telemetry/GameMetrics.cs` is the emit layer (metric names + shared attributes);
  `Telemetry/BattleMetrics.cs` is the per-run lifecycle, driven from `BattleSceneManager`.
  Anything firing more than a couple times a second goes through `GameMetrics.Record*`
  (accumulated, drained once a second); `Emit*` is for handful-per-run events. Never demo-gated.
- `Assets/Plugins` is vendored: excluded from formatting, don't restyle it.

## Unity CLI

`unity` manages Editors and projects from the terminal ([install docs](https://docs.unity.com/en-us/unity-cli)).

| Command | Use |
| --- | --- |
| `unity status` | every connected Editor: port, project, version, PID, state |
| `unity pipeline list` | Pipeline status per project — want `Pipeline=true`, `Server Reachable=true` |
| `unity pipeline install` / `upgrade` | add or update `com.unity.pipeline` in a project |
| `unity open` | open the project with the pinned Editor version |
| `unity --json <cmd>` | machine-readable output, for any command |

## Editor pipeline

`com.unity.pipeline` (`0.5.0-exp.1`, in `Packages/manifest.json`) runs an HTTP API on port
7800 **inside a running Editor**. `unity command <name> [args]` calls it.

| Command | Use |
| --- | --- |
| `unity command` | list all ~150 available commands |
| `unity command editor_status` | ready / compiling / play mode |
| `unity command get_console_logs` | compile errors and runtime logs |
| `unity command recompile` | after editing C# outside the Editor |
| `unity command run_tests` | also `list_tests`, `test_status`, `cancel_tests` |
| `unity command build` | also `build_status`, `list_build_targets` |
| `unity command capture_game_view` | screenshot, for checking visual changes |

- Also: `find_gameobjects`, `get_scene_hierarchy`, `get_component_properties`, `set_serialized_field`, `open_scene`, `eval`.
- Every call fails unless the Editor is running with this project open — check `unity pipeline list`
  first. Raise `--timeout <seconds>` (default 30) for builds and test runs.
- The Editor imports and recompiles **only while focused**, unless auto-refresh is on. Stuck on
  `compiling` means: click over to the Editor window.
- Raw `curl localhost:7800` returns `Unauthorized`; the CLI holds the token, so go through it.
- Experimental package: hidden in Package Manager under default filters; its API breaks between versions.

## MCP

`unity mcp` serves the same Editor commands as MCP tools — same running-Editor requirement.
**Prefer the MCP tools over `unity command` when they're connected**; they carry structured results.

- MCP config is per-developer, deliberately not in the repo. `unity mcp configure --list` shows
  supported clients and their config paths.
- `unity mcp configure claude-code` writes a **user-scoped** entry covering all projects; to scope it
  to this project only (verify with `claude mcp get unity-editor-mcp`):

```sh
claude mcp add --scope local --transport stdio unity-editor-mcp \
  unity mcp -- --project-path "$(pwd)"
```

## Checks before handing work back

```sh
dotnet format whitespace Assets --folder --exclude Assets/Plugins
unity command get_console_logs    # confirm the Editor compiled clean
unity command run_tests
```

- Scope the formatter to one file with `--include Scripts/Foo.cs` — paths are relative to `Assets`.
- `dotnet format` can't see inactive `#if` branches; tidy those by hand.
- CI (`ci.yml`) gates on two things only: the formatter with `--verify-no-changes`, and player builds
  for macOS, iOS, Windows, Linux, Android. No test job — clean compile + green build is the real bar.
- `run-demo.yml` exercises the demo-config path with the intentional faults on.

## Commit attribution

AI commits MUST include the agent's own identity, e.g.:

```
Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```
