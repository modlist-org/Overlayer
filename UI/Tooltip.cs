using DG.Tweening;
using Overlayer.Resource;
using Overlayer.UI.SpriteManage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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
        img.sprite = SpriteDatabase.Get(UISliceSprite.Circle256P2048);
        img.type = Image.Type.Sliced;

        GameObject t = new("Text");
        t.transform.SetParent(obj.transform, false);

        RectTransform tr = t.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new(16f, 16f);
        tr.offsetMax = new(-16f, -16f);

        text = t.AddComponent<TextMeshProUGUI>();
        text.font = ResourceManager.Get<TMP_FontAsset>(Asset.SUITRegular);
        text.fontSize = 20f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;
    }

    public static void Tick() {
        if(!visible || obj == null) {
            return;
        }

        Vector2 mouse = Input.mousePosition;
        Vector2 target = mouse + new Vector2(24f, -28f);

        Vector2 size = rect.sizeDelta;

        float maxX = Screen.width - size.x;
        float maxY = Screen.height - size.y;

        target.x = Mathf.Clamp(target.x, 0f, maxX);
        target.y = Mathf.Clamp(target.y, size.y, Screen.height);

        rect.position = Vector2.SmoothDamp(
            rect.position,
            target,
            ref velocity,
            0.02f
        );
    }

    private static Sequence seq;

    public static void Show(string tip) {
        if(obj == null) {
            return;
        }

        seq?.Kill();

        obj.SetActive(true);
        text.text = tip;

        Vector2 size = text.GetPreferredValues(tip);
        rect.sizeDelta = new(size.x + 32f, size.y + 32f);

        rect.position = Input.mousePosition + new Vector3(20f, -20f, 0f);

        canvas.alpha = 0f;
        visible = true;

        seq = DOTween.Sequence()
            .Append(DOTween.To(
                () => canvas.alpha,
                x => canvas.alpha = x,
                1f,
                0.16f
            ).SetEase(Ease.OutSine));
    }

    public static void Hide() {
        if(obj == null) {
            return;
        }

        seq?.Kill();

        seq = DOTween.Sequence()
            .Append(DOTween.To(
                () => canvas.alpha,
                x => canvas.alpha = x,
                0f,
                0.16f
            ).SetEase(Ease.OutSine))
            .OnComplete(() => {
                visible = false;
                obj.SetActive(false);
            });
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