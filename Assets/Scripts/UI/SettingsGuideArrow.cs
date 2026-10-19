using UnityEngine;
using UnityEngine.UI;

// Solid arrow used by the mouse guide; rotates with its RectTransform.
public sealed class SettingsGuideArrow : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var r = rectTransform.rect;
        var points = new[] { new Vector2(0,.33f), new Vector2(.55f,.33f), new Vector2(.55f,0),
            new Vector2(1,.5f), new Vector2(.55f,1), new Vector2(.55f,.67f), new Vector2(0,.67f) };
        foreach (var p in points) mesh.AddVert(new Vector3(r.x+p.x*r.width,r.y+p.y*r.height),color,Vector2.zero);
        mesh.AddTriangle(0,1,5);mesh.AddTriangle(0,5,6);mesh.AddTriangle(2,3,4);
    }
}
