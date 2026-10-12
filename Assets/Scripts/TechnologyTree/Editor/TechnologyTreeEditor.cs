using System;
using UnityEditor;
using UnityEngine;

namespace TechnologyTree.Editor
{
    public class TechnologyTreeEditor:EditorWindow
    {
        // 在Unity的顶部菜单增加Tools/TechnologyTree
        [MenuItem("Tools/TechnologyTree")]
        public static void OpenWindow()
        {
            GetWindow<TechnologyTreeEditor>();
        }
        
        private TechnologyNode _node;

        private void OnEnable()
        {
            _node = new TechnologyNode();
            _node.Name = "消化系统";
            _node.Position = new Vector2(100, 100);
        }

        private void OnGUI()
        {
            
            Rect rect =
                new Rect(
                    _node.Position.x,
                    _node.Position.y,
                    150,
                    80
                );


            GUI.Box(
                rect,
                _node.Name
            );
            
            HandleInput(rect);
        }
        private void HandleInput(Rect rect)
        {
            Event e = Event.current;


            if(e.type == EventType.MouseDown)
            {
                if(rect.Contains(e.mousePosition))
                {
                    _node.Selected = true;

                    Debug.Log("选中了节点");
                }
            }
        }
    }
}
