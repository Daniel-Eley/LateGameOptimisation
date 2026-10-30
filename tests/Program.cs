using LateGameOptimisation;

static void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
}

var window = new FrameTimeWindow();
Check(window.Mean == 0 && window.FramesPerSecond == 0, "Empty window");
foreach (double invalid in new[] { 0, -1, double.NaN, double.PositiveInfinity })
    Check(!window.Add(invalid) && window.Count == 0, "Ignore invalid samples");
Check(!window.Add(10) && !window.Add(30), "Do not report too early");
Check(window.Count == 2 && window.Mean == 20 && window.Maximum == 30 && window.FramesPerSecond == 50,
    "Unequal intervals must use total elapsed time");
Check(window.Add(9960) && window.Total == 10000 && window.Maximum == 9960, "Keep stalls and report at boundary");
window.Reset();
Check(window.Count == 0 && window.Total == 0 && window.Maximum == 0 && window.Mean == 0, "Reset statistics");
Check(window.Add(12000), "Single long stall completes window");
Console.WriteLine("All diagnostics checks passed.");

var recorder = new BenchmarkRecorder();
Check(recorder.Start() && !recorder.Start(), "Reject duplicate start");
Check(!recorder.Add(double.NaN) && recorder.Count == 0, "Reject invalid recording interval");
recorder.Add(10.5);
recorder.Add(20.25);
recorder.Stop();
Check(recorder.HasPendingSave && !recorder.Start(), "Protect unsaved run");
string folder = Path.Combine(Path.GetTempPath(), "lgo-tests-" + Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(folder);
    string blocked = Path.Combine(folder, "not-a-directory");
    File.WriteAllText(blocked, "test");
    try { recorder.Save(blocked); throw new Exception("Expected save failure"); }
    catch (IOException) { Check(recorder.HasPendingSave, "Retain failed save"); }
    var culture = System.Globalization.CultureInfo.CurrentCulture;
    string first;
    try
    {
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
        first = recorder.Save(folder);
    }
    finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
    var lines = File.ReadAllLines(first);
    Check(lines[0] == "record_type,sample,elapsed_ms,frame_interval_ms,state,event,simulation_ms,simulation_calls,profiling_enabled,detailed_mask,collision_ms,collision_calls,target_ms,target_calls,weapon_ms,weapon_calls,bloon_ms,bloon_calls,travel_ms,travel_calls,tracking_ms,tracking_calls,age_ms,age_calls,maintenance_ms,maintenance_calls", "CSV schema");
    Check(lines[1] == "frame,1,10.5,10.5,unknown,,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0" && lines[2] == "frame,2,30.75,20.25,unknown,,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0", "Exact invariant frame rows");
    Check(lines.All(line => line.Split(',').Length == 26), "Frame and event rows have the same column count");
    Check(lines.Any(line => line.Contains(",recording_start,")) && lines.Any(line => line.Contains(",manual_stop,")), "Start and stop events saved");
    using (File.Open(first, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    Check(!recorder.HasPendingSave && recorder.Start(), "Save closes file and allows next run");
    Check(recorder.Add(BenchmarkRecorder.DurationLimitMs), "Auto-stop at duration cap");
    string second = recorder.Save(folder);
    Check(first != second && File.Exists(first), "Unique files preserve previous recording");
    recorder.Start();
    for (int i = 0; i < BenchmarkRecorder.Capacity; i++) recorder.Add(0.01);
    Check(!recorder.IsRecording && recorder.Count == BenchmarkRecorder.Capacity, "Bounded sample buffer");
    Check(!recorder.Add(10), "Stopped recorder cannot overflow");
    recorder.Save(folder);
    recorder.ProfileEnabled = true;
    recorder.DetailedMask = 255;
    recorder.Start();
    recorder.Mark("round_start");
    recorder.Add(20, "active_round", 12.5, 3, new DetailTimings(2.5, 4, 1.25, 5, 3.5, 6, 4.75, 7, 5.25, 8, 6.5, 9, 7.75, 10, 8.125, 11));
    recorder.Mark("round_end");
    recorder.Stop("round_end_stop");
    string marked = recorder.Save(folder);
    var markedLines = File.ReadAllLines(marked);
    Check(markedLines.Contains("frame,1,20,20,active_round,,12.5,3,1,255,2.5,4,1.25,5,3.5,6,4.75,7,5.25,8,6.5,9,7.75,10,8.125,11"), "State and all detailed profiling fields");
    Check(markedLines.All(line => line.Split(',').Length == 26), "Marked CSV has consistent columns");
    Check(markedLines.Any(line => line.Contains(",round_end_stop,")), "Explicit stop reason");
    recorder.Start();
    recorder.Add(10);
    recorder.Stop();
    Check(File.ReadAllLines(recorder.Save(folder))[1].EndsWith(",255,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0"), "Next recording replaces prior detail sample values");
}
finally { Directory.Delete(folder, recursive: true); }
Console.WriteLine("All benchmark checks passed.");
