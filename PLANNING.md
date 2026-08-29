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

### Phase 1 — Data model (ScriptableObjects)
- `RaceDefinition`: start transform, end transform (grid-positioned for 6+ cars), one or
  more AI Path references, opponent count, opponent vehicle/performance presets.
- AI Paths authored with MVC's `AI Path Bezier` tool. Plan for **multiple path lines**
  (a main line plus 1-2 alternates) so AI can actually overtake instead of forming a
  single-file line.
- Lightweight progress checkpoints along the path for race-position ordering and
  wrong-way detection (not a lap counter — sprint doesn't need one).

### Phase 2 — Race Setup Tool (Editor-only)
Custom Editor window to:
- Place/select start & end transforms with scene-view handles, laid out as a grid.
- Assign/author AI Path(s) via MVC's Bezier tool.
- Set opponent count and per-opponent presets.
- Manage a library of `RaceDefinition` assets.

### Phase 3 — Free roam + trigger cubes
Invisible trigger colliders on the map, each linked to a `RaceDefinition`. Player enters
→ prompt UI ("Start Race: X?") → confirm → hand off to the race manager.

### Phase 4 — Race Manager (runtime state machine)
`Setup → Countdown (3-2-1-GO) → Racing → Finished`. Spawns AI via MVC's
`AI Controller`/`AI Agent` on the assigned path with `AI Zones` and `Obstacle Sensors`
enabled for avoidance/overtaking. Tracks progress-along-path for player and AI to compute
live race position, detects finish-line crossings, produces final results order.

### Phase 5 — Race UI
Trigger prompt → countdown → in-race HUD (position, speed via MVC's UI Controller) →
results screen. One HUD with a platform-conditional input layer (touch vs
keyboard/gamepad), not duplicate UIs per platform.

### Phase 6 — AI traffic tuning
Vary AI performance presets slightly so the pack spreads instead of clumping. Rely on
multiple path lines + Obstacle Sensors for passing behavior. Expect iteration on
brake/handbrake zones per corner — this is the highest-effort tuning phase given 6+ cars
with real overtaking.

### Phase 7 (later, ambitious) — Parts upgrade
Map upgrade "parts" onto MVC's existing Engine Preset/Performance fields rather than a
new performance model. Placeholder garage UI applies a part → adjusts those fields on the
player's vehicle instance. Needs simple persistence (owned parts) even as a placeholder.

## Build order

0 → 1 → 2 → 4 (with 1-2 AI, no traffic tuning yet) → 3 → 5 → 6 (scale to 6+, tune) → 7
