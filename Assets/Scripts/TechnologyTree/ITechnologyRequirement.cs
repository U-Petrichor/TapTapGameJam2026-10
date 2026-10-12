using UnityEngine;

namespace TechnologyTree
{
    public interface ITechnologyRequirement
    {
        /// <summary>
        /// 根据玩家传入的天赋树，决定当前天赋是否能点
        /// </summary>
        bool Check(); // TODO PlayerTechnologyTree tree
    }
}
