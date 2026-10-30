# Development guide

## Reading the C#

Start with LateGameOptimisation.cs. The class inherits BloonsTD6Mod: Mod Helper calls its lifecycle methods. An override supplies our behavior for one such method. OnApplicationStart logs the prototype's scope. OnUpdate runs once per update callback.

ModSettingBool is a true/false option exposed by Mod Helper. The static readonly field keeps one settings object for the mod; readonly prevents replacing that object, but its setting value can still change.

Stopwatch.GetTimestamp reads a monotonic high-resolution clock. Subtract two timestamps and divide by Stopwatch.Frequency to get seconds; multiply by 1000 for milliseconds. We skip the first callback because it has no previous timestamp.

FrameTimeWindow stores count, total and maximum. Its properties calculate average and callback frequency without retaining every sample. A property with a private setter can be read elsewhere but changed only inside the class. Reset discards the current measurement window.

The tests compile the same FrameTimeWindow.cs into a tiny standalone console program. No BTD6 installation is needed. They cover empty windows, invalid samples, unequal intervals, the reporting boundary, long stalls and reset.

## Research plan and acceptance gates

### Mandatory gameplay equivalence

Every stage below is subject to the owner's strict requirement: performance may improve, but gameplay must not change. For the same initial state, random seed and inputs at the same simulation ticks, preserve all gameplay-relevant state and observable behavior. Compare against the same game and dependency versions with the optimisation disabled. A stable new ordering is insufficient: preserve the original ordering wherever it can affect results.

- Preserve targeting priorities and ties, damage, pierce, collision results, movement, spawn order, abilities, buffs, cooldowns, cash, lives, rewards, progression and save behavior.
- Preserve random draws and their order, simulation step size, speed settings and input semantics. Do not lower update frequency, batch hits with changed semantics, use stale results, simplify collisions or approximate floating-point calculations. Reordering floating-point operations can change outcomes and must not be assumed harmless.
- Preserve graphics, effects, animations and audio. Disabling them is a quality change, not an acceptable optimisation for this project.
- Worker results must belong to the exact state and tick in which they are applied. If results are unavailable or invalid, run the original calculation exactly once before any replacement mutation; never skip a tick or apply both paths. Disable an unsupported patch before it affects gameplay. A fallback cannot undo already-corrupted state.
- Test serial and parallel paths against the unmodified calculation, including ties, boundary conditions, repeated seeds, long runs, different worker counts, delayed workers, save/reload and scene changes. Compare gameplay state and event order at each simulation tick, not only final scores or FPS. Any unexplained divergence blocks integration or release; numerical tolerances must not excuse gameplay differences.
- Keep gameplay patches off in unvalidated game versions or modes, including co-op. Compatibility with arbitrary other mods cannot be assumed. Publish exactly which combinations were tested and leave unsupported optimisations disabled.

These are acceptance criteria for future work. The current statistics checks do not implement or satisfy a gameplay-equivalence test suite. Prefer abandoning an optimisation over relaxing these criteria.

### Stages

1. **Baseline:** use a fixed map, save, tower layout, round, resolution and game speed. Record CPU, game, loader and Mod Helper versions. Warm up the game, then repeat comparable runs at least three times. Compare no mod, diagnostics disabled and diagnostics enabled. Do not treat menu/loading intervals as gameplay samples.
2. **Find the bottleneck:** use a profiler that can attribute native/IL2CPP and managed CPU time where possible. Frame intervals alone cannot distinguish simulation, rendering, garbage collection, VSync or a frame cap. Record round completion wall time as well as frame behavior.
3. **Choose one candidate:** inspect its actual current-game implementation and all shared-state access. A possible research area is spatial candidate filtering, but it is not an approved patch. Preserve camo rules, range rules, priorities, ordering and ties. Retaining a vanilla targeting call afterward might eliminate any saving.
4. **Serial reference:** implement the pure-data calculation and verify it against the vanilla result before introducing threads. Prefer simpler caching or reduced allocations if profiling supports those.
5. **Worker experiment:** copy only primitive data or immutable managed value records on the main thread. Never pass Unity objects, IL2CPP wrappers, native pointers, live collections or game RNG to a worker. Bound work and outstanding jobs; avoid one task per projectile.
6. **Determinism and lifetime:** tag snapshots with match identity and simulation tick. Discard results after restart, scene change or mismatch. Restore results in a stable order. Do not share mutable RNG state. Never wait indefinitely for workers on the game thread. Specify cancellation and failure behavior before hooking gameplay.
7. **Shadow comparison:** calculate proposed results without applying them, compare with serial/vanilla outcomes, and record discrepancies. This mode costs extra CPU and is for development only.
8. **Experimental integration:** only replace a game calculation after parity tests and measured net benefit, including snapshot and synchronization overhead. Provide a default-off switch and vanilla fallback. Do not skip a vanilla update unless replacement output is ready and valid.
9. **Release:** verify target game/loader/helper versions, long runs, save/reload, restart, pause, speed changes and other mods. Treat co-op compatibility as a separate unproven feature. Publish benchmark methodology and regressions, not just the best FPS figure.

## Why not Task.Run around tower updates?

Task.Run queues work; it does not make the objects used by that work thread safe. A lock can protect shared state only when every relevant access follows the same locking rule, including game code we do not control. Locking the whole simulation can also erase the speedup.

Amdahl's law bounds the benefit: if 60% of frame time could parallelize perfectly over four cores, the ideal speedup is 1 / (0.4 + 0.6 / 4), about 1.82 times. Real copying, worker scheduling and synchronization reduce that. These numbers are an example, not a BTD6 measurement.

## Build and publication choices

The project deliberately uses explicit local assembly references instead of the generated parent btd6.targets. That parent included automatic installation and downloaded build tools. The smaller project is easier to audit, but additional APIs may require explicit assembly references later.

Release builds enable compiler optimisation and deterministic compilation. Deterministic compilation requires identical inputs/toolchains to compare outputs; it is not a digital signature.

The initial workflow runs game-independent checks with read-only repository permissions, pinned action revisions and no persisted checkout credentials. Full mod builds remain local until a reviewed source for compatible build dependencies is established.

For each future release, record the exact commit, SDK, dependency versions and dependency hashes. Build from a clean checkout, test in-game, calculate a SHA-256 hash with Get-FileHash, then publish a manually reviewed release. Do not upload game assemblies or private logs. A checksum detects changed bytes; on its own it does not authenticate who built them.

Follow [the AI disclosure policy](../AI_DISCLOSURE.md) in contributions, release notes and distribution listings. Report AI involvement and actual validation without implying an independent human review occurred.

## References

- [Mod Helper getting started](https://gurrenm3.github.io/BTD-Mod-Helper/wiki/Getting-Started-Modding)
- [Mod Helper settings](https://gurrenm3.github.io/BTD-Mod-Helper/wiki/Mod-Settings)
- [Unity job data isolation](https://docs.unity.com/en-us/engine/6000.0/manual/scripting/programming-distribute-work-threads/job-system/creating-jobs)

Unity's general documentation explains the design constraints; it does not establish that BTD6 exposes a usable job-system integration.


## Initial validation (30 October 2026)

The local Release build passed with SDK 10.0.403: zero warnings and errors.
All standalone diagnostics checks passed. The game was not launched for validation;
installation, settings display, log output and gameplay compatibility remain untested.
The Thunderstore configuration is a template only; its team and dependency minimums
must be checked before any use. No Thunderstore publication is configured in CI.

## In-game overlay (0.2.0)

ShowDiagnosticsOverlay is a new Mod Helper boolean setting, defaulting to true.
It enables sampling independently of the existing opt-in log setting. OnUpdate
formats a cached display string at most twice per second, plus at the ten-second
report boundary. OnGUI draws a box and label only during repaint events using
MelonLoader's Unity IMGUI callback, so no scene-specific UI objects, input handlers
or gameplay patches are needed. A UI exception disables the overlay for the session
and logs one message. Hiding the overlay and disabling logging stops sampling.

The local Release build and statistics checks passed for 0.2.0. Visual rendering
and interaction in the actual game have not yet been verified. The explicit UI
references match the locally installed IL2CPP assemblies. The diagnostic overlay
is the user-requested UI addition; gameplay graphics and effects remain unchanged.
