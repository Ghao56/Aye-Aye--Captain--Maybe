using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests
{
    // GameStateBuilder: dựng ván mới từ config + danh sách ShipDef — trạng thái khởi đầu theo FR-0101.
    public class GameStateBuilderTests
    {
        static GameConfig Config()
        {
            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            return config;
        }

        static List<ShipDef> ShipDefs()
        {
            return new List<ShipDef>
            {
                Ship("S-TIA", "Tia Chớp", CrewId.Bob, SpeedClass.Fast, "HQ"),
                Ship("S-RUA", "Rùa Vàng", CrewId.Chen, SpeedClass.Slow, "HQ"),
                Ship("S-DQT", "Đúng Quy Trình", CrewId.Mira, SpeedClass.Normal, "HQ"),
                Ship("S-NGO", "Nghi Ngờ Số Một", CrewId.Dex, SpeedClass.Normal, "VANG"),
            };
        }

        static ShipDef Ship(string id, string name, CrewId captain, SpeedClass speed, string port)
        {
            ShipDef def = ScriptableObject.CreateInstance<ShipDef>();
            def.shipId = id;
            def.displayName = name;
            def.captain = captain;
            def.speedClass = speed;
            def.startPortId = port;
            return def;
        }

        [Test]
        public void CreateRun_InitializesStartingState_PerFR0101()
        {
            GameState state = GameStateBuilder.CreateRun(Config(), ShipDefs(), seed: 42);

            Assert.AreEqual(0f, state.ElapsedSeconds);
            Assert.AreEqual(0, state.Revenue);
            Assert.AreEqual(0, state.LateCount);
            Assert.AreEqual(0, state.DeliveredCount);
            Assert.AreEqual(4, state.Ships.Count);
            Assert.AreEqual("VANG", state.Ships[3].PortId);
            Assert.AreEqual(ShipCondition.Good, state.Ships[0].Condition);
            Assert.AreEqual(ShipStatus.Idle, state.Ships[0].Status);
            Assert.IsEmpty(state.Ships[0].OrderId);
            Assert.IsNull(state.Ships[0].Route);
            Assert.AreEqual(42, state.Seed);
            Assert.IsFalse(state.IsFinished);
            Assert.AreEqual(EndingId.None, state.FinalEnding);
            Assert.AreEqual(0, state.Orders.Count);
            Assert.IsTrue(state.CanAdvance);
            Assert.IsEmpty(state.CargoShiftedOrderIds);
            Assert.IsEmpty(state.TemporaryRepairShipIds);
        }

        [Test]
        public void NewRun_ResetsEverythingAndSeed()
        {
            GameState state = GameStateBuilder.CreateRun(Config(), ShipDefs(), seed: 42);
            state.ElapsedSeconds = 1234.5f;
            state.Revenue = 999;
            state.LateCount = 2;
            state.DeliveredCount = 3;
            state.Orders.Add(new OrderState { Id = "O-01" });
            state.CargoShiftedOrderIds.Add("O-01");
            state.TemporaryRepairShipIds.Add("S-TIA");
            state.UsedOrderIds.Add("O-01");
            state.UsedIncidentIds.Add("I-01");
            state.IsFinished = true;
            state.FinalEnding = EndingId.E2_Renowned;

            state.NewRun(seed: 7);

            Assert.AreEqual(7, state.Seed);
            Assert.AreEqual(0f, state.ElapsedSeconds);
            Assert.AreEqual(0, state.Revenue);
            Assert.AreEqual(0, state.LateCount);
            Assert.AreEqual(0, state.DeliveredCount);
            Assert.AreEqual(0, state.Orders.Count);
            Assert.AreEqual(0, state.Ships.Count); // builder phải nạp lại tàu sau NewRun
            Assert.IsEmpty(state.CargoShiftedOrderIds);
            Assert.IsEmpty(state.TemporaryRepairShipIds);
            Assert.IsEmpty(state.UsedOrderIds);
            Assert.IsEmpty(state.UsedIncidentIds);
            Assert.IsFalse(state.IsFinished);
            Assert.AreEqual(EndingId.None, state.FinalEnding);
            Assert.IsTrue(state.CanAdvance);
        }

        [Test]
        public void Initialize_RepopulatesShips_AfterNewRun()
        {
            GameState state = GameStateBuilder.CreateRun(Config(), ShipDefs(), seed: 42);
            state.NewRun(seed: 7);
            GameStateBuilder.InitializeShips(state, ShipDefs());

            Assert.AreEqual(4, state.Ships.Count);
            Assert.AreEqual(CrewId.Dex, state.Ships[3].Captain);
            Assert.AreEqual("VANG", state.Ships[3].PortId);
            Assert.AreEqual(SpeedClass.Slow, state.Ships[1].Speed);
        }
    }
}
