using AyeAye.Tests;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye
{
    public class TravelProgressTests
    {
        [Test]
        public void GetDistance_MatchesConfigValues()
        {
            GameConfig config = TestFixtures.NewConfig();

            Assert.AreEqual(1.0f, TravelProgress.GetDistance(DecisionOutcome.Normal, config));
            Assert.AreEqual(1.75f, TravelProgress.GetDistance(DecisionOutcome.Smart, config));
            Assert.AreEqual(0.4f, TravelProgress.GetDistance(DecisionOutcome.SlipUp, config));
        }

        [Test]
        public void GetDistance_UsesCustomConfigValues()
        {
            GameConfig config = TestFixtures.NewConfig();
            config.normalDistance = 2.5f;
            config.smartDistance = 3.5f;
            config.slipUpDistance = 0.1f;

            Assert.AreEqual(2.5f, TravelProgress.GetDistance(DecisionOutcome.Normal, config));
            Assert.AreEqual(3.5f, TravelProgress.GetDistance(DecisionOutcome.Smart, config));
            Assert.AreEqual(0.1f, TravelProgress.GetDistance(DecisionOutcome.SlipUp, config));
        }
    }
}
