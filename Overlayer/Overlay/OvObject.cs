using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using Overlayer.IO.Overlay;
using Overlayer.ModuleAPI;
using Overlayer.TextEngine.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if ML && IL2CPP
using MelonLoader;
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.Overlay;

public sealed class OvObject : ISettingsFile {
    public readonly GameObject GameObject;
    public readonly RectTransform RectTransform;
    public readonly CanvasGroup CanvasGroup;

    public TextEngineUpdater TextUpdater { get; private set; }

    public OvObject Parent { get; private set; }
    public readonly List<OvObject> Children = [];

    public OvObjectSettings Config = new();

    public bool HasAnyFx => Config?.HasAnyFx ?? false;

    private string _lastName;
    private bool _lastEnabled;
    private bool _lastHasRectMask2D;
    private bool _lastRectMask2DEnabled;
    private string _lastPlayingText;
    private string _lastNotPlayingText;

    public OvObject() {
        GameObject = new GameObject("OvObject");
        RectTransform = GameObject.AddComponent<RectTransform>();
        CanvasGroup = GameObject.AddComponent<CanvasGroup>();

        ApplyConfig();
    }

    public OvObject CreateOvObject() {
        var obj = new OvObject();
        Attach(obj);
        return obj;
    }

    public OvObject Clone() {
        var clone = new OvObject {
            Config = Config.Copy()
        };
        clone.ApplyComponent();
        clone.ApplyConfig();

        foreach(var child in Children) {
            clone.Attach(child.Clone());
        }

        return clone;
    }

    public void ApplyConfig() {
        _lastName = Config.Name.Value;
        _lastEnabled = Config.Enabled.Value;
        GameObject.name = _lastName;
        GameObject.SetActive(_lastEnabled);
        Config.RectTransformConfig.ToUnity(GameObject);
        Config.CanvasGroupConfig.ToUnity(GameObject);
        if(Config.TextConfig != null) {
            var tmp = GameObject.GetComponent<TextMeshProUGUI>();
            if(tmp != null) {
                TMP_FontAsset font = TextFontProvider.Current;
                if(font != null) {
                    tmp.font = font;
                }
            }

            Config.TextConfig.ToUnity(GameObject);
            Config.TextEngineConfig ??= OvTextSettings.FromLegacy(Config.TextConfig.Text.Value);
            _lastPlayingText = Config.TextEngineConfig.PlayingText.Value;
            _lastNotPlayingText = Config.TextEngineConfig.NotPlayingText.Value;
            TextUpdater?.SetText(_lastPlayingText, _lastNotPlayingText);
        }
        Config.MovingManConfig?.ToUnity(GameObject);
        Config.ColorRangeConfig?.ToUnity(GameObject);
        Config.ImageConfig?.ToUnity(GameObject);
        Config.MaskConfig?.ToUnity(GameObject);
        Config.ShadowConfig?.ToUnity(GameObject);
        Config.OutlineConfig?.ToUnity(GameObject);
        Config.ContentSizeFitterConfig?.ToUnity(GameObject);

#if !IL2CPP
        Config.BoxCollider2DConfig?.ToUnity(GameObject);
        Config.Rigidbody2DConfig?.ToUnity(GameObject);
#endif
        _lastHasRectMask2D = Config.HasRectMask2D.Value;
        _lastRectMask2DEnabled = Config.RectMask2DEnabled.Value;
    }

    public void RefreshFx() {
        if(!HasAnyFx || GameObject == null) {
            return;
        }

        FxUtil.ApplyIfChanged(ref _lastName, Config.Name.Value, v => GameObject.name = v);
        FxUtil.ApplyIfChanged(ref _lastEnabled, Config.Enabled.Value, v => GameObject.SetActive(v));
        Config.RectTransformConfig?.RefreshFx(GameObject);
        Config.CanvasGroupConfig?.RefreshFx(GameObject);
        Config.TextConfig?.RefreshFx(GameObject);
        if(Config.TextEngineConfig != null && TextUpdater != null) {
            FxUtil.ApplyIfChanged(ref _lastPlayingText, Config.TextEngineConfig.PlayingText.Value, v => TextUpdater.PlayingEngine.Text = v);
            FxUtil.ApplyIfChanged(ref _lastNotPlayingText, Config.TextEngineConfig.NotPlayingText.Value, v => TextUpdater.NotPlayingEngine.Text = v);
        }
        Config.MovingManConfig?.RefreshFx(GameObject);
        Config.ColorRangeConfig?.RefreshFx(GameObject);
        Config.ImageConfig?.RefreshFx(GameObject);
        Config.MaskConfig?.RefreshFx(GameObject);
        Config.ShadowConfig?.RefreshFx(GameObject);
        Config.OutlineConfig?.RefreshFx(GameObject);
        Config.ContentSizeFitterConfig?.RefreshFx(GameObject);
#if !IL2CPP
        Config.BoxCollider2DConfig?.RefreshFx(GameObject);
        Config.Rigidbody2DConfig?.RefreshFx(GameObject);
#endif
        bool rectMaskChanged = FxUtil.ApplyIfChanged(ref _lastHasRectMask2D, Config.HasRectMask2D.Value, _ => { });
        if(rectMaskChanged) {
            ApplyComponent();
            ApplyConfig();
            return;
        }
        if(Config.HasRectMask2D.Value) {
            var rectMask = GameObject.GetComponent<RectMask2D>();
            if(rectMask != null) {
                FxUtil.ApplyIfChanged(ref _lastRectMask2DEnabled, Config.RectMask2DEnabled.Value, v => rectMask.enabled = v);
            }
        }
    }

    public void ApplyComponent() {
        if(GameObject == null) {
            return;
        }

        if(Config.TextConfig != null && Config.ImageConfig != null) {
            Config.ImageConfig = null;
        }

        bool tc = Config.TextConfig != null;

        if(tc) {
            Config.TextEngineConfig ??= OvTextSettings.FromLegacy(
                Config.TextConfig.Text
            );
        } else {
            Config.TextEngineConfig = null;
        }
        var tmp = EnsureComponent<TextMeshProUGUI>(tc);
        var updater = EnsureComponent<TextEngineUpdater>(tc);

        if(tc) {
            if(updater != null && tmp != null) {
                updater.Init(tmp);
                TextUpdater = updater;
            }
        } else {
            TextUpdater = null;
        }

        if(!tc) {
            Config.ColorRangeConfig = null;
        }
        var movingMan = EnsureComponent<MovingManComponent>(Config.MovingManConfig != null);
        var colorRange = EnsureComponent<ColorRangeComponent>(tc && Config.ColorRangeConfig != null);
        var text = GameObject.GetComponent<TextMeshProUGUI>();
        movingMan?.Init(text, RectTransform);
        colorRange?.Init(text);
        EnsureComponent<Image>(Config.ImageConfig != null);
        EnsureComponent<ContentSizeFitter>(Config.ContentSizeFitterConfig != null);
        EnsureComponent<Mask>(Config.MaskConfig != null);
        EnsureComponent<Shadow>(Config.ShadowConfig != null);

        var rectMask = EnsureComponent<RectMask2D>(Config.HasRectMask2D.Value);
        rectMask?.enabled = Config.RectMask2DEnabled.Value;

        EnsureComponent<Outline>(Config.OutlineConfig != null);

#if !IL2CPP
        EnsureComponent<BoxCollider2D>(Config.BoxCollider2DConfig != null);
        EnsureComponent<Rigidbody2D>(Config.Rigidbody2DConfig != null);
#endif
    }

    private T EnsureComponent<T>(bool enabled) where T : Component {
        if(GameObject == null) {
            return null;
        }

        T comp = GameObject.GetComponent<T>();

        if(!enabled) {
            if(comp != null) {
                Object.Destroy(comp);
            }

            return null;
        }

        if(comp == null) {
            comp = GameObject.AddComponent<T>();
        }

        return comp;
    }

    public void Attach(OvObject child) {
        if(child == null || child.GameObject == null) {
            return;
        }

        if(child == this) {
            return;
        }

        if(child.Parent == this) {
            return;
        }

        child.Parent?.Children.Remove(child);
        child.Parent = this;

        if(!Children.Contains(child)) {
            Children.Add(child);
        }

        child.GameObject.transform.SetParent(RectTransform, false);
        child.GameObject.transform.SetSiblingIndex(Children.Count - 1);
    }

    public void Detach() {
        if(Parent == null) {
            return;
        }

        var oldParent = Parent;
        Parent = null;
        oldParent.Children.Remove(this);

        if(GameObject != null && OverlayCore.Transform != null) {
            GameObject.transform.SetParent(OverlayCore.Transform, false);
        }
    }

    public void SetChildIndex(OvObject child, int index) {
        if(child == null || child.Parent != this) {
            return;
        }

        int currentIndex = Children.IndexOf(child);
        if(currentIndex < 0) {
            return;
        }

        Children.RemoveAt(currentIndex);

        index = Math.Clamp(index, 0, Children.Count);
        Children.Insert(index, child);

        for(int i = 0; i < Children.Count; i++) {
            Children[i].GameObject.transform.SetSiblingIndex(i);
        }
    }

    public void BringToFront(OvObject child) => SetChildIndex(child, Children.Count - 1);

    public void SendToBack(OvObject child) => SetChildIndex(child, 0);

    public JToken Serialize() {
        var json = new JObject {
            [nameof(Config)] = Config.Serialize()
        };

        if(Children != null && Children.Count > 0) {
            json[nameof(Children)] = new JArray(Children.Select(x => x.Serialize()));
        }

        return json;
    }

    public void Deserialize(JToken token) {
        if(token == null) {
            return;
        }

        if(token[nameof(Config)] != null) {
            Config.Deserialize(token[nameof(Config)]);
        }

        if(token[nameof(Children)] is JArray array) {
            for(int i = Children.Count - 1; i >= 0; i--) {
                Children[i].Dispose();
            }
            Children.Clear();

            foreach(var item in array) {
                var obj = new OvObject();
                obj.Deserialize(item);

                Attach(obj);
                obj.ApplyComponent();
                obj.ApplyConfig();
            }
        }

        ApplyConfig();
    }

    internal void RefreshLayout() {
        ApplyComponent();
        ApplyConfig();

        foreach(var child in Children) {
            child.RefreshLayout();
        }
    }

    internal void RebuildLayout() {
        foreach(var child in Children) {
            child.RebuildLayout();
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(RectTransform);
    }

    public void Dispose() {
        for(int i = Children.Count - 1; i >= 0; i--) {
            Children[i].Dispose();
        }

        Children.Clear();

        Parent?.Children.Remove(this);
        Parent = null;

        if(GameObject != null) {
            GameObject.transform.SetParent(null);
            Object.Destroy(GameObject);
        }
    }

#if ML && IL2CPP
    [RegisterTypeInIl2Cpp]
#endif
    public class TextEngineUpdater
#if ML && IL2CPP
        (IntPtr ptr) : MonoBehaviour(ptr)
#else
        : MonoBehaviour
#endif
    {
        private static readonly List<WeakReference<TextEngineUpdater>> AllUpdaters = [];

        public TextMeshProUGUI Tmp;
        public TextEngineCore PlayingEngine;
        public TextEngineCore NotPlayingEngine;

        public void Awake() => AllUpdaters.Add(new WeakReference<TextEngineUpdater>(this));

        public void Init(TextMeshProUGUI tmp) {
            Tmp = tmp;
            PlayingEngine ??= new();
            NotPlayingEngine ??= new();
        }

        public void SetText(string playingText, string notPlayingText) {
            PlayingEngine.Text = playingText ?? string.Empty;
            NotPlayingEngine.Text = notPlayingText ?? string.Empty;
        }

        public void Update() {
            if(Tmp == null) {
                return;
            }

            TextEngineCore engine = PlaybackState.IsPlaying ? PlayingEngine : NotPlayingEngine;

            if(engine == null) {
                return;
            }

            Tmp.text = engine.Get();
        }

        public void OnDestroy() {
            PlayingEngine?.Dispose();
            NotPlayingEngine?.Dispose();
            AllUpdaters.RemoveAll(wr => !wr.TryGetTarget(out var u) || u == this);
        }

        public static void RecompileAll() {
            foreach(var wr in AllUpdaters) {
                if(wr.TryGetTarget(out var updater)) {
                    updater.PlayingEngine?.ForceRecompile();
                    updater.NotPlayingEngine?.ForceRecompile();
                }
            }
        }
    }
}
