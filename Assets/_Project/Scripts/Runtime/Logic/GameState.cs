using System;
using System.Collections.Generic;
namespace AyeAye
{

    public sealed class GameState
    {

        public GameConfig Config;

        public int Revenue;

        public float ElapsedSeconds;

        public int LateCount;         

        public int DeliveredCount;    

        public float NextOrderSpawnAtSeconds; 

        public int OrderSpawnCount;   

        public List<PendingIncident> PendingIncidents = new List<PendingIncident>();

        public List<ShipState> Ships = new List<ShipState>();

        public List<OrderState> Orders = new List<OrderState>();

        public HashSet<string> CargoShiftedOrderIds = new HashSet<string>();

        public HashSet<string> TemporaryRepairShipIds = new HashSet<string>();

        public HashSet<string> UsedOrderIds = new HashSet<string>();

        public HashSet<string> UsedIncidentIds = new HashSet<string>();

        public int Seed;

        public Random Rng;

        public Random RoamRng;

        public bool IsFinished;

        public EndingId FinalEnding = EndingId.None;

        public bool CanAdvance => !IsFinished && PendingIncidents.Count == 0;

        public GameState(GameConfig config)
        {
            Config = config;
            NewRun(MakeDefaultSeed());
        }

        public void NewRun(int seed)
        {
            Seed = seed;
            Rng = new Random(seed);
            RoamRng = new Random(seed ^ 0x5F5F5F5F);
            Revenue = 0;
            ElapsedSeconds = 0f;
            LateCount = 0;
            DeliveredCount = 0;
            NextOrderSpawnAtSeconds = Math.Max(0f, Config.orderFirstDelaySeconds);
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
            return Environment.TickCount & 0x7fffffff;
        }
    }
}

