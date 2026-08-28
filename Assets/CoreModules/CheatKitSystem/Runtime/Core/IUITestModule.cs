using UnityEngine;

namespace CoreModules.CheatKit
{
    /// <summary>
    /// Interface cho tất cả UI Test Module.
    /// Module là component độc lập, có thể bật/tắt riêng biệt.
    /// 
    /// DESIGN PRINCIPLE:
    /// - Interface Segregation: Mỗi module chỉ implement những gì cần thiết
    /// - Open/Closed: Thêm module mới không cần sửa UITestPanel
    /// 
    /// USAGE:
    /// public class MyTestModule : UITestModuleBase { ... }
    /// </summary>
    public interface IUITestModule
    {
        /// <summary>Tên hiển thị của module</summary>
        string ModuleName { get; }
        
        /// <summary>Module có đang active không</summary>
        bool IsActive { get; }
        
        /// <summary>Root GameObject của module</summary>
        GameObject ModuleRoot { get; }
        
        /// <summary>
        /// Initialize module - gọi một lần khi module được tạo.
        /// Dùng để setup references, cache components.
        /// </summary>
        void Initialize();
        
        /// <summary>
        /// Enable module - gọi khi module được bật.
        /// Dùng để subscribe events, start coroutines.
        /// </summary>
        void OnEnable();
        
        /// <summary>
        /// Disable module - gọi khi module bị tắt.
        /// Dùng để unsubscribe events, stop coroutines.
        /// </summary>
        void OnDisable();
        
        /// <summary>
        /// Update module - gọi mỗi frame nếu module active.
        /// Dùng cho real-time stats (FPS, memory).
        /// </summary>
        void OnUpdate();
        
        /// <summary>
        /// Cleanup module - gọi khi module bị destroy.
        /// Dùng để cleanup resources, remove listeners.
        /// </summary>
        void Cleanup();
        
        /// <summary>
        /// Set visibility của module.
        /// </summary>
        void SetVisibility(bool visible);
    }
}
