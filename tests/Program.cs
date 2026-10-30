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
