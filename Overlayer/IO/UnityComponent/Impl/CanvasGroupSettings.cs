using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;
namespace Overlayer.IO.UnityComponent.Impl;

public class CanvasGroupSettings : UnityComponentSettingsBase, ICopyable<CanvasGroupSettings> {
    public float Alpha = 1f;
    public bool Interactable = false;
    public bool BlocksRaycasts = false;
    public bool IgnoreParentGroups = false;

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<CanvasGroup>();
        if(com == null) {
            return false;
        }

        com.alpha = Alpha;
        com.interactable = Interactable;
        com.blocksRaycasts = BlocksRaycasts;
        com.ignoreParentGroups = IgnoreParentGroups;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<CanvasGroup>();
        if(com == null) {
            return false;
        }

        Alpha = com.alpha;
        Interactable = com.interactable;
        BlocksRaycasts = com.blocksRaycasts;
        IgnoreParentGroups = com.ignoreParentGroups;

        return true;
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(Alpha)] = Alpha,
            [nameof(Interactable)] = Interactable,
            [nameof(BlocksRaycasts)] = BlocksRaycasts,
            [nameof(IgnoreParentGroups)] = IgnoreParentGroups
        };
    }

    public override void Deserialize(JToken token) {
        Alpha = IOUtils.Read(token, nameof(Alpha), Alpha);
        Interactable = IOUtils.Read(token, nameof(Interactable), Interactable);
        BlocksRaycasts = IOUtils.Read(token, nameof(BlocksRaycasts), BlocksRaycasts);
        IgnoreParentGroups = IOUtils.Read(token, nameof(IgnoreParentGroups), IgnoreParentGroups);
    }

    public CanvasGroupSettings Copy() {
        return new CanvasGroupSettings {
            Alpha = Alpha,
            Interactable = Interactable,
            BlocksRaycasts = BlocksRaycasts,
            IgnoreParentGroups = IgnoreParentGroups
        };
    }
}