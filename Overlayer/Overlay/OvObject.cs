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
    public RectTransform RectTransform { get; private set; }
    public CanvasGroup CanvasGroup { get; private set; }

    internal string SourceFile { get; set; }

    public TextEngineUpdater TextUpdater { get; private set; }

    public OvObject Parent { get; private set; }
    public readonly List<OvObject> Children = [];

    public OvObjectSettings Config = new();

    private bool _hasAnyFx;
    private int _hasAnyFxVersion = -1;
    private int _hasAnyFxFrame;
    private OvObjectSettings _hasAnyFxConfig;

    // Walking every settings value is ~300 calls; recompute only when an Fx
    // flag changed, the config was swapped, or (safety net) once a second.
    public bool HasAnyFx {
        get {
            var config = Config;
            if (config == null) {
                return false;
            }
            int version = FxValue.FxVersion;
            int frame = Time.frameCount;
            if (version != _hasAnyFxVersion || !ReferenceEquals(config, _hasAnyFxConfig)
                || frame - _hasAnyFxFrame >= 60 || frame < _hasAnyFxFrame) {
                _hasAnyFx = config.HasAnyFx;
                _hasAnyFxVersion = version;
                _hasAnyFxConfig = config;
                _hasAnyFxFrame = frame;
            }
            return _hasAnyFx;
        }
    }

    private string _lastName;
    private bool _lastEnabled;
    private bool _lastHasRectMask2D;
    private bool _lastRectMask2DEnabled;
    private string _lastText;

    public OvObject() : this(false) {
    }

    public OvObject(bool bare) {
        GameObject = new GameObject("OvObject");
        if (bare) {
            Config.RectTransformConfig = null;
            Config.CanvasGroupConfig = null;
            Config.TransformConfig = new();
        } else {
            RectTransform = GameObject.AddComponent<RectTransform>();
            CanvasGroup = GameObject.AddComponent<CanvasGroup>();
            Config.RectTransformConfig = new();
            Config.CanvasGroupConfig = new();
        }

        ApplyConfig();
    }

    public OvObject CreateOvObject() {
        var obj = new OvObject(bare: Config.RectTransformConfig == null);
        Attach(obj);
        return obj;
    }

    public OvObject Clone() {
        var clone = new OvObject(bare: Config.RectTransformConfig == null) {
            Config = Config.Copy()
        };
        clone.ApplyComponent();
        clone.ApplyConfig();

        foreach (var child in Children) {
            clone.Attach(child.Clone());
        }

        return clone;
    }

    public void ApplyConfig() {
        _lastName = Config.Name.Value;
        _lastEnabled = Config.Enabled.Value;
        GameObject.name = _lastName;
        GameObject.SetActive(_lastEnabled);
        Config.RectTransformConfig?.ToUnity(GameObject);
        Config.CanvasGroupConfig?.ToUnity(GameObject);
        Config.TransformConfig?.ToUnity(GameObject);
        if (Config.TextConfig != null) {
            var tmp = GameObject.GetComponent<TextMeshProUGUI>();
            if (tmp != null) {
                TMP_FontAsset font = TextFontProvider.Current;
                if (font != null) {
                    tmp.font = font;
                }
            }

            Config.TextConfig.ToUnity(GameObject);
            Config.TextEngineConfig ??= OvTextSettings.FromLegacy(Config.TextConfig.Text.Value);
            _lastText = Config.TextEngineConfig.Text.Value;
            TextUpdater?.SetText(_lastText);
        }
        Config.MovingManConfig?.ToUnity(GameObject);
        Config.ColorRangeConfig?.ToUnity(GameObject);
        Config.CanvasConfig?.ToUnity(GameObject);
        Config.HorizontalLayoutGroupConfig?.ToUnity(GameObject);
        Config.VerticalLayoutGroupConfig?.ToUnity(GameObject);
        Config.GridLayoutGroupConfig?.ToUnity(GameObject);
#if !IL2CPP
        Config.GraphConfig?.ToUnity(GameObject);
#endif
        Config.RainConfig?.ToUnity(GameObject);
        Config.ImageConfig?.ToUnity(GameObject);
        Config.MaskConfig?.ToUnity(GameObject);
        Config.ShadowConfig?.ToUnity(GameObject);
        Config.OutlineConfig?.ToUnity(GameObject);
        Config.ContentSizeFitterConfig?.ToUnity(GameObject);

#if !IL2CPP
        Config.BoxCollider2DConfig?.ToUnity(GameObject);
        Config.Rigidbody2DConfig?.ToUnity(GameObject);
#endif
        Config.MeshFilterConfig?.ToUnity(GameObject);
        Config.MeshRendererConfig?.ToUnity(GameObject);
        Config.BoxColliderConfig?.ToUnity(GameObject);
        Config.SphereColliderConfig?.ToUnity(GameObject);
        Config.CapsuleColliderConfig?.ToUnity(GameObject);
        Config.MeshColliderConfig?.ToUnity(GameObject);
        Config.RigidbodyConfig?.ToUnity(GameObject);
        Config.LightConfig?.ToUnity(GameObject);
        _lastHasRectMask2D = Config.HasRectMask2D.Value;
        _lastRectMask2DEnabled = Config.RectMask2DEnabled.Value;
    }

    public void RefreshFx() {
        if (GameObject == null) {
            return;
        }

        if (HasAnyFx) {
            if (FxUtil.Changed(ref _lastName, Config.Name.Value)) GameObject.name = _lastName;
            if (FxUtil.Changed(ref _lastEnabled, Config.Enabled.Value)) GameObject.SetActive(_lastEnabled);
            Config.RectTransformConfig?.RefreshFx(GameObject);
            Config.CanvasGroupConfig?.RefreshFx(GameObject);
            Config.TransformConfig?.RefreshFx(GameObject);
            Config.TextConfig?.RefreshFx(GameObject);
            if (Config.TextEngineConfig != null && TextUpdater != null) {
                if (FxUtil.Changed(ref _lastText, Config.TextEngineConfig.Text.Value)) TextUpdater.Engine.Text = _lastText;
            }
            Config.MovingManConfig?.RefreshFx(GameObject);
            Config.ColorRangeConfig?.RefreshFx(GameObject);
            Config.CanvasConfig?.RefreshFx(GameObject);
            Config.HorizontalLayoutGroupConfig?.RefreshFx(GameObject);
            Config.VerticalLayoutGroupConfig?.RefreshFx(GameObject);
            Config.GridLayoutGroupConfig?.RefreshFx(GameObject);
    #if !IL2CPP
        Config.GraphConfig?.RefreshFx(GameObject);
#endif
            Config.RainConfig?.RefreshFx(GameObject);
            Config.ImageConfig?.RefreshFx(GameObject);
            Config.MaskConfig?.RefreshFx(GameObject);
            Config.ShadowConfig?.RefreshFx(GameObject);
            Config.OutlineConfig?.RefreshFx(GameObject);
            Config.ContentSizeFitterConfig?.RefreshFx(GameObject);
#if !IL2CPP
            Config.BoxCollider2DConfig?.RefreshFx(GameObject);
            Config.Rigidbody2DConfig?.RefreshFx(GameObject);
#endif
            Config.MeshFilterConfig?.RefreshFx(GameObject);
            Config.MeshRendererConfig?.RefreshFx(GameObject);
            Config.BoxColliderConfig?.RefreshFx(GameObject);
            Config.SphereColliderConfig?.RefreshFx(GameObject);
            Config.CapsuleColliderConfig?.RefreshFx(GameObject);
            Config.MeshColliderConfig?.RefreshFx(GameObject);
            Config.RigidbodyConfig?.RefreshFx(GameObject);
            Config.LightConfig?.RefreshFx(GameObject);
            bool rectMaskChanged = FxUtil.ApplyIfChanged(ref _lastHasRectMask2D, Config.HasRectMask2D.Value, _ => { });
            if (rectMaskChanged) {
                ApplyComponent();
                ApplyConfig();
            } else if (Config.HasRectMask2D.Value) {
                var rectMask = GameObject.GetComponent<RectMask2D>();
                if (rectMask != null) {
                    if (FxUtil.Changed(ref _lastRectMask2DEnabled, Config.RectMask2DEnabled.Value)) rectMask.enabled = _lastRectMask2DEnabled;
                }
            }
        }

        // Child Fx values must continue ticking even when this object's own
        // settings have no Fx. Otherwise Fx-enabled descendants never update.
        foreach (var child in Children) {
            child?.RefreshFx();
        }
    }

    public void ApplyComponent() {
        if (GameObject == null) {
            return;
        }

        // shortcut: dropping RectTransformConfig only stops managing it; the component
        // itself stays dormant (Unity won't remove the Transform). Re-adding the
        // config reuses or re-adds the component below.
        if (Config.RectTransformConfig != null && GameObject.GetComponent<RectTransform>() == null) {
            GameObject.AddComponent<RectTransform>();
        }
        RectTransform = GameObject.GetComponent<RectTransform>();
        EnsureComponent<CanvasGroup>(Config.CanvasGroupConfig != null);
        CanvasGroup = GameObject.GetComponent<CanvasGroup>();

        if (Config.TextConfig != null && Config.ImageConfig != null) {
            Config.ImageConfig = null;
        }

        bool tc = Config.TextConfig != null;

        if (tc) {
            Config.TextEngineConfig ??= OvTextSettings.FromLegacy(
                Config.TextConfig.Text
            );
        } else {
            Config.TextEngineConfig = null;
        }
        var tmp = EnsureComponent<TextMeshProUGUI>(tc);
        var updater = EnsureComponent<TextEngineUpdater>(tc);

        if (tc) {
            if (updater != null && tmp != null) {
                updater.Init(tmp);
                TextUpdater = updater;
            }
        } else {
            TextUpdater = null;
        }

        if (!tc) {
            Config.ColorRangeConfig = null;
        }
        var movingMan = EnsureComponent<MovingManComponent>(Config.MovingManConfig != null);
        var colorRange = EnsureComponent<ColorRangeComponent>(tc && Config.ColorRangeConfig != null);
        EnsureComponent<Canvas>(Config.CanvasConfig != null);
        EnsureComponent<HorizontalLayoutGroup>(Config.HorizontalLayoutGroupConfig != null);
        EnsureComponent<VerticalLayoutGroup>(Config.VerticalLayoutGroupConfig != null);
        EnsureComponent<GridLayoutGroup>(Config.GridLayoutGroupConfig != null);
#if !IL2CPP
        EnsureComponent<OvGraphComponent>(Config.GraphConfig != null);
#endif
        EnsureComponent<OvRainComponent>(Config.RainConfig != null);
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
        EnsureComponent<MeshFilter>(Config.MeshFilterConfig != null);
        EnsureComponent<MeshRenderer>(Config.MeshRendererConfig != null);
        EnsureComponent<BoxCollider>(Config.BoxColliderConfig != null);
        EnsureComponent<SphereCollider>(Config.SphereColliderConfig != null);
        EnsureComponent<CapsuleCollider>(Config.CapsuleColliderConfig != null);
        EnsureComponent<MeshCollider>(Config.MeshColliderConfig != null);
        EnsureComponent<Rigidbody>(Config.RigidbodyConfig != null);
        EnsureComponent<Light>(Config.LightConfig != null);
    }

#if ML && IL2CPP
    private static readonly HashSet<Type> _injectedComponents = [];
#endif

    private T EnsureComponent<T>(bool enabled) where T : Component {
        if (GameObject == null) {
            return null;
        }

#if ML && IL2CPP
        // Mod-defined components are never in Il2CppAssemblies: inject the
        // type before the first generic Get/AddComponent call touches it,
        // otherwise the generic method store faults (NRE) on the missing
        // native class pointer.
        lock (_injectedComponents) {
            if (_injectedComponents.Add(typeof(T))) {
                try {
                    Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<T>();
                } catch (Exception e) {
                    try {
                        Overlayer.Core.MainCore.Log.Err($"[OvObject] RegisterTypeInIl2Cpp<{typeof(T).Name}> failed: {e.GetType().Name}: {e.Message}");
                    } catch {
                    }
                }
            }
        }
#endif

        T comp = GameObject.GetComponent<T>();

        if (!enabled) {
            if (comp != null) {
                Object.Destroy(comp);
            }

            return null;
        }

        if (comp == null) {
#if ML && IL2CPP
            try {
                Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<T>();
            } catch {
            }
            try {
                comp = GameObject.AddComponent<T>();
            } catch (Exception e) {
                try {
                    Overlayer.Core.MainCore.Log.Err($"[OvObject] AddComponent<{typeof(T).Name}> failed: {e.GetType().Name}: {e.Message}");
                } catch {
                }
                return null;
            }
#else
            comp = GameObject.AddComponent<T>();
#endif
        }

        return comp;
    }

    public void Attach(OvObject child) {
        if (child == null || child.GameObject == null) {
            return;
        }

        if (child == this) {
            return;
        }

        if (child.Parent == this) {
            return;
        }

        child.Parent?.Children.Remove(child);
        child.Parent = this;

        if (!Children.Contains(child)) {
            Children.Add(child);
        }

        child.GameObject.transform.SetParent(GameObject.transform, false);
        child.GameObject.transform.SetSiblingIndex(Children.Count - 1);
    }

    public void Detach() {
        if (Parent == null) {
            return;
        }

        var oldParent = Parent;
        Parent = null;
        oldParent.Children.Remove(this);

        if (GameObject != null && OverlayCore.Transform != null) {
            GameObject.transform.SetParent(OverlayCore.Transform, false);
        }
    }

    public void SetChildIndex(OvObject child, int index) {
        if (child == null || child.Parent != this) {
            return;
        }

        int currentIndex = Children.IndexOf(child);
        if (currentIndex < 0) {
            return;
        }

        Children.RemoveAt(currentIndex);

        index = O5Kit.Core.MathCompat.Clamp(index, 0, Children.Count);
        Children.Insert(index, child);

        for (int i = 0; i < Children.Count; i++) {
            Children[i].GameObject.transform.SetSiblingIndex(i);
        }
    }

    public void BringToFront(OvObject child) => SetChildIndex(child, Children.Count - 1);

    public void SendToBack(OvObject child) => SetChildIndex(child, 0);

    public JToken Serialize() {
        var json = new JObject {
            [nameof(Config)] = Config.Serialize()
        };

        if (Children != null && Children.Count > 0) {
            json[nameof(Children)] = new JArray(Children.Select(x => x.Serialize()));
        }

        return json;
    }

    public void Deserialize(JToken token) {
        if (token == null) {
            return;
        }

        if (token[nameof(Config)] != null) {
            Config.Deserialize(token[nameof(Config)]);
        }

        if (token[nameof(Children)] is JArray array) {
            for (int i = Children.Count - 1; i >= 0; i--) {
                Children[i].Dispose();
            }
            Children.Clear();

            foreach (var item in array) {
                var obj = new OvObject(bare: item[nameof(Config)]?[nameof(OvObjectSettings.RectTransformConfig)] == null);
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

        foreach (var child in Children) {
            child.RefreshLayout();
        }
    }

    internal void RebuildLayout() {
        foreach (var child in Children) {
            child.RebuildLayout();
        }

        if (RectTransform != null) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(RectTransform);
        }
    }

    public void Dispose() {
        for (int i = Children.Count - 1; i >= 0; i--) {
            Children[i].Dispose();
        }

        Children.Clear();

        Parent?.Children.Remove(this);
        Parent = null;

        try {
            IO.Fx.FxDisposal.DisposeDeep(Config);
        } catch {
        }

        if (GameObject != null) {
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
        public TextEngineCore Engine;

        public void Awake() => AllUpdaters.Add(new WeakReference<TextEngineUpdater>(this));

        public void Init(TextMeshProUGUI tmp) {
            Tmp = tmp;
            Engine ??= new();
        }

        public void SetText(string text) {
            Engine.Text = text ?? string.Empty;
        }

        private static int renderedFrame = -1;
        private static readonly List<TextEngineUpdater> FrameUpdaters = [];
        private static readonly List<TextEngineCore> FrameEngines = [];
        private static string[] frameResults = new string[16];
        private int lastRenderedFrame = -1;
        private string lastText;

        public void Update() {
            if (Tmp == null || Engine == null) {
                return;
            }

            int frame = Time.frameCount;
            if (renderedFrame != frame) {
                renderedFrame = frame;
                RenderAllActive(frame);
            }
            // Enabled after this frame's batch ran: render on its own.
            if (lastRenderedFrame != frame) {
                lastRenderedFrame = frame;
                Apply(Engine.Get());
            }
        }

        // All active overlays share one V8 transition for their JS tags per frame.
        private static void RenderAllActive(int frame) {
            FrameUpdaters.Clear();
            FrameEngines.Clear();
            foreach (var wr in AllUpdaters) {
                if (wr.TryGetTarget(out var updater) && updater != null && updater.isActiveAndEnabled
                    && updater.Tmp != null && updater.Engine != null) {
                    FrameUpdaters.Add(updater);
                    FrameEngines.Add(updater.Engine);
                }
            }
            if (frameResults.Length < FrameEngines.Count) frameResults = new string[FrameEngines.Count * 2];
            TextEngineCore.GetAll(FrameEngines, frameResults);
            for (int i = 0; i < FrameUpdaters.Count; i++) {
                FrameUpdaters[i].lastRenderedFrame = frame;
                FrameUpdaters[i].Apply(frameResults[i]);
                frameResults[i] = null;
            }
            FrameUpdaters.Clear();
            FrameEngines.Clear();
        }

        // Unchanged text keeps its memoized instance, so skip TMP's O(n) setter
        // compare. Settings may rewrite Tmp.text, hence the second check (on
        // IL2CPP the getter allocates a new string, so always assign there).
        private void Apply(string text) {
#if !(ML && IL2CPP)
            if (ReferenceEquals(text, lastText) && ReferenceEquals(Tmp.text, text)) {
                return;
            }
#endif
            lastText = text;
            Tmp.text = text;
        }

        public void OnDestroy() {
            Engine?.Dispose();
            AllUpdaters.RemoveAll(wr => !wr.TryGetTarget(out var u) || u == this);
        }

        public static void RecompileAll() {
            foreach (var wr in AllUpdaters) {
                if (wr.TryGetTarget(out var updater)) {
                    updater.Engine?.ForceRecompile();
                }
            }
        }
    }
}
