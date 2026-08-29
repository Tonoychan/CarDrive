# Racing Game — Planning Doc

Last updated: 2026-08-29 (Phase 0 verified in-editor)

## Plugin decision

Using **BxB Studio MVC (Multiversal Vehicle Controller)** as the base for cars, AI, and
performance data, not Vehicle Physics Pro.

Reasoning:
- MVC already ships an AI/path toolkit: `AI Path`, `AI Path Bezier`, `AI Path Anchor`,
  `AI Path Visualizer`, `AI Controller`, `AI Agent`, `AI Follow Path`, `AI Vehicle`, and
  `AI Zones` (Brake Zone, Handbrake Zone, NOS Zone, Obstacle Sensors).
- MVC has a data-driven Engine Preset / Performance model (acceleration, deceleration,
  mass, RPM curves) — a natural fit for a parts-upgrade system later.
- MVC already includes a Mobile variant (joystick UI, mobile scenes) alongside the
  desktop setup.
- Vehicle Physics Pro has no AI, waypoint, traffic, or race tooling anywhere in its SDK
  or samples — physics-only. Using it would mean building AI pathing from scratch.
- The two plugins aren't designed to interoperate; don't mix them long-term in one scene.

## Confirmed decisions (from user, 2026-08-29)

- Race format: **point-to-point sprint** (start position → end position, no laps).
- Path/race setup tools: **Editor-only** (author tracks in the Unity Editor before
  shipping, not an in-game/runtime track editor).
- AI scale: **larger grid, 6+ opponents**, with real traffic behavior — overtaking and
  collision avoidance between AI cars, not just independent path-following.
- Target platform: **both PC and mobile**, from the start.

## Feature roadmap

### Phase 0 — Foundation
- ~~Remove Vehicle Physics Pro content from `Assets/Scenes/TestScene.unity`~~ — **DONE
  (verified 2026-08-29):** scanned every component on every GameObject in the scene via
  script-execute; zero VPP/EVP components found. All 7 root objects (Directional Light,
  Global Volume, Plane, `2005 BMW M3 GTR E46`, SimpleCamera, The City, `_GameController`)
  already use MVC types (`MVC.Core.Vehicle`, `MVC.Core.VehicleFollower`,
  `MVC.VehicleManager`). The `Vehicle Physics Pro` folder still exists under `Assets/` as
  an unused reference package — left in place per the original plan (reference only, not
  in the live scene).
- ~~Set up desktop + mobile input/UI in one project configuration~~ — **DONE (2026-08-29):**
  merged `_GameController` from
  `Assets/BxB Studio/MVC Getting Started - Mobile/Scenes/DefaultScene - Mobile.unity` into
  `TestScene`, replacing the old bare controller (which had `VehicleManager` only, no UI).
  The merged controller carries `PostProcessing`, `Canvas` (desktop HUD: SpeedMeter, logos)
  and `MobileCanvas` (`MobilePreset0`, `MobilePreset1`, `EventSystem`) as children, all
  driven by MVC's built-in `VehicleUIController` — confirming MVC already ships the "one
  HUD, platform-conditional input layer" pattern Phase 5 calls for (`activeMobilePreset`
  toggles which joystick/touch layout `RectTransform` group is active). `VehicleManager
  .playerTarget` was repointed to TestScene's `2005 BMW M3 GTR E46`. Verified error-free in
  Play mode: steering wheel, pedals, handbrake, NOS button, and speed/RPM HUD all render
  and respond against the BMW (see screenshot sent in chat). `GroundMappers` was left
  empty (TestScene's `Plane` has no `VehicleGroundMapper` component yet) — surface
  grip/audio tuning is Phase 6 scope, not a blocker here.
  - Known follow-up (not urgent): actual runtime platform detection (auto-picking desktop
    vs. mobile preset instead of both existing side-by-side) is Phase 5 polish, not done
    here.

### Phase 1 — Data model (ScriptableObjects) — CODE DONE (2026-08-29)
Implemented as `Assets/Scripts/Racing/`:
- `RaceOpponentPreset` (SO): vehicle prefab ref, optional engine-stat override
  (power/torque/RPM/mass — real MVC `VehicleEngine` fields), and AI behaviour tuning
  (`VehicleAIPathFollower.TrafficPolicy`, speed multiplier, follow gaps).
- `RaceDefinition` (SO): race name, opponent count, opponent preset list, countdown time.
- `RaceCourse` (scene `MonoBehaviour`, not a SO): links a `RaceDefinition` asset to the
  scene-specific bits that **can't** live in a portable asset — grid slot `Transform[]`,
  finish line `Transform`, main/alternate `VehicleAIPath` refs. This split was necessary
  because Unity SOs cannot hold persistent scene-object references.
- Checkpoints: **no separate checkpoint system needed** — `VehicleAIPath` already exposes
  `ClosestSpacedPointIndex(position)` / `TotalLength` / `IsValid`, which `RaceManager`
  uses directly for race-position ordering. One less thing to build.

### Phase 2 — Race Setup Tool (Editor-only) — CODE DONE (2026-08-29)
`Assets/Scripts/Racing/Editor/`:
- `RaceCourseEditor` (custom inspector for `RaceCourse`): Add/Auto-Arrange grid slots,
  create finish line, create AI path stub, scene-view position/rotation handles for
  slots + finish line, validation warnings (slot count vs. definition, missing path).
- `RaceDefinitionLibraryWindow` (`Racing/Race Definitions` menu): browse + create
  `RaceDefinition`/`RaceOpponentPreset` assets.
- Opened once via script to confirm it instantiates without exceptions. **Not yet
  exercised by a human clicking through it** — do that before trusting it fully.

### Phase 3 — Free roam + trigger cubes — CODE DONE (2026-08-29)
`RaceTrigger` (`OnTriggerEnter`/`Exit`, ignores AI vehicles via `Vehicle.HasAI`) +
`RacePromptUI` (shows "Press E or tap to start: {name}", listens for `Keyboard.current`
E key, confirm button for touch). Not yet wired into an actual scene trigger volume or
Canvas — code compiles clean, UI hookup is pending.

### Phase 4 — Race Manager (runtime state machine) — CODE DONE, PARTIALLY VERIFIED
`RaceEvents` (static C# events) + `RaceManager`
(`Idle → Countdown → Racing → Finished`). Uses `VehicleAIPathFollower` directly (not a
generic "AI Controller/AI Agent" — that was the plan's guess at MVC's API surface before
inspecting it via reflection; the real type is `VehicleAIPathFollower` with a built-in
`TrafficPolicy.Racing` mode that already handles overtaking/gap-keeping, so no custom
traffic logic was needed).
**Verified working in Play mode (2026-08-29):** `BeginRace()` correctly teleports the
player to grid slot 0, instantiates the opponent from `RaceOpponentPreset.vehiclePrefab`
at grid slot 1, configures its `VehicleAIPathFollower`, and transitions
`Idle → Countdown` — all with zero console errors.
**NOT verified:** the per-frame `Update()` countdown tick → `Racing` → finish-line
detection → `Finished` flow. The Editor's Play Mode loop stalled mid-session during
testing (`Time.frameCount`/`Time.time` frozen for 60+ real seconds despite
`isPlaying=true`, then appeared to reset) — an environment/session stability issue, not
a code issue, but it means this path is untested. **Re-verify this in a fresh Editor
session before relying on it.**

### Phase 5 — Race UI — CODE DONE (2026-08-29), NOT VISUALLY TESTED
`RaceUIController` (`Assets/Scripts/Racing/UI/`) subscribes to `RaceEvents` and drives
countdown/position/results `TMP_Text` fields + panel visibility. Not yet wired to actual
Canvas UI elements in a scene, so it has never actually rendered anything on screen.

### Phase 6 — AI traffic tuning — NOT STARTED
Blocked on having a real authored track to tune against (see blocker below). MVC's
`TrafficPolicy.Racing` + per-preset `followTimeGap`/`followMinimumGap`/speed multiplier
give the knobs `RaceOpponentPreset` already exposes; actual tuning needs eyes-on
playtesting once a track exists.

### Phase 7 (later, ambitious) — Parts upgrade — CODE DONE (2026-08-29)
`RacePart` (SO: id, name, cost, additive power/torque/maxRPM/mass deltas) +
`RaceGarageController` (ownership tracked via `PlayerPrefs`, `ApplyOwnedPartsToVehicle`
sums all owned deltas onto the live `VehicleEngine`). No economy/spending logic or UI —
deliberately placeholder per the original plan. Not tested.

## Known blocker: AI path curve authoring cannot be scripted outside Play Mode

`VehicleAIPath.AddNextCurve()` (the API that bakes bezier control points into the
internal curve representation — confirmed via reflection to include precomputed
basis/velocity/acceleration/jerk polynomial coefficients, not just raw P0-P3 points) only
works once the component's real `Awake()` has run. Unity does not call `Awake()` for
components added to a GameObject outside Play Mode unless the script is
`[ExecuteAlways]`, and `VehicleAIPath` is not. Manually invoking `Awake()` via reflection,
and directly writing the raw serialized `bezierCurves` array via `SerializedProperty`,
were both tried and did not work — the baked coefficient fields (`bt/vt/at/jt`) can't be
hand-computed reliably without the plugin's source.
**Practical implication:** an actual race track's `VehicleAIPath` must be authored by a
human, in the real Unity Editor, using MVC's own AI Path Bezier tooling — not scripted.
`RaceCourseEditor`'s "Create AI Path Here" button creates the `VehicleAIPath` component
correctly; a human still needs to place the actual curve points via MVC's own workflow
(likely also requires Play Mode, given what was found here — worth confirming against
MVC's own documentation/tutorial video rather than guessing further).
`RaceCourse_CitySprint` in `TestScene` currently has an **empty, unbaked** `AIPath_Main` —
`RaceManager` handles this gracefully (position tracking just no-ops when `!path.IsValid`)
but no AI vehicle will actually drive anywhere until a real path is authored.

## Example content (for testing/reference)
- `Assets/Racing/OpponentPresets/BmwRival.asset` — uses MVC's own
  `2005 BMW M3 GTR E46 - AI.prefab` (ships with a `VehicleAIPathFollower` already
  attached).
- `Assets/Racing/Races/CitySprintTest.asset` — 1 opponent, 3s countdown.
- `RaceCourse_CitySprint` in `TestScene`: 2 grid slots, a finish line 120m down +Z from
  world origin, `RaceManager` on `_GameController`. AI path unbaked (see blocker above).

## Build order

0 → 1 → 2 → 4 (with 1-2 AI, no traffic tuning yet) → 3 → 5 → 6 (scale to 6+, tune) → 7

**Actual status (2026-08-29):** all code for 0-5 and 7 is written and compiles clean.
Phase 4's core state-transition and spawning logic is confirmed working; the rest of its
state machine and all of Phase 5's UI are implemented but unverified on screen. Phase 6
can't start until a human authors a real `VehicleAIPath` in the Editor (see blocker). Next
session should: (1) restart Unity fresh — this session's Editor/MCP bridge showed repeated
instability (dropped connections after domain reloads, one stalled/reset Play session);
(2) have a human author `RaceCourse_CitySprint`'s AI path by hand; (3) wire
`RaceUIController`/`RacePromptUI` to actual Canvas elements; (4) re-run the Play-mode
smoke test end-to-end (countdown → racing → finish) now that a real path exists.
