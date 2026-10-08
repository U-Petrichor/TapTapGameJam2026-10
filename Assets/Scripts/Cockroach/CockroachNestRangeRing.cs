using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 程序化生成一个"虚线圆环"网格，铺在 XZ 平面上、圆心在原点，用于画巢穴的活动范围。
    ///
    /// 虚线是一圈小四边形拼出来的：把整个圆按段数等分，每段里前 60% 画实线、剩下 40% 留空。
    /// 段数由 <see cref="DashesFor"/> 按"每段实线长约 targetDashLength"反推，
    /// 所以半径变大时段数跟着变多，每段看起来还是差不多长。
    /// </summary>
    public static class CockroachNestRangeRing
    {
        /// <summary>网格名</summary>
        public const string MeshName = "NestRangeRing";

        /// <summary>URP 自带的 Unlit shader 名</summary>
        public const string UnlitShaderName = "Universal Render Pipeline/Unlit";

        /// <summary>段数下限，保证小半径也能看出虚线</summary>
        private const int MinDashCount = 8;

        /// <summary>段数上限，避免半径特别大时顶点数失控</summary>
        private const int MaxDashCount = 512;

        /// <summary>
        /// 按半径反推虚线段数：让每段实线的弧长约等于 targetDashLength。
        /// 半径变大 → 圈变长 → 段数跟着变多，视觉上每段还是一样长。
        /// </summary>
        /// <param name="radius">半径</param>
        /// <param name="targetDashLength">期望的单段实线弧长（世界单位）</param>
        public static int DashesFor(float radius, float targetDashLength)
        {
            radius = Mathf.Max(0.0001f, radius);
            targetDashLength = Mathf.Max(0.0001f, targetDashLength);

            float circumference = 2f * Mathf.PI * radius;

            // 每段 60% 是实线，所以"段数 × targetDashLength"对应的是 60% 的周长
            int count = Mathf.RoundToInt(circumference * 0.6f / targetDashLength);
            return Mathf.Clamp(count, MinDashCount, MaxDashCount);
        }

        /// <summary>
        /// 建一个半透明的 URP Unlit 材质。调用方负责 Destroy。
        /// </summary>
        public static Material CreateUnlitMaterial(Color color)
        {
            Shader shader = Shader.Find(UnlitShaderName);
            if (shader == null)
            {
                Debug.LogError($"[{nameof(CockroachNestRangeRing)}]：找不到 shader 「{UnlitShaderName}」");
                return null;
            }

            var material = new Material(shader) { name = "NestRangeRing (Runtime)" };

            // URP Unlit 的表面/混合/剔除开关，缺一个就可能变成不透明或被背面剔除
            material.SetFloat("_Surface", 1f);          // 0 = Opaque, 1 = Transparent
            material.SetFloat("_Blend", 0f);            // 0 = Alpha
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);           // 半透明不写深度
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            material.SetColor("_Color", color);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        /// <summary>
        /// 生成圆环网格。调用方负责在不需要时 Destroy 掉这个 Mesh。
        /// </summary>
        /// <param name="radius">半径</param>
        /// <param name="width">线宽</param>
        /// <param name="dashCount">把整个圆等分成多少段，每段含一段实线和一段空隙</param>
        public static Mesh Build(float radius, float width, int dashCount)
        {
            radius = Mathf.Max(0.0001f, radius);
            width = Mathf.Max(0.0001f, width);
            dashCount = Mathf.Clamp(dashCount, MinDashCount, MaxDashCount);

            // 每个虚线段用 4 个顶点拼一个小四边形（2 个三角形）
            const int vertsPerDash = 4;

            var vertices = new List<Vector3>(dashCount * vertsPerDash);
            var uvs = new List<Vector2>(dashCount * vertsPerDash);
            var triangles = new List<int>(dashCount * 6);

            float halfWidth = width * 0.5f;
            float inner = radius - halfWidth;
            float outer = radius + halfWidth;

            // 每段只画 60%，剩下 40% 是虚线间隙
            float dashSpan = Mathf.PI * 2f / dashCount;
            float fillSpan = dashSpan * 0.6f;

            for (int i = 0; i < dashCount; i++)
            {
                float a0 = i * dashSpan;
                float a1 = a0 + fillSpan;

                // 环绕高度：按弧长给 v，让贴图/渐变能沿圆周连续
                float v0 = (float)i / dashCount;
                float v1 = v0 + 0.6f / dashCount;

                int b = vertices.Count;

                // 四边形顶点顺序：内起点、外起点、内终点、外终点
                AddVertex(vertices, uvs, a0, inner, outer, 0f, v0);
                AddVertex(vertices, uvs, a0, inner, outer, 1f, v0);
                AddVertex(vertices, uvs, a1, inner, outer, 0f, v1);
                AddVertex(vertices, uvs, a1, inner, outer, 1f, v1);

                // 两个三角形。绕序朝下（法线是 -Y），从上方俯视才可见。
                triangles.Add(b + 0);
                triangles.Add(b + 3);
                triangles.Add(b + 1);

                triangles.Add(b + 0);
                triangles.Add(b + 2);
                triangles.Add(b + 3);
            }

            Mesh mesh = new Mesh { name = MeshName };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);

            // 统一给 -Y 法线：Unlit 不用光照，但有了它材质就不会因为缺法线而报错
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.down;
            }

            mesh.SetNormals(normals);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddVertex(
            List<Vector3> vertices,
            List<Vector2> uvs,
            float angle,
            float inner,
            float outer,
            float u,
            float v)
        {
            // Unity 里 Y 是上，所以范围圆画在 XZ 平面上：方向 = (sin a, 0, cos a)
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);
            float r = Mathf.Lerp(inner, outer, u);

            vertices.Add(new Vector3(sin * r, 0f, cos * r));
            uvs.Add(new Vector2(u, v));
        }
    }
}
