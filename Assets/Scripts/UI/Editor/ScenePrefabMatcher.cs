using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TapTapGameJam.EditorTools
{
    /// <summary>Editor-only matching and conversion; scanning never changes the scene.</summary>
    public static class ScenePrefabMatcher
    {
        public const string DefaultFolder = "Assets/Prefabs/Environment/Wall";
        public enum MatchState { Ready, Connected, Review }
        public sealed class Settings
        {
            public Transform Root;
            public string Folder = DefaultFolder;
            public GameObject SinglePrefab;
            public float Tolerance = 0.0001f;
            public bool UnifyInstances = true;
            public bool ReplaceColliders;
            public bool MatchMeshRotations = true;
            internal Settings Copy() => (Settings)MemberwiseClone();
        }
        public sealed class Group
        {
            public GameObject Prefab;
            public string Path;
            public readonly List<GameObject> Samples = new List<GameObject>();
            public readonly List<Match> Matches = new List<Match>();
            internal Geometry Geometry;
            internal Vector3[] Points;
            internal string[] Faces;
        }
        public sealed class Match
        {
            public MeshFilter Filter;
            public Group Group;
            public MatchState State;
            public string Reason, CurrentPrefab;
            public float Error;
            public bool Selected;
            public Quaternion MeshRotation = Quaternion.identity;
            public Vector3 MeshOffset;
        }
        public sealed class ScanResult
        {
            public Scene Scene;
            public Settings Settings;
            public int ScannedObjects, ReadMeshes;
            public readonly List<Group> Groups = new List<Group>();
            public readonly List<Match> Matches = new List<Match>();
            public readonly List<string> Notes = new List<string>();
        }
        public sealed class ApplyResult
        {
            public int Converted, Relinked, SyncedColliders, Renamed, SortedParents;
            public string BackupPath;
            public bool Saved;
        }
        public sealed class OrganizeResult
        {
            public int Renamed, SortedParents;
            public string BackupPath;
            public bool Saved;
        }
        sealed class NamePlan
        {
            public Match Match;
            public string Name;
        }
        sealed class OrderPlan
        {
            public Transform Parent;
            public Transform[] Before, After;
        }
        sealed class Pose
        {
            public Transform Transform, Parent;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
        }
        internal sealed class Geometry
        {
            public UnityEngine.Mesh Mesh;
            public Vector3[] Vertices;
            public int[][] Indices;
            public bool FrameReady;
            public Quaternion Frame;
            public Vector3 Center, Radii;
        }
        sealed class MeshMatch
        {
            public Group Group;
            public float Error;
            public bool Ambiguous;
            public Quaternion Rotation = Quaternion.identity;
            public Vector3 Offset;
        }
        sealed class Before
        {
            public Match Match;
            public UnityEngine.Mesh Mesh;
            public Material[] Materials;
            public Transform Parent;
            public string Name, RendererJson;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
            public int Sibling, GoId, TransformId, FilterId, RendererId;
            public Dictionary<Transform, string> Children;
            public Dictionary<GameObject, string> ChildSettings;
        }
        static void RequireEditMode(Scene scene, Transform root)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("请退出 Play 模式，并等待脚本编译完成。");
            if (!scene.IsValid() || !scene.isLoaded || (root != null && root.gameObject.scene != scene))
                throw new InvalidOperationException("扫描范围必须属于当前打开的场景。");
        }
        static string IndexCounts(UnityEngine.Mesh mesh) => mesh.subMeshCount + "/" +
            string.Join(",", Enumerable.Range(0, mesh.subMeshCount).Select(i => mesh.GetIndexCount(i).ToString()));
        static Geometry Read(UnityEngine.Mesh mesh, Dictionary<UnityEngine.Mesh, Geometry> cache)
        {
            if (cache.TryGetValue(mesh, out var cached)) return cached;
            var value = new Geometry { Mesh = mesh };
            using (var array = MeshUtility.AcquireReadOnlyMeshData(mesh))
            {
                var data = array[0];
                using (var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                { data.GetVertices(vertices); value.Vertices = vertices.ToArray(); }
                value.Indices = new int[data.subMeshCount][];
                for (int s = 0; s < data.subMeshCount; s++)
                {
                    var sub = data.GetSubMesh(s);
                    if (sub.topology != MeshTopology.Triangles || sub.indexCount % 3 != 0)
                        throw new InvalidOperationException("暂不支持非三角面网格：" + mesh.name);
                    using (var indices = new NativeArray<int>(sub.indexCount, Allocator.Temp))
                    { data.GetIndices(indices, s, true); value.Indices[s] = indices.ToArray(); }
                }
            }
            cache.Add(mesh, value); return value;
        }
        static Vector3[] Representatives(Vector3[] vertices, float tolerance)
        {
            var points = new List<Vector3>();
            foreach (var v in vertices.OrderBy(v => v.x).ThenBy(v => v.y).ThenBy(v => v.z))
                if (!points.Any(p => (p - v).sqrMagnitude <= tolerance * tolerance)) points.Add(v);
            return points.ToArray();
        }
        static string[] Faces(Geometry data, Vector3[] points, float tolerance, out float maximumError)
        {
            maximumError = 0; var mapping = new int[data.Vertices.Length];
            for (int i = 0; i < mapping.Length; i++)
            {
                int nearest = -1; float squared = float.MaxValue;
                for (int j = 0; j < points.Length; j++)
                {
                    float delta = (data.Vertices[i] - points[j]).sqrMagnitude;
                    if (delta < squared) { nearest = j; squared = delta; }
                }
                if (nearest < 0 || squared > tolerance * tolerance) return null;
                mapping[i] = nearest; maximumError = Mathf.Max(maximumError, Mathf.Sqrt(squared));
            }
            var result = new string[data.Indices.Length];
            for (int s = 0; s < data.Indices.Length; s++)
            {
                var indices = data.Indices[s]; var triangles = new List<string>();
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int a = mapping[indices[i]], b = mapping[indices[i + 1]], c = mapping[indices[i + 2]];
                    var cycles = new[] { a + "," + b + "," + c, b + "," + c + "," + a, c + "," + a + "," + b };
                    Array.Sort(cycles, StringComparer.Ordinal); triangles.Add(cycles[0]);
                }
                triangles.Sort(StringComparer.Ordinal); result[s] = string.Join(";", triangles);
            }
            return result;
        }
        static bool SameShape(UnityEngine.Mesh mesh, Group group, float tolerance,
            Dictionary<UnityEngine.Mesh, Geometry> cache, out float error)
        {
            error = 0;
            var a = mesh.bounds; var b = group.Geometry.Mesh.bounds;
            if (IndexCounts(mesh) != IndexCounts(group.Geometry.Mesh) ||
                (a.min - b.min).sqrMagnitude > 3 * tolerance * tolerance ||
                (a.max - b.max).sqrMagnitude > 3 * tolerance * tolerance) return false;
            var faces = Faces(Read(mesh, cache), group.Points, tolerance, out error);
            return faces != null && faces.SequenceEqual(group.Faces);
        }
        static void Frame(Geometry data, float tolerance)
        {
            if (data.FrameReady) return;
            var points = Representatives(data.Vertices, Mathf.Min(tolerance, 0.0001f));
            var center = new double[3];
            foreach (var p in points) { center[0] += p.x; center[1] += p.y; center[2] += p.z; }
            for (int i = 0; i < 3; i++) center[i] /= points.Length;
            var covariance = new double[3, 3]; var axes = new double[3, 3];
            for (int i = 0; i < 3; i++) axes[i, i] = 1;
            foreach (var p in points)
            {
                var d = new[] { p.x - center[0], p.y - center[1], p.z - center[2] };
                for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) covariance[i, j] += d[i] * d[j] / points.Length;
            }
            // Symmetric 3x3 Jacobi decomposition. The proper orthonormal frame
            // gives rotation proposals, not an acceptance criterion: every
            // vertex and oriented triangle must still match afterwards.
            for (int step = 0; step < 40; step++)
            {
                int p = 0, q = 1;
                if (Math.Abs(covariance[0, 2]) > Math.Abs(covariance[p, q])) { p = 0; q = 2; }
                if (Math.Abs(covariance[1, 2]) > Math.Abs(covariance[p, q])) { p = 1; q = 2; }
                if (Math.Abs(covariance[p, q]) < 1e-14) break;
                double angle = 0.5 * Math.Atan2(2 * covariance[p, q], covariance[q, q] - covariance[p, p]);
                double c = Math.Cos(angle), s = Math.Sin(angle), pp = covariance[p, p], qq = covariance[q, q], pq = covariance[p, q];
                for (int k = 0; k < 3; k++)
                {
                    if (k != p && k != q)
                    {
                        double kp = covariance[k, p], kq = covariance[k, q];
                        covariance[k, p] = covariance[p, k] = c * kp - s * kq;
                        covariance[k, q] = covariance[q, k] = s * kp + c * kq;
                    }
                    double vp = axes[k, p], vq = axes[k, q]; axes[k, p] = c * vp - s * vq; axes[k, q] = s * vp + c * vq;
                }
                covariance[p, p] = c * c * pp - 2 * c * s * pq + s * s * qq;
                covariance[q, q] = s * s * pp + 2 * c * s * pq + c * c * qq;
                covariance[p, q] = covariance[q, p] = 0;
            }
            var order = Enumerable.Range(0, 3).OrderByDescending(i => covariance[i, i]).ToArray();
            var right = new Vector3((float)axes[0, order[0]], (float)axes[1, order[0]], (float)axes[2, order[0]]).normalized;
            var up = new Vector3((float)axes[0, order[1]], (float)axes[1, order[1]], (float)axes[2, order[1]]).normalized;
            var forward = Vector3.Cross(right, up).normalized; up = Vector3.Cross(forward, right).normalized;
            data.Frame = Quaternion.LookRotation(forward, up);
            data.Center = new Vector3((float)center[0], (float)center[1], (float)center[2]);
            data.Radii = new Vector3((float)Math.Sqrt(Math.Max(0, covariance[order[0], order[0]])),
                (float)Math.Sqrt(Math.Max(0, covariance[order[1], order[1]])), (float)Math.Sqrt(Math.Max(0, covariance[order[2], order[2]])));
            data.FrameReady = true;
        }
        static bool TryShape(UnityEngine.Mesh mesh, Group group, Settings settings, Dictionary<UnityEngine.Mesh, Geometry> cache,
            out float error, out Quaternion rotation, out Vector3 offset)
        {
            rotation = Quaternion.identity; offset = Vector3.zero;
            if (SameShape(mesh, group, settings.Tolerance, cache, out error)) return true;
            if (!settings.MatchMeshRotations || IndexCounts(mesh) != IndexCounts(group.Geometry.Mesh)) return false;
            var candidate = Read(mesh, cache); var sample = group.Geometry;
            Frame(candidate, settings.Tolerance); Frame(sample, settings.Tolerance);
            if ((candidate.Radii - sample.Radii).magnitude > settings.Tolerance * 3) return false;
            foreach (var signs in new[] { Quaternion.identity, new Quaternion(1, 0, 0, 0), new Quaternion(0, 1, 0, 0), new Quaternion(0, 0, 1, 0) })
            {
                var proposed = candidate.Frame * signs * Quaternion.Inverse(sample.Frame);
                var translation = candidate.Center - proposed * sample.Center; var inverse = Quaternion.Inverse(proposed);
                var aligned = new Geometry { Mesh = mesh, Indices = candidate.Indices,
                    Vertices = candidate.Vertices.Select(v => inverse * (v - translation)).ToArray() };
                var faces = Faces(aligned, group.Points, settings.Tolerance, out var distance);
                if (faces == null || !faces.SequenceEqual(group.Faces)) continue;
                rotation = proposed; offset = translation; error = distance; return true;
            }
            return false;
        }
        static bool ValidSample(GameObject asset, out string reason)
        {
            reason = null;
            if (asset == null || !AssetDatabase.Contains(asset) || PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.Regular)
                reason = "请选择普通 .prefab 资产。";
            else if (asset.GetComponent<MeshFilter>()?.sharedMesh == null || asset.GetComponent<MeshRenderer>() == null)
                reason = "模型的 MeshFilter 和 MeshRenderer 需放在预制体根节点；子节点可放 Collider。";
            else if (asset.GetComponent<MeshFilter>().sharedMesh.vertexCount == 0 || asset.GetComponent<MeshFilter>().sharedMesh.subMeshCount == 0)
                reason = "样本网格为空。";
            else if (asset.GetComponents<Component>().Any(c => c == null || (!(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer) && !(c is Collider))))
                reason = "暂不支持带脚本或其他行为组件的样本。";
            else if (!CollisionOnlyChildren(asset))
                reason = "预制体子物体需为纯 Transform 或 Collider，暂不支持子网格、脚本或嵌套预制体。";
            else if (asset.GetComponentsInChildren<Transform>(true).GroupBy(t => RelativePath(t, asset.transform)).Any(g => g.Count() > 1))
                reason = "预制体有同名的同级子物体，请先设置不同名称。";
            return reason == null;
        }
        static MeshFilter[] Filters(Scene scene, Transform root) => root != null
            ? root.GetComponentsInChildren<MeshFilter>(true)
            : scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true)).ToArray();
        static bool ColliderOverrides(GameObject go, GameObject asset = null, Quaternion? rotation = null, Vector3? offset = null)
        {
            var mods = PrefabUtility.GetPropertyModifications(go);
            return (mods != null && mods.Any(m => m.target is Collider)) ||
                go.GetComponentsInChildren<Collider>(true).Any(PrefabUtility.IsAddedComponentOverride) ||
                (asset != null && (go.GetComponentsInChildren<Collider>(true).Length != asset.GetComponentsInChildren<Collider>(true).Length ||
                    asset.GetComponentsInChildren<Transform>(true).Where(t => t != asset.transform).Any(source =>
                    {
                        var instance = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => PrefabUtility.GetCorrespondingObjectFromSource(t) == source);
                        ChildPose(source, asset, rotation ?? Quaternion.identity, offset ?? Vector3.zero, out var position, out var orientation);
                        return instance == null || (instance.localPosition - position).sqrMagnitude > 1e-10f || Quaternion.Angle(instance.localRotation, orientation) > 0.01f ||
                            !instance.localScale.Equals(source.localScale) || instance.gameObject.activeSelf != source.gameObject.activeSelf ||
                            instance.gameObject.layer != source.gameObject.layer || instance.tag != source.tag;
                    })));
        }
        static bool CollisionOnlyChildren(GameObject go)
        {
            foreach (var child in go.GetComponentsInChildren<Transform>(true).Where(t => t != go.transform))
            {
                if (child.GetComponents<Component>().Any(c => c == null || (!(c is Transform) && !(c is Collider)))) return false;
                if (PrefabUtility.GetNearestPrefabInstanceRoot(child.gameObject) == child.gameObject) return false;
            }
            return true;
        }
        static void Classify(Match match, Settings settings, HashSet<string> samplePaths)
        {
            var go = match.Filter.gameObject; bool linked = PrefabUtility.IsPartOfPrefabInstance(go);
            match.CurrentPrefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if ((Quaternion.Angle(match.MeshRotation, Quaternion.identity) > 0.01f || match.MeshOffset.magnitude > settings.Tolerance) && match.Group.Prefab.GetComponents<Collider>().Length > 0)
            { match.State = MatchState.Review; match.Reason = "网格内部方向不同：请把预制体的 Collider 放在子物体上，才能单独补偿角度。"; return; }
            if (go.GetComponents<Component>().Any(c => c == null ||
                (!(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer) && !(c is Collider))))
            { match.State = MatchState.Review; match.Reason = "根物体带脚本或其他行为组件，保留原物体。"; return; }
            if (!CollisionOnlyChildren(go))
            { match.State = MatchState.Review; match.Reason = "子物体带网格、脚本、其他行为组件或独立预制体，保留原层级。"; return; }
            bool childColliders = go.GetComponentsInChildren<Collider>(true).Any(c => c.transform != go.transform);
            if (childColliders && !settings.ReplaceColliders && !(linked && match.CurrentPrefab == match.Group.Path))
            { match.State = MatchState.Review; match.Reason = "子物体中已有 Collider；勾选“使用预制体碰撞体”后可替换。"; return; }
            if (linked && (PrefabUtility.GetNearestPrefabInstanceRoot(go) != go || PrefabUtility.GetOutermostPrefabInstanceRoot(go) != go))
            { match.State = MatchState.Review; match.Reason = "属于嵌套预制体，保留原连接。"; return; }
            if (linked && match.CurrentPrefab == match.Group.Path)
            {
                if (settings.ReplaceColliders && ColliderOverrides(go, match.Group.Prefab, match.MeshRotation, match.MeshOffset))
                { match.State = MatchState.Ready; match.Reason = "还原 Collider，继承预制体配置。"; }
                else
                { match.State = MatchState.Connected; match.Reason = ColliderOverrides(go, match.Group.Prefab, match.MeshRotation, match.MeshOffset) ? "已连接；Collider 有独立覆盖。" : "已连接到对应预制体。"; }
                return;
            }
            if (linked && (!settings.UnifyInstances || !samplePaths.Contains(match.CurrentPrefab)))
            { match.State = MatchState.Review; match.Reason = "已连接到其他预制体，保留原连接。"; return; }
            if (!linked && go.GetComponents<Collider>().Length > 0 && !settings.ReplaceColliders)
            { match.State = MatchState.Review; match.Reason = "已有 Collider；勾选“使用预制体碰撞体”后可替换。"; return; }
            match.State = MatchState.Ready; match.Selected = true;
            match.Reason = linked ? "统一连接，保留原外观与 Transform。" : "连接预制体，保留原外观与 Transform。";
        }
        public static ScanResult Scan(Scene scene, Settings settings, bool showProgress = false)
        {
            RequireEditMode(scene, settings.Root);
            if (float.IsNaN(settings.Tolerance) || float.IsInfinity(settings.Tolerance) || settings.Tolerance <= 0)
                throw new ArgumentException("匹配容差必须为正数。");
            var result = new ScanResult { Scene = scene, Settings = settings.Copy() };
            var filters = Filters(scene, settings.Root); result.ScannedObjects = filters.Length;
            var cache = new Dictionary<UnityEngine.Mesh, Geometry>();
            var paths = settings.SinglePrefab != null ? new[] { AssetDatabase.GetAssetPath(settings.SinglePrefab) }
                : AssetDatabase.IsValidFolder(settings.Folder)
                    ? AssetDatabase.FindAssets("t:Prefab", new[] { settings.Folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToArray()
                    : Array.Empty<string>();
            var samplePaths = new HashSet<string>();
            try
            {
                foreach (var path in paths)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (!ValidSample(asset, out var reason)) { result.Notes.Add(path + "：" + reason); continue; }
                    var mesh = asset.GetComponent<MeshFilter>().sharedMesh;
                    try
                    {
                        var existing = result.Groups.FirstOrDefault(g => TryShape(mesh, g, settings, cache, out var unused, out var unusedRotation, out var unusedOffset));
                        if (existing != null) existing.Samples.Add(asset);
                        else
                        {
                            var data = Read(mesh, cache); var points = Representatives(data.Vertices, Mathf.Min(settings.Tolerance, 0.0001f));
                            var group = new Group { Geometry = data, Points = points, Faces = Faces(data, points, settings.Tolerance, out var error) };
                            group.Samples.Add(asset); result.Groups.Add(group);
                        }
                        samplePaths.Add(path);
                    }
                    catch (InvalidOperationException ex) { result.Notes.Add(path + "：" + ex.Message); }
                }
                // Choose the most-used member, so adding duplicate samples does not
                // cause established instances to be arbitrarily reconnected.
                var usage = filters.Where(f => f != null).Select(f => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(f.gameObject))
                    .Where(p => !string.IsNullOrEmpty(p)).GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
                foreach (var group in result.Groups)
                {
                    group.Prefab = group.Samples.OrderByDescending(a => usage.TryGetValue(AssetDatabase.GetAssetPath(a), out var count) ? count : 0)
                        .ThenBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal).First();
                    group.Path = AssetDatabase.GetAssetPath(group.Prefab);
                    group.Geometry = Read(group.Prefab.GetComponent<MeshFilter>().sharedMesh, cache);
                    group.Points = Representatives(group.Geometry.Vertices, Mathf.Min(settings.Tolerance, 0.0001f));
                    group.Faces = Faces(group.Geometry, group.Points, settings.Tolerance, out var canonicalError);
                }
                var buckets = result.Groups.GroupBy(g => IndexCounts(g.Geometry.Mesh)).ToDictionary(g => g.Key, g => g.ToArray());
                var assignments = new Dictionary<UnityEngine.Mesh, MeshMatch>();
                for (int i = 0; i < filters.Length; i++)
                {
                    if (showProgress && i % 128 == 0 && EditorUtility.DisplayCancelableProgressBar("匹配场景物体", i + " / " + filters.Length, (float)i / Math.Max(1, filters.Length)))
                        throw new OperationCanceledException("扫描已取消，场景没有改动。");
                    var filter = filters[i]; var mesh = filter.sharedMesh;
                    if (mesh == null || filter.GetComponent<MeshRenderer>() == null) continue;
                    if (!assignments.TryGetValue(mesh, out var assignment))
                    {
                        var matches = new List<MeshMatch>();
                        if (buckets.TryGetValue(IndexCounts(mesh), out var choices))
                            foreach (var group in choices)
                            {
                                try { if (TryShape(mesh, group, settings, cache, out var delta, out var rotation, out var offset))
                                    matches.Add(new MeshMatch { Group = group, Error = delta, Rotation = rotation, Offset = offset }); }
                                catch (InvalidOperationException ex) { result.Notes.Add(filter.name + "：" + ex.Message); break; }
                            }
                        assignment = matches.Count == 1 ? matches[0] : new MeshMatch { Ambiguous = matches.Count > 1 };
                        assignments.Add(mesh, assignment);
                    }
                    if (assignment.Group == null)
                    {
                        if (assignment.Ambiguous) result.Notes.Add(filter.name + "：同时匹配多个类型，已跳过。");
                        continue;
                    }
                    var match = new Match { Filter = filter, Group = assignment.Group, Error = assignment.Error,
                        MeshRotation = assignment.Rotation, MeshOffset = assignment.Offset };
                    Classify(match, settings, samplePaths); match.Selected = match.State == MatchState.Ready;
                    match.Group.Matches.Add(match); result.Matches.Add(match);
                }
                result.ReadMeshes = cache.Count; return result;
            }
            finally { if (showProgress) EditorUtility.ClearProgressBar(); }
        }
        static string NormalizedJson(Component component)
        {
            var json = EditorJsonUtility.ToJson(component);
            foreach (var property in new[] { "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject" })
            {
                var token = "\"" + property + "\":{"; int start = json.IndexOf(token, StringComparison.Ordinal);
                if (start < 0) continue;
                int end = json.IndexOf('}', start + token.Length);
                if (end >= 0) json = json.Substring(0, start) + token + "}" + json.Substring(end + 1);
            }
            return json;
        }
        static Before Capture(Match match)
        {
            var f = match.Filter; var t = f.transform; var r = f.GetComponent<MeshRenderer>();
            return new Before { Match = match, Mesh = f.sharedMesh, Materials = r.sharedMaterials, RendererJson = NormalizedJson(r),
                Parent = t.parent, Name = t.name, Position = t.localPosition, Rotation = t.localRotation, Scale = t.localScale,
                Sibling = t.GetSiblingIndex(), GoId = f.gameObject.GetInstanceID(), TransformId = t.GetInstanceID(), FilterId = f.GetInstanceID(), RendererId = r.GetInstanceID(),
                Children = t.GetComponentsInChildren<Transform>(true).Where(child => child != t).ToDictionary(child => child, child => NormalizedJson(child)),
                ChildSettings = t.GetComponentsInChildren<Transform>(true).Where(child => child != t).Select(child => child.gameObject).ToDictionary(go => go, ChildSettings) };
        }
        static string ChildSettings(GameObject go) => go.name + "\n" + go.tag + "\n" + go.layer + "\n" + go.activeSelf + "\n" + go.hideFlags;
        static string RelativePath(Transform t, Transform root) => AnimationUtility.CalculateTransformPath(t, root);
        static Transform SourceChild(Transform instance, GameObject asset)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            return source != null && source != asset.transform && source.IsChildOf(asset.transform) ? source : null;
        }
        static void ChildPose(Transform source, GameObject asset, Quaternion rotation, Vector3 offset, out Vector3 position, out Quaternion orientation)
        {
            bool firstLevel = source.parent == asset.transform;
            position = firstLevel ? offset + rotation * source.localPosition : source.localPosition;
            orientation = firstLevel ? rotation * source.localRotation : source.localRotation;
        }
        static void SyncColliderHierarchy(GameObject go, GameObject asset, Match match)
        {
            var sources = asset.GetComponentsInChildren<Collider>(true);
            foreach (var removed in PrefabUtility.GetRemovedComponents(go).Where(r => r.assetComponent is Collider && sources.Contains(r.assetComponent)))
                PrefabUtility.RevertRemovedComponent(removed.containingInstanceGameObject, removed.assetComponent, InteractionMode.UserAction);
            foreach (var child in go.GetComponentsInChildren<Transform>(true).Where(t => t != go.transform))
            {
                var source = SourceChild(child, asset); if (source == null) continue;
                var transform = new SerializedObject(child);
                foreach (var path in new[] { "m_LocalPosition", "m_LocalRotation", "m_LocalScale", "m_LocalEulerAnglesHint" })
                {
                    var property = transform.FindProperty(path);
                    if (property != null) PrefabUtility.RevertPropertyOverride(property, InteractionMode.UserAction);
                    transform.Update();
                }
                ChildPose(source, asset, match.MeshRotation, match.MeshOffset, out var position, out var orientation);
                if ((child.localPosition - position).sqrMagnitude > 1e-12f || Quaternion.Angle(child.localRotation, orientation) > 0.001f)
                {
                    Undo.RecordObject(child, "补偿网格内部旋转"); child.localPosition = position; child.localRotation = orientation;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                }
                var settings = new SerializedObject(child.gameObject);
                foreach (var path in new[] { "m_IsActive", "m_Layer", "m_TagString", "m_StaticEditorFlags" })
                {
                    var property = settings.FindProperty(path);
                    if (property != null) PrefabUtility.RevertPropertyOverride(property, InteractionMode.UserAction);
                    settings.Update();
                }
            }
            foreach (var collider in go.GetComponentsInChildren<Collider>(true))
                if (sources.Contains(PrefabUtility.GetCorrespondingObjectFromSource(collider)))
                    PrefabUtility.RevertObjectOverride(collider, InteractionMode.UserAction);
            var mods = PrefabUtility.GetPropertyModifications(go);
            if (mods != null && mods.Any(m => m.target is Collider))
                PrefabUtility.SetPropertyModifications(go, mods.Where(m => !(m.target is Collider)).ToArray());
        }
        static void Verify(Before old)
        {
            var f = old.Match.Filter; var go = f.gameObject; var t = f.transform; var renderer = go.GetComponent<MeshRenderer>();
            if (go.GetInstanceID() != old.GoId || t.GetInstanceID() != old.TransformId || f.GetInstanceID() != old.FilterId || renderer.GetInstanceID() != old.RendererId)
                throw new InvalidOperationException("物体或组件引用发生变化：" + old.Name);
            if (t.parent != old.Parent || t.name != old.Name || t.GetSiblingIndex() != old.Sibling || !t.localPosition.Equals(old.Position) || !t.localRotation.Equals(old.Rotation) || !t.localScale.Equals(old.Scale))
                throw new InvalidOperationException("Transform 发生变化：" + old.Name);
            if (f.sharedMesh != old.Mesh || !renderer.sharedMaterials.SequenceEqual(old.Materials) || NormalizedJson(renderer) != old.RendererJson)
                throw new InvalidOperationException("网格、材质或渲染设置发生变化：" + old.Name);
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) != old.Match.Group.Path)
                throw new InvalidOperationException("预制体连接异常：" + old.Name);
            var asset = old.Match.Group.Prefab;
            foreach (var child in old.Children)
                if (child.Key == null || (SourceChild(child.Key, asset) == null && NormalizedJson(child.Key) != child.Value))
                    throw new InvalidOperationException("原子物体引用或 Transform 发生变化：" + old.Name);
            foreach (var child in old.ChildSettings)
                if (child.Key == null || (SourceChild(child.Key.transform, asset) == null && ChildSettings(child.Key) != child.Value))
                    throw new InvalidOperationException("原子物体设置发生变化：" + old.Name);
            var expectedChildren = asset.GetComponentsInChildren<Transform>(true).Where(child => child != asset.transform).ToArray();
            foreach (var source in expectedChildren)
            {
                var child = t.GetComponentsInChildren<Transform>(true).SingleOrDefault(node => SourceChild(node, asset) == source);
                ChildPose(source, asset, old.Match.MeshRotation, old.Match.MeshOffset, out var position, out var orientation);
                if (child == null || (child.localPosition - position).sqrMagnitude > 1e-10f || Quaternion.Angle(child.localRotation, orientation) > 0.01f ||
                    !child.localScale.Equals(source.localScale) || child.gameObject.activeSelf != source.gameObject.activeSelf || child.gameObject.layer != source.gameObject.layer || child.tag != source.tag)
                    throw new InvalidOperationException("预制体子 Collider 的位置、旋转或缩放未正确继承：" + old.Name);
            }
            if (t.GetComponentsInChildren<Transform>(true).Any(child => child != t && !old.Children.ContainsKey(child) && SourceChild(child, asset) == null))
                throw new InvalidOperationException("出现了非预制体定义的新子物体：" + old.Name);
            var sources = asset.GetComponentsInChildren<Collider>(true); var colliders = go.GetComponentsInChildren<Collider>(true);
            if (colliders.Length != sources.Length || ColliderOverrides(go))
                throw new InvalidOperationException("Collider 未正确继承或仍有子碰撞体：" + old.Name);
            for (int i = 0; i < colliders.Length; i++)
                if (!sources.Contains(PrefabUtility.GetCorrespondingObjectFromSource(colliders[i])) ||
                    NormalizedJson(colliders[i]) != NormalizedJson(PrefabUtility.GetCorrespondingObjectFromSource(colliders[i])))
                    throw new InvalidOperationException("Collider 配置与预制体不一致：" + old.Name);
        }
        public static bool CanOrganize(Match match)
        {
            if (match.Filter == null || match.CurrentPrefab != match.Group.Path) return false;
            var go = match.Filter.gameObject;
            return PrefabUtility.GetNearestPrefabInstanceRoot(go) == go && PrefabUtility.GetOutermostPrefabInstanceRoot(go) == go;
        }
        static int ExistingNumber(Match match)
        {
            string prefix = match.Group.Prefab.name + "_";
            string name = match.Filter.name;
            return name.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(name.Substring(prefix.Length), out int number) && number > 0
                ? number : int.MaxValue;
        }
        static string HierarchyOrder(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Add(t.GetSiblingIndex().ToString("D10")); t = t.parent; }
            parts.Reverse(); return string.Join("/", parts);
        }
        static Transform[] Children(Scene scene, Transform parent) => parent != null
            ? parent.Cast<Transform>().ToArray()
            : scene.GetRootGameObjects().Select(g => g.transform).OrderBy(t => t.GetSiblingIndex()).ToArray();
        static string Backup(Scene scene)
        {
            var directory = "RecoverySnapshots/PrefabMatcher/" + DateTime.UtcNow.AddHours(8).ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            Directory.CreateDirectory(directory); string path = directory + "/Scene_before.unity";
            if (!EditorSceneManager.SaveScene(scene, path, true)) throw new InvalidOperationException("场景备份失败，尚未修改。");
            return path;
        }
        public static OrganizeResult Organize(ScanResult preview, bool createBackup = true, bool saveScene = false)
        {
            RequireEditMode(preview.Scene, preview.Settings.Root);
            var fresh = Scan(preview.Scene, preview.Settings);
            var targets = fresh.Matches.Where(CanOrganize).ToArray();
            var names = new List<NamePlan>(); var orders = new List<OrderPlan>();
            var groups = targets.GroupBy(m => m.Group.Path).ToArray();
            if (groups.GroupBy(g => g.First().Group.Prefab.name, StringComparer.Ordinal).Any(g => g.Count() > 1))
                throw new InvalidOperationException("不同类型的预制体名称相同，请先为预制体设置不同名称，再编号。");
            foreach (var group in groups)
            {
                int number = 1;
                foreach (var match in group.OrderBy(ExistingNumber).ThenBy(m => HierarchyOrder(m.Filter.transform), StringComparer.Ordinal))
                    names.Add(new NamePlan { Match = match, Name = match.Group.Prefab.name + "_" + number++ });
            }
            var byTransform = names.ToDictionary(n => n.Match.Filter.transform);
            foreach (var siblings in names.GroupBy(n => n.Match.Filter.transform.parent))
            {
                var before = Children(preview.Scene, siblings.Key);
                var matched = new HashSet<Transform>(siblings.Select(n => n.Match.Filter.transform));
                var untouchedNames = new HashSet<string>(before.Where(t => !matched.Contains(t)).Select(t => t.name), StringComparer.Ordinal);
                if (siblings.Any(n => untouchedNames.Contains(n.Name)))
                    throw new InvalidOperationException("新名称与未匹配的同级物体重名。请调整扫描范围或先修改冲突名称。");
                var sorted = matched.OrderBy(t => byTransform[t].Name, Comparer<string>.Create(EditorUtility.NaturalCompare)).ToArray();
                // Keep unmatched siblings in their original relative order and
                // collect matched instances at the end of their existing parent.
                var after = before.Where(t => !matched.Contains(t)).Concat(sorted).ToArray();
                if (!before.SequenceEqual(after)) orders.Add(new OrderPlan { Parent = siblings.Key, Before = before, After = after });
            }
            var outcome = new OrganizeResult { Renamed = names.Count(n => n.Match.Filter.name != n.Name), SortedParents = orders.Count };
            if (outcome.Renamed == 0 && outcome.SortedParents == 0) return outcome;
            var all = preview.Scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var poses = all.Select(t => new Pose { Transform = t, Parent = t.parent, Position = t.localPosition, Rotation = t.localRotation, Scale = t.localScale }).ToArray();
            var components = all.SelectMany(t => t.GetComponents<Component>()).Where(c => c != null && !(c is Transform))
                .ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
            if (createBackup) outcome.BackupPath = Backup(preview.Scene);
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("预制体实例编号与 Hierarchy 排序");
            try
            {
                // Complete Transform undo also captures prefab instance data.
                // Record it BEFORE renaming, otherwise undo can restore the new
                // name overrides along with the original sibling order.
                foreach (var order in orders)
                {
                    var recorded = order.Parent != null ? order.Before.Concat(new[] { order.Parent }) : order.Before;
                    Undo.RegisterCompleteObjectUndo(recorded.Cast<UnityEngine.Object>().ToArray(), "Hierarchy 排序");
                }
                foreach (var order in orders)
                {
                    foreach (var child in order.After.Where(byTransform.ContainsKey))
                        if (child.GetSiblingIndex() != order.After.Length - 1)
                            Undo.SetSiblingIndex(child, order.After.Length - 1, "Hierarchy 排序");
                }
                foreach (var name in names.Where(n => n.Match.Filter.name != n.Name))
                {
                    var go = name.Match.Filter.gameObject; Undo.RecordObject(go, "预制体实例编号"); go.name = name.Name;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                }
                foreach (var pose in poses)
                    if (pose.Transform == null || pose.Transform.parent != pose.Parent || !pose.Transform.localPosition.Equals(pose.Position) ||
                        !pose.Transform.localRotation.Equals(pose.Rotation) || !pose.Transform.localScale.Equals(pose.Scale))
                        throw new InvalidOperationException("编号或排序改变了物体的父级或空间变换，操作已撤回。");
                foreach (var pair in components)
                    if (pair.Key == null || EditorJsonUtility.ToJson(pair.Key) != pair.Value)
                        throw new InvalidOperationException("编号或排序改变了组件设置，操作已撤回。");
                if (names.Any(n => n.Match.Filter.name != n.Name) || orders.Any(o => !Children(preview.Scene, o.Parent).SequenceEqual(o.After)))
                    throw new InvalidOperationException("编号或 Hierarchy 排序未完成，操作已撤回。");
                Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(preview.Scene);
                if (saveScene)
                {
                    if (string.IsNullOrEmpty(preview.Scene.path) || !EditorSceneManager.SaveScene(preview.Scene)) throw new InvalidOperationException("场景保存失败。");
                    outcome.Saved = true;
                }
                return outcome;
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
        public static ApplyResult Apply(ScanResult preview, IEnumerable<Match> selected, bool createBackup = true, bool saveScene = false, bool organize = false)
        {
            RequireEditMode(preview.Scene, preview.Settings.Root);
            var requested = selected.Where(m => m.Selected && m.State == MatchState.Ready).ToArray();
            var fresh = Scan(preview.Scene, preview.Settings);
            var current = fresh.Matches.Where(m => m.State == MatchState.Ready).ToDictionary(m => m.Filter);
            var plan = new List<Before>();
            foreach (var match in requested)
            {
                if (match.Filter == null || !current.TryGetValue(match.Filter, out var valid) || valid.Group.Path != match.Group.Path)
                    throw new InvalidOperationException("扫描后物体或样本发生变化，请重新扫描。");
                plan.Add(Capture(valid));
            }
            var outcome = new ApplyResult(); if (plan.Count == 0) return outcome;
            var changed = new HashSet<Transform>(plan.Select(p => p.Match.Filter.transform));
            var childTransforms = new HashSet<Transform>(plan.SelectMany(p => p.Children.Keys));
            var removedChildColliders = new HashSet<Collider>(plan.SelectMany(p => p.Match.Filter.GetComponentsInChildren<Collider>(true))
                .Where(c => childTransforms.Contains(c.transform)));
            var untouched = preview.Scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true))
                .Where(c => c != null && !changed.Contains(c.transform) && !(c is Transform child && childTransforms.Contains(child)) &&
                    !(c is Collider collider && removedChildColliders.Contains(collider))).ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
            if (createBackup)
            {
                outcome.BackupPath = Backup(preview.Scene);
            }
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("批量连接相同物体到预制体");
            try
            {
                foreach (var old in plan)
                {
                    var match = old.Match; var go = match.Filter.gameObject; var asset = match.Group.Prefab;
                    bool linked = PrefabUtility.IsPartOfPrefabInstance(go); bool same = linked && match.CurrentPrefab == match.Group.Path;
                    if (preview.Settings.ReplaceColliders)
                    {
                        // Preserve collision helper GameObjects/Transforms and
                        // remove only their colliders. Added-child overrides keep
                        // anchors and any references to those nodes intact.
                        var definitions = asset.GetComponentsInChildren<Collider>(true)
                            .GroupBy(c => RelativePath(c.transform, asset.transform))
                            .ToDictionary(g => g.Key, g => g.GroupBy(c => c.GetType()).ToDictionary(types => types.Key, types => types.Count()));
                        foreach (var collider in go.GetComponentsInChildren<Collider>(true))
                        {
                            var source = SourceChild(collider.transform, asset);
                            string path = RelativePath(source != null ? source : collider.transform, source != null ? asset.transform : go.transform);
                            var type = collider.GetType();
                            if (definitions.TryGetValue(path, out var remaining) && remaining.TryGetValue(type, out int available) && available > 0)
                                remaining[type] = available - 1;
                            else Undo.DestroyObjectImmediate(collider);
                        }
                    }
                    if (same) outcome.SyncedColliders++;
                    else if (linked)
                    {
                        PrefabUtility.ReplacePrefabAssetOfPrefabInstance(go, asset, new PrefabReplacingSettings { objectMatchMode = ObjectMatchMode.ByHierarchy,
                            prefabOverridesOptions = PrefabOverridesOptions.KeepAllPossibleOverrides, changeRootNameToAssetName = false, logInfo = false }, InteractionMode.UserAction);
                        outcome.Relinked++;
                    }
                    else
                    {
                        PrefabUtility.ConvertToPrefabInstance(go, asset, new ConvertToPrefabInstanceSettings { objectMatchMode = ObjectMatchMode.ByHierarchy,
                            recordPropertyOverridesOfMatches = true, componentsNotMatchedBecomesOverride = true, gameObjectsNotMatchedBecomesOverride = true,
                            changeRootNameToAssetName = false, logInfo = false }, InteractionMode.UserAction);
                        outcome.Converted++;
                    }
                    var t = go.transform; if (t.parent != old.Parent) Undo.SetTransformParent(t, old.Parent, "保留父级");
                    Undo.RecordObject(go, "保留名称"); Undo.RecordObject(t, "保留 Transform"); go.name = old.Name;
                    if (!t.localPosition.Equals(old.Position)) t.localPosition = old.Position;
                    if (!t.localRotation.Equals(old.Rotation)) t.localRotation = old.Rotation;
                    if (!t.localScale.Equals(old.Scale)) t.localScale = old.Scale;
                    if (t.GetSiblingIndex() != old.Sibling) t.SetSiblingIndex(old.Sibling);
                    var renderer = go.GetComponent<MeshRenderer>();
                    Undo.RecordObject(match.Filter, "保留网格"); Undo.RecordObject(renderer, "保留材质");
                    match.Filter.sharedMesh = old.Mesh; renderer.sharedMaterials = old.Materials;
                    foreach (var component in new UnityEngine.Object[] { go, t, match.Filter, renderer }) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                    SyncColliderHierarchy(go, asset, match);
                    Verify(old);
                }
                foreach (var pair in untouched)
                    if (pair.Key == null || EditorJsonUtility.ToJson(pair.Key) != pair.Value) throw new InvalidOperationException("未选择的物体发生变化，操作已撤回。");
                if (organize)
                {
                    var organized = Organize(preview, false, false);
                    outcome.Renamed = organized.Renamed; outcome.SortedParents = organized.SortedParents;
                }
                Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(preview.Scene);
                if (saveScene)
                {
                    if (string.IsNullOrEmpty(preview.Scene.path) || !EditorSceneManager.SaveScene(preview.Scene)) throw new InvalidOperationException("场景保存失败。");
                    outcome.Saved = true;
                }
                return outcome;
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
    }
}
