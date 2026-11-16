using System.Collections.Generic;
using AyeAye;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    /// <summary>
    /// Dữ liệu mẫu SRS §5 dưới dạng SO trong bộ nhớ (tạo mỗi lần SetUp, hủy ở TearDown).
    /// Nguồn dữ liệu duy nhất là DemoData — DemoBuilder dùng đúng nguồn đó để dựng assets (FR-0905).
    /// </summary>
    public class TestWorld
    {
        // §5.1 port ids (alias cho DemoData) — bản đồ hình sao 5 nút, mogio đã bỏ
        public const string Goc = DemoData.Goc, Vang = DemoData.Vang, Suong = DemoData.Suong,
            Camap = DemoData.Camap, Tram = DemoData.Tram;

        public GameConfig Config;
        public RouteTable Routes;
        public List<PortDef> Ports;
        public List<RouteDef> RouteDefs;
        public OrderPool OrderPool;
        public List<ShipDef> ShipDefs;
        public List<IncidentDef> Incidents;
        public List<CrewDefinition> Crews;
        public IncidentDef FixedFirstDay;
        public IncidentDef FixedFinale;
        public readonly List<ScriptableObject> SOs = new List<ScriptableObject>();

        public static TestWorld Create()
        {
            var w = new TestWorld();
            w.Config = w.Track(DemoData.CreateConfig());
            w.Ports = DemoData.BuildPorts();
            w.RouteDefs = DemoData.BuildRouteDefs();
            w.Routes = w.Track(DemoData.BuildRouteTable());
            w.OrderPool = w.Track(DemoData.BuildOrderPool());
            w.ShipDefs = DemoData.BuildShips();
            w.Crews = DemoData.BuildCrews();

            var pool = w.Track(DemoData.BuildIncidentPool());
            w.FixedFirstDay = pool.fixedFirstDay;
            w.FixedFinale = pool.fixedFinale;
            w.Incidents = pool.incidents;

            foreach (var p in w.Ports) w.Track(p);
            foreach (var r in w.RouteDefs) w.Track(r);
            foreach (var s in w.ShipDefs) w.Track(s);
            foreach (var c in w.Crews) w.Track(c);
            foreach (var o in w.OrderPool.fixedFirstDay) w.Track(o);
            foreach (var o in w.OrderPool.pool) w.Track(o);
            return w;
        }

        private T Track<T>(T so) where T : ScriptableObject
        {
            SOs.Add(so);
            return so;
        }

        /// <summary>
        /// G7: dựng đơn tay cho test — không phụ thuộc SpawnInitial/pool cố định (đơn giờ sinh ngẫu nhiên).
        /// </summary>
        public static OrderState AddManualOrder(GameState state, string id, string from, string to,
            float deadlineSeconds, int revenue = 100, OrderTag tags = OrderTag.None, string clue = "")
        {
            var o = new OrderState
            {
                Id = id,
                CustomerName = id,
                Tags = tags,
                FromPortId = from,
                ToPortId = to,
                DeadlineAtSeconds = state.ElapsedSeconds + deadlineSeconds,
                Revenue = revenue,
                Clue = clue,
            };
            state.Orders.Add(o);
            return o;
        }
    }
}
