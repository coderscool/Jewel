using System.Collections.Generic;
using JewelPainter.UI.Components;
using JewelPainter.UI.Managers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Cửa sổ thêm hoặc gỡ SafeArea cho các canvas trong scene.
    public class SafeAreaSetupWindow : EditorWindow
    {
        private const string SafeAreaObjectName = "SafeArea";

        private bool _applyTop = true;
        private bool _applyBottom = true;
        private bool _applyLeft;
        private bool _applyRight;

        private readonly List<Canvas> _canvases = new();
        private readonly Dictionary<int, bool> _selection = new();

        private readonly Dictionary<int, bool> _childSelection = new();

        private readonly Dictionary<int, bool> _expanded = new();

        private readonly HashSet<int> _popupBackdrops = new();

        private Vector2 _scroll;

        [MenuItem("Tools/JewelPainter/Safe Area")]
        private static void Open()
        {
            var window = GetWindow<SafeAreaSetupWindow>("Safe Area");
            window.minSize = new Vector2(380f, 420f);
            window.RefreshCanvasList();
        }

        private void OnFocus() => RefreshCanvasList();

        private void OnHierarchyChange()
        {
            RefreshCanvasList();
            Repaint();
        }

        private void RefreshCanvasList()
        {
            _canvases.Clear();

            var found = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var canvas in found)
            {
                if (!canvas.isRootCanvas) continue;

                _canvases.Add(canvas);

                var id = canvas.GetInstanceID();

                if (!_selection.ContainsKey(id)) _selection[id] = !LooksLikeBackground(canvas.name);
            }

            _canvases.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            RefreshPopupBackdrops();
        }

        private void RefreshPopupBackdrops()
        {
            _popupBackdrops.Clear();

            var managers = FindObjectsByType<PopupManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var manager in managers)
            {
                var serialized = new SerializedObject(manager);
                var backdrop = serialized.FindProperty("_backdrop");

                if (backdrop == null) continue;
                if (backdrop.objectReferenceValue == null) continue;

                _popupBackdrops.Add(backdrop.objectReferenceValue.GetInstanceID());
            }
        }

        private static bool LooksLikeBackground(string name)
        {
            var lower = name.ToLowerInvariant();
            return lower.Contains("bg") || lower.Contains("background");
        }

        /// Mặc định con này có được đưa vào SafeArea không.
        private bool DefaultIncludeChild(Transform child)
        {
            return !IsPopupBackdrop(child);
        }

        private bool IsPopupBackdrop(Transform child)
            => _popupBackdrops.Contains(child.gameObject.GetInstanceID());

        private bool IncludeChild(Transform child)
        {
            var id = child.GetInstanceID();

            if (!_childSelection.TryGetValue(id, out var include))
            {
                include = DefaultIncludeChild(child);
                _childSelection[id] = include;
            }

            return include;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Cạnh cần lùi vào", EditorStyles.boldLabel);
            _applyTop = EditorGUILayout.ToggleLeft("Trên — tai thỏ, thanh trạng thái", _applyTop);
            _applyBottom = EditorGUILayout.ToggleLeft("Dưới — thanh gesture", _applyBottom);
            _applyLeft = EditorGUILayout.ToggleLeft("Trái (chỉ cần khi có xoay ngang)", _applyLeft);
            _applyRight = EditorGUILayout.ToggleLeft("Phải (chỉ cần khi có xoay ngang)", _applyRight);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Canvas trong scene", EditorStyles.boldLabel);

            if (_canvases.Count == 0)
            {
                EditorGUILayout.HelpBox("Không tìm thấy canvas nào trong scene đang mở.", MessageType.Info);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var canvas in _canvases)
            {
                if (canvas == null) continue;

                DrawCanvasRow(canvas);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            if (GUILayout.Button("Chèn / cập nhật SafeArea", GUILayout.Height(28f))) ApplyToSelection();

            if (GUILayout.Button("Gỡ SafeArea khỏi canvas đã chọn")) RemoveFromSelection();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Safe area chỉ có giá trị thật khi chạy trên máy hoặc trong Device " +
                "Simulator ở Play Mode. Game view thường trả về nguyên màn hình nên " +
                "nhìn sẽ không thấy gì đổi.", MessageType.None);
        }

        private void DrawCanvasRow(Canvas canvas)
        {
            var id = canvas.GetInstanceID();
            var safeArea = FindSafeArea(canvas);

            EditorGUILayout.BeginHorizontal();
            _selection[id] = EditorGUILayout.ToggleLeft(canvas.name, IsSelected(canvas), GUILayout.Width(200f));
            EditorGUILayout.LabelField(safeArea != null ? "đã có SafeArea" : "chưa có", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            if (!IsSelected(canvas)) return;

            if (safeArea != null) return;

            var hasChildren = canvas.transform.childCount > 0;
            if (!hasChildren) return;

            _expanded.TryGetValue(id, out var expanded);

            EditorGUI.indentLevel++;
            _expanded[id] = EditorGUILayout.Foldout(expanded, "Con nào vào trong SafeArea", true);

            if (_expanded[id])
            {
                for (var i = 0; i < canvas.transform.childCount; i++)
                {
                    var child = canvas.transform.GetChild(i);
                    var childId = child.GetInstanceID();
                    var include = IncludeChild(child);

                    EditorGUILayout.BeginHorizontal();
                    _childSelection[childId] = EditorGUILayout.ToggleLeft(
                        child.name, include, GUILayout.Width(200f));

                    if (IsPopupBackdrop(child))
                    {
                        EditorGUILayout.LabelField("tấm chặn chạm — nên để ngoài", EditorStyles.miniLabel);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                WarnIfOrderWouldChange(canvas);
            }

            EditorGUI.indentLevel--;
        }

        /// Cảnh báo khi một con bị loại nằm kẹp giữa hai con được giữ.
        private void WarnIfOrderWouldChange(Canvas canvas)
        {
            var seenIncluded = false;
            var seenExcludedAfterIncluded = false;

            for (var i = 0; i < canvas.transform.childCount; i++)
            {
                var child = canvas.transform.GetChild(i);

                if (IncludeChild(child))
                {
                    if (seenExcludedAfterIncluded)
                    {
                        EditorGUILayout.HelpBox(
                            "Có con bị loại nằm KẸP GIỮA những con được giữ. SafeArea chỉ " +
                            "đứng được một chỗ, nên thứ tự vẽ sẽ đổi. Kiểm lại xem có gì " +
                            "chồng lên nhau không.", MessageType.Warning);
                        return;
                    }

                    seenIncluded = true;
                    continue;
                }

                if (seenIncluded) seenExcludedAfterIncluded = true;
            }
        }

        private bool IsSelected(Canvas canvas)
            => _selection.TryGetValue(canvas.GetInstanceID(), out var selected) && selected;

        private static SafeAreaFitter FindSafeArea(Canvas canvas)
        {
            for (var i = 0; i < canvas.transform.childCount; i++)
            {
                var fitter = canvas.transform.GetChild(i).GetComponent<SafeAreaFitter>();
                if (fitter != null) return fitter;
            }

            return null;
        }

        private void ApplyToSelection()
        {
            var changed = 0;

            foreach (var canvas in new List<Canvas>(_canvases))
            {
                if (canvas == null || !IsSelected(canvas)) continue;

                var fitter = FindSafeArea(canvas) ?? CreateSafeArea(canvas);
                WriteEdgeFlags(fitter);

                RepointPopupRoot(canvas, (RectTransform)fitter.transform);

                changed++;
            }

            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                RefreshCanvasList();
            }
        }

        private SafeAreaFitter CreateSafeArea(Canvas canvas)
        {
            var moving = new List<Transform>(canvas.transform.childCount);
            var insertAt = canvas.transform.childCount;

            for (var i = 0; i < canvas.transform.childCount; i++)
            {
                var child = canvas.transform.GetChild(i);

                if (!IncludeChild(child)) continue;

                if (moving.Count == 0) insertAt = i;

                moving.Add(child);
            }

            var go = new GameObject(SafeAreaObjectName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Tạo SafeArea");
            Undo.SetTransformParent(go.transform, canvas.transform, false, "Tạo SafeArea");

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.SetSiblingIndex(insertAt);

            foreach (var child in moving)
            {
                Undo.SetTransformParent(child, rect, false, "Chuyển con vào SafeArea");
            }

            return Undo.AddComponent<SafeAreaFitter>(go);
        }

        /// Trỏ ô Root của PopupManager vào SafeArea vừa tạo, nếu nó đang trỏ vào canvas.
        private static void RepointPopupRoot(Canvas canvas, RectTransform safeArea)
        {
            var managers = FindObjectsByType<PopupManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var manager in managers)
            {
                var serialized = new SerializedObject(manager);
                var root = serialized.FindProperty("_root");

                if (root == null) continue;
                if (root.objectReferenceValue != (Object)canvas.transform) continue;

                root.objectReferenceValue = safeArea;

                serialized.ApplyModifiedProperties();
            }
        }

        /// Trả ô Root về chính canvas khi gỡ SafeArea đi.
        private static void RestorePopupRoot(Canvas canvas, Transform safeArea)
        {
            var managers = FindObjectsByType<PopupManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var manager in managers)
            {
                var serialized = new SerializedObject(manager);
                var root = serialized.FindProperty("_root");

                if (root == null) continue;
                if (root.objectReferenceValue != (Object)safeArea) continue;

                root.objectReferenceValue = canvas.transform;
                serialized.ApplyModifiedProperties();
            }
        }

        private void WriteEdgeFlags(SafeAreaFitter fitter)
        {
            var serialized = new SerializedObject(fitter);
            serialized.FindProperty("_applyTop").boolValue = _applyTop;
            serialized.FindProperty("_applyBottom").boolValue = _applyBottom;
            serialized.FindProperty("_applyLeft").boolValue = _applyLeft;
            serialized.FindProperty("_applyRight").boolValue = _applyRight;
            serialized.ApplyModifiedProperties();
        }

        private void RemoveFromSelection()
        {
            var removed = 0;

            foreach (var canvas in new List<Canvas>(_canvases))
            {
                if (canvas == null || !IsSelected(canvas)) continue;

                var fitter = FindSafeArea(canvas);
                if (fitter == null) continue;

                var safeArea = fitter.transform;

                RestorePopupRoot(canvas, safeArea);

                Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Gỡ SafeArea");

                var children = new List<Transform>(safeArea.childCount);
                for (var i = 0; i < safeArea.childCount; i++) children.Add(safeArea.GetChild(i));

                var insertAt = safeArea.GetSiblingIndex();

                foreach (var child in children)
                {
                    Undo.SetTransformParent(child, canvas.transform, false, "Gỡ SafeArea");
                    child.SetSiblingIndex(insertAt++);
                }

                Undo.DestroyObjectImmediate(safeArea.gameObject);
                removed++;
            }

            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                RefreshCanvasList();
            }
        }
    }
}
