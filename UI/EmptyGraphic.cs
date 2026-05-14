namespace Overlayer.UI;

public class EmptyGraphic : UnityEngine.UI.Graphic {
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh) => vh.Clear();
}