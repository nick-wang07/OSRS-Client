# Roadmap

The plan, with the reasoning behind it. `README.md` describes what exists; this describes what comes
next and *why it is shaped that way*, so the decisions do not have to be re-derived.

## Where things stand

**Done — Phase 0 (hygiene) and Phase 1 (safety and lifecycle).** Namespaces flattened, P/Invokes made
`internal` behind `INativeApi`, capture and window layers rewritten, input reshaped with cancellation,
kill switch and script lifecycle built, 201 unit tests at ~91% mutation score.

**Not started — everything below.** There is no bot logic yet.

## Target architecture

```
Client        dashboard / entry point
  └─> Scripts        per-activity bot logic ("chop tree, bank, repeat")
        └─> Game     OSRS domain: canvas anchor, region map, inventory, orbs, idle detection
              └─> Core   engine: OS interaction, vision, input, safety, script lifecycle
```

`Game` is the missing middle. Without it, every script would hardcode inventory pixel coordinates and
re-derive "am I idle" from scratch — the thing that makes color bots unmaintainable. It holds what is
OSRS-specific but activity-generic. `Core` stays free of any OSRS knowledge.

---

## Phase 2 — Vision core

`Frame` currently offers bounds-checked pixel reads, RGB tolerance matching, and colored-pixel
counting. That is not enough to click anything.

**Blob detection.** The central operation, and the one genuinely missing primitive. Not "is this color
present" but "find all clusters of this color, return their centroids, sorted by distance from screen
center." That is how you click a tree. Everything in Phase 6 depends on it.

**HSV alongside RGB.** RGB tolerance is fragile for world objects — the same tree renders differently
by time of day and camera angle. HSV (tolerant hue, loose saturation/value) is far more stable.
Keep RGB for UI chrome, which does not vary; use HSV for anything in the game world.

**Template matching** via OpenCvSharp (already referenced, currently unused) for inventory items and UI
sprites.

**Canvas anchor detection — read this before touching it.** RuneLite is a Java application that renders
everything into a **single HWND**. There is no child window for the game canvas, so it cannot be found
by enumerating child windows — do not go looking for one. `TryGetWindowBounds` returns the whole client
area including RuneLite's sidebar and toolbar, and the 765×503 canvas sits at an offset inside it that
shifts when the sidebar opens or closes.

The approach: at attach time, locate the canvas by template-matching a known anchor sprite (the compass,
or a fixed inventory tab icon), cache the resulting offset, and re-validate periodically. One template
match at startup, robust against sidebar state. Every region coordinate in Phase 4 depends on getting
this right, which is why it belongs here and not later.

## Phase 3 — Dev tooling

A color picker / region designer in `Client`: hover the game, see RGB/HSV live, drag a rectangle to
capture region coordinates.

**Deliberately before the `Game` layer.** Phase 4 is essentially a pile of calibrated constants; Phase 6
needs tree colors. Producing those by hand-editing numbers and rebuilding is the slow path. This is the
highest-ROI tooling available and pays for itself within a day.

## Phase 4 — `Game` layer

A new project between `Core` and `Scripts`.

- **Region map** derived from the 765×503 fixed-classic canvas: viewport, minimap, chatbox, orbs, tabs.
- **Inventory** — 28 slots computed from a grid formula, not 28 hardcoded points.
- **Orb readers** — hitpoints, prayer, run energy.
- **Idle detection** — the central question for most scripts ("did my character stop chopping"). Answer
  it by watching for the **absence of change** over a rolling window: XP counter static, inventory count
  static, no animation. Build it once here; do not let each script reinvent it.

Region constants and colors should be **data (JSON), not code** — they change with monitor, resolution
and RuneLite theme, and hardcoding them means one resolution change breaks every script at once.

## Phase 5 — Session humanization

`InputTimings` covers micro-timing only. This phase adds what actually matters for staying unflagged:
a **break scheduler** (micro-pauses, longer AFK breaks, session-length caps), a **reaction-time model**
(variable delay between "see" and "act", not a fixed `Task.Delay`), and idle behaviors (camera fidget,
mouse drift off-target between actions).

The reasoning: research consistently indicates that behavioral and statistical analysis *over a session*
is the dominant detection vector, not per-gesture realism. WindMouse solves the easy micro-level problem —
a single path looks human. What gets accounts flagged is identical inter-action delays, no breaks, long
uninterrupted uptime, constant XP/hr, and never misclicking. This belongs in `Core` (a `Behavior`
concern); it is not OSRS-specific.

## Phase 6 — Woodcutting

The validation target: chop tree → wait for idle → repeat → drop when inventory full.

Chosen because it exercises the three hardest primitives — blob detection, idle detection,
inventory-full checks — without needing banking or pathing. Expect it to expose gaps in Phases 2–4;
that is the point of doing it early rather than building more abstraction first.

---

## Decisions already settled

Do not re-litigate these without a concrete reason:

- **Fixed classic layout only** (765×503). Resizable/modern is out of scope.
- **OpenCvSharp adopted up front**, before it had a consumer, because template matching is considered
  inevitable.
- **Woodcutting first**, for the reasons above.
- **`IScript` gives each script its own loop** (`Run` with a `CancellationToken`) rather than an
  abstract `Tick()`. OSRS activities differ too much in shape — some poll tightly, some wait minutes —
  and a forced tick cadence fights that.
- **One script at a time.** `ScriptRunner` enforces it because the input layer cannot be driven
  concurrently; two scripts would interleave mouse events into nonsense.

## Open questions

- **OCR**, when it is needed (chat, XP counter, item counts). Worth knowing up front: OSRS uses **fixed
  bitmap fonts**, so a custom glyph matcher is both faster and more accurate than Tesseract. This is what
  the reference open-source color bot (OS-Bot-COLOR) does. Not required until a script needs to read text.
- **DPI awareness.** No manifest is set; coordinate math assumes 100% scaling. Needs
  `SetProcessDpiAwarenessContext` and re-testing before any serious click-accuracy work on scaled displays.
- **`PrintWindow` as a capture fallback** for the GPU-plugin/occlusion cases where `BitBlt` returns black
  frames. May not help for an OpenGL-rendered Java canvas — needs experimenting.
- **Process name.** `Attach` assumes a process named `RuneLite`; a jar-based launch may appear as `javaw`.
