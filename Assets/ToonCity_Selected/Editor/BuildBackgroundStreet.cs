using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildBackgroundStreet
{
    const string Folder = "Assets/ToonCity_Selected/";
    [MenuItem("Tools/ToonCity/Build Background Street")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path != "Assets/Scenes/GameScene.unity" || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(scene.GetRootGameObjects().Any(o => o.name == "Background_Street")) return;
        File.Copy(scene.path, "F:/gamejam/output/GameScene-before-background-street.unity", true);
        var group=new GameObject("Background_Street");
        Undo.RegisterCreatedObjectUndo(group,"Create background street");
        var roads=new GameObject("Roads"); roads.transform.SetParent(group.transform);
        var props=new GameObject("Roadside Props"); props.transform.SetParent(group.transform);
        Action<string,Vector3,float,float,Transform> place=(path,pos,yaw,scale,parent)=> {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Prefabs/"+path+".prefab");
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
            obj.transform.SetParent(parent,false); obj.transform.position=pos;
            obj.transform.rotation=Quaternion.Euler(0,yaw,0); obj.transform.localScale=Vector3.one*scale;
            foreach(var collider in obj.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
            foreach(var behaviour in obj.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled=false;
        };
        float cx=25, cz=3.14f, ground=-0.34f;
        for(int i=0;i<12;i++)
        {
            float x=cx-66+i*12;
            place("Roads/Road_1A",new Vector3(x,ground,cz-48),90,1,roads.transform);
            place("Roads/Road_1A",new Vector3(x,ground,cz+48),90,1,roads.transform);
            if(i%2==0) foreach(int side in new[]{-1,1}) {
                place("Vegetation/Tree_1A",new Vector3(x,0,cz+side*57),i*37,.65f,props.transform);
                place("Roads/Streetlight_1A",new Vector3(x+4,0,cz+side*42),side<0?0:180,.7f,props.transform);
                if(i%4==0) place("Props/Trash_Can_1A",new Vector3(x+6,0,cz+side*41),90,.9f,props.transform);
            }
        }
        for(int j=1;j<8;j++) foreach(int side in new[]{-1,1}) {
            float z=cz-48+j*12;
            place("Roads/Road_1A",new Vector3(cx+side*66,ground,z),0,1,roads.transform);
            place("Vegetation/Tree_1A",new Vector3(cx+side*75,0,z),j*47,.65f,props.transform);
            if(j%2==1) {
                place("Roads/Streetlight_1A",new Vector3(cx+side*60,0,z+3),side<0?90:270,.7f,props.transform);
                place("Props/Trash_Can_1A",new Vector3(cx+side*59,0,z+5),0,.9f,props.transform);
            }
        }
        // A small independent backdrop; no runtime code or collision is required.
        PrefabUtility.SaveAsPrefabAsset(group,Folder+"Background_Street.prefab");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        RenderPreview();
        Selection.activeGameObject=group;
        File.WriteAllText("F:/gamejam/output/street-built.txt", "GameScene saved. Roads: 38; Trees: 26; Lamps: 20; Bins: 14. All added colliders disabled.\n");
    }
    static void RenderPreview()
    {
        float cx=25, cz=3.14f;
        var camObj=new GameObject("StreetPreviewCamera");
        var cam=camObj.AddComponent<Camera>();
        cam.transform.position=new Vector3(cx,125,cz-90);
        cam.transform.LookAt(new Vector3(cx,0,cz));
        cam.orthographic=true; cam.orthographicSize=85; cam.farClipPlane=400;
        cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.18f,.23f,.28f);
        var rt=new RenderTexture(1600,1100,24); cam.targetTexture=rt;
        cam.Render(); var old=RenderTexture.active; RenderTexture.active=rt;
        var image=new Texture2D(1600,1100,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1600,1100),0,0); image.Apply();
        File.WriteAllBytes("F:/gamejam/output/background-street-preview.png",image.EncodeToPNG());
        RenderTexture.active=old; cam.targetTexture=null;
        UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(camObj);
    }
    static void AddGround()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/GameScene.unity") return;
        var group=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="Background_Street");
        if(!group || group.transform.Find("Surrounding Ground")) return;
        var ground=new GameObject("Surrounding Ground"); ground.transform.SetParent(group.transform);
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor",new Color(.30f,.34f,.29f)); material.SetFloat("_Smoothness",0);
        AssetDatabase.CreateAsset(material,Folder+"Background_Ground.mat");
        Action<string,Vector3,Vector3> plane=(name,pos,scale)=>{
            var obj=GameObject.CreatePrimitive(PrimitiveType.Plane); obj.name=name;
            obj.transform.SetParent(ground.transform); obj.transform.position=pos; obj.transform.localScale=scale;
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.GetComponent<Renderer>().sharedMaterial=material;
        };
        plane("North verge",new Vector3(25,-.38f,51.14f),new Vector3(18,1,3.6f));
        plane("South verge",new Vector3(25,-.38f,-44.86f),new Vector3(18,1,3.6f));
        plane("West verge",new Vector3(-45,-.38f,3.14f),new Vector3(4,1,6));
        plane("East verge",new Vector3(95,-.38f,3.14f),new Vector3(4,1,6));
        PrefabUtility.SaveAsPrefabAsset(group,Folder+"Background_Street.prefab");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        RenderPreview();
        File.WriteAllText("F:/gamejam/output/street-ground-built.txt","Background ground saved; no added active colliders.\n");
    }
    public static void Analyze()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path != "Assets/Scenes/GameScene.unity") return;
        var report = "";
        foreach (var root in scene.GetRootGameObjects())
        {
            var rs = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => !r.GetComponentInParent<Canvas>()).ToArray();
            if(rs.Length == 0) continue;
            var b = rs[0].bounds;
            foreach(var r in rs) b.Encapsulate(r.bounds);
            report += root.name + " | center " + b.center.ToString("F2") + " size " + b.size.ToString("F2") + "\n";
            if(root.name == "Scene") foreach(var r in rs.Where(r => r.bounds.size.x > 25 || r.bounds.size.z > 25))
                report += "  "+r.name+" center "+r.bounds.center.ToString("F2")+" size "+r.bounds.size.ToString("F2")+"\n";
        }
        foreach(var path in new[]{"Roads/Road_1A", "Roads/Streetlight_1A", "Vegetation/Tree_1A", "Props/Trash_Can_1A"})
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Prefabs/"+path+".prefab"));
            var rs=obj.GetComponentsInChildren<Renderer>();
            var b=rs[0].bounds; foreach(var r in rs) b.Encapsulate(r.bounds);
            report += path+" | center "+b.center.ToString("F2")+" size "+b.size.ToString("F2")+"\n";
            UnityEngine.Object.DestroyImmediate(obj);
        }
        File.WriteAllText("F:/gamejam/output/street-bounds-details.txt", report);
    }
}
