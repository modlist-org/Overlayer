using TMPro;
using UnityEngine;
using static MelonLoader.MelonLogger;

namespace Overlayer.Localization;

public class TextLocalization : MonoBehaviour {
    public string Key;
    public string Default;

    private TMP_Text tmp;

    private static readonly HashSet<TextLocalization> instances = new();

    public TextLocalization Init(string key, string defaultValue) {
        Key = key;
        Default = defaultValue;
        UpdateText();
        return this;
    }

    void Awake() {
        tmp = GetComponent<TMP_Text>();
    }

    void OnEnable() {
        instances.Add(this);
        UpdateText();
    }

    void OnDisable() {
        instances.Remove(this);
    }

    public void UpdateText() {
        if(tmp != null && !string.IsNullOrEmpty(Key)) {
            tmp.text = Core.Tr.Get(Key, Default);
        }
    }

    public static void RefreshAll() {
        foreach(var t in instances) {
            if(t != null) {
                t.UpdateText();
            }
        }
    }
}