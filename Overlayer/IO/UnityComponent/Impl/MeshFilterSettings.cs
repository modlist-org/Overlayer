using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class MeshFilterSettings : UnityComponentSettingsBase, ICopyable<MeshFilterSettings> {
    public FxValue<PrimitiveType> MeshType = new(PrimitiveType.Cube);

    private PrimitiveType _lastMeshType = PrimitiveType.Cube;

    private static readonly Dictionary<PrimitiveType, Mesh> PrimitiveMeshes = new();

    public override bool HasAnyFx => base.HasAnyFx || FxUtil.HasFx(MeshType);

    internal static Mesh PrimitiveMesh(PrimitiveType type) {
        if (!PrimitiveMeshes.TryGetValue(type, out var mesh) || mesh == null) {
            // Built-in primitive meshes are shared assets: stealing the
            // reference and destroying the temp object is safe.
            var temp = GameObject.CreatePrimitive(type);
            mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            PrimitiveMeshes[type] = mesh;
            UnityEngine.Object.Destroy(temp);
        }
        return mesh;
    }

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<MeshFilter>();
        if (com == null) {
            return false;
        }

        _lastMeshType = MeshType.Value;
        com.sharedMesh = PrimitiveMesh(_lastMeshType);

        return true;
    }

    public override bool FromUnity(GameObject source) {
        return source.GetComponent<MeshFilter>() != null;
    }

    public override void RefreshFx(GameObject target) {
        if (!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<MeshFilter>();
        if (com == null) {
            return;
        }

        if (FxUtil.Changed(ref _lastMeshType, MeshType.Value)) com.sharedMesh = PrimitiveMesh(_lastMeshType);
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(MeshType)] = IOUtils.WriteFx(MeshType)
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        MeshType = IOUtils.ReadFx(token, nameof(MeshType), MeshType);
    }

    public MeshFilterSettings Copy() {
        return new MeshFilterSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            MeshType = MeshType?.Copy()
        };
    }
}

// shortcut: custom (non-primitive) meshes unsupported; add a mesh asset reference here when needed.
