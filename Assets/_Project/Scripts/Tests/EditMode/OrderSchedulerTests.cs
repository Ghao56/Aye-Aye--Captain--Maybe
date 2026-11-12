using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests
{
    // OrderScheduler: Ngày 1 đơn cố định; Ngày 2+ bốc theo trọng số + spawnCondition,
    // không lặp trong ván, cạn pool thì fallback cho phép lặp (FR-0301/0306).
    public class OrderSchedulerTests
    {
        static GameConfig Config()
        {
            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            config.newOrdersPerDayMin = 2;
            config.newOrdersPerDayMax = 3;
            return config;
        }

        static OrderDef Order(string id, int weight = 1, FlagCondition condition = FlagCondition.Always)
        {
            OrderDef def = ScriptableObject.CreateInstance<OrderDef>();
            def.orderId = id;
            def.weight = weight;
            def.spawnCondition = condition;
            def.fromPortId = "HQ";
            def.toPortId = "P2";
            def.deadlineDay = 3;
            return def;
        }

        static OrderPool Pool(OrderDef[] fixedDay, OrderDef[] poolOrders)
        {
            OrderPool pool = ScriptableObject.CreateInstance<OrderPool>();
            pool.fixedFirstDay = new List<OrderDef>(fixedDay);
            pool.pool = new List<OrderDef>(poolOrders);
            return pool;
        }

        static List<string> Schedule(OrderPool pool, GameConfig config, int seed, int day)
        {
            GameState state = GameStateBuilder.CreateRun(config, new List<ShipDef>(), seed);
            state.CurrentDay = day;
            OrderScheduler.ScheduleNewOrders(pool, state);
            return state.Orders.Select(o => o.Id).ToList();
        }

        [Test]
        public void Day1_ReturnsFixedOrders_IgnoringSeed()
        {
            OrderPool pool = Pool(new[] { Order("O-01"), Order("O-02") }, new OrderDef[0]);
            List<string> seed1 = Schedule(pool, Config(), 1, 1);
            List<string> seed99 = Schedule(pool, Config(), 99, 1);

            CollectionAssert.AreEqual(new[] { "O-01", "O-02" }, seed1);
            CollectionAssert.AreEqual(seed1, seed99);
        }

        [Test]
        public void Day2_PicksCountInConfiguredRange_AndDeterministicPerSeed()
        {
            OrderPool pool = Pool(new OrderDef[0],
                new[] { Order("O-03"), Order("O-04"), Order("O-05"), Order("O-06"), Order("O-07"), Order("O-08") });

            List<string> first = Schedule(pool, Config(), 42, 2);
            List<string> replay = Schedule(pool, Config(), 42, 2);

            Assert.That(first.Count, Is.InRange(2, 3));
            CollectionAssert.AreEqual(first, replay);
        }

        [Test]
        public void NoRepeatWithinRun_FallbackAllowsRepeatWhenExhausted()
        {
            GameConfig config = Config();
            config.newOrdersPerDayMin = 2;
            config.newOrdersPerDayMax = 2;
            OrderPool pool = Pool(new OrderDef[0],
                new[] { Order("A"), Order("B"), Order("C"), Order("D"), Order("E"), Order("F") });

            GameState state = GameStateBuilder.CreateRun(config, new List<ShipDef>(), 7);
            var seen = new List<string>();
            for (int day = 2; day <= 5; day++)
            {
                state.CurrentDay = day;
                OrderScheduler.ScheduleNewOrders(pool, state);
                seen.AddRange(state.Orders.Select(o => o.Id).Where(id => !seen.Contains(id)));
            }

            // 3 ngày đầu bốc được 6 đơn phân biệt (không lặp trong ván).
            Assert.AreEqual(6, seen.Distinct().Count());

            // Ngày 4 của pool cạn: fallback cho phép lặp — đơn cũ quay lại Inbox (một OrderState duy nhất cho mỗi id).
            int before = state.Orders.Count;
            state.CurrentDay = 6;
            OrderScheduler.ScheduleNewOrders(pool, state);
            Assert.AreEqual(before, state.Orders.Count);
            Assert.AreEqual(state.Orders.Count, state.Orders.Select(o => o.Id).Distinct().Count());
            Assert.That(state.Orders.Any(o => o.Id == "A" && o.Status == OrderStatus.InInbox));
        }

        [Test]
        public void SpawnCondition_FiltersEligibleOrders()
        {
            GameConfig config = Config();
            config.newOrdersPerDayMin = 1;
            config.newOrdersPerDayMax = 1;
            OrderDef gated = Order("GATED", condition: FlagCondition.CargoShiftedRequired);
            OrderPool pool = Pool(new OrderDef[0], new[] { gated, Order("FREE") });

            // Chưa có cờ CargoShifted toàn ván → chỉ đơn FREE đủ điều kiện.
            List<string> before = Schedule(pool, config, 11, 2);
            CollectionAssert.AreEqual(new[] { "FREE" }, before);

            // Có cờ → GATED đủ điều kiện (bốc theo trọng số; seed cố định → kết quả xác định).
            GameState state = GameStateBuilder.CreateRun(config, new List<ShipDef>(), 11);
            state.CurrentDay = 2;
            state.CargoShiftedOrderIds.Add("O-01");
            OrderScheduler.ScheduleNewOrders(pool, state);
            CollectionAssert.Contains(state.Orders.Select(o => o.Id).ToList(), "GATED");
        }

        [Test]
        public void Weight_HeavierOrderPickedMoreOften()
        {
            GameConfig config = Config();
            config.newOrdersPerDayMin = 1;
            config.newOrdersPerDayMax = 1;
            OrderPool pool = Pool(new OrderDef[0], new[] { Order("HEAVY", weight: 10), Order("LIGHT", weight: 1) });

            int heavy = 0, light = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                List<string> ids = Schedule(pool, config, seed, 2);
                if (ids[0] == "HEAVY") heavy++; else light++;
            }

            Assert.Greater(heavy, light);
        }

        [Test]
        public void MarksPickedOrdersUsed_AndAddsStatesWithDefaults()
        {
            OrderPool pool = Pool(new[] { Order("O-01") }, new OrderDef[0]);
            GameConfig config = Config();
            GameState state = GameStateBuilder.CreateRun(config, new List<ShipDef>(), 5);
            state.CurrentDay = 1;
            OrderScheduler.ScheduleNewOrders(pool, state);

            CollectionAssert.AreEqual(new[] { "O-01" }, state.UsedOrderIds);
            Assert.AreEqual(OrderStatus.InInbox, state.Orders[0].Status);
            Assert.AreEqual(CargoCondition.Intact, state.Orders[0].Cargo);
        }

        [Test]
        public void EmptyPool_AddsNothing_DoesNotCrash()
        {
            OrderPool pool = Pool(new OrderDef[0], new OrderDef[0]);
            List<string> ids = Schedule(pool, Config(), 3, 2);
            Assert.AreEqual(0, ids.Count);
        }
    }
}
