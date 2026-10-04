// JS patching example (Mono-only).
//
// AddPatch(target, options) patches a game/mod method with JS callbacks.
// RemovePatch(handle) undoes it. Patches unpatch automatically on script
// reload, and JS exceptions never crash the game (logged once, original continues).
//
//   target:  "Type::Method" (auto-pick when unique)
//            "Type::Method(Arg1, Arg2)" (explicit overload, C# keywords ok)
//   options: { prefix, postfix } (at least one)
//
//   prefix(args): args is a mutable array; mutating it changes the call.
//     Return false            -> skip the original (default result).
//     Return { result: x }    -> skip the original with result x.
//     Anything else (or nothing) -> run the original.
//     Expanded form prefix(a, b) matches the 2-arg overload automatically.
//
//   postfix(args, result): return non-undefined to replace the result.
//     Expanded form postfix(a, b, result) matches the 2-arg overload.
//
// All examples below are commented out. Uncomment one block at a time to try.
// Watch the Overlayer log for [JSPatch] errors (bad target, ambiguous overload).

// 1. Watch calls: log arguments, change nothing.
/*
const h1 = AddPatch("scrController::Update", {
    prefix: (args) => {
        Store.Set("updateCount", (Store.Get("updateCount", 0) | 0) + 1);
    }
});
*/

// 2. Skip the original: force a method to do nothing.
/*
const h2 = AddPatch("scrController::FailAction", {
    prefix: (args) => false
});
// RemovePatch(h2);
*/

// 3. Skip with a forged result: method returns 99.9 without running.
/*
const h3 = AddPatch("scrController::GetSongBpm", {
    prefix: (args) => ({ result: 99.9 })
});
*/

// 4. Mutate arguments: double the first numeric argument.
/*
const h4 = AddPatch("SomeType::SomeMethod", {
    prefix: (args) => {
        if (typeof args[0] === "number") {
            args[0] = args[0] * 2;
        }
    }
});
*/

// 5. Rewrite the result: clamp a float result into 0..1.
/*
const h5 = AddPatch("SomeType::SomeFloatMethod", {
    postfix: (args, result) => Math.max(0, Math.min(1, result))
});
*/

// 6. Explicit overload: pick (int) out of Method() / Method(int).
/*
const h6 = AddPatch("SomeType::Method(int)", {
    postfix: (args, result) => result + 1
});
*/
