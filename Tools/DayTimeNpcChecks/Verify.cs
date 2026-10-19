using System;
using System.Reflection;
using TapTapGameJam.Core;
using TapTapGameJam.NPC;
using TapTapGameJam.Types;
using UnityEngine;

// Run with: unity command run_script --file Tools/DayTimeNpcChecks/Verify.cs
// Run in Play mode in GameScene, then exit Play mode to restore the scene.
public static class DayTimeNpcChecks
{
    private static int _checks;
    private static System.Collections.IEnumerator _routine;
    private const string Report = "Temp/DayTimeNpcChecks.json";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }

    public static object Run()
    {
        _checks = 0;
        UnityEditor.EditorApplication.isPaused = false;
        System.IO.File.WriteAllText(Report, "{\"running\":true}");
        _routine = Verify();
        UnityEditor.EditorApplication.update += Tick;
        return new { started = true, report = Report };
    }

    private static void Tick()
    {
        if (Application.isPlaying) UnityEditor.EditorApplication.isPaused = false;
        try
        {
            if (_routine.MoveNext()) return;
        }
        catch (Exception exception)
        {
            System.IO.File.WriteAllText(Report, Newtonsoft.Json.JsonConvert.SerializeObject(
                new { passed = false, assertions = _checks, error = exception.ToString() }));
        }
        UnityEditor.EditorApplication.update -= Tick;
    }

    private static System.Collections.IEnumerator Verify()
    {
        Check(Application.isPlaying, "Checks require Play mode");
        var manager = GameObject.Find("NPCManager");
        var root = manager.transform.Find("DayTimeNPC").gameObject;
        var controller = manager.GetComponent<DayTimeNpcController>();
        var phase = UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>();
        phase.EnterPhase(GamePhase.Day);
        int firstFrame = Time.frameCount;
        while (Time.frameCount < firstFrame + 3) yield return null;
        Check(root.activeInHierarchy, "First day must show NPCs");
        var npcs = root.GetComponentsInChildren<NPC>(true);
        Check(npcs.Length == 2, "Scene should contain Boss and Secretary behaviors");
        var colleague = root.transform.Find("善良同事");
        var passiveAnimator = colleague.GetComponent<Animator>();
        var poses = (System.Collections.Generic.Dictionary<Animator, Pose>)typeof(DayTimeNpcController)
            .GetField("_passiveSpawnPoses", Private).GetValue(controller);
        Check(poses.ContainsKey(passiveAnimator), "Colleague without NPC script must be captured");

        for (int day = 0; day < 3; day++)
        {
            foreach (var npc in npcs)
            {
                if (day == 1)
                {
                    npc.StateMachine.ChangeState(npc.Idle);
                    typeof(IdleState).GetField("_remainingTime", Private).SetValue(npc.Idle, -17f);
                }
                else npc.StateMachine.ChangeState(npc.Walk);
                if (npc.Agent != null && npc.Agent.isOnNavMesh)
                    npc.Agent.Warp(npc.HomePosition + Vector3.right);
                else npc.transform.position += Vector3.right;
                npc.transform.rotation = Quaternion.Euler(0, 137, 0);
                if (npc is BossNPC)
                    typeof(BossNPC).GetField("_wanderCount", Private).SetValue(npc, 99);
                if (npc is SecretaryNPC)
                    typeof(SecretaryNPC).GetField("_externalStopCount", Private).SetValue(npc, 99);
            }
            colleague.position += Vector3.right * 3f;
            colleague.rotation = Quaternion.Euler(0, 111, 0);

            phase.EnterPhase(GamePhase.Shopping);
            Check(!root.activeSelf, "Shopping must hide the whole group");
            foreach (var npc in npcs)
                Check(!npc.isActiveAndEnabled && !npc.Agent.isActiveAndEnabled,
                    npc.name + " behavior and navigation must be disabled");
            Check(!passiveAnimator.isActiveAndEnabled, "Colleague animation must be disabled");
            phase.EnterPhase(GamePhase.Night);
            Check(!root.activeSelf && controller.isActiveAndEnabled, "Controller must remain active at night");
            phase.EnterPhase(GamePhase.Day);
            foreach (var npc in npcs)
            {
                var spawnRotation = (Quaternion)typeof(NPC).GetField("_homeRotation", Private).GetValue(npc);
                Check(Quaternion.Angle(npc.transform.rotation, spawnRotation) < 0.1f,
                    npc.name + " must restore rotation before animation resumes");
            }
            // NavMeshAgent 重新激活后要经过一次引擎更新才完成注册。
            int resetFrame = Time.frameCount;
            while (Time.frameCount < resetFrame + 3) yield return null;
            Check(root.activeInHierarchy, "Next day must show the whole group");
            foreach (var npc in npcs)
            {
                Check(Vector2.Distance(new Vector2(npc.transform.position.x, npc.transform.position.z),
                        new Vector2(npc.HomePosition.x, npc.HomePosition.z)) < 0.01f &&
                        Mathf.Abs(npc.transform.position.y - npc.HomePosition.y) < 0.5f,
                    npc.name + " must return to spawn");
                Check(npc.StateMachine.CurrentState == npc.Idle, npc.name + " must restart in Idle");
                Check((float)typeof(IdleState).GetField("_remainingTime", Private).GetValue(npc.Idle) >= 0f,
                    npc.name + " must have a fresh idle timer");
                Check(npc.Agent.isOnNavMesh && npc.Agent.isStopped && !npc.Agent.hasPath,
                    npc.name + " must rejoin NavMesh without yesterday's path; onMesh=" + npc.Agent.isOnNavMesh +
                    ", stopped=" + (npc.Agent.isOnNavMesh && npc.Agent.isStopped) +
                    ", path=" + (npc.Agent.isOnNavMesh && npc.Agent.hasPath));
                if (npc is BossNPC)
                {
                    Check((int)typeof(BossNPC).GetField("_wanderCount", Private).GetValue(npc) == 0,
                        "Boss patrol count must reset");
                    Check(typeof(BossNPC).GetField("_pendingRegion", Private).GetValue(npc) == null,
                        "Boss pending region must reset");
                }
                if (npc is SecretaryNPC)
                {
                    Check((int)typeof(SecretaryNPC).GetField("_externalStopCount", Private).GetValue(npc) == 0,
                        "Secretary route count must reset");
                    Check(typeof(SecretaryNPC).GetField("_pendingStop", Private).GetValue(npc) == null &&
                          typeof(SecretaryNPC).GetField("_lastStop", Private).GetValue(npc) == null,
                        "Secretary route memory must reset");
                }
            }
            Check(Vector3.Distance(colleague.position, poses[passiveAnimator].position) < 0.01f &&
                  Quaternion.Angle(colleague.rotation, poses[passiveAnimator].rotation) < 0.01f,
                "Colleague position and rotation must reset");
        }

        phase.EnterPhase(GamePhase.Night);
        controller.enabled = false;
        controller.enabled = true;
        Check(!root.activeSelf, "Re-enabling controller at night must keep group hidden");
        phase.EnterPhase(GamePhase.Day);
        Check(root.activeSelf, "Re-enabled controller must receive the next day event");
        System.IO.File.WriteAllText(Report, Newtonsoft.Json.JsonConvert.SerializeObject(
            new { passed = true, assertions = _checks, cycles = 3, npcs = new[] { "Boss", "Secretary", "善良同事" } }));
    }
}
