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
    Check(lines[0] == "record_type,sample,elapsed_ms,frame_interval_ms,state,event,simulation_ms,simulation_calls,profiling_enabled,detailed_mask,collision_ms,collision_calls,target_ms,target_calls,weapon_ms,weapon_calls,bloon_ms,bloon_calls,travel_ms,travel_calls,tracking_ms,tracking_calls,age_ms,age_calls,maintenance_ms,maintenance_calls,collision_profile_enabled,collision_parent_ms,collision_parent_calls,bloon_loop_ms,bloon_loop_calls,bloon_hit_ms,bloon_hit_calls,bloon_loop_remainder_ms", "CSV schema");
    const string emptyCollision = ",0,0,0,0,0,0,0,0";
    Check(lines[1] == "frame,1,10.5,10.5,unknown,,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0" + emptyCollision && lines[2] == "frame,2,30.75,20.25,unknown,,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0" + emptyCollision, "Exact invariant frame rows");
    Check(lines.All(line => line.Split(',').Length == 34), "Frame and event rows have the same column count");
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
    recorder.CollisionProfileEnabled = true;
    recorder.Start();
    recorder.Mark("round_start");
    recorder.Add(20, "active_round", 12.5, 3, new DetailTimings(2.5, 4, 1.25, 5, 3.5, 6, 4.75, 7, 5.25, 8, 6.5, 9, 7.75, 10, 8.125, 11), new CollisionTimings(10.5, 2, 8.25, 6, 5.125, 12, 3.125));
    recorder.Mark("round_end");
    recorder.Stop("round_end_stop");
    string marked = recorder.Save(folder);
    var markedLines = File.ReadAllLines(marked);
    Check(markedLines.Contains("frame,1,20,20,active_round,,12.5,3,1,255,2.5,4,1.25,5,3.5,6,4.75,7,5.25,8,6.5,9,7.75,10,8.125,11,1,10.5,2,8.25,6,5.125,12,3.125"), "State and all detailed/collision profiling fields");
    Check(markedLines.All(line => line.Split(',').Length == 34), "Marked CSV has consistent columns");
    Check(markedLines.Skip(1).All(line => line.Split(',')[26] == "1"), "Enabled flag aligned in frame AND event rows");
    Check(markedLines.Any(line => line.Contains(",round_end_stop,")), "Explicit stop reason");
    recorder.CollisionProfileEnabled = false;
    recorder.Start();
    recorder.Add(10);
    recorder.Stop();
    Check(File.ReadAllLines(recorder.Save(folder))[1].EndsWith(",255,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0" + emptyCollision), "Next recording replaces prior detail and collision values");
}
finally { Directory.Delete(folder, recursive: true); }
Console.WriteLine("All benchmark checks passed.");

// Synthetic clock values exercise nesting and subtraction without game binaries.
var collision = new CollisionTimingAccumulator();
Check(collision.BeginLoop(10) == 0 && collision.BeginHit(11) == 0, "Reject orphan scopes");
long parent = collision.BeginParent(100);
Check(collision.BeginHit(105) == 0, "Ignore hits outside the selected loop");
long loop = collision.BeginLoop(110);
long hit = collision.BeginHit(120);
long nestedHit = collision.BeginHit(125);
collision.EndHit(nestedHit, 130);
// A recursive loop during a hit must not double-count either category.
long nestedLoop = collision.BeginLoop(135);
long deepHit = collision.BeginHit(136);
collision.EndHit(deepHit, 138);
collision.EndLoop(nestedLoop, 140);
collision.EndHit(hit, 150);
hit = collision.BeginHit(160);
collision.EndHit(hit, 170);
collision.EndLoop(loop, 190);
long recursiveParent = collision.BeginParent(191);
collision.EndParent(recursiveParent, 195);
collision.EndParent(parent, 200);
Check(collision.Drain(0.5) == new CollisionTimings(50, 1, 40, 1, 20, 2, 20), "Inclusive outer scopes; nested hits not counted twice");
Check(collision.Drain(1) == default, "Draining clears completed counters");

parent = collision.BeginParent(210);
loop = collision.BeginLoop(220);
hit = collision.BeginHit(230);
collision.EndHit(hit, 240);
Check(collision.Drain(1) == default, "Do not publish child totals before their loop completes");
collision.EndLoop(loop, 260);
Check(collision.Drain(1) == new CollisionTimings(0, 0, 40, 1, 10, 1, 30), "Drain preserves live scopes and matching loop/hit totals");
collision.EndParent(parent, 270);
Check(collision.Drain(1) == new CollisionTimings(60, 1, 0, 0, 0, 0, 0), "Parent can complete after an intervening drain");

parent = collision.BeginParent(300);
loop = collision.BeginLoop(310);
try
{
    hit = collision.BeginHit(320);
    try { throw new InvalidOperationException("simulated original exception"); }
    finally { collision.EndHit(hit, 330); }
}
catch (InvalidOperationException) { }
finally { collision.EndLoop(loop, 340); collision.EndParent(parent, 350); }
Check(collision.Drain(1) == new CollisionTimings(50, 1, 30, 1, 10, 1, 20), "Finalizer-style unwinding balances scopes");
Check(collision.BeginHit(360) == 0 && collision.BeginLoop(361) == 0, "No leaked depth after unwind");
Console.WriteLine("All collision timing checks passed.");
