using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AyeAye.Tests
{
    // EventScheduler: bốc event theo trọng số, không lặp trong một ván, tuân theo điều kiện cờ.
    public class EventSchedulerTests
    {
        static DayDefinition Event(string id, int weight = 1, FlagCondition condition = FlagCondition.Always)
        {
            DayDefinition day = ScriptableObject.CreateInstance<DayDefinition>();
            day.eventId = id;
            day.weight = weight;
            day.spawnCondition = condition;
            day.title = id;
            day.briefingText = "brief " + id;
            day.statusText = "status " + id;
            return day;
        }

        static List<DayDefinition> Pool(int count, int weight = 1, FlagCondition condition = FlagCondition.Always)
        {
            List<DayDefinition> pool = new List<DayDefinition>();
            for (int i = 0; i < count; i++)
                pool.Add(Event("ev" + (i + 1), weight, condition));
            return pool;
        }

        static GameState NewState(GameConfig config, int seed)
        {
            return new GameState(config, seed);
        }

        // Bốc cả ván 6 event theo pool 6, trả về chuỗi eventId.
        static string[] Sequence(GameState state, List<DayDefinition> pool)
        {
            string[] ids = new string[6];
            for (int i = 0; i < 6; i++)
            {
                DayDefinition picked = EventScheduler.Pick(pool, state.UsedEvents, state, state.Rng);
                Assert.IsNotNull(picked, "Lần bốc " + i + " không được trả null");
                state.UsedEvents.Add(picked);
                ids[i] = picked.eventId;
            }
            return ids;
        }

        [Test]
        public void Pick_SameSeed_SameSequence()
        {
            GameConfig config = TestFixtures.NewConfig();
            List<DayDefinition> pool = Pool(6);

            GameState a = NewState(config, 42);
            GameState b = NewState(config, 42);
            CollectionAssert.AreEqual(Sequence(a, pool), Sequence(b, pool));
        }

        [Test]
        public void Pick_DifferentSeeds_CanDiffer()
        {
            GameConfig config = TestFixtures.NewConfig();
            List<DayDefinition> pool = Pool(6);

            // Thử nhiều seed, chỉ cần có ít nhất 2 chuỗi khác nhau.
            HashSet<string> seen = new HashSet<string>();
            for (int seed = 0; seed < 20; seed++)
            {
                GameState state = NewState(config, seed);
                seen.Add(string.Join(",", Sequence(state, pool)));
            }
            Assert.GreaterOrEqual(seen.Count, 2, "20 seed cho ra đúng 1 chuỗi duy nhất — không ngẫu nhiên.");
        }

        [Test]
        public void Pick_NoRepeatWithinPool()
        {
            GameConfig config = TestFixtures.NewConfig();
            List<DayDefinition> pool = Pool(6);

            GameState state = NewState(config, 7);
            string[] ids = Sequence(state, pool);

            Assert.AreEqual(6, new HashSet<string>(ids).Count, "6 lần bốc phải đôi một khác nhau: " + string.Join(",", ids));
        }

        [Test]
        public void Pick_RespectsSpawnCondition()
        {
            GameConfig config = TestFixtures.NewConfig();
            List<DayDefinition> pool = new List<DayDefinition>();
            pool.Add(Event("ev1", 1, FlagCondition.CargoShiftedRequired));
            pool.Add(Event("ev2", 1));

            // Cờ tắt: 200 seed không lần nào ra ev1.
            for (int seed = 0; seed < 200; seed++)
            {
                GameState state = NewState(config, seed);
                for (int day = 0; day < 6; day++)
                {
                    DayDefinition picked = EventScheduler.Pick(pool, state.UsedEvents, state, state.Rng);
                    state.UsedEvents.Add(picked);
                    Assert.AreEqual("ev2", picked.eventId, "Event cần cờ CargoShifted không được bốc khi cờ tắt.");
                }
            }

            // Cờ bật: ev1 ra được.
            bool sawRequired = false;
            for (int seed = 0; seed < 200 && !sawRequired; seed++)
            {
                GameState state = NewState(config, seed);
                state.CargoShifted = true;
                for (int day = 0; day < 6; day++)
                {
                    DayDefinition picked = EventScheduler.Pick(pool, state.UsedEvents, state, state.Rng);
                    state.UsedEvents.Add(picked);
                    if (picked.eventId == "ev1") sawRequired = true;
                }
            }
            Assert.IsTrue(sawRequired, "Event cần cờ CargoShifted không bao giờ ra dù cờ đã bật.");
        }

        [Test]
        public void Pick_RespectsWeight()
        {
            GameConfig config = TestFixtures.NewConfig();
            List<DayDefinition> pool = new List<DayDefinition>
            {
                Event("heavy", 10),
                Event("light", 1),
            };

            int heavyCount = 0;
            int lightCount = 0;
            // used luôn rỗng để trọng số tự do thể hiện qua 2000 lần bốc.
            HashSet<DayDefinition> noneUsed = new HashSet<DayDefinition>();
            GameState state = NewState(config, 123);
            for (int i = 0; i < 2000; i++)
            {
                DayDefinition picked = EventScheduler.Pick(pool, noneUsed, state, state.Rng);
                if (picked.eventId == "heavy") heavyCount++;
                else lightCount++;
            }

            // Ngưỡng rộng để không flaky: heavy chiếm ~10/11 ≈ 1818 lần.
            Assert.Greater(heavyCount, 1400, "Trọng số 10 phải thắng rõ rệt.");
            Assert.Less(lightCount, 600, "Trọng số 1 phải hiếm hơn hẳn.");
        }

        [Test]
        public void Pick_ExhaustedPool_FallsBackToRepeat()
        {
            GameConfig config = TestFixtures.NewConfig();
            List<DayDefinition> pool = Pool(2);

            GameState state = NewState(config, 99);
            state.UsedEvents.AddRange(pool); // toàn bộ pool đã dùng

            DayDefinition picked = EventScheduler.Pick(pool, state.UsedEvents, state, state.Rng);
            Assert.IsNotNull(picked, "Pool cạn phải fallback lặp, không được null.");
            CollectionAssert.Contains(pool, picked);
        }

        [Test]
        public void Pick_EmptyOrAllFiltered_ReturnsNull()
        {
            GameConfig config = TestFixtures.NewConfig();
            GameState state = NewState(config, 1);

            Assert.IsNull(EventScheduler.Pick(new List<DayDefinition>(), new List<DayDefinition>(), state, state.Rng));

            // Toàn bộ pool bị cờ lọc ra → null.
            List<DayDefinition> pool = Pool(2, 1, FlagCondition.CargoShiftedRequired);
            Assert.IsNull(EventScheduler.Pick(pool, new List<DayDefinition>(), state, state.Rng));
        }
    }
}
