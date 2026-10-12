using UnityEngine;
using UnityEngine.AI;

namespace TapTapGameJam.NPC
{
    /// <summary>NPC 专属行为共用的 NavMesh 采样工具。</summary>
    internal static class NpcNavigationUtility
    {
        public static bool TrySamplePoint(Vector3 point, NavMeshAgent agent, out Vector3 destination)
        {
            int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, areaMask))
            {
                destination = hit.position;
                return true;
            }

            destination = point;
            return false;
        }

        public static bool TrySampleRegion(BoxCollider area, NavMeshAgent agent, Transform owner, out Vector3 destination)
        {
            destination = owner != null ? owner.position : Vector3.zero;
            if (area == null) return false;

            Bounds bounds = area.bounds;
            int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;
            for (int i = 0; i < 12; i++)
            {
                Vector3 candidate = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    bounds.center.y,
                    Random.Range(bounds.min.z, bounds.max.z));

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, areaMask))
                {
                    destination = hit.position;
                    return true;
                }
            }

            return false;
        }
    }
}
