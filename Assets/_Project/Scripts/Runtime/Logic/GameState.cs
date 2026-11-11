using System;

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

        public GameState(GameConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            TotalDistance = config.totalDistance;
            DeadlineDay = config.deadlineDay;
            startingCargo = config.startingCargo;
            Reset();
        }

        // Đưa về trạng thái ban đầu theo config; cờ và Hàng cũng được dọn sạch.
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
