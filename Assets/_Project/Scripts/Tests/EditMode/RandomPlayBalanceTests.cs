using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AyeAye.Tests
{
    // Kiểm tra NFR-07 trên dữ liệu thật: chơi hoàn toàn ngẫu nhiên phải hầu như không thắng.
    // Win = kết thúc đúng hạn (PerfectDelivery / MinorDamage / DisasterDelivery).
    public class RandomPlayBalanceTests
    {
        const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const string PoolPath = "Assets/_Project/Data/Events/EventPool.asset";

        [Test]
        public void RandomPlay_RarelyWins()
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            EventPool pool = AssetDatabase.LoadAssetAtPath<EventPool>(PoolPath);
            if (config == null || pool == null || pool.openingDay == null
                || pool.events == null || pool.events.Count == 0)
                Assert.Ignore("Chưa có asset demo — chạy Tools/AyeAye/Build Demo trước.");

            CrewDefinition fallbackCrew = TestFixtures.NewCrew(CrewId.Any, "", "");
            float rate = BalanceProbe.SimulateWinRate(config, pool, 5000, fallbackCrew);
            Debug.Log("[Balance] Tỷ lệ thắng khi chơi ngẫu nhiên: "
                      + (rate * 100f).ToString("F1") + "% trên 5000 ván (totalDistance=" + config.totalDistance + ")");
            Assert.Less(rate, BalanceProbe.WinRateLimit, "Chơi ngẫu nhiên thắng quá nhiều — lệch NFR-07.");
        }
    }
}
