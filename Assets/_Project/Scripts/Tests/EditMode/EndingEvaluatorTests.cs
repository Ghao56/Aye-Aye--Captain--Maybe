using AyeAye;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    // FR-0701 bản CHẠY LIÊN TỤC (bỏ ngày, xem ASSUMPTIONS.md G1): thắng với 0 lần trễ → E1,
    // 1 → E2, 2 → E3; thua (đủ maxLateDeliveries) → E4; thua quá sớm (giao ít hơn earlyLoseDeliveries) → E5.
    // Reputation đã bỏ hoàn toàn (chỉnh 4 — không còn thước đo thứ hai).
    public class EndingEvaluatorTests
    {
        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
        }

        [TestCase(0, EndingId.E1_Empire)]
        [TestCase(1, EndingId.E2_Renowned)]
        [TestCase(2, EndingId.E3_Survived)]
        public void Win_LateCountMapsToE1E2E3(int lateCount, EndingId expected)
        {
            var state = new GameState(_config);
            state.LateCount = lateCount;
            state.DeliveredCount = _config.winDeliveries;
            EndingEvaluator.FinishRun(state);
            Assert.AreEqual(expected, state.FinalEnding);
            Assert.IsTrue(state.IsFinished);
        }

        [TestCase(0, EndingId.E5_Bankrupt)]
        [TestCase(1, EndingId.E5_Bankrupt)]
        [TestCase(2, EndingId.E4_Debt)]
        public void Lose_EarlyLoseMapsToE4OrE5(int delivered, EndingId expected)
        {
            var state = new GameState(_config);
            state.LateCount = _config.maxLateDeliveries;
            state.DeliveredCount = delivered;
            EndingEvaluator.FinishRun(state);
            Assert.AreEqual(expected, state.FinalEnding);
        }

        [Test]
        public void FinishRun_IsIdempotent()
        {
            var state = new GameState(_config);
            state.DeliveredCount = _config.winDeliveries;
            EndingEvaluator.FinishRun(state);
            EndingEvaluator.FinishRun(state);
            Assert.AreEqual(EndingId.E1_Empire, state.FinalEnding);
        }

        [Test]
        public void FinishRun_DoesNotTouchRevenue()
        {
            var state = new GameState(_config);
            state.Revenue = 777;
            state.Orders.Add(new OrderState { Id = "A", Status = OrderStatus.Accepted }); // đơn chưa giao không bị trừ gì khi kết thúc
            EndingEvaluator.FinishRun(state);
            Assert.AreEqual(777, state.Revenue);
        }

        [Test]
        public void RevenueNeverEndsRun_OnlyWinDeliveriesOrLateCount()
        {
            // Không có điều kiện kết thúc ẩn — chỉ DeliveredCount/LateCount chốt thắng thua.
            var state = new GameState(_config);
            state.Revenue = -999;
            Assert.IsFalse(state.IsFinished);
            Assert.IsTrue(state.CanAdvance);
        }
    }
}
