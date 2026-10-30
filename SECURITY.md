# Security policy

This is an experimental diagnostics prototype, not a security-audited release.

## Runtime scope

The mod collects numeric callback intervals in bounded memory, displays diagnostics and optionally logs summaries through Mod Helper. User-started benchmark sessions save numerical CSV files under the Windows Documents/BTD6 Benchmarks directory after recording stops. Files have unique names and are closed after writing; failed saves retain the buffer for retry. It does not read existing benchmark files or upload them. It contains no network client, updater, executable downloads, shell execution, credential access, save editing, unsafe C# code. This description covers this project's code, not MelonLoader, Mod Helper, Unity or BTD6.

Mods run inside the game process. They are not sandboxed. Public source and code comments support review but do not guarantee safety.

## Contributions

Keep gameplay object access on the game thread unless proven safe for the particular API and version. Do not add telemetry, remote code loading or new dependencies without explaining their need and reviewing their behavior. Never commit tokens, personal logs, save files or proprietary game DLLs.

Review changes to workflows as executable code. CI has read-only contents permissions and cannot release binaries. Review dependency and action updates before merging.

## Releases and reports

No supported production release exists yet. Before a release, record build inputs, run integration tests and publish a source commit plus DLL SHA-256 checksum. Only claim compatibility for versions actually tested.

Private vulnerability reporting is enabled. Use https://github.com/Daniel-Eley/LateGameOptimisation/security/advisories/new to report security issues privately. Do not post credentials or exploitable sensitive details in public issues. Ordinary bugs can be filed as issues without personal data.

Version 0.4.0 optionally installs an observational Harmony timing hook on
Simulation.Simulate when explicitly enabled, requiring restart. It preserves
arguments, return values and exceptions. No simulation hook is installed with
profiling disabled at startup. The method timing adds overhead and depends on
version-specific native interception. Lifecycle markers read notifications only.
CSV files now also contain timing totals, call counts and state/event labels.
