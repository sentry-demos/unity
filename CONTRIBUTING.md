# Contributing

## Some bugs here are on purpose

This is a demo app for Sentry, an error-monitoring SDK. Part of what it demonstrates is
things going wrong — crashes, failed network calls, unhandled exceptions — so Sentry can
be seen catching them. **Those faults are deliberate. Don't fix them.**

Intentional faults are gated behind a flag on `DemoConfiguration`
(`Assets/Scripts/Config/DemoConfiguration.cs`): `AutoPlay`, `NotHotDogParticleEffect`,
`FetchUpgradeFromServer` and `CrashOnGameOver`, each ANDed with the `_enabled` master
switch. `Assets/Resources/DemoConfig.asset` ships with every flag off; they're turned on
by passing `-demo` on the command line or setting the `SENTRY_DEMO` environment
variable, which is what CI's demo run does.

So the rule of thumb when you find something broken:

* **Reachable with the demo config off?** Real bug affecting real players — fix it.
* **Reachable only with the demo config on?** Instrumentation — leave the behaviour
  alone. If the intent isn't obvious from the surrounding code, add a comment saying so.

Current examples of the second kind: the server-upgrade fetch in `LevelUpUI`, the
unguarded asset-bundle download in `NotHotDockPickupEffect`, and the native crash in
`BattleSceneManager.SaveScoreToDisk`.

## Coding Style

We mostly follow [Google's C# style guide](https://google.github.io/styleguide/csharp-style.html), because it's the most concise guide, with 4-space indentation instead of 2.

The rules live in [`.editorconfig`](.editorconfig), so you shouldn't have to think about them: Rider, Visual Studio and VS Code all read that file and apply it on save.

The key things to know:

* `PascalCase` for classes, methods, public fields, etc.
* `camelCase` for local variables and parameters
* `_camelCase` for private and protected fields
* 4 space indentation

### Formatting

Don't hand-tune whitespace. To normalise the tree:

```sh
dotnet format whitespace Assets --folder --exclude Assets/Plugins
```

CI runs the same command with `--verify-no-changes` and fails the PR on drift.

Two things worth knowing:

* **Vendored code under `Assets/Plugins` is excluded.** DOTween and friends aren't ours to restyle — reformatting them turns every upstream upgrade into a merge conflict.
* **`dotnet format` can't see inactive `#if` branches.** Code inside e.g. `#if UNITY_ANDROID && !UNITY_EDITOR` isn't compiled on your machine, so the formatter skips it. Tidy those by hand.

### Naming

Naming violations (`IDE1006`) show up as warnings in your editor but are **not** gated in CI, and `dotnet format` won't fix them for you.

That's deliberate. Most of the violations still in the codebase are on Unity-serialised fields (`public int hitpoints`, `[SerializeField] private Transform arrow`, …), and **renaming a serialised field silently drops its value in every prefab and scene that uses it**. If you do rename one, do it from inside the Unity Editor and add `[FormerlySerializedAs("oldName")]` so existing assets keep their data.

New code should follow the naming rules from the start.

## Folders

### Assets/Prefabs

These are prefab game objects from which in-game game objects are generated (anything that's duplicated). Enemies, Projectiles, etc. 

From inside the Unity Editor, you can drag these prefab objects into the scene (make sure you're in Scene view) and they'll become game objects in the game.

### Assets/Scenes

This is where the main scene files are located. There are just two scenes:

* `TitleScene` - displays the title when the game launches
* `BattleScene` - the primary in-game scene (where the player fights enemies)

### Assets/Scripts

All the component C# scripts (read: the code for the game).

### Assets/Graphics

Sprites, art assets, tiles, materials, etc.

### Assets/Sounds

Music and sound effects.

## Tips for Contributing

* Make sure you save your Scene in the Unity Editor (CMD + S) before committing code. If you don't, all the scene data (e.g. the game objects, their components, and those components' properties) won't get persisted.
* When you add a new Script component inside Unity, it gets placed in the top-level `Assets` folder. You have to manually move the newly-created file to `Assets/Scripts`.
* When renaming or moving files, move/rename the files inside Unity Editor, _not_ VS Code. Unity Editor will ensure any scripts that were attached to game objects also get renamed/mapped to the new location.
  * _If you perform the move/rename in VS Code, you'll have to manually add all the script references back inside Unity._
See [CONTRIBUTING](CONTRIBUTING.md).