using O5Kit.Core;
using Overlayer.TagImpl;
using UnityEngine;

#if ML && IL2CPP
using Il2CppTMPro;
using MelonLoader;
#else
using TMPro;
#endif

namespace Overlayer.Overlay;

[Flags]
public enum MovingManTarget {
    None = 0,
    TextSize = 1 << 0,
    PositionX = 1 << 1,
    PositionY = 1 << 2,
    PositionZ = 1 << 3,
    RotationX = 1 << 4,
    RotationY = 1 << 5,
    RotationZ = 1 << 6,
    ScaleX = 1 << 7,
    ScaleY = 1 << 8,
    ScaleZ = 1 << 9,
    SizeDeltaX = 1 << 10,
    SizeDeltaY = 1 << 11
}

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
public sealed class MovingManComponent
#if ML && IL2CPP
    (IntPtr ptr) : MonoBehaviour(ptr)
#else
    : MonoBehaviour
#endif
{
    public TMP_Text Text;
    public RectTransform Rect;
    public string TagName;
    public MovingManTarget Target;
    public double StartSize;
    public double EndSize;
    public double DefaultSize;
    public double Speed;
    public bool Invert;
    public O5Ease Ease;

    public void Init(TMP_Text text, RectTransform rect) {
        Text = text;
        Rect = rect;
    }

    public void Update() {
        if(Rect == null) {
            return;
        }

        double value = Effects.MovingMan(TagName, StartSize, EndSize, DefaultSize, Speed, Invert, Ease);
        float floatValue = (float)value;
        // Bitwise checks: Enum.HasFlag boxes on Mono, 12x per frame per object.
        MovingManTarget target = Target;
        if((target & MovingManTarget.TextSize) != 0) {
            Text?.fontSize = Mathf.Max(0f, floatValue);
        }
        if((target & MovingManTarget.PositionX) != 0) {
            SetPosition(0, floatValue);
        }
        if((target & MovingManTarget.PositionY) != 0) {
            SetPosition(1, floatValue);
        }
        if((target & MovingManTarget.PositionZ) != 0) {
            Vector3 position = Rect.anchoredPosition3D;
            position.z = floatValue;
            Rect.anchoredPosition3D = position;
        }
        if((target & MovingManTarget.RotationX) != 0) {
            SetRotation(0, floatValue);
        }
        if((target & MovingManTarget.RotationY) != 0) {
            SetRotation(1, floatValue);
        }
        if((target & MovingManTarget.RotationZ) != 0) {
            SetRotation(2, floatValue);
        }
        if((target & MovingManTarget.ScaleX) != 0) {
            SetScale(0, floatValue);
        }
        if((target & MovingManTarget.ScaleY) != 0) {
            SetScale(1, floatValue);
        }
        if((target & MovingManTarget.ScaleZ) != 0) {
            SetScale(2, floatValue);
        }
        if((target & MovingManTarget.SizeDeltaX) != 0) {
            SetSizeDelta(0, floatValue);
        }
        if((target & MovingManTarget.SizeDeltaY) != 0) {
            SetSizeDelta(1, floatValue);
        }
    }

    private void SetPosition(int axis, float value) {
        Vector2 position = Rect.anchoredPosition;
        position[axis] = value;
        Rect.anchoredPosition = position;
    }

    private void SetRotation(int axis, float value) {
        Vector3 rotation = Rect.localEulerAngles;
        rotation[axis] = value;
        Rect.localEulerAngles = rotation;
    }

    private void SetScale(int axis, float value) {
        Vector3 scale = Rect.localScale;
        scale[axis] = value;
        Rect.localScale = scale;
    }

    private void SetSizeDelta(int axis, float value) {
        Vector2 sizeDelta = Rect.sizeDelta;
        sizeDelta[axis] = value;
        Rect.sizeDelta = sizeDelta;
    }
}

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
public sealed class ColorRangeComponent
#if ML && IL2CPP
    (IntPtr ptr) : MonoBehaviour(ptr)
#else
    : MonoBehaviour
#endif
{
    public TMP_Text Text;
    public string TagName;
    public double Minimum;
    public double Maximum;
    public GradientColor MinimumColor;
    public GradientColor MaximumColor;
    public O5Ease Ease;

    public void Init(TMP_Text text) => Text = text;

    public void Update() {
        if(Text == null) {
            return;
        }

        if(!Effects.TryColorRangeProgress(TagName, Minimum, Maximum, Ease, out float progress)) {
            return;
        }

        Text.color = Color.white;
        var gradient = new VertexGradient(
            Color.LerpUnclamped(MinimumColor.TL, MaximumColor.TL, progress),
            Color.LerpUnclamped(MinimumColor.TR, MaximumColor.TR, progress),
            Color.LerpUnclamped(MinimumColor.BL, MaximumColor.BL, progress),
            Color.LerpUnclamped(MinimumColor.BR, MaximumColor.BR, progress)
        );
        // TMP's colorGradient/enableVertexGradient setters always dirty the
        // mesh; skip them when nothing changed so a static progress doesn't
        // force a text rebuild every frame.
        var currentGradient = Text.colorGradient;
        if(!currentGradient.topLeft.Equals(gradient.topLeft)
            || !currentGradient.topRight.Equals(gradient.topRight)
            || !currentGradient.bottomLeft.Equals(gradient.bottomLeft)
            || !currentGradient.bottomRight.Equals(gradient.bottomRight)) {
            Text.colorGradient = gradient;
        }
        if(!Text.enableVertexGradient) {
            Text.enableVertexGradient = true;
        }
    }
}
