# OSRS Client

Windows C# 14 solution for classic-layout RuneLite. Read `README.md` for current architecture and `ROADMAP.md` before feature work; it records expensive-to-rediscover constraints.

## Source control and validation

The root contains `OSRS Client.slnx` (not `.sln`). Do not commit unless explicitly asked. Before a Git operation that could conceal or discard work, inspect `git status --short` and recent history.

```powershell
dotnet build
dotnet test
dotnet tool restore # only when mutation testing is requested
dotnet stryker      # about 90 seconds; threshold 80%
```

Tests are offline and must never require RuneLite, a real window, or a physical desktop. Use state-based tests and hand-written fakes in `Core.Tests/Fakes/`; pass `InputTimings.Instant`. For cancellation, use `FakeNativeApi` hooks, never a timing race.

## Structure and safety

- Dependency direction is `Client → Scripts → Core`; Core cannot reference Scripts or Client, and Core sub-namespaces must not cycle.
- Project directories are source roots: do not add a `src/` directory inside a project. One type per file, named for the type. `.editorconfig` and `Directory.Build.props` are authoritative; use American spelling.
- New Win32 calls go behind `INativeApi`; raw P/Invokes stay internal in `Core/Native/NativeMethods.cs`. Keep `Win32NativeApi` a thin, branch-free translation layer; put decisions in testable callers using `FakeNativeApi`.
- Desktop-dependent code is intentionally isolated: `NativeMethods.cs`, `Win32NativeApi.cs`, `GdiScreenCapture.cs`, and `ProcessWindowFinder.cs`. Extract pure logic rather than growing that list. Async Core work observes `CancellationToken`; release buttons/modifiers in `finally`.
- Support only classic RuneLite at 765×503, 100% display scaling, GPU plugin off. Bounds include the sidebar and are not canvas bounds; locate the canvas by anchor matching. The Game layer is planned, not present: keep OSRS specifics out of Core.
