# LateGameOptimisation

An experimental BTD6 mod investigating late-game performance. **Version 0.3.0 is a diagnostics foundation, not a performance improvement or a multicore simulation mod.** It does not change targeting, damage, projectiles or game speed. In-game compatibility has not yet been tested.

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

Recording uses a fixed buffer of 120,000 intervals (under 1 MB). It automatically
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
