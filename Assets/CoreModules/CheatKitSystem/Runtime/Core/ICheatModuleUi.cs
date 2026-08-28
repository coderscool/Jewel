using UnityEngine;

namespace CoreModules.CheatKit
{
    /// <summary>
    /// Module tự dựng UI mặc định của mình (đường "no-prefab"): <see cref="CheatPanelBuilder"/>
    /// gọi để ráp panel runtime mà không cần prefab. Module gán các field UI nội bộ của nó
    /// (cùng field mà prefab sẽ wire qua Inspector) → một module chạy được cả 2 đường.
    ///
    /// <paramref name="section"/> đã có sẵn VerticalLayoutGroup; module chỉ việc thêm control vào.
    /// </summary>
    public interface ICheatModuleUi
    {
        void BuildDefaultUI(RectTransform section);
    }
}
