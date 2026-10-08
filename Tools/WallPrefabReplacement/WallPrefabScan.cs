// Run performs a read-only scan. Apply explicitly converts matching walls after
// backing up the live scene, preserving visuals and inheriting prefab colliders.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class WallPrefabScan
{
    const float Tolerance = 0.0001f;
    const string Folder = "Assets/Prefabs/Environment/Wall";
    sealed class Data { public Vector3[] Vertices; public int[][] Indices; }
    sealed class Group
    {
        public UnityEngine.Mesh Mesh;
        public Vector3[] Points;
        public string[] Faces;
        public readonly List<GameObject> Samples = new List<GameObject>();
        public int Total, Plain, Existing, DifferentMaterials, ExtraComponents;
        public float MaximumError;
    }
    static Data Read(UnityEngine.Mesh mesh, Dictionary<UnityEngine.Mesh, Data> cache)
    {
        if (cache.TryGetValue(mesh, out var found)) return found;
        var data = new Data();
        using (var snapshot = MeshUtility.AcquireReadOnlyMeshData(mesh))
        {
            var source = snapshot[0];
            using (var vertices = new NativeArray<Vector3>(source.vertexCount, Allocator.Temp))
            { source.GetVertices(vertices); data.Vertices = vertices.ToArray(); }
            data.Indices = new int[source.subMeshCount][];
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var sub = source.GetSubMesh(s);
                if (sub.topology != MeshTopology.Triangles) throw new InvalidOperationException("Not a triangle mesh: " + mesh.name);
                using (var indices = new NativeArray<int>(sub.indexCount, Allocator.Temp))
                { source.GetIndices(indices, s, true); data.Indices[s] = indices.ToArray(); }
            }
        }
        cache.Add(mesh, data); return data;
    }
    static string Counts(UnityEngine.Mesh mesh)
    {
        // Vertex counts may differ when UVs/normals split otherwise equal corners.
        return mesh.subMeshCount + "/" + string.Join(",", Enumerable.Range(0, mesh.subMeshCount).Select(s => mesh.GetIndexCount(s).ToString()));
    }
    static bool Near(Vector3 a, Vector3 b)
    { return Mathf.Abs(a.x-b.x) <= Tolerance && Mathf.Abs(a.y-b.y) <= Tolerance && Mathf.Abs(a.z-b.z) <= Tolerance; }
    static int Nearest(Vector3 point, Vector3[] points, out float distance)
    {
        int best = -1; float squared = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            float candidate = (point-points[i]).sqrMagnitude;
            if (candidate < squared) { squared = candidate; best = i; }
        }
        distance = Mathf.Sqrt(squared); return distance <= Tolerance ? best : -1;
    }
    static Vector3[] Representatives(Vector3[] vertices)
    {
        var points = new List<Vector3>();
        foreach (var v in vertices.OrderBy(v => v.x).ThenBy(v => v.y).ThenBy(v => v.z))
        {
            if (!points.Any(p => (p-v).sqrMagnitude <= Tolerance*Tolerance)) points.Add(v);
        }
        return points.ToArray();
    }
    static string[] Faces(Data data, Vector3[] points, out float maximumError)
    {
        maximumError = 0; var mapping = new int[data.Vertices.Length];
        for (int i = 0; i < mapping.Length; i++)
        {
            mapping[i] = Nearest(data.Vertices[i], points, out var error);
            if (mapping[i] < 0) return null;
            maximumError = Mathf.Max(maximumError, error);
        }
        var result = new string[data.Indices.Length];
        for (int s = 0; s < data.Indices.Length; s++)
        {
            var indices = data.Indices[s]; var triangles = new List<string>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a=mapping[indices[i]], b=mapping[indices[i+1]], c=mapping[indices[i+2]];
                // Cyclic permutations are equal; reversed winding is not equal.
                var cycles=new[]{a+","+b+","+c,b+","+c+","+a,c+","+a+","+b};
                Array.Sort(cycles,StringComparer.Ordinal);triangles.Add(cycles[0]);
            }
            triangles.Sort(StringComparer.Ordinal); result[s]=string.Join(";",triangles);
        }
        return result;
    }
    static bool Match(UnityEngine.Mesh mesh, Group group, Dictionary<UnityEngine.Mesh, Data> cache, out float error)
    {
        error=0;
        if(Counts(mesh)!=Counts(group.Mesh) || !Near(mesh.bounds.min,group.Mesh.bounds.min) || !Near(mesh.bounds.max,group.Mesh.bounds.max)) return false;
        var faces=Faces(Read(mesh,cache),group.Points,out error);
        return faces!=null && faces.SequenceEqual(group.Faces);
    }
    static string Hierarchy(Transform t)
    { var parts=new List<string>();while(t!=null){parts.Add(t.name);t=t.parent;}parts.Reverse();return string.Join("/",parts); }
    static string Materials(GameObject go)
    { return string.Join(";",go.GetComponent<MeshRenderer>().sharedMaterials.Select(m=>m==null?"null":GlobalObjectId.GetGlobalObjectIdSlow(m).ToString())); }
    static string MaterialPaths(GameObject go)
    { return string.Join(";",go.GetComponent<MeshRenderer>().sharedMaterials.Select(m=>m==null?"null":AssetDatabase.GetAssetPath(m))); }
    static string Csv(params string[] fields)
    { return string.Join(",",fields.Select(s=>"\""+(s??"").Replace("\"","\"\"")+"\"")); }
    static List<Group> BuildGroups(Dictionary<UnityEngine.Mesh,Data> cache)
    {
        var groups=new List<Group>();
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{Folder}).OrderBy(g=>AssetDatabase.GUIDToAssetPath(g)))
        {
            var sample=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var mesh=sample.GetComponent<MeshFilter>().sharedMesh;
            var existing=groups.FirstOrDefault(g=>Match(mesh,g,cache,out var unused));
            if(existing!=null){existing.Samples.Add(sample);continue;}
            var data=Read(mesh,cache);var points=Representatives(data.Vertices);
            var group=new Group{Mesh=mesh,Points=points,Faces=Faces(data,points,out var error)};
            group.Samples.Add(sample);groups.Add(group);
        }
        return groups;
    }
    sealed class Conversion
    {
        public MeshFilter Filter;
        public GameObject Asset;
        public UnityEngine.Mesh Mesh;
        public Material[] Materials;
        public Transform Parent;
        public string Name, PreviousPrefab, AssetPath, RendererState;
        public Vector3 Position, Scale;
        public Quaternion Rotation;
        public int Sibling, GoId, TransformId, FilterId, RendererId;
        public float Error;
        public bool Existing;
    }
    static string RendererState(MeshRenderer renderer)
    {
        // Remove only prefab bookkeeping; all actual renderer settings must stay.
        var json=EditorJsonUtility.ToJson(renderer);
        foreach(var property in new[]{"m_CorrespondingSourceObject","m_PrefabInstance","m_PrefabAsset"})
        {
            var token="\""+property+"\":{";
            int start=json.IndexOf(token,StringComparison.Ordinal);
            if(start<0)continue;
            int end=json.IndexOf('}',start+token.Length);
            if(end<0)throw new InvalidOperationException("Unexpected renderer serialization.");
            json=json.Substring(0,start)+token+"}"+json.Substring(end+1);
        }
        return json;
    }
    static void VerifyConversion(Conversion old)
    {
        var filter=old.Filter;var go=filter.gameObject;var t=filter.transform;var renderer=go.GetComponent<MeshRenderer>();
        if(go.GetInstanceID()!=old.GoId || t.GetInstanceID()!=old.TransformId || filter.GetInstanceID()!=old.FilterId || renderer.GetInstanceID()!=old.RendererId)
            throw new InvalidOperationException("Component/object references changed: "+old.Name);
        if(t.parent!=old.Parent || t.GetSiblingIndex()!=old.Sibling || t.name!=old.Name || !t.localPosition.Equals(old.Position) || !t.localRotation.Equals(old.Rotation) || !t.localScale.Equals(old.Scale))
            throw new InvalidOperationException("Transform/hierarchy changed: "+old.Name+"; parent="+(t.parent==old.Parent)+"; sibling="+(t.GetSiblingIndex()==old.Sibling)+"; name="+(t.name==old.Name)+"; position="+t.localPosition.ToString("R")+" vs "+old.Position.ToString("R")+"; rotation="+t.localRotation.ToString("R")+" vs "+old.Rotation.ToString("R")+"; scale="+t.localScale.ToString("R")+" vs "+old.Scale.ToString("R"));
        if(filter.sharedMesh!=old.Mesh || !renderer.sharedMaterials.SequenceEqual(old.Materials) || RendererState(renderer)!=old.RendererState)
            throw new InvalidOperationException("Mesh/material/renderer settings changed: "+old.Name);
        if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)!=old.AssetPath)
            throw new InvalidOperationException("Wrong prefab connection: "+old.Name);
        var sources=old.Asset.GetComponents<BoxCollider>();var boxes=go.GetComponents<BoxCollider>();
        if(boxes.Length!=sources.Length || go.GetComponents<Collider>().Length!=sources.Length)
            throw new InvalidOperationException("Unexpected colliders: "+old.Name);
        for(int i=0;i<boxes.Length;i++)
        {
            if(PrefabUtility.GetCorrespondingObjectFromSource(boxes[i])!=sources[i] || !boxes[i].center.Equals(sources[i].center) || !boxes[i].size.Equals(sources[i].size) || boxes[i].enabled!=sources[i].enabled || boxes[i].isTrigger!=sources[i].isTrigger || boxes[i].sharedMaterial!=sources[i].sharedMaterial)
                throw new InvalidOperationException("Collider not inherited: "+old.Name);
        }
        var modifications=PrefabUtility.GetPropertyModifications(go);
        if(modifications!=null && modifications.Any(m=>m.target is Collider))
            throw new InvalidOperationException("Collider overrides remain: "+old.Name);
    }
    public static void Apply(ExecutionResult result)
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/GameScene.unity" || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("GameScene must be open in edit mode.");
        var root=scene.GetRootGameObjects().Single(g=>g.name=="Scene").transform.Find("Environment");
        var cache=new Dictionary<UnityEngine.Mesh,Data>();var groups=BuildGroups(cache);
        var byCounts=groups.GroupBy(g=>Counts(g.Mesh)).ToDictionary(g=>g.Key,g=>g.ToArray());
        var assignment=new Dictionary<UnityEngine.Mesh,Group>();var errors=new Dictionary<UnityEngine.Mesh,float>();
        var plan=new List<Conversion>();int unchanged=0,skipped=0,matched=0;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh=filter.sharedMesh;var renderer=filter.GetComponent<MeshRenderer>();
            if(mesh==null || renderer==null)continue;
            if(!assignment.ContainsKey(mesh))
            {
                var matches=new List<Group>();float error=0;
                if(byCounts.TryGetValue(Counts(mesh),out var choices))
                    foreach(var group in choices)if(Match(mesh,group,cache,out var delta)){matches.Add(group);error=Mathf.Max(error,delta);}
                assignment.Add(mesh,matches.Count==1?matches[0]:null);errors.Add(mesh,error);
            }
            var selected=assignment[mesh];if(selected==null)continue;matched++;
            var asset=selected.Samples[0];var path=AssetDatabase.GetAssetPath(asset);
            bool linked=PrefabUtility.IsPartOfPrefabInstance(filter.gameObject);
            var previous=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(filter.gameObject);
            if(linked && previous==path){unchanged++;continue;}
            if(linked && (PrefabUtility.GetNearestPrefabInstanceRoot(filter.gameObject)!=filter.gameObject || !previous.StartsWith(Folder+"/",StringComparison.Ordinal)))
            {skipped++;continue;}
            if(filter.transform.childCount!=0 || filter.GetComponents<Component>().Any(c=>c==null || (!(c is Transform)&&!(c is MeshFilter)&&!(c is MeshRenderer)&&!(linked&&c is BoxCollider))))
            {skipped++;continue;}
            if(asset.GetComponents<BoxCollider>().Length==0 || asset.GetComponents<Collider>().Any(c=>!(c is BoxCollider)))
                throw new InvalidOperationException("Sample needs BoxCollider: "+path);
            var t=filter.transform;
            plan.Add(new Conversion{Filter=filter,Asset=asset,AssetPath=path,PreviousPrefab=previous,Existing=linked,Error=errors[mesh],
                Mesh=mesh,Materials=renderer.sharedMaterials,RendererState=RendererState(renderer),Parent=t.parent,Name=t.name,
                Position=t.localPosition,Rotation=t.localRotation,Scale=t.localScale,Sibling=t.GetSiblingIndex(),
                GoId=filter.gameObject.GetInstanceID(),TransformId=t.GetInstanceID(),FilterId=filter.GetInstanceID(),RendererId=renderer.GetInstanceID()});
        }
        if(plan.Count==0){result.Log("Nothing to convert; {0} matched walls already connected.",matched);return;}
        var changedTransforms=new HashSet<Transform>(plan.Select(p=>p.Filter.transform));
        var unchangedComponents=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true))
            .Where(c=>c!=null && !changedTransforms.Contains(c.transform)).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
        var backup="RecoverySnapshots/"+DateTime.UtcNow.AddHours(8).ToString("yyyyMMdd-HHmmss")+"-wall-prefabs-near";
        Directory.CreateDirectory(backup);File.Copy(scene.path,backup+"/GameScene_disk_before.unity",false);
        if(!EditorSceneManager.SaveScene(scene,backup+"/GameScene_live_before.unity",true))throw new InvalidOperationException("Live scene backup failed.");
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("统一墙体预制体与碰撞体");
        try
        {
            foreach(var item in plan)
            {
                var go=item.Filter.gameObject;
                if(item.Existing)
                    PrefabUtility.ReplacePrefabAssetOfPrefabInstance(go,item.Asset,new PrefabReplacingSettings{
                        objectMatchMode=ObjectMatchMode.ByHierarchy,prefabOverridesOptions=PrefabOverridesOptions.KeepAllPossibleOverrides,
                        changeRootNameToAssetName=false,logInfo=false},InteractionMode.UserAction);
                else
                    PrefabUtility.ConvertToPrefabInstance(go,item.Asset,new ConvertToPrefabInstanceSettings{
                        objectMatchMode=ObjectMatchMode.ByHierarchy,recordPropertyOverridesOfMatches=true,
                        componentsNotMatchedBecomesOverride=true,gameObjectsNotMatchedBecomesOverride=true,
                        changeRootNameToAssetName=false,logInfo=false},InteractionMode.UserAction);
                // A root scale that was inherited from its former prefab can
                // become the new prefab's default. Preserve every original value
                // explicitly before recording the new instance's overrides.
                var transform=go.transform;
                if(transform.parent!=item.Parent)Undo.SetTransformParent(transform,item.Parent,"保留墙体父级");
                Undo.RecordObject(go,"保留墙体名称");Undo.RecordObject(transform,"保留墙体变换");
                go.name=item.Name;
                if(!transform.localPosition.Equals(item.Position))transform.localPosition=item.Position;
                if(!transform.localRotation.Equals(item.Rotation))transform.localRotation=item.Rotation;
                if(!transform.localScale.Equals(item.Scale))transform.localScale=item.Scale;
                if(transform.GetSiblingIndex()!=item.Sibling)transform.SetSiblingIndex(item.Sibling);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                // Keep the exact original mesh (all UVs/normals/tangents) and
                // materials even when the new prefab uses another visual variant.
                var renderer=go.GetComponent<MeshRenderer>();
                Undo.RecordObject(item.Filter,"保留墙体网格");Undo.RecordObject(renderer,"保留墙体材质");
                item.Filter.sharedMesh=item.Mesh;renderer.sharedMaterials=item.Materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(item.Filter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                foreach(var box in go.GetComponents<BoxCollider>())
                    PrefabUtility.RevertObjectOverride(box,InteractionMode.UserAction);
                // Drop stale collider property overrides from the old prefab too.
                var mods=PrefabUtility.GetPropertyModifications(go);
                if(mods!=null && mods.Any(m=>m.target is Collider))
                    PrefabUtility.SetPropertyModifications(go,mods.Where(m=>!(m.target is Collider)).ToArray());
                VerifyConversion(item);
            }
            foreach(var pair in unchangedComponents)
                if(pair.Key==null || EditorJsonUtility.ToJson(pair.Key)!=pair.Value)
                    throw new InvalidOperationException("An unmatched component changed: "+(pair.Key==null?"destroyed":pair.Key.name));
            Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Saving GameScene failed.");
            var rows=new List<string>{Csv("name","prefab","previousPrefab","operation","geometryErrorLocal","originalMesh","materials","position","rotation","scale","globalId")};
            foreach(var item in plan)
                rows.Add(Csv(item.Name,item.AssetPath,item.PreviousPrefab,item.Existing?"relinked":"converted",item.Error.ToString("R",CultureInfo.InvariantCulture),item.Mesh.name,MaterialPaths(item.Filter.gameObject),item.Position.ToString("R"),item.Rotation.ToString("R"),item.Scale.ToString("R"),GlobalObjectId.GetGlobalObjectIdSlow(item.Filter.gameObject).ToString()));
            File.WriteAllLines(backup+"/replacements.csv",rows,new UTF8Encoding(true));
            var summary=new List<string>{"Scene: "+scene.path,"Scope: Scene/Environment","Verified and saved.",
                "New prefab instances: "+plan.Count(p=>!p.Existing),"Existing instances unified: "+plan.Count(p=>p.Existing),
                "Total matched walls: "+matched,"Already connected: "+unchanged,"Skipped: "+skipped,
                "Exact original meshes, material references, renderer settings, transforms and object/component references preserved.",
                "BoxColliders inherit from prefab assets without Collider property overrides.",
                "All unmatched components verified unchanged."};
            foreach(var group in plan.GroupBy(p=>p.AssetPath))
            {var line=group.Key+": "+group.Count()+" changed";summary.Add(line);result.Log(line);}
            File.WriteAllLines(backup+"/verification.txt",summary,new UTF8Encoding(false));
            var example=plan.FirstOrDefault(p=>p.Name=="huitugou_2200_2679");
            if(example!=null)Selection.activeGameObject=example.Filter.gameObject;
            result.Log("REPLACEMENT_COMPLETE: new={0}; unified={1}; matched={2}; unchanged={3}; skipped={4}; backup={5}",plan.Count(p=>!p.Existing),plan.Count(p=>p.Existing),matched,unchanged,skipped,backup);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undo);
            throw;
        }
    }
    public static void Run(ExecutionResult result)
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/GameScene.unity")throw new InvalidOperationException("Open GameScene first.");
        var root=scene.GetRootGameObjects().Single(g=>g.name=="Scene").transform.Find("Environment");
        var cache=new Dictionary<UnityEngine.Mesh,Data>();var groups=BuildGroups(cache);
        var byCounts=groups.GroupBy(g=>Counts(g.Mesh)).ToDictionary(g=>g.Key,g=>g.ToArray());
        var assignment=new Dictionary<UnityEngine.Mesh,Group>();var errors=new Dictionary<UnityEngine.Mesh,float>();
        var filters=root.GetComponentsInChildren<MeshFilter>(true);
        int ambiguous=0,ambiguousMeshes=0;var rows=new List<string>();
        rows.Add(Csv("name","hierarchy","samplePrefabs","status","geometryErrorLocal","materialDiffers","materials","mesh","currentPrefab","localPosition","localRotation","localScale","colliderComponents"));
        foreach(var filter in filters)
        {
            var mesh=filter.sharedMesh;
            if(mesh==null || filter.GetComponent<MeshRenderer>()==null)continue;
            if(!assignment.ContainsKey(mesh))
            {
                Group match=null;float maximum=0;
                if(byCounts.TryGetValue(Counts(mesh),out var choices))
                {
                    var matches=new List<Group>();
                    foreach(var group in choices)if(Match(mesh,group,cache,out var error)){matches.Add(group);maximum=Mathf.Max(maximum,error);}
                    if(matches.Count==1)match=matches[0];
                    else if(matches.Count>1){ambiguousMeshes++;maximum=-1;}
                }
                assignment.Add(mesh,match);errors.Add(mesh,maximum);
            }
            var selected=assignment[mesh];if(selected==null){if(errors[mesh]<0)ambiguous++;continue;}
            selected.Total++;selected.MaximumError=Mathf.Max(selected.MaximumError,errors[mesh]);
            bool differentMaterials=!selected.Samples.Any(p=>Materials(p)==Materials(filter.gameObject));
            if(differentMaterials)selected.DifferentMaterials++;
            var prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(filter.gameObject);
            bool linked=PrefabUtility.IsPartOfPrefabInstance(filter.gameObject);
            bool extras=filter.transform.childCount!=0 || filter.GetComponents<Component>().Any(c=>c==null || (!(c is Transform)&&!(c is MeshFilter)&&!(c is MeshRenderer)));
            if(linked)selected.Existing++;else selected.Plain++;
            if(extras)selected.ExtraComponents++;
            var status=linked?"existing-prefab":extras?"plain-with-extra-components-or-children":"plain-ready";
            var samples=string.Join(";",selected.Samples.Select(p=>p.name));
            rows.Add(Csv(filter.name,Hierarchy(filter.transform),samples,status,errors[mesh].ToString("R",CultureInfo.InvariantCulture),differentMaterials.ToString(),MaterialPaths(filter.gameObject),mesh.name,prefab,filter.transform.localPosition.ToString("R"),filter.transform.localRotation.ToString("R"),filter.transform.localScale.ToString("R"),string.Join(";",filter.GetComponents<Collider>().Select(c=>c.GetType().Name))));
            if(filter.name=="huitugou_2200_2679")result.Log("Confirmed 2679: samples={0}; error={1}; materialDiffers={2}; status={3}",samples,errors[mesh],differentMaterials,status);
        }
        var summary=new List<string>();summary.Add("# Wall geometry scan\n");
        summary.Add("Scene: "+scene.path+"; scope: Scene/Environment; local-space tolerance: "+Tolerance.ToString("R",CultureInfo.InvariantCulture)+". No scene/prefab modifications.\n");
        summary.Add("| Sample prefabs | Total | Plain objects | Existing prefab instances | Different materials | Extra components/children | Maximum geometry error |\n|---|---:|---:|---:|---:|---:|---:|");
        foreach(var group in groups)
        {
            var names=string.Join(" / ",group.Samples.Select(p=>p.name));
            summary.Add("| "+names+" | "+group.Total+" | "+group.Plain+" | "+group.Existing+" | "+group.DifferentMaterials+" | "+group.ExtraComponents+" | "+group.MaximumError.ToString("G6",CultureInfo.InvariantCulture)+" |");
            result.Log("{0}: total={1}; plain={2}; existing={3}; differentMaterials={4}; extras={5}; maxError={6}",names,group.Total,group.Plain,group.Existing,group.DifferentMaterials,group.ExtraComponents,group.MaximumError);
        }
        summary.Add("\nScanned mesh objects: "+filters.Length+". Unique scene meshes: "+assignment.Count+". Meshes read: "+cache.Count+". Matched objects: "+groups.Sum(g=>g.Total)+". Ambiguous objects: "+ambiguous+" ("+ambiguousMeshes+" unique meshes).\n");
        summary.Add("Matching checks local vertex positions, oriented triangle connectivity and submesh structure. Materials and vertex attributes are intentionally not identity criteria. Instance transforms are ignored. Unknown wall types outside the supplied samples are not classified. Existing colliders/scripts/children are reported for review before conversion.");
        Directory.CreateDirectory("Docs");
        File.WriteAllLines("Docs/WallPrefabScan-20261009.csv",rows,new UTF8Encoding(true));
        File.WriteAllLines("Docs/WallPrefabScan-20261009.md",summary,new UTF8Encoding(false));
        result.Log("SCAN_COMPLETE: objects={0}; plain={1}; existing={2}; ambiguous={3}; uniqueMeshesRead={4}; groups={5}; playMode={6}",groups.Sum(g=>g.Total),groups.Sum(g=>g.Plain),groups.Sum(g=>g.Existing),ambiguous,cache.Count,groups.Count,EditorApplication.isPlaying);
    }
}
