using System;
using System.Collections.Generic;

namespace AyeAye
{
    // Trạng thái runtime thuần C# của cả ván (FR-0902). Mọi ngẫu nhiên đi qua Rng với Seed (mục 2.4).
    public sealed class GameState
    {
        public GameConfig Config;

        public int CurrentDay = 1;
        public GamePhase Phase = GamePhase.Planning;

        public int Reputation;
        public int Revenue;

        public List<ShipState> Ships = new List<ShipState>();
        public List<OrderState> Orders = new List<OrderState>();
        public List<StagedCommand> StagedCommands = new List<StagedCommand>();

        // Cờ khóa theo ID tường minh: cờ của đơn/tàu này không lẫn sang đơn/tàu khác.
        public HashSet<string> CargoShiftedOrderIds = new HashSet<string>();
        public HashSet<string> TemporaryRepairShipIds = new HashSet<string>();

        // Pool đã bốc trong ván — không lặp; fallback cho phép lặp khi cạn.
        public HashSet<string> UsedOrderIds = new HashSet<string>();
        public HashSet<string> UsedIncidentIds = new HashSet<string>();

        public int Seed;
        public Random Rng;

        public bool IsFinished;
        public EndingId FinalEnding = EndingId.None;

        public GameState(GameConfig config)
        {
            Config = config;
            NewRun(MakeDefaultSeed());
        }

        // Ván mới: reset toàn bộ trạng thái (FR-0703). Tàu phải nạp lại qua GameStateBuilder.InitializeShips.
        public void NewRun(int seed)
        {
            Seed = seed;
            Rng = new Random(seed);
            CurrentDay = 1;
            Phase = GamePhase.Planning;
            Reputation = Config != null ? Config.startReputation : 50;
            Revenue = 0;
            Ships.Clear();
            Orders.Clear();
            StagedCommands.Clear();
            CargoShiftedOrderIds.Clear();
            TemporaryRepairShipIds.Clear();
            UsedOrderIds.Clear();
            UsedIncidentIds.Clear();
            IsFinished = false;
            FinalEnding = EndingId.None;
        }

        static int MakeDefaultSeed()
        {
            // Seed mặc định từ tick hệ thống; UI truyền seed riêng khi cần tái lập.
            return Environment.TickCount & 0x7fffffff;
        }
    }
}
