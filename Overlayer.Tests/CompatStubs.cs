// Minimal Unity/TMP stubs so the real compat sources compile on plain .NET.
namespace TMPro {
    public class TMP_Text {
        public bool enableWordWrapping { get; set; } = true;
    }
}

namespace UnityEngine {
    public static class Time {
        public static float time => 1.5f;
        public static float unscaledTime => 2.5f;
        public static float realtimeSinceStartup => 3.5f;
        public static float fixedTime => 4.5f;
        public static float fixedUnscaledTime => 5.5f;
    }

    public class Collider2D {
    }
}
