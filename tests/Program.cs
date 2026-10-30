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
    Check(File.ReadAllLines(first).SequenceEqual(new[] { "sample,elapsed_ms,frame_interval_ms", "1,10.5,10.5", "2,30.75,20.25" }), "Exact invariant CSV");
    using (File.Open(first, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    Check(!recorder.HasPendingSave && recorder.Start(), "Save closes file and allows next run");
    Check(recorder.Add(BenchmarkRecorder.DurationLimitMs), "Auto-stop at duration cap");
    string second = recorder.Save(folder);
    Check(first != second && File.Exists(first), "Unique files preserve previous recording");
    recorder.Start();
    for (int i = 0; i < BenchmarkRecorder.Capacity; i++) recorder.Add(0.01);
    Check(!recorder.IsRecording && recorder.Count == BenchmarkRecorder.Capacity, "Bounded sample buffer");
    Check(!recorder.Add(10), "Stopped recorder cannot overflow");
}
finally { Directory.Delete(folder, recursive: true); }
Console.WriteLine("All benchmark checks passed.");
