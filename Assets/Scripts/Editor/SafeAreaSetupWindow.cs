using System.Collections.Generic;
using JewelPainter.UI.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Chèn một object "SafeArea" bọc toàn bộ con của mỗi canvas trong scene đang mở,
    /// rồi gắn `SafeAreaFitter` lên đó.
    ///
    /// Làm bằng tool thay vì kéo tay: việc này phải lặp lại y hệt trên từng canvas,
    /// mỗi lần đều là tạo object → kéo full khung → kéo đúng thứ tự con vào trong.
    /// Kéo tay một chỗ sai thứ tự là đảo luôn thứ tự vẽ của UI, và lỗi đó chỉ lộ ra
    /// khi hai thứ chồng lên nhau.
    ///
    /// Mọi thao tác đi qua `Undo`, bấm nhầm thì Ctrl+Z là xong.
    public class SafeAreaSetupWindow : EditorWindow
    {
        private const string SafeAreaObjectName = "SafeArea";

        private bool _applyTop = true;
        private bool _applyBottom = true;
        private bool _applyLeft;
        private bool _applyRight;

        private readonly List<Canvas> _canvases = new();
        private readonly Dictionary<int, bool> _selection = new();
        private Vector2 _scroll;

        [MenuItem("Tools/JewelPainter/Safe Area")]
        private static void Open()
        {
            var window = GetWindow<SafeAreaSetupWindow>("Safe Area");
            window.minSize = new Vector2(380f, 320f);
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
                // Canvas lồng trong canvas khác chỉ dùng để tách batch, nó không phủ
                // màn hình nên chèn safe area vào đó là sai.
                if (!canvas.isRootCanvas) continue;

                _canvases.Add(canvas);

                var id = canvas.GetInstanceID();

                // Nền phải tràn ra tận mép máy, co lại chỉ tạo hai dải đen ở tai thỏ.
                if (!_selection.ContainsKey(id)) _selection[id] = !LooksLikeBackground(canvas.name);
            }

            _canvases.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private static bool LooksLikeBackground(string name)
        {
            var lower = name.ToLowerInvariant();
            return lower.Contains("bg") || lower.Contains("background");
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

                var id = canvas.GetInstanceID();
                var hasFitter = FindFitter(canvas) != null;

                EditorGUILayout.BeginHorizontal();
                _selection[id] = EditorGUILayout.ToggleLeft(canvas.name, IsSelected(canvas), GUILayout.Width(220f));
                EditorGUILayout.LabelField(hasFitter ? "đã có SafeArea" : "chưa có", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
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

        private bool IsSelected(Canvas canvas)
            => _selection.TryGetValue(canvas.GetInstanceID(), out var selected) && selected;

        private static SafeAreaFitter FindFitter(Canvas canvas)
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

            // Duyệt trên bản chụp: tạo object làm hierarchy đổi, mà OnHierarchyChange
            // thì dựng lại _canvases — duyệt thẳng trên nó là vừa đi vừa bị rút thảm.
            foreach (var canvas in new List<Canvas>(_canvases))
            {
                if (canvas == null || !IsSelected(canvas)) continue;

                var fitter = FindFitter(canvas) ?? CreateSafeArea(canvas);
                WriteEdgeFlags(fitter);
                changed++;
            }

            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                RefreshCanvasList();
            }

            Debug.Log($"[Safe Area] Đã xử lý {changed} canvas.");
        }

        private static SafeAreaFitter CreateSafeArea(Canvas canvas)
        {
            // Chụp danh sách con TRƯỚC khi tạo object mới — tạo xong rồi mới đọc là
            // gặp luôn chính nó trong danh sách và tự kéo mình vào mình.
            var children = new List<Transform>(canvas.transform.childCount);

            for (var i = 0; i < canvas.transform.childCount; i++)
            {
                children.Add(canvas.transform.GetChild(i));
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
            rect.SetAsFirstSibling();

            // worldPositionStays: false — lúc này SafeArea trùng khít khung canvas, nên
            // giữ nguyên giá trị local là giữ nguyên chỗ đứng. Để true thì Unity quy đổi
            // sang toạ độ thế giới và ghi đè anchoredPosition bằng số lẻ, layout nhìn
            // vẫn đúng nhưng mọi giá trị trong Inspector thành rác.
            foreach (var child in children)
            {
                Undo.SetTransformParent(child, rect, false, "Chuyển con vào SafeArea");
            }

            // SetTransformParent thả từng con xuống cuối, nên vòng lặp trên đã giữ
            // đúng thứ tự cũ — thứ tự con chính là thứ tự vẽ của UI.

            return Undo.AddComponent<SafeAreaFitter>(go);
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

                var fitter = FindFitter(canvas);
                if (fitter == null) continue;

                var safeArea = fitter.transform;

                // SetSiblingIndex không tự ghi vào Undo. Chụp cả nhánh canvas trước để
                // Ctrl+Z trả lại đúng thứ tự con, không chỉ trả lại chỗ đứng.
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

            Debug.Log($"[Safe Area] Đã gỡ khỏi {removed} canvas.");
        }
    }
}
