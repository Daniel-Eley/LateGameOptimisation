using System.Diagnostics;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.ModOptions;
using MelonLoader;
using LateGameOptimisation;
using UnityEngine;
using System;
using System.IO;

[assembly: MelonInfo(typeof(LateGameOptimisation.LateGameOptimisation), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6-Epic")]

namespace LateGameOptimisation;

public class LateGameOptimisation : BloonsTD6Mod
{
    // Exposed in Mod Helper's settings. Off by default: no periodic logging.
    public static readonly ModSettingBool EnableFrameTiming = new(false);
    // A separate new setting means old saved logging preferences cannot hide
    // the display on the first launch of this version. Logging stays opt-in.
    public static readonly ModSettingBool ShowDiagnosticsOverlay = new(true);
    public static readonly ModSettingBool EnableSimulationProfiling = new(false) { requiresRestart = true };
    public static readonly ModSettingBool EnableDetailedProfiling = new(false) { requiresRestart = true };
    private string gameState = "unknown";
    private bool stateChanged;
    private string stateBeforePause = "unknown";
    private bool saveRequested;
    private bool focused = true;
    public static readonly ModSettingHotkey BenchmarkHotkey = new(KeyCode.F8);
    public static readonly ModSettingButton StartBenchmark = new(() => instance?.StartRecording());
    public static readonly ModSettingButton StopAndSaveBenchmark = new(() => instance?.StopAndSave());
    private static LateGameOptimisation? instance;
    private readonly BenchmarkRecorder recorder = new();
    private long recordingTimestamp;
    private string recordingStatus = "F8: Start benchmark (10 min maximum)";
    private readonly FrameTimeWindow window = new();
    private long previousTimestamp;
    private long nextDisplayTimestamp;
    private string displayText = "LateGameOptimisation | Diagnostics\nCollecting frame intervals...\nDeveloped with OpenAI Codex (AI)";
    private GUIStyle? overlayStyle;
    private bool overlayFailed;

    public override void OnApplicationStart()
    {
        instance = this;
        if (EnableSimulationProfiling || EnableDetailedProfiling)
        {
            try { SimulationProfiler.Install(); }
            catch (Exception error) { ModHelper.Msg<LateGameOptimisation>($"Simulation profiling unavailable: {error.Message}"); }
        }
        if (EnableDetailedProfiling && SimulationProfiler.Installed)
        {
            try { DetailedProfiler.Install(); }
            catch (Exception error) { ModHelper.Msg<LateGameOptimisation>($"Detailed profiling setup error: {error.Message}"); }
        }
        ModHelper.Msg<LateGameOptimisation>("Diagnostics prototype loaded. No gameplay optimisations enabled.");
    }

    public override void OnUpdate()
    {
        if (Application.isFocused != focused)
        {
            focused = Application.isFocused;
            recorder.Mark(focused ? "focus_gained" : "focus_lost");
            stateChanged = true;
        }
        // Lifecycle hooks only request a save. Disk I/O happens here, after
        // returning from the original game's round-end or match-end callback.
        if (saveRequested) { saveRequested = false; StopAndSave(); }
        if (BenchmarkHotkey.JustPressed())
        {
            if (recorder.IsRecording || recorder.HasPendingSave) StopAndSave();
            else StartRecording();
        }
        // Separate timestamps avoid recording an interval from before Start,
        // and keep recording working when the overlay and logging are hidden.
        if (recorder.IsRecording)
        {
            long timestamp = Stopwatch.GetTimestamp();
            var profile = SimulationProfiler.Drain();
            if (recordingTimestamp != 0 && recorder.Add((timestamp - recordingTimestamp) * 1000.0 / Stopwatch.Frequency,
                    stateChanged ? "transition" : (!focused ? "unfocused" : gameState), profile.Milliseconds, profile.Calls, DetailedProfiler.Drain()))
                StopAndSave();
            stateChanged = false;
            recordingTimestamp = timestamp;
        }
        // Main-thread callback intervals, NOT CPU timings of game methods.
        if (!EnableFrameTiming && (!ShowDiagnosticsOverlay || overlayFailed))
        {
            previousTimestamp = 0;
            window.Reset();
            nextDisplayTimestamp = 0;
            displayText = "LateGameOptimisation | Diagnostics\nCollecting frame intervals...\nDeveloped with OpenAI Codex (AI)";
            return;
        }

        long now = Stopwatch.GetTimestamp();
        if (previousTimestamp != 0)
        {
            double milliseconds = (now - previousTimestamp) * 1000.0 / Stopwatch.Frequency;
            bool reportReady = window.Add(milliseconds);
            // Formatting allocates strings: refresh twice a second, not once
            // per frame or GUI event. Keep the last report until the next one.
            if (now >= nextDisplayTimestamp || reportReady)
            {
                displayText = $"LateGameOptimisation | Diagnostics\n" +
                    $"{window.FramesPerSecond:F1} FPS (update callbacks)\n" +
                    $"Frame mean: {window.Mean:F2} ms   Max: {window.Maximum:F2} ms\n" +
                    $"Window: {window.Total / 1000:F1} / 10 s   Samples: {window.Count}\n" +
                    $"State: {gameState} | Profile: {(SimulationProfiler.Installed ? "on" : "off")} | Detail: {DetailedProfiler.InstalledCount}/{DetailedProfiler.ProbeCount}\n" +
                    "Developed with OpenAI Codex (AI)";
                nextDisplayTimestamp = now + Stopwatch.Frequency / 2;
            }
            if (reportReady)
            {
                // One summary per ten seconds; no account or save data recorded.
                if (EnableFrameTiming) ModHelper.Msg<LateGameOptimisation>(
                    $"Frame intervals: {window.Count} samples, mean {window.Mean:F2} ms, " +
                    $"max {window.Maximum:F2} ms, {window.FramesPerSecond:F1} callbacks/s. " +
                    "Includes menus, pauses and loading; not simulation profiling.");
                window.Reset();
            }
        }
        previousTimestamp = now;
    }

    private void StartRecording()
    {
        if (!recorder.Start()) return;
        recorder.ProfileEnabled = SimulationProfiler.Installed;
        recorder.DetailedMask = DetailedProfiler.InstalledMask;
        DetailedProfiler.Drain();
        recorder.Mark("detailed_" + DetailedProfiler.Status);
        recorder.Mark("initial_state_" + gameState);
        SimulationProfiler.Drain();
        SimulationProfiler.Recording = true;
        recordingTimestamp = Stopwatch.GetTimestamp();
        recordingStatus = SimulationProfiler.Installed ? "RECORDING + simulation profiling" : "RECORDING - F8: Stop and save";
    }

    private void StopAndSave()
    {
        recorder.Stop();
        SimulationProfiler.Recording = false;
        if (!recorder.HasPendingSave) return;
        try
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(documents)) throw new IOException("Windows Documents folder was not found.");
            string path = recorder.Save(Path.Combine(documents, "BTD6 Benchmarks"));
            recordingStatus = $"Saved {recorder.Count} frames to Documents/BTD6 Benchmarks";
            ModHelper.Msg<LateGameOptimisation>($"Benchmark saved and closed: {path}");
        }
        catch (Exception exception)
        {
            recordingStatus = "SAVE FAILED - F8 or Stop and Save to retry";
            ModHelper.Msg<LateGameOptimisation>($"Benchmark save failed; buffer retained: {exception.Message}");
        }
    }

    private void State(string state, string marker, bool stop = false)
    {
        gameState = state;
        stateChanged = true;
        recorder.Mark(marker);
        if (stop && recorder.IsRecording)
        {
            // Retain the last partial interval with an explicit boundary label.
            long now = Stopwatch.GetTimestamp();
            var profile = SimulationProfiler.Drain();
            if (recordingTimestamp != 0) recorder.Add((now - recordingTimestamp) * 1000.0 / Stopwatch.Frequency,
                "transition", profile.Milliseconds, profile.Calls, DetailedProfiler.Drain());
            recorder.Stop(marker + "_stop");
            SimulationProfiler.Recording = false;
            saveRequested = true;
        }
    }

    public override void OnRoundStart() => State("active_round", "round_start");
    public override void OnRoundEnd() => State("between_rounds", "round_end", true);
    public override void OnMatchStart() => State("unknown", "match_start");
    public override void OnMatchEnd() => State("outside_match", "match_end", true);
    public override void OnRestart() => State("unknown", "restart", true);
    public override void OnPauseScreenOpened(Il2CppAssets.Scripts.Unity.UI_New.Pause.PauseScreen screen)
    {
        stateBeforePause = gameState;
        State("pause_menu", "pause_menu_open");
    }
    public override void OnPauseScreenClosed(Il2CppAssets.Scripts.Unity.UI_New.Pause.PauseScreen screen)
        => State(stateBeforePause, "pause_menu_closed");

    public override void OnGUI()
    {
        // IMGUI is drawn by MelonLoader across scene changes. Repaint-only
        // drawing has no buttons, focus, input handling or gameplay hooks.
        if (!ShowDiagnosticsOverlay || overlayFailed)
            return;

        try
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            overlayStyle ??= new GUIStyle()
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                wordWrap = true
            };
            // Bottom-left keeps the overlay away from the top resource bar.
            // Screen-relative bounds also adapt to window-size changes.
            float width = Mathf.Min(390, Screen.width - 16);
            float top = Mathf.Max(8, Screen.height - 198);
            GUI.Box(new Rect(8, top, width, 190), "");
            overlayStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(18, top + 8, width - 20, 134), displayText, overlayStyle);
            GUI.Label(new Rect(18, top + 140, width - 20, 46), recordingStatus, overlayStyle);
        }
        catch (System.Exception exception)
        {
            // A UI failure must not repeatedly throw during gameplay.
            overlayFailed = true;
            ModHelper.Msg<LateGameOptimisation>($"Diagnostics overlay disabled after UI error: {exception.Message}");
        }
    }
}
