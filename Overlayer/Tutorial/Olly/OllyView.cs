using Overlayer.Core;
using Overlayer.Resource;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.Tutorial.Olly;

// Olly's portrait made of plain Images, one per part, each placed by hand at its pixel
// anchor. She looks through the calibration table; everything follows together a little
// except the brow and the back hair, and the body stays put. Her expressions are fixed
// until the line changes; the mouth doesn't flap along with the typing.
internal sealed class OllyView {
    public const float Size = 240f;
    private const string Prefix = "Image.Olly.";
    // Gaze circle around her: past this radius she looks fully that way.
    private const float GazeRadius = 35f;

    public readonly RectTransform Rect;
    public Face Face = Face.Normal;
    /// <summary>Where she looks in screen pixels; null looks straight ahead.</summary>
    public Vector2? LookTarget;
    /// <summary>True while her head is being patted.</summary>
    public bool Patted => patting;
    public bool IsHead(Vector2 screen) => OllyAnchors.Head.Contains(ToPortrait(screen));

    public void ForceFace(Face face) {
        Face = face;
        patting = false;
        flusteredUntil = 0f;
        blinkTimer = 0f;
        LookTarget = null;
        eyeLOffset = eyeROffset = eyeLVelocity = eyeRVelocity = headOffset = Vector2.zero;
    }

    private readonly RectTransform mask;
    private readonly Image bg, hairBg, body, patArea, mouth, nose, eyeRight, eyeLeft, lidDBg, lidDown, lidUBg, lidUp, shineLeft, shineRight, shut, brow, hair;
    private readonly Image[] marks = new Image[3];
    private readonly Image[] fronts = new Image[4];

    private float blinkTimer, blinkInterval = 2.5f;
    private Vector2 eyeLOffset, eyeLVelocity, eyeROffset, eyeRVelocity, headOffset;
    private bool patting;
    private float patTime;
    private float flusteredUntil;
    /// <summary>How many times she's been patted.</summary>
    public int Pats { get; private set; }

    public OllyView(Transform parent) {
        Rect = UiStyle.NewRect("Olly", parent);
        Rect.sizeDelta = new Vector2(Size, Size);

        var frameBg = Stretch("FrameBg", Rect).gameObject.AddComponent<Image>();
        frameBg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        frameBg.type = Image.Type.Sliced;
        frameBg.color = O5Kit.Core.O5Palette.WithAlpha(Overlayer.UI.UIColors.SurfaceOpaque, 0.97f);
        frameBg.raycastTarget = true;

        mask = Stretch("PortraitMask", Rect);
        var maskImage = mask.gameObject.AddComponent<Image>();
        maskImage.sprite = frameBg.sprite;
        maskImage.type = Image.Type.Sliced;
        maskImage.raycastTarget = false;
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;

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
        // Lower white under the lash: hides iris overflow when she looks down.
        lidDBg = Part("LidDBg", "ELDBG");
        lidDown = Part("LidDown", "ELD");
        for (int i = 0; i < marks.Length; i++) {
            marks[i] = Part("Mark" + i, "FX_" + (Mark)(1 << i));
        }
        lidUBg = Part("LidUBg", "ELUBG");
        lidUp = Part("LidUp", "ELU");
        shineLeft = Part("ShineLeft", "EHL");
        shineRight = Part("ShineRight", "EHR");
        shut = Part("Shut", null);
        brow = Part("Brow", null);
        for (int i = 0; i < fronts.Length; i++) {
            fronts[i] = Part("Front" + i, "FXF_" + (MarkFront)(1 << i));
        }
        hair = Part("Hair", "HAIR");

        var border = Stretch("FrameBorder", Rect).gameObject.AddComponent<Image>();
        border.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256O32P1024);
        border.type = Image.Type.Sliced;
        border.color = O5Kit.Core.O5Palette.WithAlpha(Overlayer.UI.UIColors.ObjectActive, 0.75f);
        border.raycastTarget = false;

        patArea = Part("PatArea", null, Rect);
        patArea.color = Color.clear;
        patArea.raycastTarget = true;
        patArea.rectTransform.sizeDelta = OllyAnchors.Head.size;
        Put(patArea, OllyAnchors.Head.position);
        Put(bg, OllyAnchors.Bg);
        Put(body, Vector2.zero);
        Tick(0f);
    }

    private static RectTransform Stretch(string name, Transform parent) {
        var r = UiStyle.NewRect(name, parent);
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return r;
    }

    private Image Part(string name, string file, Transform host = null) {
        var r = UiStyle.NewRect(name, host ?? mask);
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

    // Portrait center in screen pixels.
    public Vector2 ScreenCenter() {
        var r = Rect.rect;
        Vector3 world = Rect.TransformPoint(new Vector3(r.center.x, r.center.y, 0f));
        return RectTransformUtility.WorldToScreenPoint(null, world);
    }

    public void Tick(float dt, bool interactive = true) {
        Vector2 pointer = Pointer();
        // Flustered for a moment after a pat.
        var face = !patting && Time.unscaledTime < flusteredUntil ? Face.Flustered : Face;
        var eye = face.Eye;
        var shutState = face.Shut;
        var mouthState = face.Mouth;
        var mark = face.Mark;

        // A pat: press on her head and hold. Leaving the head or releasing ends it,
        // like Legacy; holding still closes her eyes after a moment.
        if (!interactive) {
            patting = false;
        } else if (Input.GetMouseButtonDown(0) && OllyAnchors.Head.Contains(pointer) && Rect.gameObject.activeInHierarchy) {
            patting = true;
            patTime = 0f;
        } else if (patting && (!Input.GetMouseButton(0) || !OllyAnchors.Head.Contains(pointer))) {
            patting = false;
            blinkTimer = 0f;
            Pats++;
            flusteredUntil = Time.unscaledTime + 1.6f;
        }
        Vector2 headTarget = Vector2.zero;
        if (patting) {
            patTime += dt;
            mouthState = Mouth.OpenMicro;
            mark |= Mark.Blush;
            if (patTime > 0.3f) {
                shutState = EyeShut.Down;
            }
            // Follow the cursor subtly inside the silhouette margin (HAIRBG/BASE stay put).
            Vector2 delta = pointer - OllyAnchors.Head.center;
            headTarget = new Vector2(
                Mathf.Clamp(delta.x * 0.04f, -2.5f, 2.5f),
                Mathf.Clamp(delta.y * 0.04f, -1.2f, 2f));
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

        // She looks through the calibration table at what she's looking at (a highlighted
        // part, or the player's mouse while they do their turn); otherwise straight ahead.
        // Closed happy eyes don't follow anything.
        Vector2? focus = (patting || face.Shut == EyeShut.Up) ? null : LookTarget;
        Vector2 eyeLTarget = Vector2.zero, eyeRTarget = Vector2.zero;
        if (focus.HasValue) {
            // Radial gaze around her: past a small circle, look fully that way.
            Vector2 c = ScreenCenter();
            Vector2 v = focus.Value - c;
            float d = v.magnitude;
            if (d > 0.001f) {
                float maxComp = Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y));
                Vector2 rim = v / maxComp;
                float rx = OllyCalibration.Fx[2] - OllyCalibration.Fx[1];
                float ry = OllyCalibration.Fy[0] - OllyCalibration.Fy[1];
                Vector2 t = new((0.5f + rim.x * rx) * Screen.width, (0.5f + rim.y * ry) * Screen.height);
                float response = Mathf.Min(1f, d / GazeRadius);
                var sample = OllyCalibration.Sample(t);
                eyeLTarget = sample.eyeL * response;
                eyeRTarget = sample.eyeR * response;
                headTarget = sample.head * response;
            }
        }
        float safeDt = Mathf.Max(dt, 0.0001f);
        eyeLOffset = Vector2.SmoothDamp(eyeLOffset, eyeLTarget, ref eyeLVelocity, 0.12f, Mathf.Infinity, safeDt);
        eyeROffset = Vector2.SmoothDamp(eyeROffset, eyeRTarget, ref eyeRVelocity, 0.12f, Mathf.Infinity, safeDt);
        headOffset = Vector2.Lerp(headOffset, headTarget, 1f - Mathf.Exp(-safeDt * (patting ? 10f : 8f)));
        // Breathing.
        float breath = Mathf.Sin(Time.unscaledTime * 1.7f);
        Vector2 sway = new(0f, breath * 0.6f);

        // Everything follows together a little, except the brow and the back hair.
        // The body stays put.
        Vector2 eyeAvg = (eyeLOffset + eyeROffset) * 0.5f;
        Vector2 faceOffset = headOffset + eyeAvg * 0.5f;
        Vector2 lidOffset = headOffset + eyeAvg * 0.4f;
        Put(body, Vector2.zero);
        Put(hairBg, OllyAnchors.HairBg);
        Put(hair, OllyAnchors.Hair + headOffset + sway);

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
            Put(eyeLeft, at.Left + headOffset + eyeLOffset);
            Put(eyeRight, at.Right + headOffset + eyeROffset);
        }

        Show(lidDown, open);
        Show(lidDBg, open);
        Show(lidUBg, open);
        Show(lidUp, open);
        Show(shineLeft, open);
        Show(shineRight, open);
        if (open) {
            float closing = Mathf.Max(0f, blinking);
            Put(lidDown, OllyAnchors.LidDown + lidOffset + new Vector2(0f, -closing * 6f));
            Put(lidDBg, OllyAnchors.LidDBg + lidOffset);
            Put(lidUBg, OllyAnchors.LidUBg + lidOffset + new Vector2(0f, closing * 22f));
            Put(lidUp, OllyAnchors.LidUp + lidOffset + new Vector2(0f, closing * 22f));
            bool small = eye == Eye.Small;
            Put(shineLeft, OllyAnchors.ShineLeft + headOffset + eyeLOffset + (small ? new Vector2(4f, 6f) : Vector2.zero));
            Put(shineRight, OllyAnchors.ShineRight + headOffset + eyeROffset + (small ? new Vector2(0f, 5f) : Vector2.zero));
        }
        Show(shut, !open);
        if (!open) {
            SetSprite(shut, "ES_" + shutState);
            Put(shut, OllyAnchors.Shut[(int)shutState - 1] + faceOffset);
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
            // The brow lowers a little on down-gaze.
            Vector2 browOffset = eyeAvg.y > 0f ? new Vector2(0f, eyeAvg.y * 0.4f) : Vector2.zero;
            Put(brow, OllyAnchors.Brows[(int)face.Brow - 1] + browOffset);
        }

        for (int i = 0; i < fronts.Length; i++) {
            var f = (MarkFront)(1 << i);
            bool on = (face.Front & f) != 0;
            Show(fronts[i], on);
            if (on) {
                Put(fronts[i], OllyAnchors.Fronts[i] + headOffset);
            }
        }
    }
}
