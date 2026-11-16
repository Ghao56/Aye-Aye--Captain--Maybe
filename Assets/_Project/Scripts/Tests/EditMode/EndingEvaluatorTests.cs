using AyeAye;
using NUnit.Framework;
using UnityEngine;
namespace AyeAye.Tests.EditMode
{

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
            state.Orders.Add(new OrderState { Id = "A", Status = OrderStatus.Accepted }); 
            EndingEvaluator.FinishRun(state);
            Assert.AreEqual(777, state.Revenue);
        }
        [Test]

        public void RevenueNeverEndsRun_OnlyWinDeliveriesOrLateCount()
        {
            var state = new GameState(_config);
            state.Revenue = -999;
            Assert.IsFalse(state.IsFinished);
            Assert.IsTrue(state.CanAdvance);
        }
    }
}

