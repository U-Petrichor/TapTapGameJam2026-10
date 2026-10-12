
using System.Collections.Generic;
using UnityEngine;

namespace TechnologyTree
{
    public class TechnologyData : ScriptableObject
    {
        public string id;
        public string name;
        public int maxLevel;
        // 前置条件
        public List<ITechnologyRequirement> Requirements;
        // 此天赋带来的效果
        // public List<ITechnologyEffect> Effects;

    }
}
