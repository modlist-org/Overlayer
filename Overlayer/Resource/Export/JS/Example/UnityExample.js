// Unity API examples (real Unity names, main thread only).
//
// Host types (construct, call statics, touch instance members directly):
//   GameObject, Transform, RectTransform, Component, Behaviour,
//   Vector2, Vector3, Vector4, Quaternion, Color, Color32,
//   Mathf, Time, Random, UnityObject (= UnityEngine.Object;
//   renamed because JS already has a global `Object`).
//
//   var go = GameObject.Find("Player");
//   go.transform.position = new Vector3(0, 5, 0);
//   go.transform.localEulerAngles = new Vector3(0, 90, 0);
//   UnityObject.Destroy(go, 1.0);
//   UnityObject.DontDestroyOnLoad(go);
//   var clone = UnityObject.Instantiate(prefab);
//   var t = Time.deltaTime;
//   var v = Mathf.Clamp(x, 0, 1);
//
// Unity glue (things the raw API can't do from JS):
//   Unity.FindObjectsOfType("Rigidbody") / Unity.FindObjectOfType("Rigidbody")
//   Unity.AddComponent(go, "Rigidbody") / Unity.GetComponent /
//   Unity.GetComponents / Unity.HasComponent  (string-named types)
//   Unity.IsValid(obj)  (dead Unity objects are NOT js null, check with this)
//   Unity.NextTick(fn)  (run fn on the main thread next frame)
//
// NOTE: scripts load off the main thread, so defer load-time Unity work:
//   Unity.NextTick(() => { /* touch the scene here */ });
// Patch callbacks already run on the game thread, use the API directly.
//
// All examples below are commented out. Uncomment one block at a time.

// 1. Find and move: lift the player by 2 units.
/*
Unity.NextTick(() => {
    const player = GameObject.Find("Player");
    if (!Unity.IsValid(player)) {
        return;
    }
    const p = player.transform.position;
    player.transform.position = new Vector3(p.x, p.y + 2, p.z);
});
*/

// 2. Spawn: create an empty object, parent it, drop a component on it.
/*
Unity.NextTick(() => {
    const root = GameObject.Find("SceneRoot");
    const spawned = new GameObject("SpawnedHelper");
    spawned.transform.SetParent(root.transform, false);
    spawned.transform.localPosition = new Vector3(0, 5, 0);
    const rb = Unity.AddComponent(spawned, "Rigidbody");
    rb.useGravity = false;
});
*/

// 3. Toggle: flip every "Torch" object's active state.
/*
Unity.NextTick(() => {
    const torches = GameObject.FindGameObjectsWithTag("Torch");
    for (const t of torches) {
        t.SetActive(!t.activeSelf);
    }
});
*/

// 4. Cleanup: destroy a component, then its object after 1s.
/*
Unity.NextTick(() => {
    const obj = GameObject.Find("TempEffect");
    if (!Unity.IsValid(obj)) {
        return;
    }
    const audio = Unity.GetComponent(obj, "AudioSource");
    if (audio !== null) {
        UnityObject.Destroy(audio);
    }
    UnityObject.Destroy(obj, 1.0);
});
*/

// 5. Inspect: log children and physics bodies.
/*
Unity.NextTick(() => {
    const root = GameObject.Find("SceneRoot");
    Log.Msg("children: " + root.transform.childCount);
    const bodies = Unity.FindObjectsOfType("Rigidbody");
    Log.Msg("rigidbodies: " + bodies.length);
});
*/

// 6. Per-frame style: read Time inside a patch postfix.
/*
const h = AddPatch("scrController::Update", {
    postfix: (args, result) => {
        const dt = Time.deltaTime;
        Store.Set("acc", (Store.Get("acc", 0)) + dt);
    }
});
*/

// 7. Repeat: run something every 0.5s without patching Update.
/*
Unity.NextTick(() => {
    const rep = Unity.Repeat(0.5, () => {
        const player = GameObject.Find("Player");
        if (Unity.IsValid(player)) {
            Log.Msg("player height: " + player.transform.position.y);
        }
    });
    // Unity.CancelTick(rep);
});
*/

// 8. Scene / screen / input: real Unity names.
/*
Unity.NextTick(() => {
    Log.Msg("scene: " + SceneManager.GetActiveScene().name);
    // SceneManager.LoadScene("Menu");
    Screen.SetResolution(1920, 1080, false);
    Application.targetFrameRate = 144;
    if (Input.GetKey(KeyCode.Space)) {
        Log.Msg("space held");
    }
    Cursor.visible = false;
});
*/
