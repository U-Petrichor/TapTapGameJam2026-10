using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TapTapGameJam.Events;
using TapTapGameJam.Interactables;
using TapTapGameJam.Player;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Run in a fresh GameScene Play Mode session. Never save runtime state.
public static class VerifyDoor10Passage
{
    static readonly List<string> Checks=new();
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message); Checks.Add("PASS: "+message);}
    public static async Task<object> Main()
    {
        Check(EditorApplication.isPlaying,"Play Mode is active");
        EditorApplication.isPaused=false;
        bool background=Application.runInBackground;
        Application.runInBackground=true;
        var director=Object.FindFirstObjectByType<SecurityDoorRevealDirector>();
        var player=Object.FindFirstObjectByType<PlayerBasic>();
        var input=player.GetComponent<PlayerInputManager>();
        var rb=player.GetComponent<Rigidbody>();
        var interaction=player.GetComponent<PlayerInteractionManager>();
        var door=Object.FindObjectsByType<SecurityDoor>(FindObjectsSortMode.None).First(d=>d.name=="SecurityDoor (10)");
        Vector3 original=rb.position;
        try
        {
            director.enabled=false;
            EventPipeline.Publish(new EventNewDay(2));
            var start=new Vector3(door.transform.position.x,original.y,door.transform.position.z-1.1f);
            rb.position=start; player.transform.position=start; rb.linearVelocity=Vector3.zero;
            Physics.SyncTransforms();
            await Task.Delay(250);
            Check(!door.IsAvailable && Field<Collider>(door,"_blockCollider").enabled,"Locked door blocks player");
            EventPipeline.Publish(new EventNewDay(3));
            await Task.Delay(700);
            Check(!Field<Collider>(door,"_blockCollider").enabled,"Player opening removes blocking collider");
            Check(Field<Collider>(door,"_detectCollider").enabled && !interaction.Candidates.Any(c => c.GetComponent<SecurityDoor>() != null),"Opened door keeps detection without interaction candidates");
            var cam=Field<Transform>(player.GetComponent<PlayerMovement>(),"_camTransform");
            Vector3 forward=cam.forward; forward.y=0; forward.Normalize();
            Vector3 right=cam.right; right.y=0; right.Normalize();
            input.MoveInput=new Vector2(Vector3.Dot(Vector3.forward,right),Vector3.Dot(Vector3.forward,forward));
            await Task.Delay(1500);
            input.MoveInput=Vector2.zero;
            Check(rb.position.z>door.transform.position.z+0.6f,"Real player movement crosses door into room: "+rb.position);
            return new {passed=true,graphics=SystemInfo.graphicsDeviceType.ToString(),checks=Checks.ToArray()};
        }
        finally
        {
            input.MoveInput=Vector2.zero; rb.linearVelocity=Vector3.zero; rb.position=original;
            Application.runInBackground=background;
            System.IO.File.WriteAllText("Temp/Door10PassageVerification.txt",string.Join("\n",Checks));
        }
    }
}

