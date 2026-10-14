using System.Collections.Generic;

namespace TalentTree
{
    public static class TalentGraphEvaluator
    {
        public static bool CanRemove(TalentTreeAsset asset, PlayerTalentTree progress,
            TalentData candidate, out string reason)
        {
            reason = string.Empty;
            if (asset == null || progress == null || candidate == null)
            {
                reason = "找不到天赋。";
                return false;
            }

            if (progress.GetLevel(candidate) <= 0)
            {
                reason = "该天赋尚未解锁。";
                return false;
            }

            HashSet<TalentData> reachable = ReachableFromRoots(asset, progress, candidate);
            foreach (TalentNodeData node in asset.Nodes)
            {
                TalentData talent = node?.Data;
                if (talent == null || talent == candidate || progress.GetLevel(talent) <= 0) continue;

                if (!reachable.Contains(talent))
                {
                    reason = $"无法解除：天赋「{talent.DisplayName}」将失去天赋树路径。";
                    return false;
                }

                if (node.IsRoot || node.UnlockRule != TalentUnlockRule.AllConnectedTalents) continue;
                // Revalidate the same directed prerequisites after simulating removal.
                // An alternative path cannot substitute for an AllConnectedTalents prerequisite.
                if (!AllPrerequisitesUnlocked(asset, progress, talent, reachable, candidate))
                {
                    reason = $"无法解除：天赋「{talent.DisplayName}」仍然需要全部前置天赋。";
                    return false;
                }
            }

            return true;
        }

        public static bool CanUnlock(TalentTreeAsset asset, PlayerTalentTree progress, TalentData candidate, out string reason)
        {
            reason = string.Empty;
            TalentNodeData node = asset?.FindNode(candidate);
            if (node == null)
            {
                reason = "天赋不在当前天赋树中。";
                return false;
            }
            if (progress == null)
            {
                reason = "找不到天赋进度。";
                return false;
            }
            if (node.IsRoot) return true;

            HashSet<TalentData> reachable = ReachableFromRoots(asset, progress);
            bool valid = node.UnlockRule == TalentUnlockRule.AllConnectedTalents
                ? AllPrerequisitesUnlocked(asset, progress, candidate, reachable, null)
                : HasReachablePrerequisite(asset, candidate, reachable);

            if (!valid) reason = node.UnlockRule == TalentUnlockRule.AllConnectedTalents
                ? "该天赋要求所有前置天赋解锁。"
                : "该天赋还没有沿前置连接连通到起点的路径。";
            return valid;
        }

        private static bool AllPrerequisitesUnlocked(TalentTreeAsset asset, PlayerTalentTree progress,
            TalentData candidate, HashSet<TalentData> reachable, TalentData removed)
        {
            bool hasPrerequisite = false;
            foreach (TalentData prerequisite in asset.GetPrerequisites(candidate))
            {
                hasPrerequisite = true;
                TalentNodeData node = asset.FindNode(prerequisite);
                if (node == null || prerequisite == removed || !reachable.Contains(prerequisite) ||
                    (!node.IsRoot && EffectiveLevel(progress, prerequisite, removed) <= 0)) return false;
            }
            return hasPrerequisite;
        }

        private static bool HasReachablePrerequisite(TalentTreeAsset asset, TalentData candidate,
            HashSet<TalentData> reachable)
        {
            foreach (TalentData prerequisite in asset.GetPrerequisites(candidate))
                if (reachable.Contains(prerequisite)) return true;
            return false;
        }

        private static HashSet<TalentData> ReachableFromRoots(TalentTreeAsset asset, PlayerTalentTree progress)
            => ReachableFromRoots(asset, progress, null);

        private static HashSet<TalentData> ReachableFromRoots(TalentTreeAsset asset,
            PlayerTalentTree progress, TalentData removed)
        {
            var reachable = new HashSet<TalentData>();
            var queue = new Queue<TalentData>();
            foreach (TalentNodeData node in asset.Nodes)
            {
                if (node?.Data == null || !node.IsRoot || node.Data == removed || !reachable.Add(node.Data)) continue;
                queue.Enqueue(node.Data);
            }

            while (queue.Count > 0)
            {
                TalentData current = queue.Dequeue();
                foreach (TalentData neighbor in asset.GetDependents(current))
                {
                    TalentNodeData node = asset.FindNode(neighbor);
                    if (node == null || neighbor == removed || (!node.IsRoot && EffectiveLevel(progress, neighbor, removed) <= 0) || !reachable.Add(neighbor)) continue;
                    queue.Enqueue(neighbor);
                }
            }
            return reachable;
        }

        private static int EffectiveLevel(PlayerTalentTree progress, TalentData talent, TalentData removed)
            => talent == removed ? 0 : progress.GetLevel(talent);
    }
}
