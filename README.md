# LateGameOptimisation

An experimental BTD6 mod investigating late-game performance. **Version 0.6.0 is a diagnostics foundation, not a performance improvement or a multicore simulation mod.** It does not change targeting, damage, projectiles or game speed. In-game compatibility has not yet been tested.

**AI development disclosure:** This project is developed with OpenAI Codex. AI generated the initial custom diagnostic code, tests, documentation and workflow changes from the Mod Helper template, under Daniel Eley's direction. This is substantial AI involvement, not just autocomplete. See [AI_DISCLOSURE.md](AI_DISCLOSURE.md) for scope and validation limits.

## Strict gameplay preservation requirement

Performance is the only permitted gameplay-facing change. Optimisations must preserve the unmodified game's rules, simulation timing, targeting, damage, collisions, random-number sequence, economy, progression and outcomes for the same starting state and inputs at the same simulation ticks. Visual effects, audio and controls must also be preserved; reducing quality or removing projectiles is not an acceptable shortcut.

Smoother rendering and less real-world waiting from reduced lag are intended. Changing simulation speed, skipping updates, approximating results or changing the order of gameplay events is prohibited. If a proposed optimisation cannot meet this requirement, it must remain disabled and the original game calculation must run. Multicore execution is a possible technique, never a reason to compromise correctness.

This is a project requirement, not a claim of proven compatibility. The current source only measures callback intervals and logs summaries; it does not patch or write gameplay state. In-game parity remains untested, and optional diagnostics add some overhead.

## What currently works in the source

**Show Diagnostics Overlay** is enabled by default in Mod Helper settings. A non-interactive bottom-left panel shows callback FPS, mean and maximum frame interval, elapsed window time and sample count. Text refreshes twice per second; statistics reset every ten seconds. It appears in menus and gameplay after the loader initializes. Disable that setting to hide it.

**Enable Frame Timing** separately enables ten-second summaries in the MelonLoader log and remains off by default. Sampling runs while either the overlay or logging is enabled. Turn both off to stop sampling and discard the window. The overlay does not capture input or modify gameplay state; drawing it adds some overhead.

These numbers include menus, loading, pauses and background-window stalls. They do not tell us which method is slow, how much CPU a method uses, or how fast simulation time progresses. Logging itself has a small cost. Compare like-for-like gameplay and use a profiler before choosing an optimisation.

## Why multiple cores require research

A thread is a sequence of instructions; multiple CPU cores can run different threads simultaneously. That helps only when the work is independent. Two towers may affect the same bloon, and one hit may change what the next tower should target. Running both updates at once can change results or corrupt shared state.

We cannot assume BTD6's existing simulation APIs are thread safe. Nor should we assume the entire game currently uses only one core. The proposed design is: copy a small, relevant snapshot on the game thread; perform pure calculations on worker threads; validate and apply results in a deterministic order on the game thread. Snapshot copying, scheduling and waiting all cost time. Only measurements can establish a gain.

See [the development guide](docs/DEVELOPMENT.md) for the research stages and C# walkthrough.

## Build

1. Install BTD6, a compatible MelonLoader and BTD6 Mod Helper using the [Mod Helper installation guide](https://gurrenm3.github.io/BTD-Mod-Helper/wiki/Install-Guide).
2. Launch the modded game once to generate its assemblies, then close it.
3. Install a .NET SDK capable of building net6.0. The separate diagnostics checks use the .NET 10 SDK. net6.0 is retained for the installed loader's runtime compatibility.
4. In this repository, run:

```powershell
dotnet build LateGameOptimisation.csproj -c Release -p:RestoreConfigFile=NuGet.Config
# For a different game location:
dotnet build LateGameOptimisation.csproj -c Release -p:RestoreConfigFile=NuGet.Config -p:BloonsTD6="D:\Games\BloonsTD6"
dotnet run --project tests/DiagnosticsTests.csproj -c Release -p:RestoreConfigFile=NuGet.Config
```

If dotnet is not on PATH, restart your terminal after installation, or replace it with `& 'C:/Program Files/dotnet/dotnet.exe'` in PowerShell.

The mod DLL is written to `bin/Release/net6.0/LateGameOptimisation.dll`. Copy only that DLL into the game's Mods folder while the game is closed. Remove it to uninstall. Build does not install anything in the game directory. Game and loader assemblies are referenced locally, never bundled in this repository. Mod Helper must be directly under Mods for this build configuration.

The GitHub workflow tests the statistics class without game dependencies. A green check does **not** certify game compatibility or performance. No workflow publishes binaries automatically.

## Security and trust

Our runtime code has no network requests, telemetry, process launching or dynamic code downloads. User-started benchmarks write numerical CSV files to Documents/BTD6 Benchmarks only after recording stops. It logs numerical timing summaries through Mod Helper/MelonLoader. Those dependencies have their own behavior and permissions; this does not sandbox them or guarantee that the whole modded game is secure.

Read [SECURITY.md](SECURITY.md) for the review and release policy. Source visibility makes review possible; it does not prove a downloaded DLL matches that source. No release binary is being advertised yet.

## Project status

The first milestone is measurement. The next is identifying a specific expensive calculation that can be safely isolated. Multicore gameplay work remains unimplemented, and speedups are not promised.

Original project code is provided under the MIT license. BTD6 and its dependencies are separately owned and licensed.

## Save benchmarks without closing BTD6 (0.3.0)

1. Load your round-300 save and select built-in 3x speed.
2. Unpause and let the game settle. Press **F8** to start recording.
3. Play for roughly 60 seconds without opening menus or switching applications.
4. Press **F8** again to stop and save, then pause. Wait for the overlay to say **Saved**.
5. Open your Windows **Documents** folder, then **BTD6 Benchmarks**. The newest
   `benchmark-*.csv` is closed and can be copied immediately while the game stays open.
6. Reload the same starting save and repeat. Each run receives a unique filename.

The hotkey is configurable in Mod Helper settings under **Benchmark Hotkey**.
Choose an unused key if F8 conflicts with another mod. The mod observes the key;
it does not suppress other bindings. **Start Benchmark** and **Stop And Save Benchmark**
buttons are also available in those settings, but opening menus adds menu time.
Overlay instructions refer to the default F8 key; use your chosen key if rebound.
Logging does not need to be enabled. Recording works even with the overlay hidden.

Each CSV contains `sample`, `elapsed_ms` and `frame_interval_ms`. It contains no
save data, account data or automatic hardware inventory. Filenames use UTC time
and a unique suffix. Windows may redirect Documents into OneDrive; use the actual
Documents folder rather than assuming it is under your user profile. Any cloud
sync configured for that folder is controlled by Windows/OneDrive, not this mod.

Recording uses fixed buffers for 120,000 intervals and their diagnostic fields. It automatically
stops and saves at that limit or after an observed interval reaches ten minutes
of recording, whichever occurs first. Writing happens synchronously after stopping
and may briefly stall that moment; it is outside the saved measurement. No disk
writes occur during collection. A full-disk or permission error retains the run in
memory: correct the problem and press Stop And Save again. Do not use a partial
CSV left by a failed save. New recordings cannot replace an unsaved run.

Pauses, menus, loading and loss of focus are not automatically excluded. Stop
before reloading or pausing and record the round/speed/settings separately. This
is frame-interval capture, not profiling of individual game calculations. Stop
and save before quitting: an unfinished run in memory is lost on exit or crash.

The 0.3.0 Release build and recorder checks passed; its hotkey and settings UI
still need in-game validation. The 0.2.0 overlay was confirmed working by the owner.

## Round markers and simulation profiling (0.4.0)

This supersedes the 0.3.0 CSV schema and round-end behavior described above.
For comparable runs, load the same round-300 starting save, press F8 **before
starting the round**, then start it at built-in 3x. Recording now stops and saves
when Mod Helper reports round end. Match end and restart also stop and save.
Wait for Saved before starting another recording. The game itself is not paused,
restarted or otherwise controlled by this mod.

Round start/end, match start/end, restart, pause-menu open/close and sampled
window-focus changes are recorded as event rows. State begins as unknown until
a relevant hook is observed; starting mid-round may leave it unknown. Pause-menu
hooks do not detect every possible pause imposed by other mods. Do not interpret
unknown as active. A frame crossing a state change is labelled transition.
Use active_round frame rows for the active interval analysis; preserve transition
rows and round markers separately to assess the boundary. Event rows follow frame
rows in the file and must be sorted by elapsed_ms to create a timeline.

CSV columns: record_type, sample, elapsed_ms, frame_interval_ms, state, event,
simulation_ms, simulation_calls, profiling_enabled. Old CSVs remain unchanged.
Times are wall-clock milliseconds. The event buffer is bounded at 4,096 entries;
its last slot signals event_capacity_reached if full. Collection is not a replay
and does not record game inputs or full state.

For a second, diagnostic run, enable **Enable Simulation Profiling** in this mod's
settings and restart BTD6. Confirm the overlay says **Profile: on**. Repeat the
same round. Profiling installs an observational Harmony prefix/finalizer on the
installed Simulation.Simulate method. It does not skip the method, modify its
arguments/results, or suppress its exceptions. Only main-thread outermost calls
are timed, avoiding double-counting recursive calls. Other threads are excluded.

simulation_ms is inclusive elapsed time in the method during that sample, not
CPU time, and can include waiting and other mods' work. simulation_calls indicates
whether calls were actually observed. Zero calls is not evidence that simulation
costs nothing: the hook may not intercept the native path in this game version.
Do not add these times to frame time or label the remainder rendering time.
Profiler overhead affects measurements; compare on/off runs before interpreting
results. This is a broad first measurement, not a targeting/collision breakdown.
Disable profiling and restart to remove the hook for ordinary benchmarks.

The Release build and standalone recorder tests passed. Round/pause hooks and
native simulation interception still require validation in the running game.
No claim of measured simulation cost or gameplay-equivalence testing is made yet.

## Detailed simulation probes (0.5.0)

Enable **Enable Detailed Profiling** in Mod Helper settings, then restart BTD6.
This also enables the broad simulation timer even if Enable Simulation Profiling
is off. The overlay's **Detail: 15/15** means all four selected hooks installed;
it is a bitmask, not a percentage. A smaller value means incomplete coverage:
1 = collision, 2 = targeting, 4 = weapons, 8 = bloons (sum the installed bits).
Installation alone does not prove the game executes an intercepted method.

Record one round-300 run first, starting with F8 before the round. Let round-end
automatically save it. Check the CSV call counts before repeating runs. The CSV
adds detailed_mask and four timing/count pairs:

| Columns | Exact method observed |
| --- | --- |
| collision_ms / collision_calls | CollisionChecker.Process |
| target_ms / target_calls | Attack.FindTarget |
| weapon_ms / weapon_calls | Weapon.Process |
| bloon_ms / bloon_calls | Bloon.Process |

Only outermost calls of each category on the main thread, nested within the
observed Simulation.Simulate call, are measured. Specialized behavior can bypass
these methods; labels are entry-point timings, not complete subsystem totals.
Zero calls means no calls observed through that probe, not zero subsystem cost.

Times are inclusive and can overlap across categories. Never sum them or subtract
their sum from simulation time. Wrapper/interop and clock-reading overhead can
be substantial for frequently called methods, even though only four entry points
are instrumented. Compare broad-only runs against detailed runs before making
performance claims; treat this mode as a diagnostic experiment, not an optimisation.
The rest of the simulation remains unmeasured at this level.

Turn **Enable Detailed Profiling** off and restart to return to broad-only profiling.
Turn both profiling settings off and restart for an uninstrumented benchmark.
No original method is skipped and no arguments, return values, game state or
exceptions are changed. The new native hooks have not yet been verified in-game.
Build and CSV checks passed, including detailed values, schema alignment and buffer
reuse. Your existing CSV files are unchanged.

## Expanded coverage and revised benchmarks (0.6.0)

Detailed profiling now includes eight selected methods. With Enable Detailed
Profiling on and after restarting, the overlay displays **Detail: 8/8** when all
hooks installed. This is an actual count, replacing the old bitmask-style display.
The CSV still records a bitmask: all eight installed = 255, original four = 15.
Zero observed calls can still mean a method is unused or not intercepted.

Additional timing/count pairs:

| Columns | Exact method | Bit |
| --- | --- | --- |
| travel_ms / travel_calls | TravelStrait.Process | 16 |
| tracking_ms / tracking_calls | TrackTarget.Process | 32 |
| age_ms / age_calls | Age.Process | 64 |
| maintenance_ms / maintenance_calls | Simulation.FlushProcessList | 128 |

These are candidate paths for projectile travel, homing, lifetime and process-list
maintenance. They do not cover every movement style or tower behavior. Their
names alone do not prove how much work they perform. Measurements remain inclusive,
may overlap, and apply only inside main-thread Simulation.Simulate calls. Do not
sum categories. High-frequency hooks can materially distort measured cost.

When changing towers, upgrades, round or other workload settings, treat the result
as a NEW benchmark. Keep the older save/results separately. Finish setup changes,
then preserve that starting save and document map, round, tower/upgrade changes,
3x speed and other mod settings. Do not compare changed setups as an optimisation
before/after result. The prior heavy test was a regular Spa Pits round-234 saved
game with ninja spam, not sandbox.

For the new setup, first collect three broad-only runs (Simulation Profiling on,
Detailed Profiling off; restart after changing these switches). Then enable
Detailed Profiling, restart, confirm 8/8 and collect ONE diagnostic run. Start
recording with your configured benchmark hotkey before starting the round and
wait for the round-end save. Preserve the exact same starting conditions across
runs. Report any partial hook count before collecting further detailed runs.

Version 0.6.0 builds with zero warnings/errors and passes recorder/schema tests.
The four new native probes have not yet been validated in-game. No gameplay
optimisation is introduced by this release.

## Optimisation feasibility assessment

Read [the collision and homing feasibility assessment](docs/FEASIBILITY.md) for
inspected interfaces, binary identity, confirmed findings, unknowns and acceptance
gates. Direct parallel calls to existing simulation methods are not approved.
A copied-data homing geometry experiment is conditional on establishing its native
formula and dependencies; no safe replacement or performance gain is established.
The local read-only inspection can be reproduced with scripts/Inspect-Feasibility.ps1.
