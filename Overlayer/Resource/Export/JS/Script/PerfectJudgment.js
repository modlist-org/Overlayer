// Force every timing judgment to XPerfect.
//
// Patches the two game methods that convert a hit timing error into a
// HitMargin (scrMisc::GetHitMarginInDeg / GetHitMarginInSec) and rewrites
// the result. Miss/overload/multipress paths are untouched (they never
// reach these methods), so failing a tile still fails.
//
// Change "XPerfect" to EarlyPerfect / PerfectMinus / PerfectPlus / LatePerfect
// for signed judgments.
//
// NOTE: this file is ACTIVE on install. Delete it or comment the lines out
// to play normally.

const PerfectX = AddPatch("scrMisc::GetHitMarginInDeg(Difficulty, float, float, bool, float, float, double)", {
    postfix: (args, result) => "XPerfect"
});

const PerfectS = AddPatch("scrMisc::GetHitMarginInSec(Difficulty, double, float, float, double)", {
    postfix: (args, result) => "XPerfect"
});
