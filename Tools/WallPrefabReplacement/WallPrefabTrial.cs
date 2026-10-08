// Run inside the project's Unity MCP bridge. Preview is the default; set Apply=true
// in the command wrapper to convert matched leaf objects in Scene/Environment.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class WallPrefabTrial
{
    const string Folder = "Assets/Prefabs/Environment/Wall";
    const string ScenePath = "Assets/Scenes/GameScene.unity";
    sealed class Fingerprint { public string Shape, Appearance; }
    sealed class Sample
    {
        public GameObject Asset;
        public UnityEngine.Mesh SourceMesh;
        public string Path, Key;
        public readonly List<MeshFilter> Matches = new List<MeshFilter>();
        public int Existing, MeshOverrides;
    }
    [Serializable] internal sealed class Row
    {
        public string prefab, originalName, mesh, preservedMesh;
        public string globalId;
        public Vector3 localPosition, localScale;
        public Quaternion localRotation;
    }
    [Serializable] internal sealed class Report
    {
        public string scene, scope, backup, status;
        public int scanned, fingerprinted, converted, existing, skipped;
        public string[] summary;
        public Row[] objects;
    }
    sealed class State
    {
        public MeshFilter Filter;
        public Transform Parent;
        public string Name;
        public Vector3 Position, Scale;
        public Quaternion Rotation;
        public int Sibling, GameObjectId, FilterId, RendererId;
        public UnityEngine.Mesh OriginalMesh;
        public Material[] Materials;
    }
    static string Hash(string text)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
    }
    static string Point(Vector3 v)
    {
        // Local-space coordinates retain the original pivot. Quantization only
        // tolerates FBX float round-off; appearance differences keep the old mesh.
        return Math.Round(v.x * 100000).ToString(CultureInfo.InvariantCulture) + "," +
               Math.Round(v.y * 100000).ToString(CultureInfo.InvariantCulture) + "," +
               Math.Round(v.z * 100000).ToString(CultureInfo.InvariantCulture);
    }
    static string Counts(UnityEngine.Mesh mesh)
    {
        return mesh.vertexCount + "/" + mesh.subMeshCount + "/" +
            string.Join(",", Enumerable.Range(0, mesh.subMeshCount).Select(i => mesh.GetIndexCount(i).ToString()));
    }
    static Fingerprint GetFingerprint(UnityEngine.Mesh mesh, Dictionary<UnityEngine.Mesh, Fingerprint> cache)
    {
        if (cache.TryGetValue(mesh, out var cached)) return cached;
        var shape = new StringBuilder();
        var appearance = new StringBuilder();
        using (var array = MeshUtility.AcquireReadOnlyMeshData(mesh))
        {
            var data = array[0];
            using (var positions = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
            {
                data.GetVertices(positions);
                for (int s = 0; s < data.subMeshCount; s++)
                {
                    var sub = data.GetSubMesh(s);
                    if (sub.topology != MeshTopology.Triangles)
                        throw new InvalidOperationException("Non-triangle wall mesh: " + mesh.name);
                    var faces = new List<string>();
                    using (var indices = new NativeArray<int>(sub.indexCount, Allocator.Temp))
                    {
                        data.GetIndices(indices, s, true);
                        for (int i = 0; i < indices.Length; i += 3)
                        {
                            var a = Point(positions[indices[i]]);
                            var b = Point(positions[indices[i + 1]]);
                            var c = Point(positions[indices[i + 2]]);
                            // Canonical cyclic order retains winding and ignores
                            // importer vertex/index ordering.
                            var cycles = new[] {a + ";" + b + ";" + c, b + ";" + c + ";" + a, c + ";" + a + ";" + b};
                            Array.Sort(cycles, StringComparer.Ordinal);
                            faces.Add(cycles[0]);
                        }
                    }
                    faces.Sort(StringComparer.Ordinal);
                    shape.Append(s).Append(':').Append(string.Join("|", faces)).Append('\n');
                    appearance.Append(sub.topology).Append(':').Append(sub.indexStart).Append(':')
                        .Append(sub.indexCount).Append(':').Append(sub.baseVertex).Append('\n');
                }
            }
            // Include all vertex streams (normals, tangents, colors and every UV
            // channel) and index buffers. Different bytes keep the original Mesh
            // as an override instead of changing its appearance.
            for (int stream = 0; stream < data.vertexBufferCount; stream++)
            {
                var bytes = data.GetVertexData<byte>(stream);
                appearance.Append(data.GetVertexBufferStride(stream)).Append(':')
                    .Append(Convert.ToBase64String(bytes.ToArray())).Append('\n');
            }
            appearance.Append(Convert.ToBase64String(data.GetIndexData<byte>().ToArray()));
        }
        var fp = new Fingerprint { Shape = Hash(shape.ToString()), Appearance = Hash(appearance.ToString()) };
        cache.Add(mesh, fp);
        return fp;
    }
    static string Materials(MeshRenderer renderer)
    {
        return string.Join(";", renderer.sharedMaterials.Select(m => m == null ? "null" : GlobalObjectId.GetGlobalObjectIdSlow(m).ToString()));
    }
    static State Capture(MeshFilter filter)
    {
        var t = filter.transform;
        return new State { Filter = filter, Parent = t.parent, Name = t.name, Position = t.localPosition,
            Rotation = t.localRotation, Scale = t.localScale, Sibling = t.GetSiblingIndex(),
            GameObjectId = filter.gameObject.GetInstanceID(), FilterId = filter.GetInstanceID(),
            RendererId = filter.GetComponent<MeshRenderer>().GetInstanceID(), OriginalMesh = filter.sharedMesh,
            Materials = filter.GetComponent<MeshRenderer>().sharedMaterials };
    }
    static void Verify(State old, Sample sample, Dictionary<UnityEngine.Mesh, Fingerprint> cache)
    {
        var f = old.Filter;
        if (f == null || f.gameObject.GetInstanceID() != old.GameObjectId || f.GetInstanceID() != old.FilterId ||
            f.GetComponent<MeshRenderer>().GetInstanceID() != old.RendererId)
            throw new InvalidOperationException("Object/component references changed: " + old.Name);
        var t = f.transform;
        if (t.parent != old.Parent || t.name != old.Name || t.GetSiblingIndex() != old.Sibling ||
            !t.localPosition.Equals(old.Position) || !t.localRotation.Equals(old.Rotation) || !t.localScale.Equals(old.Scale))
            throw new InvalidOperationException("Transform or hierarchy changed: " + old.Name);
        if (!f.GetComponent<MeshRenderer>().sharedMaterials.SequenceEqual(old.Materials))
            throw new InvalidOperationException("Materials changed: " + old.Name);
        if (GetFingerprint(f.sharedMesh, cache).Appearance != GetFingerprint(old.OriginalMesh, cache).Appearance)
            throw new InvalidOperationException("Vertex attributes changed: " + old.Name);
        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(f.gameObject) != sample.Path)
            throw new InvalidOperationException("Wrong prefab connection: " + old.Name);
        var sourceBoxes = sample.Asset.GetComponents<BoxCollider>();
        var boxes = f.GetComponents<BoxCollider>();
        if (boxes.Length != sourceBoxes.Length)
            throw new InvalidOperationException("Collider count differs: " + old.Name);
        for (int i = 0; i < boxes.Length; i++)
        {
            if (PrefabUtility.GetCorrespondingObjectFromSource(boxes[i]) != sourceBoxes[i] ||
                !boxes[i].center.Equals(sourceBoxes[i].center) || !boxes[i].size.Equals(sourceBoxes[i].size))
                throw new InvalidOperationException("Collider is not inherited: " + old.Name);
            var mods = PrefabUtility.GetPropertyModifications(f.gameObject);
            if (mods != null && mods.Any(m => m.target == sourceBoxes[i]))
                throw new InvalidOperationException("Collider has property overrides: " + old.Name);
        }
    }
    public static void Diagnose(string name, ExecutionResult result)
    {
        var scene = SceneManager.GetActiveScene();
        var root = scene.GetRootGameObjects().Single(x => x.name == "Scene").transform.Find("Environment");
        var target = root.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == name);
        var mesh = target.sharedMesh;
        var renderer = target.GetComponent<MeshRenderer>();
        var cache = new Dictionary<UnityEngine.Mesh, Fingerprint>();
        var fingerprint = GetFingerprint(mesh, cache);
        result.Log("Target {0}: playing={1}; mesh={2}; counts={3}; boundsCenter={4}; boundsSize={5}; localScale={6}; components={7}; children={8}; prefab={9}; materials={10}",
            name, EditorApplication.isPlaying, mesh.name, Counts(mesh), mesh.bounds.center, mesh.bounds.size,
            target.transform.localScale, string.Join(",",target.GetComponents<Component>().Select(c=>c==null?"missing":c.GetType().Name)),
            target.transform.childCount, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target.gameObject), Materials(renderer));
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{Folder}).OrderBy(x=>AssetDatabase.GUIDToAssetPath(x)))
        {
            var path=AssetDatabase.GUIDToAssetPath(guid);
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var sample=asset.GetComponent<MeshFilter>().sharedMesh;
            var fp=GetFingerprint(sample,cache);
            double maxDelta=-1, rms=-1;
            if(mesh.vertexCount==sample.vertexCount)
            {
                using(var a=MeshUtility.AcquireReadOnlyMeshData(mesh))
                using(var b=MeshUtility.AcquireReadOnlyMeshData(sample))
                using(var va=new NativeArray<Vector3>(mesh.vertexCount,Allocator.Temp))
                using(var vb=new NativeArray<Vector3>(sample.vertexCount,Allocator.Temp))
                {
                    a[0].GetVertices(va);b[0].GetVertices(vb);maxDelta=0;rms=0;
                    for(int i=0;i<va.Length;i++){var distance=Vector3.Distance(va[i],vb[i]);maxDelta=Math.Max(maxDelta,distance);rms+=distance*distance;}
                    rms=Math.Sqrt(rms/va.Length);
                }
            }
            result.Log("{0}: countsMatch={1}; shapeMatch={2}; materialsMatch={3}; appearanceMatch={4}; maxVertexDelta={5}; rms={6}; boundsSize={7}",
                asset.name,Counts(mesh)==Counts(sample),fingerprint.Shape==fp.Shape,
                Materials(renderer)==Materials(asset.GetComponent<MeshRenderer>()),fingerprint.Appearance==fp.Appearance,maxDelta,rms,sample.bounds.size);
        }
    }
    public static void Run(bool apply, ExecutionResult result)
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || scene.path != ScenePath)
            throw new InvalidOperationException("Open GameScene in edit mode first.");
        var root = scene.GetRootGameObjects().Single(x => x.name == "Scene").transform.Find("Environment");
        if (root == null) throw new InvalidOperationException("Scene/Environment missing.");
        var cache = new Dictionary<UnityEngine.Mesh, Fingerprint>();
        var samples = new List<Sample>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[]{Folder}).OrderBy(x => AssetDatabase.GUIDToAssetPath(x)))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var filter = asset.GetComponent<MeshFilter>();
            var renderer = asset.GetComponent<MeshRenderer>();
            if (asset.transform.childCount != 0 || filter == null || renderer == null || filter.sharedMesh == null)
                throw new InvalidOperationException("Only single-mesh leaf samples supported: " + path);
            var fp = GetFingerprint(filter.sharedMesh, cache);
            samples.Add(new Sample {Asset = asset, SourceMesh = filter.sharedMesh, Path = path, Key = fp.Shape + "/" + Materials(renderer)});
        }
        var byKey = samples.GroupBy(s => s.Key).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var group in byKey.Values.Where(g => g.Count > 1))
            result.Log("Same shape/material samples: {0}; use source Mesh or complete appearance to distinguish", string.Join(",", group.Select(s => s.Asset.name)));
        var counts = new HashSet<string>(samples.Select(s => Counts(s.SourceMesh)));
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        int skipped = 0;
        foreach (var filter in filters)
        {
            var mesh = filter.sharedMesh;
            var renderer = filter.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null || !counts.Contains(Counts(mesh))) continue;
            var key = GetFingerprint(mesh, cache).Shape + "/" + Materials(renderer);
            if (!byKey.TryGetValue(key, out var options)) continue;
            Sample sample;
            if (options.Count == 1) sample = options[0];
            else
            {
                var exact = options.Where(s => s.SourceMesh == mesh).ToList();
                if (exact.Count != 1)
                    exact = options.Where(s => GetFingerprint(s.SourceMesh, cache).Appearance == GetFingerprint(mesh, cache).Appearance).ToList();
                if (exact.Count != 1) { skipped++; continue; }
                sample = exact[0];
            }
            if (PrefabUtility.IsPartOfPrefabInstance(filter.gameObject))
            {
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(filter.gameObject) == sample.Path) sample.Existing++;
                else skipped++;
                continue;
            }
            // Never discard scripts, children or existing collision setups.
            if (filter.transform.childCount != 0 || filter.GetComponents<Component>().Any(c =>
                c == null || (!(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer))))
            { skipped++; continue; }
            sample.Matches.Add(filter);
        }
        var report = new Report { scene = scene.path, scope = "Scene/Environment", status = apply ? "applying" : "preview",
            scanned = filters.Length, fingerprinted = cache.Count, skipped = skipped,
            existing = samples.Sum(s => s.Existing), converted = samples.Sum(s => s.Matches.Count) };
        var rows = new List<Row>();
        var directory = apply ? "RecoverySnapshots/" + DateTime.UtcNow.AddHours(8).ToString("yyyyMMdd-HHmmss") + "-wall-prefabs" : "Temp/WallPrefabTrial";
        Directory.CreateDirectory(directory);
        var states = samples.SelectMany(s => s.Matches).Select(Capture).ToDictionary(s => s.Filter);
        var outsideBefore = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true))
            .Where(t => t != root && !t.IsChildOf(root)).ToDictionary(t => t, t => EditorJsonUtility.ToJson(t));
        // All transforms, including unmatched objects, must survive conversion unchanged.
        var matchedTransforms = new HashSet<Transform>(states.Keys.Select(f => f.transform));
        var allTransforms = root.GetComponentsInChildren<Transform>(true).Where(t => !matchedTransforms.Contains(t)).ToDictionary(t => t, t => EditorJsonUtility.ToJson(t));
        string liveBackup = null;
        int undoGroup = -1;
        if (apply && report.converted > 0)
        {
            File.Copy(ScenePath, directory + "/GameScene_disk_before.unity", false);
            report.backup = directory + "/GameScene_live_before.unity";
            liveBackup = report.backup;
            if (!EditorSceneManager.SaveScene(scene, liveBackup, true)) throw new InvalidOperationException("Live scene backup failed.");
            Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("将相同墙体连接到 Wall 预制体");
        }
        try
        {
            foreach (var sample in samples)
            {
                foreach (var filter in sample.Matches)
                {
                    var old = states[filter];
                    bool keepMesh = GetFingerprint(old.OriginalMesh, cache).Appearance != GetFingerprint(sample.SourceMesh, cache).Appearance;
                    if (keepMesh) sample.MeshOverrides++;
                    rows.Add(new Row { prefab = sample.Path, originalName = old.Name, mesh = old.OriginalMesh.name,
                        preservedMesh = keepMesh ? "appearance differs; retain original Mesh override" : "shares prefab Mesh",
                        globalId = GlobalObjectId.GetGlobalObjectIdSlow(filter.gameObject).ToString(),
                        localPosition = old.Position, localRotation = old.Rotation, localScale = old.Scale });
                    if (!apply) continue;
                    PrefabUtility.ConvertToPrefabInstance(filter.gameObject, sample.Asset,
                        new ConvertToPrefabInstanceSettings {objectMatchMode = ObjectMatchMode.ByHierarchy,
                            recordPropertyOverridesOfMatches = true, componentsNotMatchedBecomesOverride = true,
                            gameObjectsNotMatchedBecomesOverride = true, changeRootNameToAssetName = false, logInfo = false},
                        InteractionMode.UserAction);
                    // Appearance-equal meshes share the sample. Other meshes keep
                    // UVs/normals/etc via a MeshFilter override; colliders still inherit.
                    if (!keepMesh)
                        PrefabUtility.RevertPropertyOverride(new SerializedObject(filter).FindProperty("m_Mesh"), InteractionMode.UserAction);
                    Verify(old, sample, cache);
                }
                result.Log("{0}: matches={1}, existing={2}, originalMeshOverrides={3}, BoxColliders={4}",
                    sample.Asset.name, sample.Matches.Count, sample.Existing, sample.MeshOverrides, sample.Asset.GetComponents<BoxCollider>().Length);
            }
            if (apply && report.converted > 0)
            {
                foreach (var item in allTransforms.Concat(outsideBefore))
                    if (item.Key == null || EditorJsonUtility.ToJson(item.Key) != item.Value)
                        throw new InvalidOperationException("A transform outside the conversion changed.");
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Saving GameScene failed.");
                report.status = "verified-and-saved";
            }
            report.objects = rows.ToArray();
            report.summary = samples.Select(s => s.Asset.name + ": " + s.Matches.Count + " converted; " + s.Existing + " existing; " + s.MeshOverrides + " mesh overrides; " + s.Asset.GetComponents<BoxCollider>().Length + " inherited BoxColliders").ToArray();
            File.WriteAllText(directory + "/report.json", JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            result.Log("{0}: {1} matches; {2} scanned; {3} unique meshes fingerprinted; {4} skipped; report={5}/report.json",
                report.status, report.converted, report.scanned, report.fingerprinted, report.skipped, directory);
        }
        catch
        {
            if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }
}
