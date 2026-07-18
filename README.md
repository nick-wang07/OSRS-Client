# OSRS Client

A home-made, personal-use color bot client for Old School RuneScape, built on top of RuneLite. Reads the screen (color/pixel matching) and drives input at the OS level (Win32 `SendInput`) — no memory reading, no code injection into the game client.

This is a hobby project. It is **not** compliant with Jagex's rules (see [Rules of Old School RuneScape](https://legal.jagex.com/docs/rules/rules-of-old-school-runescape)) — using it on an account you care about risks a permanent ban. Use at your own risk, on an account you're fine with losing.

## Architecture

Four projects, layered so each only knows about the one below it:

```
Client   (dashboard / entry point)
   |
   +--> Scripts  (per-activity bot logic)
   |        |
   +--------+--> Core  (engine: OS interaction only)
```

- **Core** — the engine. Owns all interaction with the OS: finding/focusing the RuneLite window (`GameWindowProvider`), capturing pixels from it (`FastPixelReader`), and driving the mouse/keyboard (`WindMouseInput`, via raw `SendInput`). No knowledge of OSRS itself — just window handles, pixels, and input. Depends only on `System.Drawing.Common` for bitmap handling.
- **Scripts** — the actual bot logic, one class per activity (woodcutting, fishing, etc.), written against `Core`'s primitives. Currently empty — this is the next thing to build.
- **Client** — the dashboard: a console app that lets you attach to RuneLite, run diagnostics, and (eventually) pick and run a script. References both `Core` and `Scripts`.
- **Core.Tests** — xunit tests for `Core`. Some tests require a running RuneLite process and are tagged `[Trait("Category", "Integration")]`; exclude them with `dotnet test --filter Category!=Integration` if RuneLite isn't open.

## Requirements

- .NET 10 SDK
- Windows (uses `user32.dll`/`gdi32.dll` P/Invoke directly — will not run on other platforms)
- RuneLite running, at 100% display scaling (see Known limitations)

## Building & running

```
dotnet build
dotnet run --project Client
```

`Client` is the startup project (`Client.src.Client`). It shows a numbered menu for manual testing: mouse movement, window tracking, and pixel/color detection diagnostics.

## Testing

```
dotnet test --filter Category!=Integration   # no RuneLite required
dotnet test                                  # full suite, RuneLite must be running
```

## Known limitations

- **BitBlt capture + GPU plugin**: `FastPixelReader` captures via `BitBlt` on the window's device context. RuneLite's GPU plugin is known to interact badly with this style of capture (black frames, and a [confirmed bug](https://github.com/runelite/runelite/issues/11403) where captures miss part of the canvas at non-100% DPI scaling). Test with the GPU plugin off and display scaling at 100% first.
- **No DPI-awareness manifest**: coordinate math (virtual screen metrics, client-rect conversion) assumes 100% scaling. Untested behavior on scaled displays.
- **Foreground-only input**: input is real `SendInput` to the whole OS, and `GameWindowProvider.EnsureFocused` calls `SetForegroundWindow` before acting — the RuneLite window must be focused and visible. This takes over the real mouse cursor while running and can't operate in the background.
- **Process name assumption**: `GameWindowProvider.Attach` looks for a process literally named `RuneLite`. A jar-based launch may show up as `javaw` instead.

## Status

Early stage. The engine layer (window tracking, input, pixel reading) works; there is no bot logic yet. `Scripts` is the next thing to build — likely one class per activity implementing a small shared interface (start/stop/tick) that `Client` can discover and run.
