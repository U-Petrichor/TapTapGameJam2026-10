
using System.Collections.Generic;
using UnityEngine;

namespace TechnologyTree
{
    public class TechnologyNode
    {
        public string Id;
        public string Name;
        public Vector2 Position;
        public bool Selected;
        public TechnologyData Data;
        public List<TechnologyNode> Children;
    }
}
