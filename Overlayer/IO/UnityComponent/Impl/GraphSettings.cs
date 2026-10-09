using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using Overlayer.Overlay;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public sealed class GraphSettings : UnityComponentSettingsBase, ICopyable<GraphSettings> {
    public FxValue<string> JsCode = FxValue<string>.FromValue("50 + 40 * Math.sin(t * 2)");
    public FxValue<float> Window = new(10f);
    public FxValue<int> Samples = new(120);
    public FxValue<float> Min = new(0f);
    public FxValue<float> Max = new(100f);
    public FxValue<bool> AutoScale = new(false);
    public FxValue<Color> LineColor = new(Color.white);
    public FxValue<float> Thickness = new(2f);
    public FxValue<bool> ShowAxes = new(true);
    public FxValue<bool> ShowGrid = new(false);
    public FxValue<bool> ShowFill = new(false);
    public FxValue<Color> FillColor = new(new Color(1f, 1f, 1f, 0.25f));

    private string _lastJsCode = "50 + 40 * Math.sin(t * 2)";
    private float _lastWindow = 10f;
    private int _lastSamples = 120;
    private float _lastMin;
    private float _lastMax = 100f;
    private bool _lastAutoScale;
    private Color _lastLineColor = Color.white;
    private float _lastThickness = 2f;
    private bool _lastShowAxes = true;
    private bool _lastShowGrid;
    private bool _lastShowFill;
    private Color _lastFillColor = new(1f, 1f, 1f, 0.25f);

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(JsCode)
        || FxUtil.HasFx(Window)
        || FxUtil.HasFx(Samples)
        || FxUtil.HasFx(Min)
        || FxUtil.HasFx(Max)
        || FxUtil.HasFx(AutoScale)
        || FxUtil.HasFx(LineColor)
        || FxUtil.HasFx(Thickness)
        || FxUtil.HasFx(ShowAxes)
        || FxUtil.HasFx(ShowGrid)
        || FxUtil.HasFx(ShowFill)
        || FxUtil.HasFx(FillColor);

    public override bool ToUnity(GameObject target) {
        var component = target.GetComponent<OvGraphComponent>();
        if (component == null) {
            return false;
        }

        _lastJsCode = JsCode.Value;
        _lastWindow = Window.Value;
        _lastSamples = Samples.Value;
        _lastMin = Min.Value;
        _lastMax = Max.Value;
        _lastAutoScale = AutoScale.Value;
        _lastLineColor = LineColor.Value;
        _lastThickness = Thickness.Value;
        _lastShowAxes = ShowAxes.Value;
        _lastShowGrid = ShowGrid.Value;
        _lastShowFill = ShowFill.Value;
        _lastFillColor = FillColor.Value;
        Push(component);
        ToUnity(component);
        return true;
    }

    private void Push(OvGraphComponent component) {
        component.JsCode = _lastJsCode;
        component.Window = _lastWindow;
        component.Samples = _lastSamples;
        component.Min = _lastMin;
        component.Max = _lastMax;
        component.AutoScale = _lastAutoScale;
        component.LineColor = _lastLineColor;
        component.Thickness = _lastThickness;
        component.ShowAxes = _lastShowAxes;
        component.ShowGrid = _lastShowGrid;
        component.ShowFill = _lastShowFill;
        component.FillColor = _lastFillColor;
        component.SetVerticesDirty();
    }

    public override bool FromUnity(GameObject source) {
        var component = source.GetComponent<OvGraphComponent>();
        if (component == null) {
            return false;
        }

        JsCode.Value = component.JsCode;
        Window.Value = component.Window;
        Samples.Value = component.Samples;
        Min.Value = component.Min;
        Max.Value = component.Max;
        AutoScale.Value = component.AutoScale;
        LineColor.Value = component.LineColor;
        Thickness.Value = component.Thickness;
        ShowAxes.Value = component.ShowAxes;
        ShowGrid.Value = component.ShowGrid;
        ShowFill.Value = component.ShowFill;
        FillColor.Value = component.FillColor;
        _lastJsCode = component.JsCode;
        _lastWindow = component.Window;
        _lastSamples = component.Samples;
        _lastMin = component.Min;
        _lastMax = component.Max;
        _lastAutoScale = component.AutoScale;
        _lastLineColor = component.LineColor;
        _lastThickness = component.Thickness;
        _lastShowAxes = component.ShowAxes;
        _lastShowGrid = component.ShowGrid;
        _lastShowFill = component.ShowFill;
        _lastFillColor = component.FillColor;
        FromUnity(component);
        return true;
    }

    public override void RefreshFx(GameObject target) {
        if (!HasAnyFx) {
            return;
        }

        var component = target.GetComponent<OvGraphComponent>();
        if (component == null) {
            return;
        }

        RefreshEnabled(component);
        if (FxUtil.Changed(ref _lastJsCode, JsCode.Value)) { component.JsCode = _lastJsCode; component.SetVerticesDirty(); }
        if (FxUtil.Changed(ref _lastWindow, Window.Value)) { component.Window = _lastWindow; component.SetVerticesDirty(); }
        if (FxUtil.Changed(ref _lastSamples, Samples.Value)) { component.Samples = _lastSamples; component.SetVerticesDirty(); }
        if (FxUtil.Changed(ref _lastMin, Min.Value)) { component.Min = _lastMin; component.SetVerticesDirty(); }
        if (FxUtil.Changed(ref _lastMax, Max.Value)) { component.Max = _lastMax; component.SetVerticesDirty(); }
        if (FxUtil.Changed(ref _lastAutoScale, AutoScale.Value)) { component.AutoScale = _lastAutoScale; component.SetVerticesDirty(); }
        if (FxUtil.Changed(ref _lastLineColor, LineColor.Value)) { component.LineColor = _lastLineColor; component.SetVerticesDirty(); }
        if (FxUtil.Changed(ref _lastThickness, Thickness.Value)) { component.Thickness = _lastThickness; component.SetVerticesDirty(); }
        if (FxUtil.Changed(ref _lastShowAxes, ShowAxes.Value)) { component.ShowAxes = _lastShowAxes; component.SetVerticesDirty(); }
        if (FxUtil.Changed(ref _lastShowGrid, ShowGrid.Value)) { component.ShowGrid = _lastShowGrid; component.SetVerticesDirty(); }
        if (FxUtil.Changed(ref _lastShowFill, ShowFill.Value)) { component.ShowFill = _lastShowFill; component.SetVerticesDirty(); }
        if (FxUtil.Changed(ref _lastFillColor, FillColor.Value)) { component.FillColor = _lastFillColor; component.SetVerticesDirty(); }
    }

    public override JToken Serialize() => SerializeComponent(new JObject {
        [nameof(JsCode)] = IOUtils.WriteFx(JsCode),
        [nameof(Window)] = IOUtils.WriteFx(Window),
        [nameof(Samples)] = IOUtils.WriteFx(Samples),
        [nameof(Min)] = IOUtils.WriteFx(Min),
        [nameof(Max)] = IOUtils.WriteFx(Max),
        [nameof(AutoScale)] = IOUtils.WriteFx(AutoScale),
        [nameof(LineColor)] = IOUtils.WriteFx(LineColor),
        [nameof(Thickness)] = IOUtils.WriteFx(Thickness),
        [nameof(ShowAxes)] = IOUtils.WriteFx(ShowAxes),
        [nameof(ShowGrid)] = IOUtils.WriteFx(ShowGrid),
        [nameof(ShowFill)] = IOUtils.WriteFx(ShowFill),
        [nameof(FillColor)] = IOUtils.WriteFx(FillColor)
    });

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        JsCode = IOUtils.ReadFx(token, nameof(JsCode), JsCode);
        Window = IOUtils.ReadFx(token, nameof(Window), Window);
        Samples = IOUtils.ReadFx(token, nameof(Samples), Samples);
        Min = IOUtils.ReadFx(token, nameof(Min), Min);
        Max = IOUtils.ReadFx(token, nameof(Max), Max);
        AutoScale = IOUtils.ReadFx(token, nameof(AutoScale), AutoScale);
        LineColor = IOUtils.ReadFx(token, nameof(LineColor), LineColor);
        Thickness = IOUtils.ReadFx(token, nameof(Thickness), Thickness);
        ShowAxes = IOUtils.ReadFx(token, nameof(ShowAxes), ShowAxes);
        ShowGrid = IOUtils.ReadFx(token, nameof(ShowGrid), ShowGrid);
        ShowFill = IOUtils.ReadFx(token, nameof(ShowFill), ShowFill);
        FillColor = IOUtils.ReadFx(token, nameof(FillColor), FillColor);
    }

    public GraphSettings Copy() => new() {
        ComponentEnabled = ComponentEnabled?.Copy(),
        JsCode = JsCode?.Copy(),
        Window = Window?.Copy(),
        Samples = Samples?.Copy(),
        Min = Min?.Copy(),
        Max = Max?.Copy(),
        AutoScale = AutoScale?.Copy(),
        LineColor = LineColor?.Copy(),
        Thickness = Thickness?.Copy(),
        ShowAxes = ShowAxes?.Copy(),
        ShowGrid = ShowGrid?.Copy(),
        ShowFill = ShowFill?.Copy(),
        FillColor = FillColor?.Copy()
    };
}
