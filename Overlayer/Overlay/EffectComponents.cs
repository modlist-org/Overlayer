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

public enum MovingManTarget {
    TextSize,
    PositionX,
    PositionY,
    PositionZ,
    RotationX,
    RotationY,
    RotationZ,
    ScaleX,
    ScaleY,
    ScaleZ
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
        if(Rect == null) return;

        double value = Effects.MovingMan(TagName, StartSize, EndSize, DefaultSize, Speed, Invert, Ease);
        switch(Target) {
            case MovingManTarget.TextSize:
                if(Text != null) Text.fontSize = Mathf.Max(0f, (float)value);
                break;
            case MovingManTarget.PositionX:
                SetPosition(0, (float)value);
                break;
            case MovingManTarget.PositionY:
                SetPosition(1, (float)value);
                break;
            case MovingManTarget.PositionZ:
                Vector3 position = Rect.anchoredPosition3D;
                position.z = (float)value;
                Rect.anchoredPosition3D = position;
                break;
            case MovingManTarget.RotationX:
                SetRotation(0, (float)value);
                break;
            case MovingManTarget.RotationY:
                SetRotation(1, (float)value);
                break;
            case MovingManTarget.RotationZ:
                SetRotation(2, (float)value);
                break;
            case MovingManTarget.ScaleX:
                SetScale(0, (float)value);
                break;
            case MovingManTarget.ScaleY:
                SetScale(1, (float)value);
                break;
            case MovingManTarget.ScaleZ:
                SetScale(2, (float)value);
                break;
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
    public Color MinimumColor;
    public Color MaximumColor;
    public Easing Ease;

    public void Init(TMP_Text text) => Text = text;

    public void Update() {
        if(Text == null) return;

        string hex = Effects.ColorRange(
            TagName,
            Minimum,
            Maximum,
            ColorUtility.ToHtmlStringRGBA(MinimumColor),
            ColorUtility.ToHtmlStringRGBA(MaximumColor),
            Ease
        );
        if(!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out Color color)) {
            Text.color = color;
        }
    }
}
