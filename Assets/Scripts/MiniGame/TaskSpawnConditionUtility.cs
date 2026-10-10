using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    internal static class TaskSpawnConditionUtility
    {
        public static bool TryEvaluate(
            IEnumerable<MonoBehaviour> conditionObjects,
            out string unavailableReason)
        {
            if (conditionObjects != null)
            {
                foreach (var component in conditionObjects)
                {
                    if (component == null)
                    {
                        unavailableReason = "存在为空的任务生成条件引用";
                        return false;
                    }
                    if (!(component is ITaskSpawnCondition condition))
                    {
                        unavailableReason = $"组件 {component.GetType().Name} 没有实现 ITaskSpawnCondition";
                        return false;
                    }
                    if (!condition.IsAvailable)
                    {
                        unavailableReason = string.IsNullOrWhiteSpace(condition.UnavailableReason)
                            ? $"条件 {component.GetType().Name} 尚未满足"
                            : condition.UnavailableReason;
                        return false;
                    }
                }
            }

            unavailableReason = string.Empty;
            return true;
        }
    }
}
