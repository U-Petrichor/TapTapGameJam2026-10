using UnityEngine;
using TechnologyTree;

namespace TechnologyTree.Requirements
{
    public class RequireTechnologyLevel : ITechnologyRequirement
    {
        public TechnologyData Technology;
        public int Level;
        public bool Check()
        {
            throw new System.NotImplementedException();
        }
    }
}
