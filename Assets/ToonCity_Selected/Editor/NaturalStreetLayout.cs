using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NaturalStreetLayout
{
    const string Dir="Assets/ToonCity_Selected/NaturalStreet";
    static Transform root, surfaces, props;
    static readonly Dictionary<Material,Material> converted=new Dictionary<Material,Material>();
    static Material lawn,paving,soil,curb;
    static GameObject Place(string path,float x,float z,float scale=1,float yaw=0)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Toon City/Prefabs/"+path+".prefab");
        if(!prefab) throw new Exception("Missing prefab "+path);
        var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        obj.transform.SetParent(props); obj.transform.position=new Vector3(x,.06f,z);
        obj.transform.rotation=Quaternion.Euler(0,yaw,0); obj.transform.localScale=Vector3.one*scale;
        foreach(var c in obj.GetComponentsInChildren<Collider>(true)) c.enabled=false;
        foreach(var b in obj.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled=false;
        foreach(var r in obj.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials=r.sharedMaterials.Select(Convert).ToArray();
        return obj;
    }
    static Material Convert(Material src)
    {
        if(!src) throw new Exception("Missing source material");
        if(converted.TryGetValue(src,out var result)) return result;
        result=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        foreach(var key in new[]{"_BaseMap","_TextureSample3","_TextureSample1","_TextureSample","_MainTex"})
            if(src.HasProperty(key) && src.GetTexture(key)) {result.SetTexture("_BaseMap",src.GetTexture(key));result.SetTextureScale("_BaseMap",src.GetTextureScale(key));result.SetTextureOffset("_BaseMap",src.GetTextureOffset(key));break;}
        result.SetFloat("_Smoothness",.12f);
        result.SetColor("_BaseColor",src.HasProperty("_Color")?src.GetColor("_Color"):Color.white);
        AssetDatabase.CreateAsset(result,AssetDatabase.GenerateUniqueAssetPath(Dir+"/"+src.name+"_URP.mat"));
        converted[src]=result; return result;
    }
    static Material Ground(string name,string texture,Color tint,float tiling)
    {
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor",tint); mat.SetFloat("_Smoothness",0);
        if(texture!=null) mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Toon City/Textures/"+texture+".psd"));
        mat.SetTextureScale("_BaseMap",Vector2.one*tiling);
        AssetDatabase.CreateAsset(mat,AssetDatabase.GenerateUniqueAssetPath(Dir+"/"+name+".mat"));return mat;
    }
    static void Tile(string name,float x,float z,float width,float depth,float y,Material mat)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(surfaces);
        obj.transform.position=new Vector3(x,y-.045f,z);obj.transform.localScale=new Vector3(width,.09f,depth);
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());obj.GetComponent<Renderer>().sharedMaterial=mat;
    }
    [MenuItem("Tools/ToonCity/Build Natural Street")]
    public static void Build()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/GameScene.unity" || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(scene.GetRootGameObjects().Any(o=>o.name=="Background_NaturalStreet")) return;
        File.Copy(scene.path,"F:/gamejam/output/GameScene-before-natural-street.unity",true);
        Directory.CreateDirectory(Dir);AssetDatabase.Refresh();
        var original=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="Scene");
        if(!original) throw new Exception("Scene geometry root missing");
        var meshes=original.GetComponentsInChildren<MeshRenderer>(true).Where(r=>!r.GetComponentInParent<Canvas>()).ToArray();
        // Use authored geometry to protect rooms. Large base grids are not buildings.
        var occupied=meshes.Where(r=>r.bounds.size.x<45 && r.bounds.size.z<35 && r.bounds.min.y<2 && r.bounds.max.y>-.9f && r.bounds.size.x>.2f && r.bounds.size.z>.2f).Select(r=>r.bounds).ToArray();
        Func<float,float,bool> inside=(x,z)=>occupied.Any(b=>x>b.min.x-.7f && x<b.max.x+.7f && z>b.min.z-.7f && z<b.max.z+.7f);
        var old=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="Background_Street");
        if(old){Undo.RecordObject(old,"Replace distant street");old.SetActive(false);}
        root=new GameObject("Background_NaturalStreet").transform;Undo.RegisterCreatedObjectUndo(root.gameObject,"Build natural street");
        surfaces=new GameObject("Ground - paths and planted beds").transform;surfaces.SetParent(root);
        props=new GameObject("Street furniture and vegetation").transform;props.SetParent(root);
        lawn=Ground("Lawn","Grass_1A",new Color(.8f,.9f,.7f),1);
        paving=Ground("Warm paving","Tiles_1A",new Color(.86f,.82f,.72f),1);
        soil=Ground("Tree soil","Dirt_1A",new Color(.65f,.58f,.45f),1);
        curb=Ground("Stone edging",null,new Color(.52f,.53f,.48f),1);
        foreach(var r in meshes.Where(r=>r.bounds.size.x>90 && r.bounds.size.z>50)) {Undo.RecordObject(r,"Replace exterior grid finish");r.sharedMaterial=paving;}
        var green=new List<Vector2>();
        for(float z=-26;z<=40;z+=2) for(float x=-24;x<=74;x+=2)
        {
            if(inside(x,z)) continue;
            bool near=false; foreach(var d in new[]{new Vector2(2.4f,0),new Vector2(-2.4f,0),new Vector2(0,2.4f),new Vector2(0,-2.4f)}) if(inside(x+d.x,z+d.y)) near=true;
            bool cross=Mathf.Abs(x-25)<2.2f || Mathf.Abs(x-9)<1.6f || Mathf.Abs(x-59)<1.6f || Mathf.Abs(z-3)<1.6f;
            bool edge=x<-21||x>71||z<-23||z>37;
            bool grass=!near&&!cross&&!edge;
            Tile(grass?"Lawn patch":"Pedestrian paving",x,z,2,2,.045f,grass?lawn:paving);
            if(grass) green.Add(new Vector2(x,z));
        }
        // Tight outer pavement joins the existing authored platform.
        Tile("South sidewalk",25,-27.5f,108,3,.04f,paving);Tile("North sidewalk",25,41.5f,108,3,.04f,paving);
        Tile("West sidewalk",-26,7,4,72,.04f,paving);Tile("East sidewalk",76,7,4,72,.04f,paving);
        for(int i=0;i<10;i++) foreach(float z in new[]{-34f,48f}) {
            var road=Place("Roads/Road_1A",-25.4f+i*11.2f,z,1,90);
            road.transform.localScale=new Vector3(.67f,1,.934f);road.transform.position=new Vector3(road.transform.position.x,-.33f,z);
        }
        for(int i=0;i<6;i++) foreach(float x in new[]{-32f,82f}) {
            var road=Place("Roads/Road_1A",x,-22+i*12,1,0);road.transform.localScale=new Vector3(.67f,1,1);road.transform.position=new Vector3(x,-.33f,road.transform.position.z);
        }
        var used=new List<Vector2>();int bushes=0,trees=0;
        foreach(var p in green.OrderBy(v=>Mathf.Sin(v.x*3.7f+v.y*8.3f))) {
            if(used.Any(v=>Vector2.Distance(v,p)<6)) continue;
            used.Add(p);bool tree=trees<9 && !inside(p.x+2,p.y)&&!inside(p.x-2,p.y)&&!inside(p.x,p.y+2)&&!inside(p.x,p.y-2);
            if(tree){Tile("Soil under tree",p.x,p.y,2,2,.065f,soil);Place("Vegetation/Tree_1A",p.x,p.y,.55f,trees*47);trees++;}
            else if(bushes<28){Place("Vegetation/Bush_1A",p.x,p.y,.65f,bushes*63);bushes++;}
        }
        foreach(float x in new[]{-17f,6f,43f,67f}) foreach(float z in new[]{-27f,41f}) {
            Place("Roads/Streetlight_1A",x,z,.65f,z<0?0:180);
            if(x==-17 || x==43){Place("Props/Bench_1A",x+3,z,.9f,z<0?0:180);Place("Props/Trash_Can_1A",x+5,z,.8f);}
        }
        foreach(float z in new[]{-16f,8f,30f}) foreach(float x in new[]{-26f,76f}) Place("Vegetation/Plant_Pot_1A",x,z,.8f);
        Place("Roads/Hydrant_1A",-26,-24,.85f);Place("Roads/Hydrant_1A",76,37,.85f);
        Place("Props/Power_Box_1A",76,-15,.8f);
        if(root.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled)) throw new Exception("Unexpected enabled background collider");
        foreach(var r in root.GetComponentsInChildren<Renderer>(true)) foreach(var m in r.sharedMaterials)
            if(!m || !m.shader || !m.shader.isSupported || ShaderUtil.ShaderHasError(m.shader)) throw new Exception("Invalid shader on "+r.name);
        PrefabUtility.SaveAsPrefabAsset(root.gameObject,Dir+"/Background_NaturalStreet.prefab");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Preview(); Selection.activeGameObject=root.gameObject;
        File.WriteAllText("F:/gamejam/output/natural-street-built.txt","Saved GameScene. Background colliders disabled. Materials validated. Trees: "+trees+", bushes: "+bushes+". Old street disabled and preserved.\n");
    }
    static void Preview()
    {
        var obj=new GameObject("NaturalStreetPreviewCamera");var camera=obj.AddComponent<Camera>();
        camera.transform.position=new Vector3(25,125,-82);camera.transform.LookAt(new Vector3(25,0,7));camera.orthographic=true;camera.orthographicSize=65;camera.farClipPlane=400;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.23f,.28f);
        var rt=new RenderTexture(1600,1100,24);var prev=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();File.WriteAllBytes("F:/gamejam/output/natural-street-preview.png",tex.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=prev;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(obj);
    }
    static void Refine()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/GameScene.unity")return;
        var group=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="Background_NaturalStreet");if(!group)return;
        root=group.transform;surfaces=root.Find("Ground - paths and planted beds");props=root.Find("Street furniture and vegetation");
        string backup=File.ReadAllText("F:/gamejam/output/GameScene-before-natural-street.unity");
        var original=scene.GetRootGameObjects().First(o=>o.name=="Scene");
        foreach(var r in original.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.bounds.size.x>90&&r.bounds.size.z>50))
        {
            var id=GlobalObjectId.GetGlobalObjectIdSlow(r).targetObjectId;
            var block=System.Text.RegularExpressions.Regex.Match(backup,@"--- !u!23 &"+id+@"\r?\n([\s\S]*?)(?=\r?\n--- !u!|\z)").Value;
            var mat=System.Text.RegularExpressions.Regex.Match(block,@"m_Materials:\s*- \{fileID: 2100000, guid: ([a-f0-9]{32})");
            if(mat.Success){var restored=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(mat.Groups[1].Value));if(restored)r.sharedMaterial=restored;}
        }
        var asphalt=Ground("Corner asphalt",null,new Color(.13f,.14f,.15f),1);
        foreach(float x in new[]{-32f,82f})foreach(float z in new[]{-34f,48f})Tile("Road corner",x,z,8,8,.025f,asphalt);
        var trees=props.Cast<Transform>().Where(t=>t.name=="Tree_1A").ToArray();
        foreach(var tree in trees){var p=tree.position;var yaw=tree.eulerAngles.y;UnityEngine.Object.DestroyImmediate(tree.gameObject);Place("Vegetation/Tree_3A",p.x,p.z,.55f,yaw);}
        PrefabUtility.SaveAsPrefabAsset(group,Dir+"/Background_NaturalStreet.prefab");EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Preview();
        File.WriteAllText("F:/gamejam/output/natural-street-refined.txt","Original base material restored; corners joined; 9 tree props updated; scene saved.\n");
    }
    [MenuItem("Tools/ToonCity/Finalize Natural Street")]
    static void Once(){EditorApplication.delayCall+=()=>{
        if(File.Exists("F:/gamejam/output/natural-street-final.txt"))return;
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/GameScene.unity")return;
        var original=scene.GetRootGameObjects().First(o=>o.name=="Scene");
        int restored=0;
        foreach(var r in original.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.bounds.size.x>90&&r.bounds.size.z>50)){
            var source=PrefabUtility.GetCorrespondingObjectFromSource(r);
            if(source){r.sharedMaterials=source.sharedMaterials;restored++;}
        }
        var g=scene.GetRootGameObjects().First(o=>o.name=="Background_NaturalStreet");
        if(g.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled))throw new Exception("Background collision enabled");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Preview();
        File.WriteAllText("F:/gamejam/output/natural-street-final.txt","Saved; collision check passed; original prefab base renderers restored: "+restored);
    };}
}
