using System;
namespace AyeAye
{

    public enum DecisionOutcome { Normal, Smart, SlipUp }

    public enum CargoCondition { Ruined = 1, Scratched = 2, Intact = 3 }

    public enum ShipCondition { Worn = 1, Good = 2 }

    public enum ShipStatus { Idle, Moving, Roaming }

    public enum SpeedClass { Slow, Normal, Fast }

    public enum CrewId { Any = 0, Bob = 1, Mira = 2, Chen = 3, Dex = 4 }

    public enum OrderStatus { InInbox, Accepted, Assigned, Delivered, Declined, Expired }
    [Flags]

    public enum OrderTag { None = 0, Fragile = 1, Cold = 2, Urgent = 4 }

    public enum HazardType { Any = 0, Clear = 1, Fog = 2, Storm = 3, Pirates = 4, Ice = 5, PortCongestion = 6 }

    public enum CommandKind { Assign, Reposition }

    public enum FlagCondition
    {
        Always,
        CargoShiftedRequired,
        CargoShiftedForbidden,
        TemporaryRepairRequired,
        TemporaryRepairForbidden,
    }

    public enum FlagToSet { None, CargoShifted, TemporaryRepair }

    public enum EndingId { None = 0, E1_Empire = 1, E2_Renowned = 2, E3_Survived = 3, E4_Debt = 4, E5_Bankrupt = 5 }
}

