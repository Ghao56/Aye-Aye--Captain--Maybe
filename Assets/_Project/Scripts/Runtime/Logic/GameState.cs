using System;
using System.Collections.Generic;

namespace AyeAye
{
    // Trạng thái game thuần C# (không MonoBehaviour, không UI) để unit test được.
    public class GameState
    {
        readonly CargoCondition startingCargo;

        public int CurrentDay;
        public float Progress;
        public float TotalDistance;
        public int DeadlineDay;
        public CargoCondition Cargo;
        public bool CargoShifted;
        public bool TemporaryRepair;
        public ResolvedDecision PendingResolved;
        public bool IsFinished;
        public EndingId? FinalEnding;
        public bool ReachedEarly;

        public int Seed;
        public System.Random Rng;
        public DayDefinition CurrentEvent;
        public readonly List<DayDefinition> UsedEvents = new List<DayDefinition>();

        public GameState(GameConfig config) : this(config, Environment.TickCount) { }

        // Overload seed cho test: cùng seed + cùng chuỗi lựa chọn → cùng kết quả (SRS NFR-06).
        public GameState(GameConfig config, int seed)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            TotalDistance = config.totalDistance;
            DeadlineDay = config.deadlineDay;
            startingCargo = config.startingCargo;
            NewRun(seed);
        }

        // Bắt đầu một ván mới: Reset() rồi thiết lập seed/RNG. Dùng cho cả chạy đầu và Restart.
        public void NewRun(int seed)
        {
            Reset();
            Seed = seed;
            Rng = new System.Random(seed);
        }

        // Đưa về trạng thái ban đầu theo config; cờ và Hàng cũng được dọn sạch.
        // Không đụng Seed/Rng — việc đó là của NewRun.
        public void Reset()
        {
            CurrentDay = 1;
            Progress = 0f;
            Cargo = startingCargo;
            CargoShifted = false;
            TemporaryRepair = false;
            PendingResolved = null;
            IsFinished = false;
            FinalEnding = null;
            ReachedEarly = false;
            CurrentEvent = null;
            UsedEvents.Clear();
        }

        // Dùng Math để logic không phụ thuộc UnityEngine.
        public float ProgressPercent
        {
            get
            {
                if (TotalDistance <= 0f) return 1f;
                return Math.Min(1f, Math.Max(0f, Progress / TotalDistance));
            }
        }
    }
}
