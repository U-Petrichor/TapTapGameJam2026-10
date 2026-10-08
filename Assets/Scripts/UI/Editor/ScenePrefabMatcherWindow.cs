using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TapTapGameJam.EditorTools
{
    public sealed class ScenePrefabMatcherWindow : EditorWindow
    {
        [SerializeField] Transform _root;
        [SerializeField] DefaultAsset _folder;
        [SerializeField] GameObject _singlePrefab;
        [SerializeField] bool _autoScan = true, _unify = true, _replaceColliders, _backup = true, _save, _organize = true;
        [SerializeField] float _tolerance = 0.0001f;
        [SerializeField] bool _advanced, _showConnected;
        [SerializeField] bool _matchMeshRotations = true;
        [SerializeField] string _search = "";
        ScenePrefabMatcher.ScanResult _result;
        readonly HashSet<string> _expanded = new HashSet<string>();
        Vector2 _scroll;
        bool _pending, _busy, _stale = true;
        double _scanAfter;
        string _message = "指定预制体或目录后，自动查找场景中的同类物体。";
        MessageType _messageType = MessageType.Info;

        [MenuItem("Tools/Prefab Matcher/场景物体批量替换")]
        public static void Open()
        {
            var window = GetWindow<ScenePrefabMatcherWindow>();
            window.titleContent = new GUIContent("预制体匹配");
            window.minSize = new Vector2(570, 450); window.Show();
        }
        [MenuItem("Assets/查找场景中的同类物体", false, 32)]
        static void OpenForSelected()
        {
            Open(); var window = GetWindow<ScenePrefabMatcherWindow>();
            window._singlePrefab = Selection.activeObject as GameObject; window.Schedule();
        }
        [MenuItem("Assets/查找场景中的同类物体", true)]
        static bool ValidSelection() => Selection.activeObject is GameObject go && AssetDatabase.Contains(go) &&
            PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.Regular;
        void OnEnable()
        {
            if (_folder == null) _folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(ScenePrefabMatcher.DefaultFolder);
            if (_root == null) _root = DefaultRoot();
            EditorApplication.projectChanged += Schedule;
            EditorApplication.hierarchyChanged += Schedule;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayChanged;
            EditorSceneManager.activeSceneChangedInEditMode += SceneChanged;
            Undo.undoRedoPerformed += Schedule;
            Schedule();
        }
        void OnDisable()
        {
            EditorApplication.projectChanged -= Schedule;
            EditorApplication.hierarchyChanged -= Schedule;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayChanged;
            EditorSceneManager.activeSceneChangedInEditMode -= SceneChanged;
            Undo.undoRedoPerformed -= Schedule;
        }
        static Transform DefaultRoot()
        {
            var scene = SceneManager.GetActiveScene();
            var parent = scene.IsValid() ? scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Scene") : null;
            return parent != null ? parent.transform.Find("Environment") : null;
        }
        void SceneChanged(Scene oldScene, Scene newScene) { _root = DefaultRoot(); Schedule(); }
        void PlayChanged(PlayModeStateChange state)
        {
            _stale = true;
            if (state == PlayModeStateChange.EnteredEditMode) Schedule();
            Repaint();
        }
        void Schedule()
        {
            if (_busy) return;
            _stale = true; _pending = _autoScan; _scanAfter = EditorApplication.timeSinceStartup + 0.8; Repaint();
        }
        void Tick()
        {
            if (!_pending || _busy || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < _scanAfter) return;
            _pending = false; Scan(false);
        }
        ScenePrefabMatcher.Settings Settings() => new ScenePrefabMatcher.Settings
        {
            Root = _root, Folder = _folder != null ? AssetDatabase.GetAssetPath(_folder) : "",
            SinglePrefab = _singlePrefab, Tolerance = _tolerance, UnifyInstances = _unify, ReplaceColliders = _replaceColliders, MatchMeshRotations = _matchMeshRotations
        };
        void Scan(bool manual)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                _result = ScenePrefabMatcher.Scan(SceneManager.GetActiveScene(), Settings(), manual);
                _stale = false;
                int ready = _result.Matches.Count(m => m.State == ScenePrefabMatcher.MatchState.Ready);
                int connected = _result.Matches.Count(m => m.State == ScenePrefabMatcher.MatchState.Connected);
                _message = $"扫描 {_result.ScannedObjects} 个物体，读取 {_result.ReadMeshes} 个候选网格；匹配 {_result.Matches.Count} 个：待替换 {ready}，已连接 {connected}。";
                _messageType = MessageType.Info;
                if (_result.Groups.Count == 0) _message = "未找到可用的单网格普通预制体。请检查样本或预制体目录。";
            }
            catch (Exception ex) { _stale = true; _message = ex.Message; _messageType = ex is OperationCanceledException ? MessageType.Info : MessageType.Warning; }
            finally { _busy = false; Repaint(); }
        }
        void Replace()
        {
            _busy = true; _pending = false;
            try
            {
                var selected = _result.Matches.Where(m => m.Selected && m.State == ScenePrefabMatcher.MatchState.Ready).ToArray();
                var outcome = ScenePrefabMatcher.Apply(_result, selected, _backup, _save, _organize);
                int total = outcome.Converted + outcome.Relinked + outcome.SyncedColliders;
                _message = $"已处理 {total} 个物体：新增 {outcome.Converted}，改接 {outcome.Relinked}，同步 Collider {outcome.SyncedColliders}。支持 Ctrl+Z 撤销。" +
                    (_organize ? $"\n编号 {outcome.Renamed} 个，整理 {outcome.SortedParents} 个父级的顺序。" : "") +
                    (outcome.Saved ? " 场景已保存。" : " 请按 Ctrl+S 保存场景。") +
                    (string.IsNullOrEmpty(outcome.BackupPath) ? "" : "\n备份：" + outcome.BackupPath);
                _messageType = MessageType.Info;
                _result = ScenePrefabMatcher.Scan(SceneManager.GetActiveScene(), Settings()); _stale = false;
            }
            catch (Exception ex) { _message = ex.Message; _messageType = MessageType.Error; _stale = true; Debug.LogException(ex); }
            finally { _busy = false; Repaint(); }
        }
        void Organize()
        {
            _busy = true; _pending = false;
            try
            {
                var outcome = ScenePrefabMatcher.Organize(_result, _backup, _save);
                _message = outcome.Renamed == 0 && outcome.SortedParents == 0 ? "名称和 Hierarchy 顺序已符合设置，无需修改。" :
                    $"已编号 {outcome.Renamed} 个物体，整理 {outcome.SortedParents} 个父级的顺序。支持 Ctrl+Z 撤销。" +
                    (outcome.Saved ? " 场景已保存。" : " 请按 Ctrl+S 保存场景。") +
                    (string.IsNullOrEmpty(outcome.BackupPath) ? "" : "\n备份：" + outcome.BackupPath);
                _messageType = MessageType.Info; _result = ScenePrefabMatcher.Scan(SceneManager.GetActiveScene(), Settings()); _stale = false;
            }
            catch (Exception ex) { _message = ex.Message; _messageType = MessageType.Error; _stale = true; Debug.LogException(ex); }
            finally { _busy = false; Repaint(); }
        }
        void OnGUI()
        {
            bool blocked = _busy || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode;
            EditorGUILayout.LabelField("场景物体 → 共享预制体", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("保留外观、位置、旋转和缩放；碰撞体继承预制体。", EditorStyles.wordWrappedLabel);
            using (new EditorGUI.DisabledScope(blocked))
            {
                EditorGUI.BeginChangeCheck();
                _singlePrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("指定一个预制体", "留空时使用下方目录中的所有样本。"), _singlePrefab, typeof(GameObject), false);
                using (new EditorGUI.DisabledScope(_singlePrefab != null))
                    _folder = (DefaultAsset)EditorGUILayout.ObjectField("预制体目录", _folder, typeof(DefaultAsset), false);
                _root = (Transform)EditorGUILayout.ObjectField(new GUIContent("扫描根物体", "包含所有子物体；留空扫描当前场景。"), _root, typeof(Transform), true);
                _autoScan = EditorGUILayout.ToggleLeft("预制体或场景变化后自动检测", _autoScan);
                _unify = EditorGUILayout.ToggleLeft("统一目录内的同类预制体实例", _unify);
                _replaceColliders = EditorGUILayout.ToggleLeft(new GUIContent("使用预制体碰撞体，替换现有 Collider 设置", "也支持纯碰撞体子物体；移除旧子 Collider，保留子物体和 Transform。带网格、脚本或嵌套预制体的子物体仍需手动处理。"), _replaceColliders);
                _advanced = EditorGUILayout.Foldout(_advanced, "高级设置", true);
                if (_advanced)
                {
                    EditorGUI.indentLevel++;
                    _tolerance = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("几何容差", "网格局部坐标误差。默认 0.0001，用于 FBX 浮点误差。"), _tolerance), 0.0000001f, 0.01f);
                    _matchMeshRotations = EditorGUILayout.ToggleLeft("识别烘焙在网格内部的旋转，并补偿子 Collider", _matchMeshRotations);
                    EditorGUI.indentLevel--;
                }
                if (EditorGUI.EndChangeCheck()) Schedule();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("重新扫描")) { _pending = false; Scan(true); }
                    using (new EditorGUI.DisabledScope(_result == null))
                        if (GUILayout.Button("选中所有待替换物体"))
                            Selection.objects = _result.Matches.Where(m => m.State == ScenePrefabMatcher.MatchState.Ready && m.Filter != null).Select(m => (UnityEngine.Object)m.Filter.gameObject).ToArray();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    _backup = EditorGUILayout.ToggleLeft("替换前备份", _backup, GUILayout.Width(135));
                    _save = EditorGUILayout.ToggleLeft("完成后保存场景", _save);
                }
                _organize = EditorGUILayout.ToggleLeft("替换后为全部已连接的匹配物体编号、排序（预制体名_1）", _organize);
                int count = _result?.Matches.Count(m => m.State == ScenePrefabMatcher.MatchState.Ready && m.Selected) ?? 0;
                using (new EditorGUI.DisabledScope(_stale || count == 0))
                    if (GUILayout.Button($"替换勾选的 {count} 个物体", GUILayout.Height(30))) Replace();
                int connected = _result?.Matches.Count(ScenePrefabMatcher.CanOrganize) ?? 0;
                using (new EditorGUI.DisabledScope(_stale || connected == 0))
                    if (GUILayout.Button($"只编号并排序已连接的 {connected} 个物体")) Organize();
            }
            if (blocked) EditorGUILayout.HelpBox("请在 Edit 模式中使用，脚本编译期间稍候。", MessageType.Info);
            else if (_stale && _result != null) EditorGUILayout.HelpBox(_pending ? "变化已检测到，正在等待自动扫描…" : "结果已过期，请重新扫描。", MessageType.Info);
            EditorGUILayout.HelpBox(_message, _messageType);
            if (_result == null) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                _showConnected = EditorGUILayout.ToggleLeft("显示已连接物体", _showConnected, GUILayout.Width(160));
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("全选", GUILayout.Width(48))) foreach (var m in _result.Matches) m.Selected = m.State == ScenePrefabMatcher.MatchState.Ready;
                if (GUILayout.Button("清空", GUILayout.Width(48))) foreach (var m in _result.Matches) m.Selected = false;
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var group in _result.Groups) DrawGroup(group);
            if (_result.Notes.Count > 0)
            {
                EditorGUILayout.Space(); EditorGUILayout.LabelField("未处理的样本或物体", EditorStyles.boldLabel);
                foreach (var note in _result.Notes) EditorGUILayout.LabelField(note, EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
        }
        void DrawGroup(ScenePrefabMatcher.Group group)
        {
            var visible = group.Matches.Where(m => (_showConnected || m.State != ScenePrefabMatcher.MatchState.Connected) &&
                (string.IsNullOrEmpty(_search) || (m.Filter != null && m.Filter.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0))).ToArray();
            int ready = group.Matches.Count(m => m.State == ScenePrefabMatcher.MatchState.Ready);
            string label = $"{group.Prefab.name}    匹配 {group.Matches.Count} / 待替换 {ready}";
            if (group.Samples.Count > 1) label += "    同类样本：" + string.Join("、", group.Samples.Select(p => p.name));
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool open = _expanded.Contains(group.Path);
                bool next = EditorGUILayout.Foldout(open, label, true);
                if (next) _expanded.Add(group.Path); else _expanded.Remove(group.Path);
                if (!next) return;
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("统一使用", group.Prefab, typeof(GameObject), false);
                foreach (var match in visible)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(_stale || match.State != ScenePrefabMatcher.MatchState.Ready))
                            match.Selected = EditorGUILayout.Toggle(match.Selected, GUILayout.Width(18));
                        if (GUILayout.Button(match.Filter != null ? match.Filter.name : "物体已删除", EditorStyles.linkLabel, GUILayout.MinWidth(175)))
                        { if (match.Filter != null) { Selection.activeGameObject = match.Filter.gameObject; EditorGUIUtility.PingObject(match.Filter.gameObject); } }
                        string status = match.State == ScenePrefabMatcher.MatchState.Ready ? "待替换" : match.State == ScenePrefabMatcher.MatchState.Connected ? "已连接" : "需检查";
                        float angle = Quaternion.Angle(match.MeshRotation, Quaternion.identity);
                        GUILayout.Label(new GUIContent(status + "  误差 " + match.Error.ToString("G3") + (angle > 0.01f ? " / " + angle.ToString("F1") + "°" : ""), match.Reason), GUILayout.Width(210));
                    }
                    if (match.State == ScenePrefabMatcher.MatchState.Review) EditorGUILayout.LabelField(match.Reason, EditorStyles.miniLabel);
                }
                if (visible.Length == 0) EditorGUILayout.LabelField("没有符合显示条件的物体。", EditorStyles.miniLabel);
            }
        }
    }
}
