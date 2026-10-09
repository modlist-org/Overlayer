using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.IO.UnityComponent.Impl;

public class GridLayoutGroupSettings : UnityComponentSettingsBase, ICopyable<GridLayoutGroupSettings> {
    public FxValue<Vector2> PaddingH = new(Vector2.zero);
    public FxValue<Vector2> PaddingV = new(Vector2.zero);
    public FxValue<Vector2> CellSize = new(new Vector2(100f, 100f));
    public FxValue<Vector2> Spacing = new(Vector2.zero);
    public FxValue<GridLayoutGroup.Corner> StartCorner = new(GridLayoutGroup.Corner.UpperLeft);
    public FxValue<GridLayoutGroup.Axis> StartAxis = new(GridLayoutGroup.Axis.Horizontal);
    public FxValue<TextAnchor> ChildAlignment = new(TextAnchor.UpperLeft);
    public FxValue<GridLayoutGroup.Constraint> Constraint = new(GridLayoutGroup.Constraint.Flexible);
    public FxValue<int> ConstraintCount = new(2);

    private Vector2 _lastPaddingH;
    private Vector2 _lastPaddingV;
    private Vector2 _lastCellSize = new(100f, 100f);
    private Vector2 _lastSpacing;
    private GridLayoutGroup.Corner _lastStartCorner = GridLayoutGroup.Corner.UpperLeft;
    private GridLayoutGroup.Axis _lastStartAxis = GridLayoutGroup.Axis.Horizontal;
    private TextAnchor _lastChildAlignment = TextAnchor.UpperLeft;
    private GridLayoutGroup.Constraint _lastConstraint = GridLayoutGroup.Constraint.Flexible;
    private int _lastConstraintCount = 2;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(PaddingH)
        || FxUtil.HasFx(PaddingV)
        || FxUtil.HasFx(CellSize)
        || FxUtil.HasFx(Spacing)
        || FxUtil.HasFx(StartCorner)
        || FxUtil.HasFx(StartAxis)
        || FxUtil.HasFx(ChildAlignment)
        || FxUtil.HasFx(Constraint)
        || FxUtil.HasFx(ConstraintCount);

    public override bool ToUnity(GameObject target) {
        var component = target.GetComponent<GridLayoutGroup>();
        if (component == null) {
            return false;
        }

        _lastPaddingH = PaddingH.Value;
        _lastPaddingV = PaddingV.Value;
        _lastCellSize = CellSize.Value;
        _lastSpacing = Spacing.Value;
        _lastStartCorner = StartCorner.Value;
        _lastStartAxis = StartAxis.Value;
        _lastChildAlignment = ChildAlignment.Value;
        _lastConstraint = Constraint.Value;
        _lastConstraintCount = ConstraintCount.Value;
        ApplyValues(component);
        ToUnity(component);
        LayoutRebuilder.MarkLayoutForRebuild(target.GetComponent<RectTransform>());
        return true;
    }

    private void ApplyValues(GridLayoutGroup component) {
        component.padding.left = Mathf.RoundToInt(_lastPaddingH.x);
        component.padding.right = Mathf.RoundToInt(_lastPaddingH.y);
        component.padding.top = Mathf.RoundToInt(_lastPaddingV.x);
        component.padding.bottom = Mathf.RoundToInt(_lastPaddingV.y);
        component.cellSize = _lastCellSize;
        component.spacing = _lastSpacing;
        component.startCorner = _lastStartCorner;
        component.startAxis = _lastStartAxis;
        component.childAlignment = _lastChildAlignment;
        component.constraint = _lastConstraint;
        component.constraintCount = Math.Max(1, _lastConstraintCount);
    }

    public override bool FromUnity(GameObject source) {
        var component = source.GetComponent<GridLayoutGroup>();
        if (component == null) {
            return false;
        }

        PaddingH.Value = new Vector2(component.padding.left, component.padding.right);
        PaddingV.Value = new Vector2(component.padding.top, component.padding.bottom);
        CellSize.Value = component.cellSize;
        Spacing.Value = component.spacing;
        StartCorner.Value = component.startCorner;
        StartAxis.Value = component.startAxis;
        ChildAlignment.Value = component.childAlignment;
        Constraint.Value = component.constraint;
        ConstraintCount.Value = component.constraintCount;
        _lastPaddingH = PaddingH.Value;
        _lastPaddingV = PaddingV.Value;
        _lastCellSize = CellSize.Value;
        _lastSpacing = Spacing.Value;
        _lastStartCorner = StartCorner.Value;
        _lastStartAxis = StartAxis.Value;
        _lastChildAlignment = ChildAlignment.Value;
        _lastConstraint = Constraint.Value;
        _lastConstraintCount = ConstraintCount.Value;
        FromUnity(component);
        return true;
    }

    public override void RefreshFx(GameObject target) {
        if (!HasAnyFx) {
            return;
        }

        var component = target.GetComponent<GridLayoutGroup>();
        if (component == null) {
            return;
        }

        RefreshEnabled(component);
        bool changed = false;
        changed |= FxUtil.ApplyIfChanged(ref _lastPaddingH, PaddingH.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastPaddingV, PaddingV.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastCellSize, CellSize.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastSpacing, Spacing.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastStartCorner, StartCorner.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastStartAxis, StartAxis.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastChildAlignment, ChildAlignment.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastConstraint, Constraint.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastConstraintCount, ConstraintCount.Value, _ => { });
        if (changed) {
            ApplyValues(component);
            LayoutRebuilder.MarkLayoutForRebuild(target.GetComponent<RectTransform>());
        }
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(PaddingH)] = IOUtils.WriteFx(PaddingH),
            [nameof(PaddingV)] = IOUtils.WriteFx(PaddingV),
            [nameof(CellSize)] = IOUtils.WriteFx(CellSize),
            [nameof(Spacing)] = IOUtils.WriteFx(Spacing),
            [nameof(StartCorner)] = IOUtils.WriteFx(StartCorner),
            [nameof(StartAxis)] = IOUtils.WriteFx(StartAxis),
            [nameof(ChildAlignment)] = IOUtils.WriteFx(ChildAlignment),
            [nameof(Constraint)] = IOUtils.WriteFx(Constraint),
            [nameof(ConstraintCount)] = IOUtils.WriteFx(ConstraintCount)
        });
    }

    public override void Deserialize(JToken token) {
        if (token == null) {
            return;
        }

        DeserializeComponent(token);
        PaddingH = IOUtils.ReadFx(token, nameof(PaddingH), PaddingH);
        PaddingV = IOUtils.ReadFx(token, nameof(PaddingV), PaddingV);
        CellSize = IOUtils.ReadFx(token, nameof(CellSize), CellSize);
        Spacing = IOUtils.ReadFx(token, nameof(Spacing), Spacing);
        StartCorner = IOUtils.ReadFx(token, nameof(StartCorner), StartCorner);
        StartAxis = IOUtils.ReadFx(token, nameof(StartAxis), StartAxis);
        ChildAlignment = IOUtils.ReadFx(token, nameof(ChildAlignment), ChildAlignment);
        Constraint = IOUtils.ReadFx(token, nameof(Constraint), Constraint);
        ConstraintCount = IOUtils.ReadFx(token, nameof(ConstraintCount), ConstraintCount);
    }

    public GridLayoutGroupSettings Copy() {
        return new GridLayoutGroupSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            PaddingH = PaddingH?.Copy(),
            PaddingV = PaddingV?.Copy(),
            CellSize = CellSize?.Copy(),
            Spacing = Spacing?.Copy(),
            StartCorner = StartCorner?.Copy(),
            StartAxis = StartAxis?.Copy(),
            ChildAlignment = ChildAlignment?.Copy(),
            Constraint = Constraint?.Copy(),
            ConstraintCount = ConstraintCount?.Copy()
        };
    }
}
