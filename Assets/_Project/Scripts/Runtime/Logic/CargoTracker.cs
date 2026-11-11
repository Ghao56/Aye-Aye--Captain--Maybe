using System;

namespace AyeAye
{
    // Nơi duy nhất áp dụng hệ quả lên Hàng và cờ. Cờ chỉ được đặt, không bao giờ tự xóa.
    public static class CargoTracker
    {
        public static void Apply(GameState state, ResolvedDecision resolved)
        {
            if (state == null || resolved == null) return;

            if (resolved.CargoDelta < 0)
            {
                int next = Math.Max((int)CargoCondition.Ruined, (int)state.Cargo + resolved.CargoDelta);
                state.Cargo = (CargoCondition)next;
            }

            if (resolved.FlagToSet == FlagToSet.CargoShifted) state.CargoShifted = true;
            if (resolved.FlagToSet == FlagToSet.TemporaryRepair) state.TemporaryRepair = true;
        }
    }
}
