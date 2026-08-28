using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CoreModules.CheatKit.Examples.EditorTools
{
    /// <summary>
    /// Tạo NHANH một example scene cho từng dòng game: menu <c>CoreModules ▸ CheatKit ▸ Examples ▸ …</c>
    /// dựng scene rỗng (Camera + Light + EventSystem mặc định) chứa một
    /// <see cref="ExampleCheatBootstrap"/> đã set sẵn genre → bấm Play là test.
    ///
    /// Sinh scene bằng code (thay vì commit file .unity) để tránh vỡ GUID/ref khi đổi script.
    /// Cả asmdef này lẫn Examples đều gate <c>CHEAT_ENABLED</c> → menu chỉ hiện khi cheat bật.
    /// </summary>
    public static class CheatExampleSceneMenu
    {
        private const string Root = "CoreModules/CheatKit/Examples/";

        [MenuItem(Root + "Puzzle", priority = 100)]
        private static void Puzzle() => CreateScene(GenreKind.Puzzle);

        [MenuItem(Root + "Casual", priority = 101)]
        private static void Casual() => CreateScene(GenreKind.Casual);

        [MenuItem(Root + "RPG", priority = 102)]
        private static void Rpg() => CreateScene(GenreKind.Rpg);

        [MenuItem(Root + "Action", priority = 103)]
        private static void Action() => CreateScene(GenreKind.Action);

        private static void CreateScene(GenreKind genre)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var go = new GameObject($"CheatExample-{genre}");
            var bootstrap = go.AddComponent<ExampleCheatBootstrap>();

            // _genre là private [SerializeField] → set qua SerializedObject (không cần API public).
            var so = new SerializedObject(bootstrap);
            var prop = so.FindProperty("_genre");
            if (prop != null)
            {
                prop.enumValueIndex = (int)genre;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[CheatKit] Example scene '{genre}' đã tạo. Bấm ▶ Play, rồi chạm 5 lần " +
                      "vùng giữa-trên (hoặc F1) để mở cheat panel. Dùng File ▸ Save As để lưu nếu muốn.");
        }
    }
}
