# OSRS Client

A home-made, personal-use color bot client for Old School RuneScape, built on top of RuneLite. Reads the screen (color/pixel matching) and drives input at the OS level (Win32 `SendInput`) — no memory reading, no code injection into the game client.

This is a hobby project. It is **not** compliant with Jagex's rules (see [Rules of Old School RuneScape](https://legal.jagex.com/docs/rules/rules-of-old-school-runescape)) — using it on an account you care about risks a permanent ban. Use at your own risk, on an account you're fine with losing.

## Architecture

Four projects, layered so each only knows about the one below it:

```
Client        dashboard / entry point
  └─> Scripts        per-activity bot logic
        └─> (Game)   OSRS domain knowledge — planned, not yet built
              └─> Core   engine: OS interaction, vision, input, safety, script lifecycle
```

`Game` does not exist yet. It is the planned home for everything OSRS-specific but
activity-generic — the canvas anchor, the UI region map, inventory slots, orb readers. Until it
exists there is nothing between `Scripts` and `Core`. See [ROADMAP.md](ROADMAP.md).

- **Core** — the engine. Everything that touches the OS or the screen.
  - `Native` — Win32 P/Invoke, behind the `INativeApi` abstraction so everything above it is unit testable.
  - `Vision` — `Frame` (an immutable BGRA pixel buffer with color queries) and `GdiScreenCapture`.
  - `Windows` — `GameWindowProvider`: attaches to RuneLite, tracks bounds, manages focus.
  - `Input` — `IInputProvider` / `WindMouseInput`, with cursor paths from `WindMousePath`.
  - `Safety` — the kill switch and its hotkey monitor.
  - `Scripting` — `IScript`, `ScriptContext`, `ScriptRunner`.
- **Scripts** — the actual bot logic, one class per activity. Currently empty; this is what gets built next.
- **Client** — console dashboard: attach to RuneLite, run diagnostics, start/stop scripts.
- **Core.Tests** — xunit tests against hand-written fakes. No RuneLite required; the whole suite runs offline in under a second.

### Why `INativeApi` exists

Win32 calls can't be exercised in a unit test, so `Core` talks to a narrow interface expressed in managed types (`Rectangle`, `Point`, `VirtualKey`). `Win32NativeApi` is the thin real implementation and holds no logic; tests substitute `FakeNativeApi`, which records the input it was asked to send. This is what makes window focus, mouse pathing and the kill switch testable at all.

## Safety

**The kill switch is the most important part of this codebase.** `HotkeyKillSwitchMonitor` polls for **F12** globally and, when pressed, cancels the `CancellationToken` that every script and every input operation observes. Input work releases held mouse buttons and modifier keys on the way out, so a canceled drag or shift-click can't leave the desktop in a stuck state.

Nothing should be run unattended without this armed. The `Client` dashboard arms it at startup.

## Requirements

- .NET 10 SDK, C# 14 (pinned in `Directory.Build.props`)
- Windows (P/Invokes `user32.dll` / `gdi32.dll` directly — will not run elsewhere)
- RuneLite running, at 100% display scaling, with the GPU plugin off (see Known limitations)

Shared build settings — target framework, language version, nullable, analyzers — live in
`Directory.Build.props` rather than being repeated in each `.csproj`.

## Building and running

```
dotnet build
dotnet run --project Client
```

## Testing

```
dotnet test
```

All tests are pure unit tests against fakes — no running game needed.

### Mutation testing

Line coverage says a line ran; it doesn't say a test would notice if the line were wrong. [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) mutates the code and checks the suite fails:

```
dotnet tool restore
dotnet stryker
```

Configuration is in `stryker-config.json`, with a break threshold of 80%. **Current score: 90.91%** (279 killed, 11 timeout, 29 survived).

Two categories are deliberately excluded:

- **`mutate` exclusions** — `NativeMethods`, `Win32NativeApi`, `GdiScreenCapture` and `ProcessWindowFinder`. These are thin translations to Win32/GDI with no branching logic; they cannot be verified without a real desktop and a running game, so mutating them only produces noise.
- **`ignore-methods`** — logger calls (log text is not behavior) and guard-clause helpers like `ArgumentNullException.ThrowIfNull` (the throw is already asserted directly).

The ~29 surviving mutants are predominantly equivalent: exception *message* text, `>=` vs `>` on floating-point distance thresholds, and statement removals in `Dispose` paths whose effect isn't observable through public behavior.

## Known limitations

- **GDI capture and the GPU plugin**: `GdiScreenCapture` reads the on-screen composite via `BitBlt`. RuneLite's GPU plugin can defeat this and yield black frames, and there is a [confirmed bug](https://github.com/runelite/runelite/issues/11403) where captures miss part of the canvas at non-100% DPI scaling. The window must also be visible and unoccluded. Test with the GPU plugin off and scaling at 100%.
- **No DPI-awareness manifest**: coordinate math assumes 100% scaling. Untested on scaled displays.
- **Foreground-only input**: real `SendInput` to the OS, and the window is brought to the foreground before acting. This takes over the physical cursor and cannot run in the background.
- **Window bounds are not the game canvas**: `GameWindowProvider.TryGetWindowBounds` returns RuneLite's *client area*, including its sidebar and toolbar. Locating the 765×503 game canvas inside it requires anchor detection and belongs to the Game layer (see below).
- **Process name assumption**: `Attach` looks for a process named `RuneLite`. A jar-based launch may appear as `javaw`.
- **Input is not concurrency-safe**: two callers driving the mouse at once would interleave events. `ScriptRunner` enforces one script at a time.

## Status

The engine layer is in place: window tracking, capture, humanized input, kill switch, and script lifecycle. There is no bot logic yet.

Next up is the vision core, then dev tooling, the `Game` layer, session humanization, and a first
woodcutting script. **[ROADMAP.md](ROADMAP.md) has the full plan** — each phase with the reasoning,
the findings behind it, and the open questions. Start there when picking this up.
