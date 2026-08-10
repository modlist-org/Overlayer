using GTweens.Easings;
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
    SizeDelta = 1 << 10
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
    public Easing Ease;

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
        if(Target.HasFlag(MovingManTarget.TextSize)) {
            Text?.fontSize = Mathf.Max(0f, floatValue);
        }
        if(Target.HasFlag(MovingManTarget.PositionX)) {
            SetPosition(0, floatValue);
        }
        if(Target.HasFlag(MovingManTarget.PositionY)) {
            SetPosition(1, floatValue);
        }
        if(Target.HasFlag(MovingManTarget.PositionZ)) {
            Vector3 position = Rect.anchoredPosition3D;
            position.z = floatValue;
            Rect.anchoredPosition3D = position;
        }
        if(Target.HasFlag(MovingManTarget.RotationX)) {
            SetRotation(0, floatValue);
        }
        if(Target.HasFlag(MovingManTarget.RotationY)) {
            SetRotation(1, floatValue);
        }
        if(Target.HasFlag(MovingManTarget.RotationZ)) {
            SetRotation(2, floatValue);
        }
        if(Target.HasFlag(MovingManTarget.ScaleX)) {
            SetScale(0, floatValue);
        }
        if(Target.HasFlag(MovingManTarget.ScaleY)) {
            SetScale(1, floatValue);
        }
        if(Target.HasFlag(MovingManTarget.ScaleZ)) {
            SetScale(2, floatValue);
        }
        if(Target.HasFlag(MovingManTarget.SizeDelta)) {
            Rect.sizeDelta = new Vector2(floatValue, floatValue);
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
    public Easing Ease;

    public void Init(TMP_Text text) => Text = text;

    public void Update() {
        if(Text == null) {
            return;
        }

        if(!Effects.TryColorRangeProgress(TagName, Minimum, Maximum, Ease, out float progress)) {
            return;
        }

        Text.color = Color.white;
        Text.colorGradient = new VertexGradient(
            Color.LerpUnclamped(MinimumColor.TL, MaximumColor.TL, progress),
            Color.LerpUnclamped(MinimumColor.TR, MaximumColor.TR, progress),
            Color.LerpUnclamped(MinimumColor.BL, MaximumColor.BL, progress),
            Color.LerpUnclamped(MinimumColor.BR, MaximumColor.BR, progress)
        );
        Text.enableVertexGradient = true;
    }
}
