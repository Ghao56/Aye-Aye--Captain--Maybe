using System;

namespace AyeAye
{
    // Enums dùng chung v3. Giữ nguyên các enum còn giá trị của bản 2 (DecisionOutcome, CargoCondition, CrewId).
    public enum DecisionOutcome { Normal, Smart, SlipUp }

    public enum GamePhase { Planning, Simulation, Incident, Summary }

    public enum CargoCondition { Ruined = 1, Scratched = 2, Intact = 3 }

    // FR-0408 [S] (Repair) chưa làm → Condition chỉ có Tốt / Hao mòn (giới hạn hạ cấp ở FR-0506).
    public enum ShipCondition { Worn = 1, Good = 2 }

    public enum ShipStatus { Idle, Moving, Docked }

    public enum SpeedClass { Slow, Normal, Fast }

    public enum CrewId { Any = 0, Bob = 1, Mira = 2, Chen = 3, Dex = 4 }

    public enum OrderStatus { InInbox, Accepted, Assigned, Delivered, Declined, Expired }

    [Flags]
    public enum OrderTag { None = 0, Fragile = 1, Cold = 2, Urgent = 4 }

    public enum HazardType { Any = 0, Clear = 1, Fog = 2, Storm = 3, Pirates = 4, Ice = 5, PortCongestion = 6 }

    public enum CommandKind { Assign, Reposition }

    // Cờ điều kiện: ở mức spawn/pool xét cờ toàn ván; ở mức bảng sự cố xét cờ của tàu/đơn trong ngữ cảnh.
    public enum FlagCondition
    {
        Always,
        CargoShiftedRequired,
        CargoShiftedForbidden,
        TemporaryRepairRequired,
        TemporaryRepairForbidden,
    }

    public enum FlagToSet { None, CargoShifted, TemporaryRepair }

    // 5 đoạn kết theo Uy tín cuối (FR-0701).
    public enum EndingId { None = 0, E1_Empire = 1, E2_Renowned = 2, E3_Survived = 3, E4_Debt = 4, E5_Bankrupt = 5 }
}
