# LateGameOptimisation

An experimental BTD6 mod investigating late-game performance. **Version 0.1.0 is a diagnostics foundation, not a performance improvement or a multicore simulation mod.** It does not change targeting, damage, projectiles or game speed. In-game compatibility has not yet been tested.

## What currently works in the source

Enable **Enable Frame Timing** in Mod Helper's mod settings to collect frame callback intervals. About every ten seconds, the MelonLoader log receives sample count, average interval, longest interval and callbacks per second. Turn the setting off to discard the current window. Collection is off by default.

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

Our runtime code has no network requests, telemetry, process launching, dynamic code downloads or direct file access. It logs numerical timing summaries through Mod Helper/MelonLoader. Those dependencies have their own behavior and permissions; this does not sandbox them or guarantee that the whole modded game is secure.

Read [SECURITY.md](SECURITY.md) for the review and release policy. Source visibility makes review possible; it does not prove a downloaded DLL matches that source. No release binary is being advertised yet.

## Project status

The first milestone is measurement. The next is identifying a specific expensive calculation that can be safely isolated. Multicore gameplay work remains unimplemented, and speedups are not promised.

Original project code is provided under the MIT license. BTD6 and its dependencies are separately owned and licensed.
