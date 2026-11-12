using UnityEngine;

namespace AyeAye
{
    // FR-0203: bảng khóa tab theo pha — hàm thuần (không phụ thuộc UI); UI chỉ đọc kết quả.
    public enum TabAccess { Interactive, ReadOnly, Locked }
    public enum BottomAction { MakeItSo, Hidden, Continue, NextDay }

    public static class PhaseGate
    {
        public static TabAccess TabAccess(GamePhase phase, int tabIndex)
        {
            switch (phase)
            {
                case GamePhase.Planning:
                    return tabIndex == 2 ? AyeAye.TabAccess.ReadOnly : AyeAye.TabAccess.Interactive;
                case GamePhase.Simulation:
                    // tab 2 (ĐỘI TÀU) khóa nhưng vẫn hiện tàu đang chạy → ReadOnly, còn lại Locked.
                    return tabIndex == 1 ? AyeAye.TabAccess.ReadOnly : AyeAye.TabAccess.Locked;
                case GamePhase.Incident:
                    return tabIndex == 2 ? AyeAye.TabAccess.Interactive : AyeAye.TabAccess.ReadOnly;
                case GamePhase.Summary:
                    return AyeAye.TabAccess.Locked; // overlay tổng kết che toàn màn
                default:
                    return AyeAye.TabAccess.Locked;
            }
        }

        public static BottomAction BottomButton(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Planning: return BottomAction.MakeItSo;
                case GamePhase.Simulation: return BottomAction.Hidden;
                case GamePhase.Incident: return BottomAction.Continue;
                case GamePhase.Summary: return BottomAction.NextDay;
                default: return BottomAction.Hidden;
            }
        }
    }
}
