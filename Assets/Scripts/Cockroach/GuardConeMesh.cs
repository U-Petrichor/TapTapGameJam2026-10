using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 守卫用的锥形网格生成。玩家看到的锥和真正判定用的锥必须同源，
    /// 所以视野扇形（MeshCollider 用）和喷雾雾壳（MeshRenderer 用）都从这里出。
    ///
    /// 顶点排布（沿 +Z 铺开，竖直方向 height 厚）：
    ///   0 / 1   = 尖端（下 / 上）
    ///   2 + 2i  = 第 i 段的下沿，3 + 2i = 第 i 段的上沿
    /// UV：x = 横向（角度）0..1，y = 轴向（离尖端）0..1。视野锥用不到 UV，顺手一起给。
    /// </summary>
    public static class GuardConeMesh
    {
        public static Mesh Build(float range, float halfAngle, float height, int segments = 16,
            string meshName = "GuardCone")
        {
            float halfHeight = height * 0.5f;

            var vertices = new List<Vector3>(4 + segments * 2)
            {
                new Vector3(0f, -halfHeight, 0f),
                new Vector3(0f, halfHeight, 0f)
            };

            var uvs = new List<Vector2>(4 + segments * 2)
            {
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f)
            };

            var triangles = new List<int>(segments * 12);

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
                float x = Mathf.Sin(angle) * range;
                float z = Mathf.Cos(angle) * range;

                vertices.Add(new Vector3(x, -halfHeight, z));
                vertices.Add(new Vector3(x, halfHeight, z));
                uvs.Add(new Vector2(t, 1f));
                uvs.Add(new Vector2(t, 1f));
            }

            for (int i = 0; i < segments; i++)
            {
                int bottom0 = 2 + i * 2;
                int top0 = bottom0 + 1;
                int bottom1 = bottom0 + 2;
                int top1 = bottom0 + 3;

                triangles.AddRange(new[] { 0, bottom1, bottom0 });
                triangles.AddRange(new[] { 1, top0, top1 });
                triangles.AddRange(new[] { bottom0, bottom1, top1, bottom0, top1, top0 });
            }

            // 两侧的封口（扇形的两条直边）
            int last = 2 + segments * 2;
            triangles.AddRange(new[] { 0, 1, 3, 0, 3, 2 });
            triangles.AddRange(new[] { 0, last, last + 1, 0, last + 1, 1 });

            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
