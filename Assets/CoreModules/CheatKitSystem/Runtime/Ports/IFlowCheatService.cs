namespace CoreModules.CheatKit.Ports
{
    /// <summary>Pha chơi tổng quát — đủ để panel gate nút, không lệ thuộc FSM cụ thể của game.</summary>
    public enum CheatGamePhase
    {
        Unknown,
        Menu,
        Loading,
        Playing,
        Paused,
        Win,
        Lose
    }

    /// <summary>
    /// PORT — can thiệp luồng thắng/thua để test. Adapter map sang event bus / FSM thật.
    /// Tách khỏi <see cref="ILevelCheatService"/> theo ISP: game chỉ thua-được mới cần ForceLose.
    /// </summary>
    public interface IFlowCheatService
    {
        /// <summary>Pha hiện tại để panel quyết định khoá/mở nút (vd chặn ForceWin khi đang Loading).</summary>
        CheatGamePhase Phase { get; }

        /// <summary>Có đang ở trạng thái cho phép ép thắng không (thường = Playing).</summary>
        bool CanForceWin { get; }

        /// <summary>Ép thắng level hiện tại đi qua ĐÚNG win-flow thật (không bypass animation/save).</summary>
        void ForceWin();

        /// <summary>Ép thua level hiện tại.</summary>
        void ForceLose();

        /// <summary>Kích hoạt trạng thái hết nước đi để test cơ chế rescue.</summary>
        void TriggerNoMoves();
    }
}
