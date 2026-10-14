using System.Collections.Generic;
using UnityEngine;

namespace TalentTree
{
    [CreateAssetMenu(menuName = "Talent Tree/Talent Tree", fileName = "TalentTree")]
    public sealed class TalentTreeAsset : ScriptableObject
    {
        [SerializeField] private List<TalentNodeData> _nodes = new();
        [SerializeField] private List<TalentGraphEdgeData> _edges = new();

        public IReadOnlyList<TalentNodeData> Nodes => _nodes;
        public IReadOnlyList<TalentGraphEdgeData> Edges => _edges;
        public List<TalentNodeData> MutableNodes => _nodes ??= new List<TalentNodeData>();
        public List<TalentGraphEdgeData> MutableEdges => _edges ??= new List<TalentGraphEdgeData>();

        private void OnEnable()
        {
            _nodes ??= new List<TalentNodeData>();
            _edges ??= new List<TalentGraphEdgeData>();
        }

        public TalentData FindTalent(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || _nodes == null) return null;
            foreach (TalentNodeData node in _nodes)
            {
                if (node != null && node.Data != null && node.Data.Id == id) return node.Data;
            }
            return null;
        }

        public TalentNodeData FindNode(TalentData data)
        {
            if (data == null || _nodes == null) return null;
            foreach (TalentNodeData node in _nodes)
                if (node?.Data == data) return node;
            return null;
        }

        public bool HasEdge(TalentData first, TalentData second)
        {
            if (first == null || second == null || _edges == null) return false;
            foreach (TalentGraphEdgeData edge in _edges)
                if (edge != null && edge.First == first && edge.Second == second) return true;
            return false;
        }

        public bool AddEdge(TalentData first, TalentData second)
        {
            if (first == null || second == null || first == second || FindNode(first) == null || FindNode(second) == null ||
                HasEdge(first, second) || WouldCreateCycle(first, second)) return false;
            MutableEdges.Add(new TalentGraphEdgeData(first, second));
            return true;
        }

        public void RemoveNode(TalentData data)
        {
            if (data == null) return;
            MutableNodes.RemoveAll(node => node?.Data == data);
            MutableEdges.RemoveAll(edge => edge == null || edge.Connects(data));
        }

        public IEnumerable<TalentData> GetNeighbors(TalentData data)
        {
            if (data == null || _edges == null) yield break;
            foreach (TalentGraphEdgeData edge in _edges)
            {
                TalentData neighbor = edge?.Other(data);
                if (neighbor != null) yield return neighbor;
            }
        }

        public IEnumerable<TalentData> GetPrerequisites(TalentData data)
        {
            if (data == null || _edges == null) yield break;
            foreach (TalentGraphEdgeData edge in _edges)
                if (edge?.Second == data && edge.First != null) yield return edge.First;
        }

        public IEnumerable<TalentData> GetDependents(TalentData data)
        {
            if (data == null || _edges == null) yield break;
            foreach (TalentGraphEdgeData edge in _edges)
                if (edge?.First == data && edge.Second != null) yield return edge.Second;
        }

        public bool WouldCreateCycle(TalentData prerequisite, TalentData dependent)
        {
            if (prerequisite == null || dependent == null) return false;
            var visited = new HashSet<TalentData>();
            var pending = new Stack<TalentData>();
            pending.Push(dependent);
            while (pending.Count > 0)
            {
                TalentData current = pending.Pop();
                if (current == prerequisite) return true;
                if (!visited.Add(current)) continue;
                foreach (TalentData next in GetDependents(current)) pending.Push(next);
            }
            return false;
        }
    }
}
