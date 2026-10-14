using System;
using UnityEditor;
using UnityEngine;

namespace TalentTree.Editor
{
    public sealed class TalentTreeEditorWindow : EditorWindow
    {
        private const float InspectorWidth = 330f;
        private const float NodeWidth = 190f;
        private const float NodeHeight = 82f;
        private const float PortSize = 10f;
        private const float PortHitRadius = 12f;

        private TalentTreeAsset _asset;
        private TalentNodeData _selectedNode;
        private int _selectedLevel = 1;
        private Vector2 _pan;
        private float _zoom = 1f;
        private TalentNodeData _draggingNode;
        private Vector2 _dragOffset;
        private TalentNodeData _linkSource;

        [MenuItem("Tools/TalentTree")]
        public static void OpenWindow() => GetWindow<TalentTreeEditorWindow>("Talent Tree");

        private void OnSelectionChange()
        {
            if (Selection.activeObject is TalentTreeAsset asset)
            {
                _asset = asset;
                _selectedNode = null;
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (_asset == null)
            {
                EditorGUILayout.HelpBox("请选择一个 TalentTreeAsset，或点击 New 创建天赋树。", MessageType.Info);
                return;
            }

            Rect canvas = new(0f, 20f, Mathf.Max(0f, position.width - InspectorWidth), Mathf.Max(0f, position.height - 20f));
            Rect inspector = new(canvas.xMax, canvas.y, InspectorWidth, canvas.height);
            DrawCanvas(canvas);
            DrawInspector(inspector);
            HandleCanvasEvents(canvas);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            TalentTreeAsset next = (TalentTreeAsset)EditorGUILayout.ObjectField(_asset, typeof(TalentTreeAsset), false, GUILayout.Width(220f));
            if (next != _asset)
            {
                _asset = next;
                _selectedNode = null;
            }

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(48f))) CreateTreeAsset();
            using (new EditorGUI.DisabledScope(_asset == null))
            {
                if (GUILayout.Button("Add Node", EditorStyles.toolbarButton, GUILayout.Width(70f))) AddNode();
                if (GUILayout.Button("Frame All", EditorStyles.toolbarButton, GUILayout.Width(70f))) FrameAll();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"Zoom {_zoom:0.00}x", EditorStyles.label, GUILayout.Width(82f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCanvas(Rect canvas)
        {
            EditorGUI.DrawRect(canvas, new Color(0.12f, 0.13f, 0.15f));
            GUI.BeginGroup(canvas);
            DrawGrid(canvas.size);
            DrawConnections(canvas);

            GUIStyle nodeTitleStyle = CreateNodeStyle(EditorStyles.boldLabel, 13);
            GUIStyle nodeMetaStyle = CreateNodeStyle(EditorStyles.miniLabel, 10);

            if (_asset != null)
            {
                foreach (TalentNodeData node in _asset.Nodes)
                {
                    if (node?.Data == null) continue;
                    DrawNode(canvas, node, nodeTitleStyle, nodeMetaStyle);
                }
            }

            if (_linkSource != null)
            {
                Handles.BeginGUI();
                Handles.color = new Color(0.4f, 0.8f, 1f, 0.9f);
                Vector2 source = GetPortPoint(_linkSource, Event.current.mousePosition);
                Vector2 target = Event.current.mousePosition;
                DrawConnection(source, target);
                Handles.EndGUI();
            }

            GUI.EndGroup();
        }

        private void DrawGrid(Vector2 size)
        {
            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.04f);
            float spacing = 32f * _zoom;
            Vector2 offset = new Vector2(_pan.x % spacing, _pan.y % spacing);
            for (float x = offset.x; x < size.x; x += spacing) Handles.DrawLine(new Vector3(x, 0f), new Vector3(x, size.y));
            for (float y = offset.y; y < size.y; y += spacing) Handles.DrawLine(new Vector3(0f, y), new Vector3(size.x, y));
            Handles.EndGUI();
        }

        private void DrawConnections(Rect canvas)
        {
            if (_asset == null || _asset.Edges == null) return;
            Handles.BeginGUI();
            Handles.color = new Color(0.45f, 0.75f, 0.95f, 0.9f);
            foreach (TalentGraphEdgeData edge in _asset.Edges)
            {
                TalentNodeData first = FindNode(edge?.First);
                TalentNodeData second = FindNode(edge?.Second);
                if (first == null || second == null) continue;
                Vector2 source = GetPortPoint(first, LocalNodeRect(second).center);
                Vector2 destination = GetPortPoint(second, LocalNodeRect(first).center);
                DrawConnection(source, destination);
            }
            Handles.EndGUI();
        }

        private void DrawNode(Rect canvas, TalentNodeData node, GUIStyle titleStyle, GUIStyle metaStyle)
        {
            Rect rect = LocalNodeRect(node);
            Color background = node == _selectedNode ? new Color(0.18f, 0.38f, 0.55f) : new Color(0.2f, 0.22f, 0.25f);
            EditorGUI.DrawRect(rect, background);
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            float padding = Mathf.Max(4f, 10f * _zoom);
            GUI.Label(new Rect(rect.x + padding, rect.y + 6f * _zoom, rect.width - padding * 2f, 22f * _zoom), node.Data.DisplayName, titleStyle);
            if (_zoom >= 0.55f)
            {
                GUI.Label(new Rect(rect.x + padding, rect.y + 32f * _zoom, rect.width - padding * 2f, 18f * _zoom), $"ID: {node.Data.Id}", metaStyle);
                string rule = node.IsRoot ? "起点" : node.UnlockRule == TalentUnlockRule.AllConnectedTalents ? "全部前置" : "前置路径";
                GUI.Label(new Rect(rect.x + padding, rect.y + 52f * _zoom, rect.width - padding * 2f, 18f * _zoom), $"最高等级: {node.Data.MaxLevel} · {rule}", metaStyle);
            }

            Color portColor = new Color(0.4f, 0.8f, 1f);
            DrawPort(rect, PortSide.Left, portColor);
            DrawPort(rect, PortSide.Right, portColor);
            DrawPort(rect, PortSide.Top, portColor);
            DrawPort(rect, PortSide.Bottom, portColor);
        }

        private void DrawInspector(Rect rect)
        {
            GUI.BeginGroup(rect, EditorStyles.helpBox);
            GUILayout.BeginArea(new Rect(10f, 8f, rect.width - 20f, rect.height - 16f));
            if (_selectedNode == null || _selectedNode.Data == null)
            {
                EditorGUILayout.HelpBox("选择一个节点编辑天赋。从前置天赋的端点拖到后续天赋，创建有向连接：前置 → 后续。", MessageType.Info);
                GUILayout.EndArea();
                GUI.EndGroup();
                return;
            }

            TalentData data = _selectedNode.Data;
            EditorGUILayout.LabelField("Talent", EditorStyles.boldLabel);
            string id = EditorGUILayout.TextField("ID", data.Id);
            string displayName = EditorGUILayout.TextField("名称", data.DisplayName);
            Sprite icon = (Sprite)EditorGUILayout.ObjectField("图标", data.Icon, typeof(Sprite), false);
            if (id != data.Id || displayName != data.DisplayName || icon != data.Icon)
            {
                Undo.RecordObject(data, "Edit Talent");
                data.SetIdentity(id, displayName);
                SerializedObject serialized = new(data);
                serialized.FindProperty("_icon").objectReferenceValue = icon;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }

            bool isRoot = EditorGUILayout.Toggle("起点", _selectedNode.IsRoot);
            TalentUnlockRule unlockRule = (TalentUnlockRule)EditorGUILayout.EnumPopup("解锁规则", _selectedNode.UnlockRule);
            EditorGUILayout.HelpBox("连线方向：前置 → 后续。All Connected Talents 要求全部前置；Any Path From Root 允许任一前置路径。", MessageType.Info);
            if (isRoot != _selectedNode.IsRoot || unlockRule != _selectedNode.UnlockRule)
            {
                Undo.RecordObject(_asset, "Edit Talent Graph Rule");
                _selectedNode.IsRoot = isRoot;
                _selectedNode.UnlockRule = unlockRule;
                EditorUtility.SetDirty(_asset);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("等级", EditorStyles.boldLabel);
            if (GUILayout.Button("+", GUILayout.Width(24f)))
            {
                Undo.RecordObject(data, "Add Talent Level");
                data.AddLevel();
                _selectedLevel = data.MaxLevel;
                EditorUtility.SetDirty(data);
            }
            if (GUILayout.Button("-", GUILayout.Width(24f)) && data.MaxLevel > 0)
            {
                Undo.RecordObject(data, "Remove Talent Level");
                data.MutableLevels.RemoveAt(data.MaxLevel - 1);
                _selectedLevel = Mathf.Clamp(_selectedLevel, 1, Mathf.Max(1, data.MaxLevel));
                EditorUtility.SetDirty(data);
            }
            EditorGUILayout.EndHorizontal();

            if (data.MaxLevel > 0)
            {
                _selectedLevel = Mathf.Clamp(EditorGUILayout.IntSlider("编辑等级", _selectedLevel, 1, data.MaxLevel), 1, data.MaxLevel);
                TalentLevelData level = data.GetLevelData(_selectedLevel);
                int cost = EditorGUILayout.IntField("天赋点消耗", level.TalentPointCost);
                if (cost != level.TalentPointCost)
                {
                    Undo.RecordObject(data, "Edit Talent Cost");
                    level.SetTalentPointCost(cost);
                    EditorUtility.SetDirty(data);
                }

                DrawRequirements(data, level);
                DrawEffects(data, level);
            }

            DrawConnectedEdges();
            EditorGUILayout.Space(8f);
            if (GUILayout.Button("删除节点")) RemoveSelectedNode();
            GUILayout.EndArea();
            GUI.EndGroup();
        }

        private void DrawRequirements(TalentData data, TalentLevelData level)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("前置条件（全部满足）", EditorStyles.boldLabel);
            for (int i = level.MutableRequirements.Count - 1; i >= 0; i--)
            {
                TalentRequirement requirement = level.MutableRequirements[i];
                if (requirement is TalentLevelRequirement techRequirement)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    TalentData talent = (TalentData)EditorGUILayout.ObjectField("天赋", techRequirement.Talent, typeof(TalentData), false);
                    int requiredLevel = EditorGUILayout.IntField("最低等级", techRequirement.RequiredLevel);
                    if (talent != techRequirement.Talent || requiredLevel != techRequirement.RequiredLevel)
                    {
                        Undo.RecordObject(data, "Edit Talent Requirement");
                        techRequirement.SetRequirement(talent, requiredLevel);
                        EditorUtility.SetDirty(data);
                    }
                    if (GUILayout.Button("删除条件"))
                    {
                        Undo.RecordObject(data, "Remove Talent Requirement");
                        level.MutableRequirements.RemoveAt(i);
                        EditorUtility.SetDirty(data);
                    }
                    EditorGUILayout.EndVertical();
                }
            }

            if (GUILayout.Button("添加天赋等级条件"))
            {
                Undo.RecordObject(data, "Add Talent Requirement");
                level.MutableRequirements.Add(new TalentLevelRequirement());
                EditorUtility.SetDirty(data);
            }
        }

        private void DrawEffects(TalentData data, TalentLevelData level)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("效果", EditorStyles.boldLabel);
            for (int i = level.MutableEffects.Count - 1; i >= 0; i--)
            {
                TalentEffect effect = level.MutableEffects[i];
                if (effect == null) continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                string key = EditorGUILayout.TextField("效果 Key", effect.EffectKey);
                float value = EditorGUILayout.FloatField("数值", effect.NumericValue);
                string payload = EditorGUILayout.TextField("Payload", effect.Payload);
                if (key != effect.EffectKey || !Mathf.Approximately(value, effect.NumericValue) || payload != effect.Payload)
                {
                    Undo.RecordObject(data, "Edit Talent Effect");
                    effect.SetData(key, value, payload);
                    EditorUtility.SetDirty(data);
                }
                if (GUILayout.Button("删除效果"))
                {
                    Undo.RecordObject(data, "Remove Talent Effect");
                    level.MutableEffects.RemoveAt(i);
                    EditorUtility.SetDirty(data);
                }
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("添加效果"))
            {
                Undo.RecordObject(data, "Add Talent Effect");
                level.MutableEffects.Add(new TalentEffect());
                EditorUtility.SetDirty(data);
            }
        }

        private void DrawConnectedEdges()
        {
            if (_asset == null || _selectedNode?.Data == null) return;
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("图连接（前置 → 后续）", EditorStyles.boldLabel);
            for (int i = _asset.MutableEdges.Count - 1; i >= 0; i--)
            {
                TalentGraphEdgeData edge = _asset.MutableEdges[i];
                if (edge == null || !edge.Connects(_selectedNode.Data)) continue;
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                bool incoming = edge.Second == _selectedNode.Data;
                EditorGUILayout.LabelField($"{(incoming ? "前置 ←" : "后续 →")} {edge.Other(_selectedNode.Data)?.DisplayName ?? "缺少节点"}");
                if (GUILayout.Button("删除", GUILayout.Width(44f)))
                {
                    Undo.RecordObject(_asset, "Remove Talent Graph Edge");
                    _asset.MutableEdges.RemoveAt(i);
                    EditorUtility.SetDirty(_asset);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void HandleCanvasEvents(Rect canvas)
        {
            Event e = Event.current;
            if (!canvas.Contains(e.mousePosition)) return;
            Vector2 localMouse = e.mousePosition - canvas.position;

            if (e.type == EventType.ScrollWheel)
            {
                float oldZoom = _zoom;
                _zoom = Mathf.Clamp(_zoom - e.delta.y * 0.05f, 0.35f, 2.5f);
                _pan = localMouse - (localMouse - _pan) * (_zoom / oldZoom);
                e.Use();
                Repaint();
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 2)
            {
                _dragOffset = e.mousePosition;
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDrag && e.button == 2)
            {
                _pan += e.mousePosition - _dragOffset;
                _dragOffset = e.mousePosition;
                Repaint();
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                TalentNodeData node = FindNodeAt(localMouse);
                if (node != null)
                {
                    _selectedNode = node;
                    if (FindPortAt(localMouse) == node)
                    {
                        _linkSource = node;
                        e.Use();
                    }
                    else
                    {
                        _draggingNode = node;
                        _dragOffset = CanvasPosition(localMouse) - node.Position;
                        e.Use();
                    }
                    Repaint();
                }
            }

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                if (_draggingNode != null)
                {
                    Undo.RecordObject(_asset, "Move Talent Node");
                    _draggingNode.Position = CanvasPosition(localMouse) - _dragOffset;
                    EditorUtility.SetDirty(_asset);
                    Repaint();
                    e.Use();
                }
                else if (_linkSource != null)
                {
                    Repaint();
                    e.Use();
                }
            }

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (_linkSource != null)
                {
                    TalentNodeData target = FindNodeAt(localMouse);
                    if (target != null && target != _linkSource && target.Data != null)
                    {
                        AddLink(_linkSource.Data, target.Data);
                    }
                    _linkSource = null;
                    Repaint();
                    e.Use();
                }
                _draggingNode = null;
            }
        }

        private void AddLink(TalentData source, TalentData target)
        {
            if (source == null || target == null) return;
            if (_asset.WouldCreateCycle(source, target))
            {
                ShowNotification(new GUIContent("无法创建连接：会形成循环依赖。"));
                return;
            }
            Undo.RecordObject(_asset, "Add Talent Graph Edge");
            if (_asset.AddEdge(source, target)) EditorUtility.SetDirty(_asset);
            _selectedNode = FindNode(target);
        }

        private void CreateTreeAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Talent Tree", "TalentTree", "asset", "Choose a location for the talent tree.");
            if (string.IsNullOrEmpty(path)) return;
            TalentTreeAsset asset = CreateInstance<TalentTreeAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _asset = asset;
            _selectedNode = null;
            Selection.activeObject = asset;
        }

        private void AddNode()
        {
            if (_asset == null) return;
            Undo.RecordObject(_asset, "Add Talent Node");
            TalentData data = CreateInstance<TalentData>();
            data.SetIdentity($"talent_{Guid.NewGuid():N}", "新天赋");
            data.AddLevel();
            AssetDatabase.AddObjectToAsset(data, _asset);
            TalentNodeData node = new();
            node.SetData(data);
            node.IsRoot = _asset.MutableNodes.Count == 0;
            node.Position = new Vector2(80f + _asset.MutableNodes.Count * 30f, 80f + _asset.MutableNodes.Count * 30f);
            _asset.MutableNodes.Add(node);
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
            _selectedNode = node;
            Selection.activeObject = data;
        }

        private void RemoveSelectedNode()
        {
            if (_asset == null || _selectedNode == null) return;
            TalentData data = _selectedNode.Data;
            Undo.RecordObject(_asset, "Remove Talent Node");
            _asset.RemoveNode(data);
            if (data != null)
            {
                Undo.DestroyObjectImmediate(data);
            }
            _selectedNode = null;
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
        }

        private void FrameAll()
        {
            if (_asset == null || _asset.Nodes.Count == 0) return;
            _pan = Vector2.zero;
            _zoom = 1f;
        }

        private TalentNodeData FindNodeAt(Vector2 localMouse)
        {
            if (_asset == null) return null;
            for (int i = _asset.Nodes.Count - 1; i >= 0; i--)
            {
                TalentNodeData node = _asset.Nodes[i];
                if (node != null && LocalNodeRect(node).Contains(localMouse)) return node;
            }
            return null;
        }

        private TalentNodeData FindNode(TalentData data)
        {
            if (_asset == null || data == null) return null;
            foreach (TalentNodeData node in _asset.Nodes)
                if (node?.Data == data) return node;
            return null;
        }

        private Rect LocalNodeRect(TalentNodeData node)
            => new(node.Position.x * _zoom + _pan.x, node.Position.y * _zoom + _pan.y, NodeWidth * _zoom, NodeHeight * _zoom);

        private enum PortSide { Left, Right, Top, Bottom }

        private static void DrawPort(Rect rect, PortSide side, Color color)
        {
            Vector2 point = PortPoint(rect, side);
            EditorGUI.DrawRect(new Rect(point.x - PortSize * 0.5f, point.y - PortSize * 0.5f, PortSize, PortSize), color);
        }

        private static Vector2 PortPoint(Rect rect, PortSide side)
        {
            return side switch
            {
                PortSide.Left => new Vector2(rect.x, rect.center.y),
                PortSide.Right => new Vector2(rect.xMax, rect.center.y),
                PortSide.Top => new Vector2(rect.center.x, rect.y),
                _ => new Vector2(rect.center.x, rect.yMax)
            };
        }

        private Vector2 GetPortPoint(TalentNodeData node, Vector2 toward)
        {
            Rect rect = LocalNodeRect(node);
            Vector2 delta = toward - rect.center;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                return PortPoint(rect, delta.x >= 0f ? PortSide.Right : PortSide.Left);
            return PortPoint(rect, delta.y >= 0f ? PortSide.Bottom : PortSide.Top);
        }

        private TalentNodeData FindPortAt(Vector2 localMouse)
        {
            if (_asset == null) return null;
            for (int i = _asset.Nodes.Count - 1; i >= 0; i--)
            {
                TalentNodeData node = _asset.Nodes[i];
                if (node?.Data == null) continue;
                Rect rect = LocalNodeRect(node);
                if (Vector2.Distance(localMouse, PortPoint(rect, PortSide.Left)) <= PortHitRadius ||
                    Vector2.Distance(localMouse, PortPoint(rect, PortSide.Right)) <= PortHitRadius ||
                    Vector2.Distance(localMouse, PortPoint(rect, PortSide.Top)) <= PortHitRadius ||
                    Vector2.Distance(localMouse, PortPoint(rect, PortSide.Bottom)) <= PortHitRadius)
                    return node;
            }
            return null;
        }

        private static void DrawConnection(Vector2 source, Vector2 destination)
        {
            Vector2 delta = destination - source;
            float tangent = Mathf.Clamp(delta.magnitude * 0.35f, 24f, 100f);
            Vector2 direction = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector2.right;
            Handles.DrawBezier(source, destination, source + direction * tangent, destination - direction * tangent, Handles.color, null, 2f);
            Vector2 perpendicular = new(-direction.y, direction.x);
            Vector2 arrowBase = destination - direction * 12f;
            Handles.DrawLine(destination, arrowBase + perpendicular * 6f);
            Handles.DrawLine(destination, arrowBase - perpendicular * 6f);
        }

        private GUIStyle CreateNodeStyle(GUIStyle source, int baseFontSize)
        {
            GUIStyle style = new(source)
            {
                fontSize = Mathf.Clamp(Mathf.RoundToInt(baseFontSize * _zoom), 7, 24),
                clipping = TextClipping.Clip,
                wordWrap = false
            };
            return style;
        }

        private Vector2 CanvasPosition(Vector2 localPoint) => (localPoint - _pan) / _zoom;
    }
}
