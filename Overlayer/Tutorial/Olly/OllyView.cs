using Overlayer.Core;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.Tutorial.Olly;

// Olly's portrait made of plain Images, one per part, each placed by hand at its pixel
// anchor. Moving a few of them a little (eyes and hair toward what she's looking at, eyelids
// for a blink) is enough to make her feel alive. Her expressions are fixed until the line
// changes; the mouth doesn't flap along with the typing.
internal sealed class OllyView {
    public const float Size = 240f;
    private const string Prefix = "Image.Olly.";

    public readonly RectTransform Rect;
    public Face Face = Face.Normal;
    /// <summary>Where she looks in screen pixels; null looks straight ahead.</summary>
    public Vector2? LookTarget;
    /// <summary>True while her head is being patted.</summary>
    public bool Patted => patting;

    private readonly Image bg, hairBg, body, patArea, mouth, nose, eyeRight, eyeLeft, lidDown, lidBg, lidUp, shineLeft, shineRight, shut, brow, hair;
    private readonly Image[] marks = new Image[3];
    private readonly Image[] fronts = new Image[4];

    private float blinkTimer, blinkInterval = 2.5f;
    private Vector2 eyeOffset, eyeVelocity, hairOffset, hairVelocity;
    private bool patting;
    private float patTime;
    private float flusteredUntil;
    /// <summary>How many times she's been patted.</summary>
    public int Pats { get; private set; }

    public OllyView(Transform parent) {
        Rect = UiStyle.NewRect("Olly", parent);
        Rect.sizeDelta = new Vector2(Size, Size);
        bg = Part("Bg", "BG");
        hairBg = Part("HairBg", "HAIRBG");
        body = Part("Base", "BASE");
        // Only the head takes clicks (patting); the rest of the portrait is transparent there
        // and must not swallow clicks meant for the UI behind it.
        body.raycastTarget = false;
        mouth = Part("Mouth", null);
        nose = Part("Nose", "NOSE");
        eyeRight = Part("EyeRight", null);
        eyeLeft = Part("EyeLeft", null);
        lidDown = Part("LidDown", "ELD");
        for (int i = 0; i < marks.Length; i++) {
            marks[i] = Part("Mark" + i, "FX_" + (Mark)(1 << i));
        }
        lidBg = Part("LidBg", "ELBG");
        lidUp = Part("LidUp", "ELU");
        shineLeft = Part("ShineLeft", "EHL");
        shineRight = Part("ShineRight", "EHR");
        shut = Part("Shut", null);
        brow = Part("Brow", null);
        for (int i = 0; i < fronts.Length; i++) {
            fronts[i] = Part("Front" + i, "FXF_" + (MarkFront)(1 << i));
        }
        hair = Part("Hair", "HAIR");
        patArea = Part("PatArea", null);
        patArea.color = Color.clear;
        patArea.raycastTarget = true;
        patArea.rectTransform.sizeDelta = OllyAnchors.Head.size;
        Put(patArea, OllyAnchors.Head.position);
        Put(bg, OllyAnchors.Bg);
        Put(body, Vector2.zero);
        Tick(0f);
    }

    private Image Part(string name, string file) {
        var r = UiStyle.NewRect(name, Rect);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        var image = r.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        if (file != null) {
            SetSprite(image, file);
        }
        return image;
    }

    private static void SetSprite(Image image, string file) {
        var sprite = MainCore.Spr.Get(Prefix + file + ".png");
        if (image.sprite == sprite) {
            return;
        }
        image.sprite = sprite;
        if (sprite != null) {
            image.rectTransform.sizeDelta = sprite.rect.size;
        }
    }

    private static void Put(Image image, Vector2 px) => image.rectTransform.anchoredPosition = new Vector2(px.x, -px.y);

    private static void Show(Image image, bool on) {
        if (image.enabled != on) {
            image.enabled = on;
        }
    }

    // The pointer in portrait pixels (top left origin, y down).
    private Vector2 Pointer() => ToPortrait(Input.mousePosition);

    // A screen point in portrait pixels (top left origin, y down).
    private Vector2 ToPortrait(Vector2 screen) {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, null, out var local);
        var r = Rect.rect;
        return new Vector2(local.x - r.xMin, r.yMax - local.y);
    }

    // Toward a point: up to max pixels, eased in over the first 100.
    private static Vector2 Lean(Vector2 from, Vector2 to, float max) {
        Vector2 d = to - from;
        float t = Mathf.Clamp01(d.magnitude / 100f);
        return d.normalized * (Mathf.Sin(t * Mathf.PI * 0.5f) * max);
    }

    public void Tick(float dt) {
        Vector2 pointer = Pointer();
        // Flustered for a moment after a pat.
        var face = !patting && Time.unscaledTime < flusteredUntil ? Face.Flustered : Face;
        var eye = face.Eye;
        var shutState = face.Shut;
        var mouthState = face.Mouth;
        var mark = face.Mark;

        // A pat: press on her head and hold.
        if (Input.GetMouseButtonDown(0) && OllyAnchors.Head.Contains(pointer) && Rect.gameObject.activeInHierarchy) {
            patting = true;
            patTime = 0f;
        } else if (patting && !Input.GetMouseButton(0)) {
            patting = false;
            blinkTimer = 0f;
            Pats++;
            flusteredUntil = Time.unscaledTime + 1.6f;
        }
        if (patting) {
            patTime += dt;
            mouthState = Mouth.OpenMicro;
            mark |= Mark.Blush;
            if (patTime > 0.3f) {
                shutState = EyeShut.Down;
            }
        }

        // Blink now and then: lids slide in, eyes shut for a moment.
        blinkTimer += dt;
        float blinking = patting ? -1f : blinkTimer - blinkInterval;
        if (blinking > 0.3f && shutState == EyeShut.None) {
            shutState = EyeShut.Down;
        }
        if (blinking > 0.4f) {
            blinkTimer = 0f;
            blinkInterval = UnityEngine.Random.Range(2.5f, 7f);
        }

        // Eyes and hair lean toward what she's looking at (a highlighted part, or the
        // player's mouse while they do their turn); otherwise straight ahead.
        Vector2? focus = patting ? null : LookTarget;
        var eyes = OllyAnchors.Eyes[0];
        Vector2 eyeMid = (eyes.Left + eyes.Right) * 0.5f + new Vector2(14f, 14f);
        Vector2 eyeTarget = Vector2.zero;
        Vector2 hairTarget = Vector2.zero;
        if (focus.HasValue) {
            Vector2 portraitFocus = ToPortrait(focus.Value);
            eyeTarget = Lean(eyeMid, portraitFocus, 3.6f);
            eyeTarget.x *= eyeTarget.x < 0f ? 2.6f : 0.8f;
            eyeTarget.y *= eyeTarget.y < 0f ? 0.8f : 2f;
            hairTarget = Lean(OllyAnchors.Hair + new Vector2(112f, 106f), portraitFocus, 1.7f);
        }
        eyeOffset = Vector2.SmoothDamp(eyeOffset, eyeTarget, ref eyeVelocity, 0.2f, Mathf.Infinity, Mathf.Max(dt, 0.0001f));
        hairOffset = Vector2.SmoothDamp(hairOffset, hairTarget, ref hairVelocity, 0.2f, Mathf.Infinity, Mathf.Max(dt, 0.0001f));
        // Breathing.
        float breath = Mathf.Sin(Time.unscaledTime * 1.7f);
        Vector2 sway = new(0f, breath * 0.6f);

        Vector2 faceOffset = eyeOffset * 0.5f;
        Put(hairBg, OllyAnchors.HairBg + hairOffset + sway);
        Put(hair, OllyAnchors.Hair + hairOffset + sway);

        Show(mouth, mouthState != Mouth.None);
        if (mouthState != Mouth.None) {
            SetSprite(mouth, "M_" + mouthState);
            Put(mouth, OllyAnchors.Mouths[(int)mouthState - 1] + faceOffset);
        }
        Put(nose, OllyAnchors.Nose + faceOffset);

        bool open = shutState == EyeShut.None;
        bool eyesOn = open && eye != Eye.None;
        Show(eyeLeft, eyesOn);
        Show(eyeRight, eyesOn);
        if (eyesOn) {
            var at = OllyAnchors.Eyes[(int)eye - 1];
            SetSprite(eyeLeft, "EL_" + eye);
            SetSprite(eyeRight, "ER_" + eye);
            Put(eyeLeft, at.Left + eyeOffset);
            Put(eyeRight, at.Right + eyeOffset);
        }

        Vector2 lidOffset = eyeOffset * 0.4f;
        Show(lidDown, open);
        Show(lidBg, open);
        Show(lidUp, open);
        Show(shineLeft, open);
        Show(shineRight, open);
        if (open) {
            float closing = Mathf.Max(0f, blinking);
            Put(lidDown, OllyAnchors.LidDown + lidOffset + new Vector2(0f, -closing * 6f));
            Put(lidBg, OllyAnchors.LidBg + lidOffset + new Vector2(0f, closing * 22f));
            Put(lidUp, OllyAnchors.LidUp + lidOffset + new Vector2(0f, closing * 22f));
            bool small = eye == Eye.Small;
            Put(shineLeft, OllyAnchors.ShineLeft + eyeOffset + (small ? new Vector2(4f, 6f) : Vector2.zero));
            Put(shineRight, OllyAnchors.ShineRight + eyeOffset + (small ? new Vector2(0f, 5f) : Vector2.zero));
        }
        Show(shut, !open);
        if (!open) {
            SetSprite(shut, "ES_" + shutState);
            Put(shut, OllyAnchors.Shut[(int)shutState - 1] + eyeOffset * 0.5f);
        }

        for (int i = 0; i < marks.Length; i++) {
            var m = (Mark)(1 << i);
            // Tears only fit open, normal eyes.
            bool on = (mark & m) != 0 && (m != Mark.Tear || (open && eye == Eye.Normal));
            Show(marks[i], on);
            if (on) {
                Put(marks[i], OllyAnchors.Marks[i] + (m == Mark.Tear ? lidOffset : faceOffset));
            }
        }

        Show(brow, face.Brow != Brow.None);
        if (face.Brow != Brow.None) {
            SetSprite(brow, "B_" + face.Brow);
            Put(brow, OllyAnchors.Brows[(int)face.Brow - 1] + eyeOffset * 0.23f);
        }

        for (int i = 0; i < fronts.Length; i++) {
            var f = (MarkFront)(1 << i);
            bool on = (face.Front & f) != 0;
            Show(fronts[i], on);
            if (on) {
                Vector2 offset = f switch {
                    MarkFront.Tremble or MarkFront.Tendon => hairOffset,
                    MarkFront.BlushBig => faceOffset,
                    _ => Vector2.zero,
                };
                Put(fronts[i], OllyAnchors.Fronts[i] + offset);
            }
        }
    }
}
