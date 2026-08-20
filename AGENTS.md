# Sentaur Survivors — agent guide

A Sentry-themed Vampire Survivors clone. Unity **6000.3.21f1**, URP, new Input System.

## Read this first

**Some bugs here are on purpose.** This is a Sentry demo app; deliberate faults exist so
Sentry can be seen catching them. They're gated behind `DemoConfiguration`
(`Assets/Scripts/Config/DemoConfiguration.cs`) and only active with `-demo` or
`SENTRY_DEMO` set. Before "fixing" anything broken, read
[CONTRIBUTING.md](CONTRIBUTING.md) — it has the rule for telling a real bug from
instrumentation, plus the coding style and the trap around renaming serialised fields.

## Layout

| Path | Assembly |
| --- | --- |
| `Assets/Scripts/` | `Sentaur.Runtime` |
| `Assets/Editor/` | `Sentaur.Editor` |
| `Assets/Tests/EditMode/` | `Sentaur.Tests.EditMode` |
| `Assets/Tests/PlayMode/` | `Sentaur.Tests.PlayMode` |

The two test assemblies are **empty scaffolding** — asmdefs with no `.cs` files yet, so
`list_tests` reports 0 and CI has no test job. Put new tests there.

Runtime code is grouped by concern: `Characters`, `Config`, `Managers`, `Pickups`,
`SceneManagers`, `Telemetry`, `UI`, `Upgrades`, `Weapons`.

`Telemetry` holds the Sentry metrics. `GameMetrics` is the emit layer -- every metric name
and the attributes they all carry -- and `BattleMetrics` is the per-run lifecycle, driven
from `BattleSceneManager`. Anything that can fire more than a couple of times a second goes
through `GameMetrics.Record*`, which accumulates and is drained once a second; `Emit*` is
for the handful-per-run events. Metrics are always on and are not demo-gated.

Vendored third-party code lives in `Assets/Plugins` and is excluded from formatting.

## Driving the Editor

This project has the **Unity Pipeline package** (`com.unity.pipeline`) in
`Packages/manifest.json`. It runs a local HTTP API inside a *running* Editor on port
7800, which lets you build, run tests, enter play mode, read the console, and inspect the
scene graph without leaving the terminal.

**It only works while the Editor has this project open.** Check first:

```sh
unity pipeline list          # want: Pipeline=true, Server Reachable=true
```

Then:

```sh
unity command                       # list all ~150 available commands
unity command editor_status         # ready / compiling / play mode
unity command get_console_logs      # compile errors and runtime logs
unity command run_tests             # also: list_tests, test_status, cancel_tests
unity command recompile             # after editing C# outside the Editor
unity command build                 # also: build_status, list_build_targets
unity command capture_game_view     # screenshot, for checking visual changes
```

Useful ones beyond the basics: `find_gameobjects`, `get_scene_hierarchy`,
`get_component_properties`, `set_serialized_field`, `open_scene`, `eval`.

Notes:

* The Editor only imports and recompiles **when it has focus**, unless auto-refresh is
  on. If a command hangs on `compiling`, click over to the Editor window.
* The HTTP API rejects unauthenticated requests — go through the `unity` CLI, which
  handles the token. Raw `curl localhost:7800` returns `Unauthorized`.
* `com.unity.pipeline` is an **experimental** package (`0.5.0-exp.1`). It won't show in
  the Package Manager UI under default filters, and its API can break between versions.

If the `unity` CLI is missing, see [the Unity CLI docs](https://docs.unity.com/en-us/unity-cli).
Install the package into a project with `unity pipeline install`.

## MCP

The same Editor commands are available as MCP tools via `unity mcp`, which is a thin
wrapper over the pipeline API above — same requirement of a running Editor, same
capabilities. Prefer it over shelling out when your client has it connected.

It is **not** checked into this repo, because MCP config is per-developer. To enable it:

```sh
unity mcp configure --list        # supported clients and their config paths
unity mcp configure <client>      # e.g. cursor, vscode, codex, claude
```

For the Claude Code CLI specifically, `unity mcp configure claude-code` writes a
**user-scoped** entry that applies to all your projects. To keep it scoped to this
project only:

```sh
claude mcp add --scope local --transport stdio unity-editor-mcp \
  unity mcp -- --project-path "$(pwd)"
```

## Checks before you hand work back

```sh
dotnet format whitespace Assets --folder --exclude Assets/Plugins
unity command get_console_logs    # confirm the Editor compiled clean
unity command run_tests           # no-op until the test assemblies have tests
```

CI (`.github/workflows/ci.yml`) gates on two things only: the formatter run with
`--verify-no-changes`, and the player builds for macOS, iOS, Windows, Linux and Android.
There is no test job, so a clean compile and a green build are the real bar.
`run-demo.yml` exercises the demo-config path with the intentional faults on.
