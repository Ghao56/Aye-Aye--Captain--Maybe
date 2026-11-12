using AyeAye;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    // FR-0701 + FR-0105: kết thúc theo danh tiếng.
    public class EndingEvaluatorTests
    {
        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
        }

        [TestCase(100, EndingId.E1_Empire)]
        [TestCase(85, EndingId.E1_Empire)]
        [TestCase(84, EndingId.E2_Renowned)]
        [TestCase(65, EndingId.E2_Renowned)]
        [TestCase(64, EndingId.E3_Survived)]
        [TestCase(40, EndingId.E3_Survived)]
        [TestCase(39, EndingId.E4_Debt)]
        [TestCase(20, EndingId.E4_Debt)]
        [TestCase(19, EndingId.E5_Bankrupt)]
        [TestCase(0, EndingId.E5_Bankrupt)]
        [TestCase(-10, EndingId.E5_Bankrupt)]
        public void FromReputation_MatchesThresholds(int rep, EndingId expected)
        {
            Assert.AreEqual(expected, EndingEvaluator.FromReputation(rep, _config));
        }

        [Test]
        public void FinishRun_AllDelivered_AppliesNoPenalty()
        {
            var state = new GameState(_config);
            state.Reputation = 60;
            state.Orders.Add(new OrderState { Id = "A", Status = OrderStatus.Delivered });
            EndingEvaluator.FinishRun(state);
            Assert.AreEqual(60, state.Reputation);
            Assert.IsTrue(state.IsFinished);
            Assert.AreEqual(EndingId.E3_Survived, state.FinalEnding);
        }

        [Test]
        public void FinishRun_Undelivered_PenalizesEachThenEvaluates()
        {
            var state = new GameState(_config);
            state.Reputation = 25;
            state.Orders.Add(new OrderState { Id = "A", Status = OrderStatus.Delivered });
            state.Orders.Add(new OrderState { Id = "B", Status = OrderStatus.Accepted });
            state.Orders.Add(new OrderState { Id = "C", Status = OrderStatus.Assigned });
            EndingEvaluator.FinishRun(state);
            Assert.AreEqual(25 - 16, state.Reputation);
            Assert.IsTrue(state.IsFinished);
            Assert.AreEqual(EndingId.E5_Bankrupt, state.FinalEnding);
        }
    }
}
