using Overlayer.Core;
using UnityEngine;

#if ML && IL2CPP
using MelonLoader;
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.Localization;

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
public class TextLocalization
#if ML && IL2CPP
    (IntPtr ptr) : MonoBehaviour(ptr)
#else
    : MonoBehaviour
#endif
{
    public string Key;
    public string Default;
    public string Value => tr?.Get(Key, Default) ?? Default;

    private Translator tr;

    private TMP_Text tmp;

    private static readonly HashSet<TextLocalization> instances = [];

    public TextLocalization Init(string key, string defaultValue, Translator translator = null) {
        tr = translator ?? MainCore.Tr;
        Key = key;
        Default = defaultValue;

        UpdateText();
        return this;
    }

    void Awake() => tmp = GetComponent<TMP_Text>();

    void OnEnable() {
        instances.Add(this);
        UpdateText();
    }

    void OnDisable() => instances.Remove(this);

    public void UpdateText() {
        if(tmp == null || tr == null || string.IsNullOrEmpty(Key)) {
            return;
        }

        tmp.text = Value;
    }

    public static void RefreshAll() {
        foreach(TextLocalization t in instances) {
            t?.UpdateText();
        }
    }
}