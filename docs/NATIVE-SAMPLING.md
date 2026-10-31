# Native sampling investigation — 1 November 2026

## Why change measurement methods?

The 0.7.1 collision pilot took 92.845 s from round start to round end. Three fresh
broad-only runs took 77.038, 78.465 and 79.978 s. The instrumented run was 18.3%
slower than their median. This is evidence of substantial measurement distortion,
not a calibrated overhead correction. One experimental run and sequential sessions
do not isolate every source of variation. Do not subtract this difference from
one method or treat the measured 72% hit-processing share as an original-game share.

## Available tools and prepared profile

Windows' installed WPR 10.0.26100 lists CPU sampling support. The custom profile
`scripts/NativeCpu.wprp` was accepted by `wpr -profiledetails` and reported exactly
Loader, ProcessThread, SampledProfile keywords and SampledProfile stacks. It uses
128 buffers of 1024 KB in memory mode (128 MiB of configured collector buffers).
This is a bounded circular buffer, not a guarantee of retaining every event.
No recording has yet been started or validated with this profile.

The standard CPU.Verbose profile on this machine also enabled context-switch and
other event providers, with much larger buffers. The custom profile omits those
to narrow this first CPU investigation. It is system-wide, not BTD6-only capture.
ETL may contain process names, paths and other system metadata: store it outside
OneDrive and do not commit/upload the raw trace. The profile adds no disk/network
event providers, no game hooks, and no security-setting changes.

WPA and WPAExporter were not found on PATH or in the standard toolkit directory;
the current-user WPA Appx query returned no package. An analysis tool still needs
to be obtained or located before full trace analysis. Microsoft's supported option
is Windows Performance Analyzer from the Windows Performance Toolkit/ADK. No
analyzer has been installed by this investigation.

## First capture procedure

Keep the known-working 0.7.1 DLL, Simulation Profiling on, Detailed Profiling off,
Collision Profiling off. Restart after changing hook switches. Load the same saved
round at 3x and pause before starting. Coordinate capture before playing: no useful
CPU trace can be collected from a closed or paused game.

Use an elevated terminal, check `wpr -status`, and do not cancel another recording.
Use a unique WPR instance for this experiment. Start the custom memory profile,
record a short 30-second interval of active laggy gameplay, and stop/save that same
instance to a unique file under `%LOCALAPPDATA%/LateGameOptimisation/Traces`.
Do not leave recording running while waiting indefinitely for user input. Keep
the round timing CSV alongside the experiment notes, but do not assume its relative
timestamps directly align with ETW timestamps. Focus/menu transitions must be excluded.

The next pilot must verify: successful start/stop, captured BTD6 process identity,
module load/rundown information, native instruction addresses, usable stacks and
event/buffer loss. If stacks are incomplete, report that limitation rather than
claiming inclusive attribution. Compare capture-on/off performance before relying
on small differences; sampling has overhead too. No speedup is implemented.

## Attribution without private game symbols

WPA can inspect ETW CPU samples. Game method names may be unavailable without
private symbols. The local Cpp2IL address map provides a possible fallback:

1. Verify the exact GameAssembly hash matches the inspected build.
2. Use the module's loaded base address for the sampled process and timestamp.
   Subtract that base from the sampled instruction address to obtain an RVA;
   never subtract the preferred PE base blindly because of ASLR.
3. Match the RVA to validated method ranges. Keep every overlapping/shared alias,
   and leave gaps unresolved. A matching shared generic body does not identify T.
4. Separate leaf samples (where the CPU was executing) from inclusive stack
   samples (where the method was on the stack). Do not add inclusive categories.

This mapping pipeline is proposed, not yet verified on an actual ETL export.
Inlining, tail calls, incomplete unwind data and generic sharing limit attribution.
Sampling measures CPU execution, unlike our previous elapsed wall-time probes.

## Additional static leads

The existing hash-checked inspection script decoded these local entries:

| Entry | RVA | Mapped bytes | Observed direct call |
| --- | --- | ---: | --- |
| Projectile.FilterBloon | C8C980 | 529 | HaveAlreadyCollidedWithBloonInLineage |
| Map.IsTargetLineClear | F5C3B0 | 736 | Area.Intersects |
| Projectile.Exhausted | C8C570 | 542 | RootObject.Destroy |

These extend the prior IsCollisionValid → FilterBloon / conditional line-clear path.
They are candidate sample-attribution targets, not proof of dominant cost or
redundancy. Virtual/interface callbacks remain unresolved. No guard, history check,
line-of-sight test or destruction callback may be skipped just because it looks
expensive. Raw disassembly remains in ignored local storage.

## Sources

- [Microsoft WPR provider definitions](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/2-system-and-event-provider-definitions)
- [Microsoft CPU sampling overview](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/xperf/cpu-sampling-by-process)
- [Microsoft Windows Performance Analyzer](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/windows-performance-analyzer)
- [Microsoft symbol support](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/symbol-support)
