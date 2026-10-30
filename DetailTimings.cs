namespace LateGameOptimisation;

// Value type: storing a sample does not allocate a new object each frame.
internal readonly record struct DetailTimings(
    double CollisionMs, int CollisionCalls,
    double TargetMs, int TargetCalls,
    double WeaponMs, int WeaponCalls,
    double BloonMs, int BloonCalls);
