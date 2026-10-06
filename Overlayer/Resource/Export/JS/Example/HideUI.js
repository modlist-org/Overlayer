// Hide UI: build/version text, judgement popups, timing difficulty selector.
// Copy into JS/Script to use. Settings live in its own "Hide UI" menu tab
// and persist through Prefs (JS/Script/Prefs.json).
//
// Ref: Quartz UiHider / HideJudgements modules.

const KEY = "HideUI.";
const opts = {
    buildText: Prefs.Get(KEY + "buildText", true),
    judgements: Prefs.Get(KEY + "judgements", true),
    difficulty: Prefs.Get(KEY + "difficulty", true),
};

// Same list/labels as Quartz HideJudgements, keyed by the game's HitMargin names.
const JUDGEMENTS = [
    ["TooEarly", "Too Early"],
    ["VeryEarly", "Very Early"],
    ["EarlyPerfect", "Early Perfect"],
    ["PerfectMinus", "- Perfect"],
    ["XPerfect", "X Perfect"],
    ["PerfectPlus", "+ Perfect"],
    ["LatePerfect", "Late Perfect"],
    ["VeryLate", "Very Late"],
    ["TooLate", "Too Late"],
    ["Multipress", "Multipress"],
    ["FailMiss", "Miss"],
    ["FailOverload", "Overload (No Fail)"],
    ["Auto", "Auto"],
    ["OverPress", "Overload (Fail)"],
];
const hiddenJudgements = new Set(
    JUDGEMENTS.map(([name]) => name).filter(name => Prefs.Get(KEY + "j." + name, name === "XPerfect"))
);

// Objects we hid, per option, so turning a toggle off restores only those.
const hidden = { buildText: new Set(), difficulty: new Set() };

AddTab("Hide UI", tab => {
    tab.Header("Hide UI");
    const toggle = (label, key) => tab.Toggle(label, opts[key], v => {
        opts[key] = v;
        Prefs.Set(KEY + key, v);
        if (!v && hidden[key]) restore(key);
    });
    toggle("Hide build text", "buildText");
    toggle("Hide judgements", "judgements");
    const card = tab.Card("Judgements to hide");
    for (const [name, label] of JUDGEMENTS) {
        card.Toggle(label, hiddenJudgements.has(name), v => {
            if (v) hiddenJudgements.add(name);
            else hiddenJudgements.delete(name);
            Prefs.Set(KEY + "j." + name, v);
        });
    }
    toggle("Hide timing difficulty selector", "difficulty");
}, { icon: "Gear128" });

// Logs once per hook so the log shows which patches actually fire.
const seen = new Set();
function hooked(name) {
    if (seen.has(name)) return;
    seen.add(name);
    Log.Msg("[HideUI] " + name + " hooked");
}

function valid(obj) {
    return obj !== null && obj !== undefined && Unity.IsValid(obj);
}

// Hide (or restore) a GameObject, remembering what we hid.
function setHidden(key, go, hide) {
    if (!valid(go)) return;
    if (hide) {
        if (go.activeSelf) {
            go.SetActive(false);
            hidden[key].add(go);
        }
    } else if (hidden[key].delete(go)) {
        go.SetActive(true);
    }
}

function restore(key) {
    for (const go of hidden[key]) {
        if (valid(go)) go.SetActive(true);
    }
    hidden[key].clear();
}

// Judgements: kill the popup right after it shows (same as Quartz HideJudgements).
AddPatch("scrHitTextMesh::Show", {
    postfix: (args, result, __instance) => {
        hooked("judgements");
        if (!opts.judgements || !valid(__instance)) return;
        const margin = Clr.TryGet(__instance, "hitMargin");
        if (margin === null) return;
        // JUDGEMENTS order matches HitMargin values 0..13, in case the enum arrives as a number.
        const name = typeof margin === "number" ? JUDGEMENTS[margin]?.[0] : margin.ToString();
        if (!hiddenJudgements.has(name)) return;
        Clr.TrySet(__instance, "dead", true);
        __instance.gameObject.SetActive(false);
    }
});

// Build text: the "v3.x (platform)" / "r<commit> (<date>)" label.
function hideVersionText(__instance) {
    hooked("build text");
    if (!valid(__instance)) return;
    const text = Clr.TryGet(__instance, "text");
    if (valid(text)) setHidden("buildText", text.gameObject, opts.buildText);
}
AddPatch("scrVersionText::Awake", { postfix: (args, result, __instance) => hideVersionText(__instance) });
AddPatch("scrVersionText::Init", { postfix: (args, result, __instance) => hideVersionText(__instance) });
AddPatch("scrVersionText::UpdatePage", { postfix: (args, result, __instance) => hideVersionText(__instance) });

// Beta branch label ("Beta Build"), only exists on Steam beta branches.
AddPatch("scrEnableIfBeta::Awake", {
    postfix: (args, result, __instance) => {
        if (valid(__instance)) setHidden("buildText", __instance.gameObject, opts.buildText);
    }
});

// Timing difficulty selector (Lenient / Normal / Strict).
function goOf(component) {
    return valid(component) ? component.gameObject : null;
}
AddPatch("scrUIController::Update", {
    postfix: (args, result, __instance) => {
        hooked("difficulty (game)");
        if (!opts.difficulty || !valid(__instance)) return;
        setHidden("difficulty", goOf(Clr.TryGet(__instance, "difficultyContainer")), true);
        setHidden("difficulty", goOf(Clr.TryGet(__instance, "difficultyFadeContainer")), true);
    }
});
AddPatch("scnEditor::Update", {
    postfix: (args, result, __instance) => {
        hooked("difficulty (editor)");
        if (!opts.difficulty || !valid(__instance)) return;
        setHidden("difficulty", goOf(Clr.TryGet(__instance, "editorDifficultySelector")), true);
    }
});
