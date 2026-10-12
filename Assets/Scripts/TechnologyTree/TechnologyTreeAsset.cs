
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using UnityEngine;
using UnityEngine.Serialization;

namespace TechnologyTree
{
    public class TechnologyTreeAsset : ScriptableObject
    {
        public List<NodeData> nodes;

    }
    [Serializable]
    public class NodeData
    {
        public TechnologyData data;
        public Vector2 position;
    }
}