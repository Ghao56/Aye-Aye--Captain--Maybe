using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests
{
    // OrderScheduler bản CHẠY LIÊN TỤC (bỏ ngày — xem ASSUMPTIONS.md G1): SpawnInitial đặt 2 đơn tutorial
    // lúc 0 giây; SpawnOrders bốc theo ĐỒNG HỒ — mỗi mốc NextOrderSpawnAtSeconds bốc 1 đơn theo trọng số +
    // spawnCondition, không lặp trong ván, cạn pool thì fallback cho phép lặp (FR-0301/0306).
    public class OrderSchedulerTests
    {
        static GameConfig Config()
        {
            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            config.orderIntervalSeconds = 40f;
            config.orderIntervalDecayPerOrder = 0.92f;
            config.orderIntervalMinSeconds = 15f;
            return config;
        }

        static OrderDef Order(string id, int weight = 1, FlagCondition condition = FlagCondition.Always, float deadlineSeconds = 90f)
        {
            OrderDef def = ScriptableObject.CreateInstance<OrderDef>();
            def.orderId = id;
            def.weight = weight;
            def.spawnCondition = condition;
            def.fromPortId = "HQ";
            def.toPortId = "P2";
            def.deadlineSeconds = deadlineSeconds;
            return def;
        }

        static OrderPool Pool(OrderDef[] fixedOrders, OrderDef[] poolOrders)
        {
            OrderPool pool = ScriptableObject.CreateInstance<OrderPool>();
            pool.fixedFirstDay = new List<OrderDef>(fixedOrders);
            pool.pool = new List<OrderDef>(poolOrders);
            return pool;
        }

        static GameState State(GameConfig config, int seed)
        {
            return GameStateBuilder.CreateRun(config, new List<ShipDef>(), seed);
        }

        // Đặt đồng hồ tới mốc bốc rồi gọi SpawnOrders trực tiếp (unit — không qua Advance).
        static List<string> SpawnAt(OrderPool pool, GameConfig config, int seed, float elapsedSeconds)
        {
            GameState state = State(config, seed);
            OrderScheduler.SpawnInitial(pool, state);
            state.ElapsedSeconds = elapsedSeconds;
            OrderScheduler.SpawnOrders(pool, state);
            return state.Orders.Select(o => o.Id).ToList();
        }

        [Test]
        public void SpawnInitial_ReturnsFixedOrders_IgnoringSeed()
        {
            OrderPool pool = Pool(new[] { Order("O-01"), Order("O-02") }, new OrderDef[0]);
            GameState s1 = State(Config(), 1);
            GameState s99 = State(Config(), 99);
            OrderScheduler.SpawnInitial(pool, s1);
            OrderScheduler.SpawnInitial(pool, s99);

            CollectionAssert.AreEqual(new[] { "O-01", "O-02" }, s1.Orders.Select(o => o.Id).ToList());
            CollectionAssert.AreEqual(s1.Orders.Select(o => o.Id).ToList(), s99.Orders.Select(o => o.Id).ToList());
            // Mốc bốc đơn kế tiếp = orderIntervalSeconds.
            Assert.AreEqual(40f, s1.NextOrderSpawnAtSeconds);
        }

        [Test]
        public void SpawnOrders_OnePerMilestone_DeterministicPerSeed()
        {
            OrderPool pool = Pool(new OrderDef[0],
                new[] { Order("O-03"), Order("O-04"), Order("O-05"), Order("O-06"), Order("O-07"), Order("O-08") });

            List<string> first = SpawnAt(pool, Config(), 42, 40.01f);
            List<string> replay = SpawnAt(pool, Config(), 42, 40.01f);

            Assert.AreEqual(1, first.Count); // mỗi mốc bốc đúng 1 đơn (pool rỗng fixed → không có tutorial)
            CollectionAssert.AreEqual(first, replay);
        }

        [Test]
        public void NoRepeatWithinRun_FallbackAllowsRepeatWhenExhausted()
        {
            GameConfig config = Config();
            OrderPool pool = Pool(new OrderDef[0],
                new[] { Order("A"), Order("B"), Order("C"), Order("D"), Order("E"), Order("F") });

            GameState state = State(config, 7);
            OrderScheduler.SpawnInitial(pool, state);
            var seen = new List<string>();
            // 6 mốc đầu (bỏ mốc 0 vì SpawnInitial đặt Next=40): bốc được 6 đơn phân biệt.
            for (float t = 40f; t <= 400f; t += config.orderIntervalSeconds)
            {
                state.ElapsedSeconds = t;
                OrderScheduler.SpawnOrders(pool, state);
                seen.AddRange(state.Orders.Select(o => o.Id).Where(id => !seen.Contains(id)));
            }

            Assert.AreEqual(6, seen.Distinct().Count());

            // Mốc sau khi pool cạn: fallback cho phép lặp — đơn cũ quay lại Inbox (một OrderState duy nhất cho mỗi id).
            state.ElapsedSeconds += config.orderIntervalSeconds;
            OrderScheduler.SpawnOrders(pool, state);
            Assert.AreEqual(state.Orders.Count, state.Orders.Select(o => o.Id).Distinct().Count());
            Assert.That(state.Orders.Any(o => o.Id == "A" && o.Status == OrderStatus.InInbox));
        }

        [Test]
        public void Interval_DecaysPerSpawn_FlooredAtMin()
        {
            GameConfig config = Config();
            config.orderIntervalSeconds = 40f;
            config.orderIntervalDecayPerOrder = 0.5f;
            config.orderIntervalMinSeconds = 15f;
            OrderPool pool = Pool(new OrderDef[0], new[] { Order("X"), Order("Y"), Order("Z") });

            GameState state = State(config, 9);
            OrderScheduler.SpawnInitial(pool, state); // Next=40, Count=0

            state.ElapsedSeconds = 40f;
            OrderScheduler.SpawnOrders(pool, state);
            Assert.AreEqual(80f, state.NextOrderSpawnAtSeconds, "interval 40 * decay^0 = 40 → Next=80");

            state.ElapsedSeconds = 80f;
            OrderScheduler.SpawnOrders(pool, state);
            Assert.AreEqual(100f, state.NextOrderSpawnAtSeconds, "interval 40 * 0.5 = 20 → Next=100");

            state.ElapsedSeconds = 100f;
            OrderScheduler.SpawnOrders(pool, state);
            Assert.AreEqual(115f, state.NextOrderSpawnAtSeconds, "interval 10 < min 15 → Next=115");
        }

        [Test]
        public void SpawnCondition_FiltersEligibleOrders()
        {
            GameConfig config = Config();
            OrderDef gated = Order("GATED", condition: FlagCondition.CargoShiftedRequired);
            OrderPool pool = Pool(new OrderDef[0], new[] { gated, Order("FREE") });

            // Chưa có cờ CargoShifted toàn ván → chỉ đơn FREE đủ điều kiện.
            List<string> before = SpawnAt(pool, config, 11, 40.01f);
            Assert.That(before, Does.Contain("FREE"));

            // Có cờ → GATED đủ điều kiện (bốc theo trọng số; seed cố định → kết quả xác định).
            GameState state = State(config, 11);
            OrderScheduler.SpawnInitial(pool, state);
            state.CargoShiftedOrderIds.Add("O-01");
            state.ElapsedSeconds = 40.01f;
            OrderScheduler.SpawnOrders(pool, state);
            CollectionAssert.Contains(state.Orders.Select(o => o.Id).ToList(), "GATED");
        }

        [Test]
        public void Weight_HeavierOrderPickedMoreOften()
        {
            GameConfig config = Config();
            OrderPool pool = Pool(new OrderDef[0], new[] { Order("HEAVY", weight: 10), Order("LIGHT", weight: 1) });

            int heavy = 0, light = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                List<string> ids = SpawnAt(pool, config, seed, 40.01f);
                Assert.AreEqual(1, ids.Count);
                if (ids[0] == "HEAVY") heavy++; else light++;
            }

            Assert.Greater(heavy, light);
        }

        [Test]
        public void MarksSpawnedOrdersUsed_AndAddsStatesWithDefaults()
        {
            OrderPool pool = Pool(new[] { Order("O-01", deadlineSeconds: 120f) }, new OrderDef[0]);
            GameConfig config = Config();
            GameState state = State(config, 5);
            OrderScheduler.SpawnInitial(pool, state);

            CollectionAssert.AreEqual(new[] { "O-01" }, state.UsedOrderIds);
            Assert.AreEqual(OrderStatus.InInbox, state.Orders[0].Status);
            Assert.AreEqual(CargoCondition.Intact, state.Orders[0].Cargo);
            // Hạn là giây tuyệt đối trên đồng hồ ván: lúc bốc (0) + deadlineSeconds.
            Assert.AreEqual(120f, state.Orders[0].DeadlineAtSeconds);
            Assert.IsFalse(state.Orders[0].IsLate);
        }

        [Test]
        public void EmptyPool_AddsNothing_DoesNotCrash()
        {
            OrderPool pool = Pool(new OrderDef[0], new OrderDef[0]);
            List<string> ids = SpawnAt(pool, Config(), 3, 40.01f);
            Assert.AreEqual(0, ids.Count);
        }
    }
}
