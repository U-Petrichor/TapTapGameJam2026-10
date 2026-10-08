using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TapTapGameJam.EditorTools;

internal class CommandScript : IRunCommand
{
    void Check(bool condition, string message, ExecutionResult result)
    { if (!condition) throw new InvalidOperationException(message); result.Log("PASS: " + message); }
    GameObject Item(string name, UnityEngine.Mesh mesh, Material material, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material; return go;
    }
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Exit Play and wait for compilation.");
        var original = SceneManager.GetActiveScene(); bool wasDirty = original.isDirty;
        var matcherWindow = Resources.FindObjectsOfTypeAll<ScenePrefabMatcherWindow>().FirstOrDefault();
        var originalWindowRoot = matcherWindow != null ? new SerializedObject(matcherWindow).FindProperty("_root").objectReferenceValue : null;
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        string directory = "Assets/_Recovery/PrefabMatcherSmoke-" + Guid.NewGuid().ToString("N");
        File.WriteAllText("Temp/PrefabMatcherSmokeFixture.txt", directory);
        try
        {
            SceneManager.SetActiveScene(fixture);
            if (!AssetDatabase.IsValidFolder("Assets/_Recovery")) AssetDatabase.CreateFolder("Assets", "_Recovery");
            AssetDatabase.CreateFolder("Assets/_Recovery", Path.GetFileName(directory));
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader");
            var firstMaterial = new Material(shader) { name = "First", color = Color.red };
            var otherMaterial = new Material(shader) { name = "Other", color = Color.blue };
            AssetDatabase.CreateAsset(firstMaterial, directory + "/First.mat");
            AssetDatabase.CreateAsset(otherMaterial, directory + "/Other.mat");
            var prototype = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prototype.name = "A_Base"; prototype.transform.localScale = new Vector3(1, 1, 0.46f);
            prototype.GetComponent<MeshRenderer>().sharedMaterial = firstMaterial;
            var box = prototype.GetComponent<BoxCollider>(); box.size = new Vector3(0.7f, 0.8f, 0.9f); box.center = new Vector3(0.1f, 0.02f, -0.05f);
            var sample = PrefabUtility.SaveAsPrefabAsset(prototype, directory + "/A_Base.prefab");
            prototype.name = "B_Alternate"; prototype.transform.localScale = new Vector3(1, 0.44354f, 0.46f); box.size = Vector3.one * 0.6f;
            var alternate = PrefabUtility.SaveAsPrefabAsset(prototype, directory + "/B_Alternate.prefab");
            var mesh = sample.GetComponent<MeshFilter>().sharedMesh;
            var nearMesh = UnityEngine.Object.Instantiate(mesh); nearMesh.name = "TinyDifference";
            var positions = nearMesh.vertices;
            for (int i = 0; i < positions.Length; i++) positions[i].x += 0.0000033f;
            nearMesh.vertices = positions; nearMesh.RecalculateBounds(); AssetDatabase.CreateAsset(nearMesh, directory + "/Near.asset");
            var reordered = UnityEngine.Object.Instantiate(mesh); reordered.name = "Reordered";
            reordered.vertices = mesh.vertices.Reverse().ToArray(); reordered.triangles = mesh.triangles.Select(i => mesh.vertexCount - 1 - i).ToArray();
            reordered.RecalculateBounds(); AssetDatabase.CreateAsset(reordered, directory + "/Reordered.asset");
            var wrongMesh = UnityEngine.Object.Instantiate(mesh); wrongMesh.name = "WrongWinding";
            var indices = wrongMesh.triangles; int swap = indices[1]; indices[1] = indices[2]; indices[2] = swap;
            wrongMesh.triangles = indices; AssetDatabase.CreateAsset(wrongMesh, directory + "/Wrong.asset");
            var root = new GameObject("MatcherSmokeRoot");
            var target = Item("TinyErrorAndDifferentMaterial", nearMesh, otherMaterial, root.transform);
            target.transform.localPosition = new Vector3(4, 1, 3); target.transform.localRotation = Quaternion.Euler(23, 54, 17);
            target.transform.localScale = new Vector3(-1, -0.43f, -0.4601f);
            var pose = target.transform.localRotation; var scale = target.transform.localScale; var position = target.transform.localPosition;
            var reorderingTarget = Item("DifferentVertexOrder", reordered, otherMaterial, root.transform);
            var wrong = Item("DifferentTopology", wrongMesh, otherMaterial, root.transform);
            var scripted = Item("HasBehaviour", nearMesh, otherMaterial, root.transform); scripted.AddComponent<Rigidbody>();
            var oldColliderTarget = Item("HasMeshCollider", nearMesh, otherMaterial, root.transform);
            oldColliderTarget.AddComponent<MeshCollider>().sharedMesh = nearMesh;
            var options = new ScenePrefabMatcher.Settings { Root = root.transform, SinglePrefab = sample };
            var scan = ScenePrefabMatcher.Scan(fixture, options);
            Check(scan.Matches.Single(m => m.Filter.gameObject == target).State == ScenePrefabMatcher.MatchState.Ready, "tiny FBX error and different material match", result);
            Check(scan.Matches.Single(m => m.Filter.gameObject == reorderingTarget).State == ScenePrefabMatcher.MatchState.Ready, "vertex/index reordering matches", result);
            Check(!scan.Matches.Any(m => m.Filter.gameObject == wrong), "different triangle winding is excluded", result);
            Check(scan.Matches.Single(m => m.Filter.gameObject == scripted).State == ScenePrefabMatcher.MatchState.Review, "behaviour components are protected", result);
            Check(scan.Matches.Single(m => m.Filter.gameObject == oldColliderTarget).State == ScenePrefabMatcher.MatchState.Review, "existing colliders are protected by default", result);
            var selected = scan.Matches.Single(m => m.Filter.gameObject == target);
            target.GetComponent<MeshFilter>().sharedMesh = wrongMesh; bool staleRejected = false;
            try { ScenePrefabMatcher.Apply(scan, new[] { selected }, false, false); } catch (InvalidOperationException) { staleRejected = true; }
            Check(staleRejected && !PrefabUtility.IsPartOfPrefabInstance(target), "stale geometry is rejected before conversion", result);
            target.GetComponent<MeshFilter>().sharedMesh = nearMesh;
            var applied = ScenePrefabMatcher.Apply(scan, new[] { selected }, false, false);
            Check(applied.Converted == 1 && target.GetComponent<MeshFilter>().sharedMesh == nearMesh && target.GetComponent<MeshRenderer>().sharedMaterial == otherMaterial,
                "conversion preserves exact mesh and material references", result);
            Check(target.transform.localRotation.Equals(pose) && target.transform.localScale.Equals(scale) && target.transform.localPosition.Equals(position),
                "rotation and mirrored nonuniform scale remain exact", result);
            Check(PrefabUtility.GetCorrespondingObjectFromSource(target.GetComponent<BoxCollider>()) == sample.GetComponent<BoxCollider>(), "collider inherits from sample", result);
            Undo.PerformUndo();
            Check(!PrefabUtility.IsPartOfPrefabInstance(target) && target.GetComponent<Collider>() == null && target.GetComponent<MeshFilter>().sharedMesh == nearMesh,
                "one undo restores original plain object", result);
            var inherited = (GameObject)PrefabUtility.InstantiatePrefab(alternate, fixture); inherited.transform.SetParent(root.transform, false);
            var inheritedScale = inherited.transform.localScale;
            for (int i = 0; i < 2; i++)
            { var existing = (GameObject)PrefabUtility.InstantiatePrefab(sample, fixture); existing.transform.SetParent(root.transform, false); }
            options.SinglePrefab = null; options.Folder = directory;
            scan = ScenePrefabMatcher.Scan(fixture, options);
            Check(scan.Groups.Count == 1 && scan.Groups[0].Prefab == sample, "most-used duplicate sample remains canonical", result);
            applied = ScenePrefabMatcher.Apply(scan, scan.Matches.Where(m => m.Filter.gameObject == inherited), false, false);
            Check(applied.Relinked == 1 && inherited.transform.localScale.Equals(inheritedScale) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(inherited) == AssetDatabase.GetAssetPath(sample),
                "relink preserves scale formerly inherited from old prefab", result);
            Undo.PerformUndo();
            Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(inherited) == AssetDatabase.GetAssetPath(alternate), "undo restores old prefab connection", result);
            options.ReplaceColliders = true; scan = ScenePrefabMatcher.Scan(fixture, options);
            var collider = oldColliderTarget.GetComponent<MeshCollider>();
            applied = ScenePrefabMatcher.Apply(scan, scan.Matches.Where(m => m.Filter.gameObject == oldColliderTarget), false, false);
            Check(applied.Converted == 1 && oldColliderTarget.GetComponent<MeshCollider>() == null && oldColliderTarget.GetComponent<BoxCollider>() != null,
                "explicit collider replacement inherits prepared BoxCollider", result);
            Undo.PerformUndo();
            Check(oldColliderTarget.GetComponent<MeshCollider>() == collider && oldColliderTarget.GetComponent<BoxCollider>() == null,
                "undo restores removed collider", result);
            for (int i = 0; i < 11; i++)
            {
                var numbered = (GameObject)PrefabUtility.InstantiatePrefab(sample, fixture); numbered.transform.SetParent(root.transform, false);
                var untouched = new GameObject("KeepRelativeOrder" + i); untouched.transform.SetParent(root.transform, false);
            }
            var otherParent = new GameObject("OtherParent"); otherParent.transform.SetParent(root.transform, false);
            var remote = (GameObject)PrefabUtility.InstantiatePrefab(sample, fixture); remote.transform.SetParent(otherParent.transform, false);
            var rootBefore = root.transform.Cast<Transform>().ToArray();
            var untouchedBefore = rootBefore.Where(t => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) != AssetDatabase.GetAssetPath(sample)).ToArray();
            options.ReplaceColliders = false; scan = ScenePrefabMatcher.Scan(fixture, options);
            var organized = ScenePrefabMatcher.Organize(scan, false, false);
            var renamed = root.transform.Cast<Transform>().Where(t => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == AssetDatabase.GetAssetPath(sample)).ToArray();
            Check(organized.Renamed == 14 && renamed.Select(t => t.name).SequenceEqual(Enumerable.Range(1, 13).Select(i => "A_Base_" + i)),
                "numbering begins at one and Hierarchy uses numeric ordering past ten", result);
            Check(remote.transform.parent == otherParent.transform && remote.name == "A_Base_14", "one prefab counter spans parents without reparenting", result);
            Check(root.transform.Cast<Transform>().Where(t => !renamed.Contains(t)).SequenceEqual(untouchedBefore), "unmatched siblings retain their relative order", result);
            var repeated = ScenePrefabMatcher.Organize(scan, false, false);
            Check(repeated.Renamed == 0 && repeated.SortedParents == 0, "repeated organization is stable", result);
            Undo.PerformUndo();
            result.Log("Naming undo: order={0}; names={1}; remote={2}",root.transform.Cast<Transform>().SequenceEqual(rootBefore),renamed.All(t=>t.name==sample.name),remote.name);
            Check(root.transform.Cast<Transform>().SequenceEqual(rootBefore) && renamed.All(t => t.name == sample.name) && remote.name == sample.name,
                "one undo restores names and sibling order", result);
            var collision = new GameObject("A_Base_1"); collision.transform.SetParent(root.transform, false);
            bool collisionRejected = false;
            try { ScenePrefabMatcher.Organize(scan, false, false); } catch (InvalidOperationException) { collisionRejected = true; }
            Check(collisionRejected && renamed.All(t => t.name == sample.name), "unmatched sibling name collisions are rejected before mutation", result);
            collision.name = "NoCollision";
            var sceneRootA = (GameObject)PrefabUtility.InstantiatePrefab(sample, fixture);
            var sceneRootB = (GameObject)PrefabUtility.InstantiatePrefab(sample, fixture);
            var sceneRootUnmatched = new GameObject("UnmatchedSceneRoot");
            var sceneRootsBefore = fixture.GetRootGameObjects().OrderBy(g => g.transform.GetSiblingIndex()).ToArray();
            options.Root = null; scan = ScenePrefabMatcher.Scan(fixture, options);
            ScenePrefabMatcher.Organize(scan, false, false); Undo.PerformUndo();
            Check(fixture.GetRootGameObjects().OrderBy(g => g.transform.GetSiblingIndex()).SequenceEqual(sceneRootsBefore), "scene-root sorting also supports undo", result);
            var beforeBatchNames = fixture.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToDictionary(t => t, t => t.name);
            scan = ScenePrefabMatcher.Scan(fixture, options);
            applied = ScenePrefabMatcher.Apply(scan, scan.Matches.Where(m => m.Filter.gameObject == target), false, false, true);
            Check(applied.Converted == 1 && applied.Renamed > 0 && target.name.StartsWith("A_Base_", StringComparison.Ordinal), "replacement can number all connected matches in the same batch", result);
            Undo.PerformUndo();
            Check(!PrefabUtility.IsPartOfPrefabInstance(target) && beforeBatchNames.All(p => p.Key.name == p.Value) &&
                fixture.GetRootGameObjects().OrderBy(g => g.transform.GetSiblingIndex()).SequenceEqual(sceneRootsBefore),
                "one undo restores conversion, all names and Hierarchy sorting together", result);
            var childTarget = Item("ChairWithChildCollider", nearMesh, otherMaterial, root.transform);
            var helper = new GameObject("CollisionHelper"); helper.transform.SetParent(childTarget.transform, false);
            helper.transform.localPosition = new Vector3(0.1f, 0.2f, 0.3f); helper.transform.localRotation = Quaternion.Euler(0, 0, 80.1f);
            helper.transform.localScale = new Vector3(0.9f, 1.1f, 0.8f);
            var childBox = helper.AddComponent<BoxCollider>(); var childPose = helper.transform.localRotation;
            options.ReplaceColliders = false; scan = ScenePrefabMatcher.Scan(fixture, options);
            Check(scan.Matches.Single(m => m.Filter.gameObject == childTarget).State == ScenePrefabMatcher.MatchState.Review,
                "child colliders require explicit replacement option", result);
            options.ReplaceColliders = true; scan = ScenePrefabMatcher.Scan(fixture, options);
            Check(scan.Matches.Single(m => m.Filter.gameObject == childTarget).State == ScenePrefabMatcher.MatchState.Ready,
                "pure collider children are eligible when enabled", result);
            applied = ScenePrefabMatcher.Apply(scan, scan.Matches.Where(m => m.Filter.gameObject == childTarget), false, false);
            Check(applied.Converted == 1 && helper != null && helper.transform.parent == childTarget.transform && helper.GetComponent<Collider>() == null &&
                helper.transform.localRotation.Equals(childPose) && childTarget.GetComponentsInChildren<Collider>(true).Length == 1,
                "child object and pose survive while duplicate collision is removed", result);
            Undo.PerformUndo();
            Check(!PrefabUtility.IsPartOfPrefabInstance(childTarget) && helper.GetComponent<BoxCollider>() == childBox && childTarget.GetComponent<Collider>() == null,
                "one undo restores chair and its original child collider", result);
            helper.AddComponent<Rigidbody>(); scan = ScenePrefabMatcher.Scan(fixture, options);
            Check(scan.Matches.Single(m => m.Filter.gameObject == childTarget).State == ScenePrefabMatcher.MatchState.Review,
                "child behaviour components remain protected", result);
            Undo.DestroyObjectImmediate(prototype.GetComponent<BoxCollider>());
            var colliderDefinition = new GameObject("CollisionHelper"); colliderDefinition.transform.SetParent(prototype.transform, false);
            colliderDefinition.transform.localPosition = new Vector3(0.12f, 0.03f, -0.07f);
            colliderDefinition.transform.localRotation = Quaternion.Euler(0, 45, 0);
            colliderDefinition.AddComponent<BoxCollider>().size = new Vector3(0.8f, 0.7f, 0.6f);
            var childSample = PrefabUtility.SaveAsPrefabAsset(prototype, directory + "/C_RotatedCollider.prefab");
            var rotatedTarget = Item("UsesRotatedCollider", nearMesh, otherMaterial, root.transform);
            var existingHelper = new GameObject("CollisionHelper"); existingHelper.transform.SetParent(rotatedTarget.transform, false);
            existingHelper.transform.localRotation = Quaternion.Euler(0, 0, 80.1f);
            var oldHelperPose = existingHelper.transform.localRotation; var originalChildBox = existingHelper.AddComponent<BoxCollider>();
            options.SinglePrefab = childSample; options.ReplaceColliders = true;
            scan = ScenePrefabMatcher.Scan(fixture, options);
            Check(scan.Groups.Count == 1 && scan.Matches.Single(m => m.Filter.gameObject == rotatedTarget).State == ScenePrefabMatcher.MatchState.Ready,
                "prefab with root mesh and child Collider is a valid sample", result);
            applied = ScenePrefabMatcher.Apply(scan, scan.Matches.Where(m => m.Filter.gameObject == rotatedTarget), false, false);
            var sourceTransform = childSample.transform.GetChild(0);
            Check(existingHelper.transform.localRotation.Equals(sourceTransform.localRotation) && rotatedTarget.GetComponentsInChildren<Collider>(true).Length == 1 &&
                PrefabUtility.GetCorrespondingObjectFromSource(existingHelper.GetComponent<BoxCollider>()) == sourceTransform.GetComponent<BoxCollider>(),
                "child collider inherits authored 45-degree pose without duplicates", result);
            Undo.PerformUndo();
            Check(existingHelper.transform.localRotation.Equals(oldHelperPose) && existingHelper.GetComponent<BoxCollider>() == originalChildBox && !PrefabUtility.IsPartOfPrefabInstance(rotatedTarget),
                "undo restores the original collision helper pose and connection", result);
            var plainRotatedTarget = Item("NewCollisionHierarchy", nearMesh, otherMaterial, root.transform);
            scan = ScenePrefabMatcher.Scan(fixture, options);
            applied = ScenePrefabMatcher.Apply(scan, scan.Matches.Where(m => m.Filter.gameObject == plainRotatedTarget), false, false);
            Check(plainRotatedTarget.transform.childCount == 1 && plainRotatedTarget.transform.GetChild(0).localRotation.Equals(sourceTransform.localRotation),
                "prefab collider hierarchy is added when the original model has no children", result);
            Undo.PerformUndo();
            Check(plainRotatedTarget.transform.childCount == 0 && !PrefabUtility.IsPartOfPrefabInstance(plainRotatedTarget), "undo removes newly inherited collider children", result);
            var asymmetric = UnityEngine.Object.Instantiate(mesh); asymmetric.name = "Asymmetric";
            asymmetric.vertices = mesh.vertices.Select(v => Vector3.Scale(v, new Vector3(1, 2, 3))).ToArray();
            asymmetric.RecalculateBounds(); AssetDatabase.CreateAsset(asymmetric, directory + "/Asymmetric.asset");
            prototype.GetComponent<MeshFilter>().sharedMesh = asymmetric;
            var asymmetricSample = PrefabUtility.SaveAsPrefabAsset(prototype, directory + "/D_Asymmetric.prefab");
            var bakeRotation = Quaternion.Euler(13, 37, -27); var bakeOffset = new Vector3(0.07f, -0.11f, 0.13f);
            var baked = UnityEngine.Object.Instantiate(asymmetric); baked.name = "RotatedReordered";
            baked.vertices = asymmetric.vertices.Select(v => bakeRotation * v + bakeOffset).Reverse().ToArray();
            baked.triangles = asymmetric.triangles.Select(i => asymmetric.vertexCount - 1 - i).ToArray();
            baked.RecalculateBounds(); AssetDatabase.CreateAsset(baked, directory + "/Baked.asset");
            var bakedTarget = Item("BakedRotation", baked, otherMaterial, root.transform);
            bakedTarget.transform.localPosition = new Vector3(5, 6, 7); bakedTarget.transform.localRotation = Quaternion.Euler(20, 30, 40);
            bakedTarget.transform.localScale = new Vector3(-2, 3, 4);
            var bakedPosition = bakedTarget.transform.localPosition; var bakedPose = bakedTarget.transform.localRotation; var bakedScale = bakedTarget.transform.localScale;
            options.SinglePrefab = asymmetricSample; options.MatchMeshRotations = false;
            scan = ScenePrefabMatcher.Scan(fixture, options);
            Check(!scan.Matches.Any(m => m.Filter.gameObject == bakedTarget), "disabled rotation matching excludes baked rotations", result);
            options.MatchMeshRotations = true; scan = ScenePrefabMatcher.Scan(fixture, options);
            var bakedMatch = scan.Matches.Single(m => m.Filter.gameObject == bakedTarget);
            Check(bakedMatch.State == ScenePrefabMatcher.MatchState.Ready && bakedMatch.Error < 0.00001f,
                "baked rotation, translation and reordered topology match", result);
            applied = ScenePrefabMatcher.Apply(scan, new[] { bakedMatch }, false, false);
            var bakedHelper = bakedTarget.transform.GetChild(0);
            Check(applied.Converted == 1 && bakedTarget.GetComponent<MeshFilter>().sharedMesh == baked && bakedTarget.GetComponent<MeshRenderer>().sharedMaterial == otherMaterial &&
                bakedTarget.transform.localPosition.Equals(bakedPosition) && bakedTarget.transform.localRotation.Equals(bakedPose) && bakedTarget.transform.localScale.Equals(bakedScale),
                "baked model conversion preserves exact appearance and root pose", result);
            // The rectangular box has rotational symmetries, so validate its transformed corners instead of assuming one PCA sign.
            var expectedCorners = new System.Collections.Generic.List<Vector3>(); var actualCorners = new System.Collections.Generic.List<Vector3>();
            var sourceBox = asymmetricSample.transform.GetChild(0).GetComponent<BoxCollider>(); var actualBox = bakedHelper.GetComponent<BoxCollider>();
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            {
                var corner = sourceBox.center + Vector3.Scale(sourceBox.size * 0.5f, new Vector3(x, y, z));
                expectedCorners.Add(bakedMatch.MeshRotation * (sourceBox.transform.localPosition + sourceBox.transform.localRotation * Vector3.Scale(corner, sourceBox.transform.localScale)) + bakedMatch.MeshOffset);
                actualCorners.Add(bakedHelper.localPosition + bakedHelper.localRotation * Vector3.Scale(corner, bakedHelper.localScale));
            }
            Check(expectedCorners.All(c => actualCorners.Any(a => (a - c).sqrMagnitude < 0.00000001f)) &&
                PrefabUtility.GetCorrespondingObjectFromSource(actualBox) == sourceBox, "child collider follows baked geometry while inheriting collider properties", result);
            scan = ScenePrefabMatcher.Scan(fixture, options);
            Check(scan.Matches.Single(m => m.Filter.gameObject == bakedTarget).State == ScenePrefabMatcher.MatchState.Connected,
                "rotation compensation is stable on repeated scan", result);
            Undo.PerformUndo();
            Check(!PrefabUtility.IsPartOfPrefabInstance(bakedTarget) && bakedTarget.transform.childCount == 0 && bakedTarget.GetComponent<MeshFilter>().sharedMesh == baked,
                "one undo restores baked model and removes compensated collider hierarchy", result);
            result.Log("PREFAB_MATCHER_SMOKE_PASS; fixture=" + directory);
        }
        finally
        {
            SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(fixture, true);
            if(matcherWindow!=null)
            {
                var settings=new SerializedObject(matcherWindow);settings.FindProperty("_root").objectReferenceValue=originalWindowRoot;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            if (original.isDirty != wasDirty) throw new InvalidOperationException("Original scene dirty state changed.");
            result.Log("Original active scene and unsaved state preserved.");
        }
    }
}
