#if ML && IL2CPP
using MelonLoader;
#endif

namespace Overlayer.UI.Utility;

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
public class EmptyGraphic : UnityEngine.UI.Graphic {
#if ML && IL2CPP
    public
#else
    protected
#endif
    override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh) => vh.Clear();
}