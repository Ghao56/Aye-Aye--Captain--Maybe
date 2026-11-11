namespace AyeAye
{
    public enum DecisionOutcome { Normal, Smart, SlipUp }

    public enum ScreenStep { Briefing, Status, Assignment }

    // Số lớn = tốt hơn (Ruined=1, Scratched=2, Intact=3).
    public enum CargoCondition { Ruined = 1, Scratched = 2, Intact = 3 }

    // Any chỉ dùng làm wildcard trong entry, không dùng làm lựa chọn của người chơi.
    public enum CrewId { Any = 0, Bob, Mira, Chen, Dex }

    public enum FlagCondition
    {
        Always,
        CargoShiftedRequired,
        CargoShiftedForbidden,
        TemporaryRepairRequired,
        TemporaryRepairForbidden
    }

    public enum FlagToSet { None, CargoShifted, TemporaryRepair }

    public enum EndingId { PerfectDelivery, MinorDamage, DisasterDelivery, LateButIntact, LateDelivery }
}
