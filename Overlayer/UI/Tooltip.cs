using Overlayer.Core;
using Overlayer.Resource;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using GTweens.Tweens;
using GTweens.Extensions;

using GTweens.Builders;
using GTweens.Easings;
using Overlayer.Compat.OVC;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI;

public class Tooltip {
    private static GameObject obj;
    private static RectTransform rect;
    private static CanvasGroup canvas;
    private static TextMeshProUGUI text;

    private static Vector2 velocity;
    private static bool visible = false;

    public static void Initialize(Transform parent) {
        obj = new GameObject("Tooltip");
        obj.transform.SetParent(parent, false);

        rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new(0.5f, 0.5f);
        rect.anchorMax = new(0.5f, 0.5f);
        rect.pivot = new(0f, 1f);

        canvas = obj.AddComponent<CanvasGroup>();
        canvas.alpha = 0f;
        canvas.blocksRaycasts = false;

        GameObject bg = new("Bg");
        bg.transform.SetParent(obj.transform, false);

        RectTransform bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Image img = bg.AddComponent<Image>();
        img.color = new(0f, 0f, 0f, 0.6f);
        img.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        img.type = Image.Type.Sliced;

        GameObject t = new("Text");
        t.transform.SetParent(obj.transform, false);

        RectTransform tr = t.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new(16f, 16f);
        tr.offsetMax = new(-16f, -16f);

        text = t.AddComponent<TextMeshProUGUI>();
        text.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Regular);
        text.fontSize = 20f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;
        text.characterSpacing = -3f;
    }

    public static void Tick() {
        if(!MainCore.Conf.Tooltip || !visible || obj == null) {
            return;
        }

        float scale = MainCore.Conf.UIScale;

        Vector2 mouse = OVC_Input.MousePosition;

        Vector2 offset = new Vector2(24f, -28f) * scale;
        Vector2 target = mouse + offset;

        Vector2 size = rect.sizeDelta * scale;

        float maxX = Screen.width - size.x;
        float maxY = Screen.height - size.y;

        target.x = Math.Clamp(target.x, 0f, maxX);
        target.y = Math.Clamp(target.y, 0f, maxY);

        rect.position = Vector2.SmoothDamp(
            rect.position,
            target,
            ref velocity,
            0.02f
        );
    }

    private static GTween seq;

    public static void Show(string tip) {
        if(!MainCore.Conf.Tooltip || obj == null) {
            return;
        }

        seq?.Kill();

        obj.SetActive(true);
        text.text = tip;

        Vector2 size = text.GetPreferredValues(tip);
        rect.sizeDelta = new(size.x + 32f, size.y + 32f);

        rect.position = OVC_Input.MousePosition + new Vector2(20f, -20f);

        canvas.alpha = 0f;
        visible = true;

        seq = GTweenSequenceBuilder.New()
            .AppendTime(0.14f)
            .Append(
                GTweenExtensions.Tween(
                    () => canvas.alpha,
                    x => canvas.alpha = x,
                    1f,
                    0.16f
                ).SetEasing(Easing.OutSine)
            )
            .Build();
        MainCore.TC.Play(seq);
    }

    public static void Hide() {
        if(!MainCore.Conf.Tooltip || obj == null) {
            return;
        }

        seq?.Kill();
        seq = GTweenSequenceBuilder.New()
            .Append(
                GTweenExtensions.Tween(
                    () => canvas.alpha,
                    x => canvas.alpha = x,
                    0f,
                    0.16f
                ).SetEasing(Easing.OutSine)
            )
            .AppendCallback(() => {
                visible = false;
                obj.SetActive(false);
            }).Build();
        MainCore.TC.Play(seq);
    }

    public static void Dispose() {
        visible = false;
        velocity = Vector2.zero;

        if(obj != null) {
            Object.Destroy(obj);
        }

        obj = null;
        rect = null;
        canvas = null;
        text = null;
    }
}