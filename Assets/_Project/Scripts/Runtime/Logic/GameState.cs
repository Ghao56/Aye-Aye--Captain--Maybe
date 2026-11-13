using System;
using System.Collections.Generic;

namespace AyeAye
{
    // Trạng thái runtime thuần C# của cả ván. Mọi ngẫu nhiên đi qua Rng với Seed.
    // Bản chạy liên tục (bỏ ngày — xem ASSUMPTIONS.md G1): đồng hồ ElapsedSeconds, thắng/thua theo DeliveredCount/LateCount.
    public sealed class GameState
    {
        public GameConfig Config;

        public int Revenue;

        // Đồng hồ ván (giây mô phỏng) + đếm thắng/thua.
        public float ElapsedSeconds;
        public int LateCount;         // số đơn đã chạm hạn mà chưa giao
        public int DeliveredCount;    // số đơn đã giao
        public float NextOrderSpawnAtSeconds; // mốc bốc đơn kế tiếp
        public int OrderSpawnCount;   // số đơn đã bốc (để rút ngắn khoảng)

        // Sự cố đang chờ xử lý — có phần tử → đồng hồ TẠM DỪNG.
        public List<PendingIncident> PendingIncidents = new List<PendingIncident>();

        public List<ShipState> Ships = new List<ShipState>();
        public List<OrderState> Orders = new List<OrderState>();

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

        // Đồng hồ được phép chạy khi ván chưa kết và không có sự cố chờ.
        public bool CanAdvance => !IsFinished && PendingIncidents.Count == 0;

        public GameState(GameConfig config)
        {
            Config = config;
            NewRun(MakeDefaultSeed());
        }

        // Ván mới: reset toàn bộ trạng thái (FR-0703). Tàu phải nạp lại qua GameStateBuilder.InitializeShips,
        // đơn đầu ván qua OrderScheduler.SpawnInitial.
        public void NewRun(int seed)
        {
            Seed = seed;
            Rng = new Random(seed);
            Revenue = 0;
            ElapsedSeconds = 0f;
            LateCount = 0;
            DeliveredCount = 0;
            NextOrderSpawnAtSeconds = 0f;
            OrderSpawnCount = 0;
            PendingIncidents.Clear();
            Ships.Clear();
            Orders.Clear();
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
