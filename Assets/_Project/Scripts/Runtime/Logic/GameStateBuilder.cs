using System.Collections.Generic;

namespace AyeAye
{
    // Dựng trạng thái khởi đầu của một ván (FR-0101): doanh thu 0,
    // tàu ở cảng khởi đầu theo ShipDef, Condition Tốt, không cờ nào.
    public static class GameStateBuilder
    {
        public static GameState CreateRun(GameConfig config, IReadOnlyList<ShipDef> shipDefs, int seed)
        {
            GameState state = new GameState(config);
            state.NewRun(seed);
            InitializeShips(state, shipDefs);
            return state;
        }

        // Nạp (nạp lại) tàu từ ShipDef vào state — dùng sau NewRun vì NewRun xóa sạch tàu.
        public static void InitializeShips(GameState state, IReadOnlyList<ShipDef> shipDefs)
        {
            state.Ships.Clear();
            if (shipDefs == null) return;
            for (int i = 0; i < shipDefs.Count; i++)
            {
                ShipDef def = shipDefs[i];
                if (def == null) continue;
                state.Ships.Add(new ShipState
                {
                    Id = def.shipId,
                    DisplayName = def.displayName,
                    Captain = def.captain,
                    Speed = def.speedClass,
                    Condition = def.startCondition,
                    Status = ShipStatus.Idle,
                    PortId = def.startPortId,
                });
            }
        }
    }
}
