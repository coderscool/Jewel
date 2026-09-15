using System.Collections.Generic;
using JewelPainter.UI.Components;
using JewelPainter.UI.Managers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Chèn một object "SafeArea" bọc con của mỗi canvas trong scene đang mở, rồi gắn
    /// `SafeAreaFitter` lên đó.
    ///
    /// Làm bằng tool thay vì kéo tay: việc này phải lặp lại y hệt trên từng canvas,
    /// mỗi lần đều là tạo object → kéo full khung → kéo đúng thứ tự con vào trong.
    /// Kéo tay một chỗ sai thứ tự là đảo luôn thứ tự vẽ của UI, và lỗi đó chỉ lộ ra
    /// khi hai thứ chồng lên nhau.
    ///
    /// KHÔNG phải con nào cũng nên vào trong. Tấm chặn chạm của popup là ví dụ: nó phải
    /// phủ tới tận mép máy, co vào safe area là chừa ra hai dải ăn chạm ở tai thỏ và
    /// thanh gesture. Nên mỗi canvas có một danh sách con để bỏ tick — xem phần gập ra
    /// dưới tên canvas.
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

        /// Con nào được đưa vào SafeArea, khoá theo InstanceID của chính con đó.
        ///
        /// Khoá theo con chứ không theo (canvas, chỉ số): chỉ số đổi mỗi lần ai đó kéo
        /// thả trong Hierarchy, và ô tick sẽ lặng lẽ nhảy sang một object khác.
        private readonly Dictionary<int, bool> _childSelection = new();

        private readonly Dictionary<int, bool> _expanded = new();

        /// InstanceID của những object đang được một PopupManager giữ làm tấm chặn chạm.
        ///
        /// Dựng sẵn một lần mỗi lần làm mới danh sách, không hỏi lại trong OnGUI: OnGUI
        /// chạy lại liên tục, và quét cả scene tìm PopupManager ở mỗi lần vẽ là biến một
        /// cửa sổ nhỏ thành thứ làm Editor ì ra.
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
                // Canvas lồng trong canvas khác chỉ dùng để tách batch, nó không phủ
                // màn hình nên chèn safe area vào đó là sai.
                if (!canvas.isRootCanvas) continue;

                _canvases.Add(canvas);

                var id = canvas.GetInstanceID();

                // Nền phải tràn ra tận mép máy, co lại chỉ tạo hai dải đen ở tai thỏ.
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

        /// Con này có nên vào SafeArea không — giá trị MẶC ĐỊNH, chỉ dùng ở lần đầu thấy nó.
        ///
        /// Chỉ một thứ bị loại sẵn: tấm chặn chạm mà PopupManager đang giữ. Nhận diện bằng
        /// THAM CHIẾU THẬT chứ không bằng tên, vì tên của nó là do người dựng scene đặt và
        /// không có từ khoá nào đáng tin — trong scene này nó tên "Transperian".
        ///
        /// Cố tình không đoán thêm bằng hình dạng: "con này căng kín canvas" đúng với tấm
        /// chặn, nhưng cũng đúng với HomeRoot và LoadingRoot, mà hai cái đó thì phải vào
        /// trong. Đoán sai kiểu đó thì tool im lặng làm hỏng đúng thứ nó sinh ra để sửa.
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

            // Đã có SafeArea rồi thì không còn gì để chọn: con đã nằm đâu thì ở đấy, và
            // tool này không tự kéo chúng ra vào lần thứ hai. Muốn đổi thì Gỡ rồi Chèn lại.
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

        /// Cảnh báo khi một con BỊ LOẠI nằm kẹp giữa hai con được giữ.
        ///
        /// Tách một danh sách con thành trong/ngoài thì SafeArea chỉ đứng được ở MỘT chỗ,
        /// nên mọi con bên trong nó cùng đứng trước hoặc cùng đứng sau con bị loại. Thứ tự
        /// con chính là thứ tự vẽ, nên trường hợp kẹp giữa là đổi thật — và đổi thứ tự vẽ
        /// thì chỉ lộ ra ở chỗ hai thứ chồng lên nhau, có khi vài hôm sau mới thấy.
        ///
        /// Báo chứ không tự sửa: cái đúng ở đây tuỳ vào thứ người dựng scene muốn nằm trên
        /// thứ gì, mà tool thì không biết điều đó.
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

            // Duyệt trên bản chụp: tạo object làm hierarchy đổi, mà OnHierarchyChange
            // thì dựng lại _canvases — duyệt thẳng trên nó là vừa đi vừa bị rút thảm.
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

            Debug.Log($"[Safe Area] Đã xử lý {changed} canvas.");
        }

        private SafeAreaFitter CreateSafeArea(Canvas canvas)
        {
            // Chụp danh sách con TRƯỚC khi tạo object mới — tạo xong rồi mới đọc là
            // gặp luôn chính nó trong danh sách và tự kéo mình vào mình.
            var moving = new List<Transform>(canvas.transform.childCount);
            var insertAt = canvas.transform.childCount;

            for (var i = 0; i < canvas.transform.childCount; i++)
            {
                var child = canvas.transform.GetChild(i);

                if (!IncludeChild(child)) continue;

                // Chỗ đứng của SafeArea là chỗ của con ĐƯỢC GIỮ đầu tiên: nó thay chỗ cho
                // cả nhóm, nên phải nằm đúng chỗ nhóm đó từng đứng so với những con ở lại.
                // Không con nào được giữ thì nó xuống cuối — đứng trên mọi thứ còn lại,
                // đúng thứ ta cần cho canvas popup (tấm chặn dưới, popup trên).
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

            // worldPositionStays: false — lúc này SafeArea trùng khít khung canvas, nên
            // giữ nguyên giá trị local là giữ nguyên chỗ đứng. Để true thì Unity quy đổi
            // sang toạ độ thế giới và ghi đè anchoredPosition bằng số lẻ, layout nhìn
            // vẫn đúng nhưng mọi giá trị trong Inspector thành rác.
            foreach (var child in moving)
            {
                Undo.SetTransformParent(child, rect, false, "Chuyển con vào SafeArea");
            }

            // SetTransformParent thả từng con xuống cuối, nên vòng lặp trên đã giữ
            // đúng thứ tự cũ — thứ tự con chính là thứ tự vẽ của UI.

            return Undo.AddComponent<SafeAreaFitter>(go);
        }

        /// Trỏ ô Root của PopupManager vào SafeArea vừa tạo, nếu nó đang trỏ vào canvas.
        ///
        /// Không có bước này thì việc bọc canvas popup là công cốc, mà lại KHÔNG có gì
        /// báo: popup được sinh ra lúc chạy và đặt thẳng vào Root, nên chúng rơi ra ngoài
        /// cái SafeArea vừa dựng. Hierarchy nhìn đúng, Inspector nhìn đúng, chỉ có điều
        /// popup vẫn nằm dưới tai thỏ y như trước.
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

                // ApplyModifiedProperties tự ghi một mốc Undo, không cần RecordObject.
                serialized.ApplyModifiedProperties();

                Debug.Log($"[Safe Area] Đã trỏ Root của {manager.name} vào SafeArea của " +
                          $"{canvas.name} — popup sinh lúc chạy giờ nằm trong safe area.", manager);
            }
        }

        /// Trả ô Root về chính canvas khi gỡ SafeArea đi.
        ///
        /// Thiếu bước này thì Root trỏ vào một object vừa bị xoá, và popup đầu tiên mở ra
        /// sau đó sẽ được đặt vào null — tức là ra thẳng gốc scene, ngoài mọi canvas.
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

                Debug.Log($"[Safe Area] Đã trả Root của {manager.name} về {canvas.name}.", manager);
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

                // Trả Root về TRƯỚC khi xoá: sau khi xoá thì không còn gì để so sánh, và
                // ô Root chỉ còn là một tham chiếu rỗng không ai lần ra được nữa.
                RestorePopupRoot(canvas, safeArea);

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
