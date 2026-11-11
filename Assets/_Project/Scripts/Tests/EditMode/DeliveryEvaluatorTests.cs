using AyeAye.Tests;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye
{
    public class DeliveryEvaluatorTests
    {
        static GameState StateAt(int currentDay, float progress, CargoCondition cargo)
        {
            GameConfig config = TestFixtures.NewConfig();
            config.startingCargo = cargo;
            GameState state = TestFixtures.NewState(config);
            state.CurrentDay = currentDay;
            state.Progress = progress;
            return state;
        }

        [Test]
        public void Evaluate_AllNormalThreeDays_LateButIntact()
        {
            // 3 ngày Normal = 3.0 < 3.2 → hết hạn.
            GameState state = StateAt(3, 3.0f, CargoCondition.Intact);
            DeliveryEvaluator.Evaluate(state);

            Assert.IsTrue(state.IsFinished);
            Assert.AreEqual(EndingId.LateButIntact, state.FinalEnding);
            Assert.IsFalse(state.ReachedEarly);
        }

        [Test]
        public void Evaluate_LateWithDamagedCargo_LateDelivery()
        {
            GameState scratched = StateAt(3, 3.0f, CargoCondition.Scratched);
            DeliveryEvaluator.Evaluate(scratched);
            Assert.AreEqual(EndingId.LateDelivery, scratched.FinalEnding);

            GameState ruined = StateAt(3, 2.9f, CargoCondition.Ruined);
            DeliveryEvaluator.Evaluate(ruined);
            Assert.AreEqual(EndingId.LateDelivery, ruined.FinalEnding);
        }

        [Test]
        public void Evaluate_ArrivesOnLastDay_StillOnTime()
        {
            // Smart + Normal + Normal = 1.75 + 1 + 1 = 3.75 ≥ 3.2 → thắng cuối ngày 3, không phải đến sớm.
            GameState state = StateAt(3, 3.75f, CargoCondition.Intact);
            DeliveryEvaluator.Evaluate(state);

            Assert.IsTrue(state.IsFinished);
            Assert.AreEqual(EndingId.PerfectDelivery, state.FinalEnding);
            Assert.IsFalse(state.ReachedEarly);
        }

        [Test]
        public void Evaluate_ArrivesEarly()
        {
            // Smart + Smart = 3.5 sau ngày 2 → thắng sớm.
            GameState state = StateAt(2, 3.5f, CargoCondition.Intact);
            DeliveryEvaluator.Evaluate(state);

            Assert.IsTrue(state.IsFinished);
            Assert.AreEqual(EndingId.PerfectDelivery, state.FinalEnding);
            Assert.IsTrue(state.ReachedEarly);
        }

        [Test]
        public void Evaluate_ArrivalWithDamagedCargo()
        {
            GameState scratched = StateAt(3, 3.2f, CargoCondition.Scratched);
            DeliveryEvaluator.Evaluate(scratched);
            Assert.AreEqual(EndingId.MinorDamage, scratched.FinalEnding);

            GameState ruined = StateAt(2, 3.5f, CargoCondition.Ruined);
            DeliveryEvaluator.Evaluate(ruined);
            Assert.AreEqual(EndingId.DisasterDelivery, ruined.FinalEnding);
        }

        [Test]
        public void Evaluate_BoundaryExactArrival_Wins()
        {
            GameState state = StateAt(3, 3.2f, CargoCondition.Intact);
            DeliveryEvaluator.Evaluate(state);

            Assert.IsTrue(state.IsFinished);
            Assert.AreEqual(EndingId.PerfectDelivery, state.FinalEnding);
            Assert.IsFalse(state.ReachedEarly);
        }

        [Test]
        public void Evaluate_MidGame_IncrementsDay()
        {
            GameState state = StateAt(1, 1.0f, CargoCondition.Intact);
            DeliveryEvaluator.Evaluate(state);

            Assert.IsFalse(state.IsFinished);
            Assert.IsNull(state.FinalEnding);
            Assert.AreEqual(2, state.CurrentDay);
        }

        [Test]
        public void Evaluate_AfterFinish_DoesNotChangeAnything()
        {
            GameState state = StateAt(3, 3.0f, CargoCondition.Intact);
            DeliveryEvaluator.Evaluate(state);
            int day = state.CurrentDay;
            EndingId? ending = state.FinalEnding;

            DeliveryEvaluator.Evaluate(state);

            Assert.AreEqual(day, state.CurrentDay);
            Assert.AreEqual(ending, state.FinalEnding);
        }
    }
}
