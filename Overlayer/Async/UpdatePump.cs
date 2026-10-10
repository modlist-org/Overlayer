using Overlayer.Core;
using UnityEngine;

#if ML && IL2CPP
using MelonLoader;
#endif

namespace Overlayer.Async;

/// <summary>
/// Own per-frame pump. MelonLoader's <c>OnUpdate</c> does not fire in every
/// title (observed: Superliminal 2019.4), so the mod drives itself from a
/// MonoBehaviour like UniverseLib/UnityExplorer do. Works on old (2019) and
/// new (2026) Unity alike. <see cref="MainCore.Tick"/> runs at most once per
/// frame even with several drivers (MelonLoader, this pump, <see cref="RenderPump"/>).
/// </summary>
#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
internal sealed
#else
public
#endif
class UpdatePump
#if ML && IL2CPP
    (IntPtr ptr) : MonoBehaviour(ptr)
#else
    : MonoBehaviour
#endif
{
    private void Update() {
        MainCore.Tick();
    }
}
