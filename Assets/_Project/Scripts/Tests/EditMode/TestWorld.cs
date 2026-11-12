using System.Collections.Generic;
using System.Linq;
using AyeAye;
using UnityEngine;

namespace AyeAye.Tests.EditMode
{
    /// <summary>
    /// Dữ liệu mẫu SRS §5 dưới dạng SO trong bộ nhớ (tạo mỗi lần SetUp, hủy ở TearDown).
    /// RouteOption hazards/length tự suy từ danh sách cạnh §5.2 — single source of truth.
    /// </summary>
    public class TestWorld
    {
        // §5.1 port ids
        public const string Goc = "goc", Vang = "vang", Suong = "suong", Camap = "camap", Mogio = "mogio", Tram = "tram";

        // §5.2 cạnh (không hướng): "a|b|độ dài|nguy hiểm"
        private static readonly string[] Edges =
        {
            Goc + "|" + Vang + "|1|Clear",
            Goc + "|" + Suong + "|1|Fog",
            Vang + "|" + Camap + "|2|Pirates",
            Suong + "|" + Camap + "|1|Fog",
            Vang + "|" + Mogio + "|1|Storm",
            Camap + "|" + Tram + "|1|Ice",
            Mogio + "|" + Tram + "|2|Clear",
        };

        public GameConfig Config;
        public RouteTable Routes;
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
            w.Config = w.Track(ScriptableObject.CreateInstance<GameConfig>());
            w.Routes = BuildRoutes();
            w.OrderPool = BuildOrders();
            w.ShipDefs = BuildShips();
            w.Crews = BuildCrews();
            BuildIncidents(w);
            foreach (var s in w.ShipDefs) w.Track(s);
            foreach (var c in w.Crews) w.Track(c);
            foreach (var i in w.Incidents) w.Track(i);
            foreach (var o in w.OrderPool.fixedFirstDay) w.Track(o);
            foreach (var o in w.OrderPool.pool) w.Track(o);
            return w;
        }

        private T Track<T>(T so) where T : ScriptableObject
        {
            SOs.Add(so);
            return so;
        }

        private static RouteOption Opt(string label, params string[] path)
        {
            var opt = new RouteOption { label = label, portPath = new List<string>(path), hazards = new List<HazardType>() };
            float length = 0f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                foreach (var e in Edges)
                {
                    var p = e.Split('|');
                    bool fwd = p[0] == path[i] && p[1] == path[i + 1];
                    bool rev = p[1] == path[i] && p[0] == path[i + 1];
                    if (!fwd && !rev) continue;
                    length += float.Parse(p[2]);
                    opt.hazards.Add(System.Enum.Parse<HazardType>(p[3]));
                    break;
                }
            }
            opt.length = length;
            return opt;
        }

        private static RouteTable BuildRoutes()
        {
            var rt = ScriptableObject.CreateInstance<RouteTable>();
            rt.pairs = new List<RoutePairEntry>();

            void Add(string from, string to, RouteOption[] opts)
            {
                rt.pairs.Add(new RoutePairEntry { fromPortId = from, toPortId = to, options = new List<RouteOption>(opts) });
                rt.pairs.Add(new RoutePairEntry { fromPortId = to, toPortId = from, options = opts.Select(o =>
                    new RouteOption
                    {
                        label = o.label + " (chiều về)",
                        portPath = o.portPath.AsEnumerable().Reverse().ToList(),
                        hazards = o.hazards.AsEnumerable().Reverse().ToList(),
                        length = o.length,
                    }).ToList() });
            }

            // Đường đi xấp xỉ (multi-hop) cho mọi cặp cảng — đều 2 chiều qua Add
            Add(Goc, Vang, new[] { Opt("Qua thẳng", Goc, Vang) });
            Add(Goc, Suong, new[] { Opt("Đường mù", Goc, Suong) });
            Add(Goc, Camap, new[] { Opt("Qua Sương Mù", Goc, Suong, Camap), Opt("Qua Vàng", Goc, Vang, Camap) });
            Add(Goc, Mogio, new[] { Opt("Qua Vàng", Goc, Vang, Mogio) });
            Add(Goc, Tram, new[] { Opt("Ven Sương Mù", Goc, Suong, Camap, Tram), Opt("Qua Mỏm Gió", Goc, Vang, Mogio, Tram) });
            Add(Vang, Suong, new[] { Opt("Qua Gốc", Vang, Goc, Suong), Opt("Vòng Cá Mập", Vang, Camap, Suong) });
            Add(Vang, Camap, new[] { Opt("Biển cướp", Vang, Camap) });
            Add(Vang, Tram, new[] { Opt("Qua Mỏm Gió", Vang, Mogio, Tram), Opt("Qua Cá Mập", Vang, Camap, Tram) });
            Add(Suong, Camap, new[] { Opt("Đường mù", Suong, Camap) });
            Add(Suong, Mogio, new[] { Opt("Qua Gốc", Suong, Goc, Vang, Mogio), Opt("Vòng Trạm Lạnh", Suong, Camap, Tram, Mogio) });
            Add(Suong, Tram, new[] { Opt("Qua Cá Mập", Suong, Camap, Tram) });
            Add(Camap, Mogio, new[] { Opt("Qua Vàng", Camap, Vang, Mogio), Opt("Qua Trạm Lạnh", Camap, Tram, Mogio) });
            Add(Camap, Tram, new[] { Opt("Biển băng", Camap, Tram) });
            Add(Mogio, Tram, new[] { Opt("Đường thẳng", Mogio, Tram) });
            return rt;
        }

        private static ShipDef Ship(string id, string name, CrewId captain, string capName, SpeedClass speed, string traits, string port)
        {
            var s = ScriptableObject.CreateInstance<ShipDef>();
            s.shipId = id; s.displayName = name; s.captain = captain; s.captainName = capName;
            s.speedClass = speed; s.traits = traits; s.startPortId = port; s.startCondition = ShipCondition.Good;
            return s;
        }

        private static List<ShipDef> BuildShips()
        {
            // §5.3
            return new List<ShipDef>
            {
                Ship("tau-tia-chop", "Tia Chớp", CrewId.Bob, "Bob", SpeedClass.Fast, "Nhanh · Ẩu · Thích đường tắt", Goc),
                Ship("tau-rua-vang", "Rùa Vàng", CrewId.Chen, "Chen", SpeedClass.Slow, "Chậm · Thân vững · Bình tĩnh", Goc),
                Ship("tau-dung-quy-trinh", "Đúng Quy Trình", CrewId.Mira, "Mira", SpeedClass.Normal, "Chuẩn sách · Hơi cứng nhắc", Goc),
                Ship("tau-nghi-ngo", "Nghi Ngờ Số Một", CrewId.Dex, "Dex", SpeedClass.Normal, "Đa nghi · Kiểm tra kỹ", Vang),
            };
        }

        private static OrderDef Order(string id, string customer, OrderTag tags, string from, string to, int deadline, int revenue, string clue)
        {
            var o = ScriptableObject.CreateInstance<OrderDef>();
            o.orderId = id; o.customerName = customer; o.tags = tags; o.fromPortId = from; o.toPortId = to;
            o.deadlineDay = deadline; o.revenue = revenue; o.clue = clue; o.weight = 1;
            return o;
        }

        private static OrderPool BuildOrders()
        {
            // §5.4
            var pool = ScriptableObject.CreateInstance<OrderPool>();
            pool.fixedFirstDay = new List<OrderDef>
            {
                Order("O-01", "Tiệm Bánh Hành Tinh Xa", OrderTag.Fragile, Goc, Vang, 3, 120, "DO NOT SHAKE / KEEP UPRIGHT / DELIVER BEFORE THE CANDLES"),
                Order("O-02", "Cửa hàng Dép Lê Số 1", OrderTag.None, Goc, Camap, 4, 100, "Tuyến Sương Mù ngắn nhưng... mù như tâm trạng sếp"),
            };
            pool.pool = new List<OrderDef>
            {
                Order("O-03", "Kem Que Sa Mạc", OrderTag.Cold, Goc, Mogio, 4, 90, "Kem mà đi đường bão... ẵm về nước"),
                Order("O-04", "Tiệm Gương Vỡ Hạnh Phúc", OrderTag.Fragile, Suong, Camap, 5, 110, "Gương vỡ = 7 năm xui. Đừng cho Bob chở."),
                Order("O-05", "Pizza Giao Chậm Là Miễn Phí", OrderTag.Urgent, Goc, Vang, 3, 70, "Tên cửa hàng đã nói tất cả."),
                Order("O-06", "Hội Sưu Tập Đá Cuội", OrderTag.None, Vang, Tram, 7, 100, "Đá cuội không vội. Nhưng sếp thì có."),
                Order("O-07", "Đám Cưới Cá Heo", OrderTag.Fragile | OrderTag.Urgent, Camap, Goc, 6, 160, "Cá heo không đợi. Hàng dễ vỡ thì càng không."),
                Order("O-08", "Bưu Kiện Không Ghi Tên", OrderTag.None, Mogio, Camap, 8, 130, "Không ai nhận trách nhiệm. Đặc biệt là nội dung."),
            };
            return pool;
        }

        private static List<CrewDefinition> BuildCrews()
        {
            CrewDefinition C(CrewId id, string name, string normal, string quote)
            {
                var c = ScriptableObject.CreateInstance<CrewDefinition>();
                c.id = id; c.displayName = name; c.personalityLabel = name;
                c.defaultNormalLine = normal; c.defaultQuote = quote;
                return c;
            }
            return new List<CrewDefinition>
            {
                C(CrewId.Bob, "Bob", "Chưa chết con nào.", "Tốc độ là sức mạnh!"),
                C(CrewId.Mira, "Mira", "Theo đúng quy trình.", "Quy trình tồn tại vì có lý do."),
                C(CrewId.Chen, "Chen", "Bình tĩnh, sẽ tới.", "Tàu chậm nhưng hàng nguyên."),
                C(CrewId.Dex, "Dex", "Kiểm tra lại lần nữa.", "Ta nghi ngờ mọi thứ."),
            };
        }

        private static IncidentDef Inc(string id, string title, string situation, string clue, IncidentTrigger[] triggers, params IncidentOption[] options)
        {
            var d = ScriptableObject.CreateInstance<IncidentDef>();
            d.incidentId = id; d.title = title; d.situationText = situation; d.clueText = clue;
            d.weight = 1; d.triggers = new List<IncidentTrigger>(triggers);
            d.options = new List<IncidentOption>(options);
            return d;
        }

        private static IncidentTrigger Trig(HazardType hazard = HazardType.Any, bool fragile = false, bool justArrived = false, bool worn = false, bool tempRepair = false, bool carrying = false, int mult = 1)
        {
            return new IncidentTrigger { requiredHazard = hazard, requireFragileCargo = fragile, requireJustArrivedAtPort = justArrived, requireWornCondition = worn, requireTemporaryRepair = tempRepair, requireCarryingCargo = carrying, weightMultiplier = mult };
        }

        private static IncidentOption OptI(string label, string desc, float baseDelta, params IncidentOutcomeEntry[] entries)
        {
            return new IncidentOption { label = label, description = desc, baseProgressDelta = baseDelta, entries = new List<IncidentOutcomeEntry>(entries) };
        }

        private static IncidentOutcomeEntry E(CrewId crew, FlagCondition cond, DecisionOutcome outcome, string explanation, string quote, FlagToSet flag = FlagToSet.None, int cargoDelta = 0, bool conditionDown = false, int revenueDelta = 0)
        {
            return new IncidentOutcomeEntry { crew = crew, condition = cond, outcome = outcome, explanation = explanation, quote = quote, setFlag = flag, cargoDelta = cargoDelta, conditionDown = conditionDown, revenueDelta = revenueDelta };
        }

        private static void BuildIncidents(TestWorld w)
        {
            // §5.5 — đầy đủ bảng (captain, option). Quy ước: entry khớp đầu tiên thắng; không khớp → Normal mặc định.
            var i01 = Inc("I-01", "Sương mù dày", "Bờ trước mũi tàu biến mất trong sương.", "Mira có sách tín hiệu cảng; Dex ngờ tín hiệu.",
                new[] { Trig(hazard: HazardType.Fog) },
                OptI("Giữ tốc độ", "Không giảm tốc.", 0.3f,
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.SlipUp, "Bob lao thẳng trong mù.", "Mù gì mà mù! Vào thôi!"),
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "Tàu lướt qua mù mà không bị gì — lần này.", "Thấy không, cứ chạy đều.")),
                OptI("Giảm tốc dò đường", "Chậm, bấm còi liên tục.", 0f,
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "Chậm nhưng an toàn.", "Bíp bíp... bíp..."),
                    E(CrewId.Chen, FlagCondition.Always, DecisionOutcome.Smart, "Chen nghe sóng, đo mù chính xác.", "Chậm một nhịp, đỡ cả ngày."),
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.Normal, "Dex bấm còi hơi nhiều.", "Cẩn thận không bao giờ là thừa.")),
                OptI("Theo tín hiệu cảng", "Nhớ sơ đồ đèn các cảng.", 0f,
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.Smart, "Mira đọc chuẩn sách tín hiệu.", "Trang 47: hai chớp, một dài."),
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.SlipUp, "Dex đọc nhầm đèn.", "Khoan... đèn đó là của bến phụ?"))
                );

            var i02 = Inc("I-02", "Bão bất ngờ", "Khối mây đen nghiêng về phía mũi tàu.", "Rùa Vàng đã gia cố thân tàu.",
                new[] { Trig(hazard: HazardType.Storm) },
                OptI("Xuyên bão", "Giữ hướng, đâm thẳng.", 0.3f,
                    E(CrewId.Chen, FlagCondition.Always, DecisionOutcome.Smart, "Thân tàu gia cố chịu được sóng.", "Sóng lớn thì tàu phải cứng."),
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.SlipUp, "Bob lái như đi mô-tô.", "Bay lên nào!", conditionDown: true),
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "Tàu rung lắc nhưng qua được.", "Cầm chặt tay lái.")),
                OptI("Neo chờ", "Neo lại cho bão đi qua.", -0.3f,
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "Chờ bão tan, mất thời gian.", "Chờ cho lành.")),
                OptI("Vòng tránh", "Đi vòng cung tránh tâm bão.", 0f,
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.Smart, "Mira tính đúng vòng cung.", "Tâm bão ở đây, ta ở đây."),
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.SlipUp, "Dex vòng sai, chạm rìa bão.", "Đúng là phải vòng... hướng kia!", conditionDown: true))
                );

            var i03 = Inc("I-03", "Hải tặc chặn đường", "Ba tàu nhỏ không cờ, chặn ngang mũi tàu.", "Dex đã nghi từ lúc rời cảng.",
                new[] { Trig(hazard: HazardType.Pirates) },
                OptI("Trả phí qua đường", "Đưa 'phí qua lại' cho bọn cướp.", 0f,
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "Được qua, sạc hụt 20.", "Đây... coi như tiền xe ôm.", revenueDelta: -20),
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.Smart, "Bob mặc cả cực mạnh, phí rẻ nhất.", "Mười chục thôi, ông nội!")),
                OptI("Phóng nhanh thoát", "Tăng tốc toàn lực.", 0.3f,
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.Smart, "Không tàu nào nhanh hơn Tia Chớp.", "Tạm biệt, đàn cá!"),
                    E(CrewId.Chen, FlagCondition.Always, DecisionOutcome.SlipUp, "Rùa Vàng mà đua? Kẹt.", "Ơ... chân vịt quay rồi!")),
                OptI("Mặc cả / đánh lừa", "Chém gió cho bọn cướp quay đầu.", 0f,
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.Smart, "Dex nói dối có mặt giàu kinh nghiệm.", "Đằng kia có cảnh sát biển."),
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.SlipUp, "Mira không biết nói dối.", "Xin chào, chúng tôi chở hàng quý giá..."))
                );

            var i04 = Inc("I-04", "Kẹt cảng", "Cảng đông, tàu xếp hàng dài.", "Bob quen nửa bến cảng.",
                new[] { Trig(justArrived: true) },
                OptI("Xếp hàng chờ", "Thứ tự đã có luật.", 0f,
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "Chờ theo lượt.", "Vào hàng đi, lần lượt."),
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.Smart, "Bob chen được nhờ quen mặt.", "Ê already! Vào trước được không?")),
                OptI("Nhờ quan hệ chen ngang", "Gọi một người quen bến.", 0.3f,
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.SlipUp, "Người quen của Dex hóa ra... hận Dex.", "Cậu còn nợ tôi bữa nhậu!")),
                OptI("Chuyển bến phụ", "Cập bến phụ cách đó một chút.", 0f,
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.Smart, "Mira có bản đồ bến phụ chuẩn.", "Sơ đồ cảng, phụ lục 12."))
                );

            var i05 = Inc("I-05", "Hàng rung lắc", "Thùng hàng trên boong rung như trống hội.", "Mira chằng hàng theo sách; Chen biết cách chèn.",
                new[] { Trig(fragile: true) },
                OptI("Giảm tốc", "Chậm lại cho bớt rung.", 0f,
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "Rung ít hơn, hàng ổn.", "Đi chậm thôi, hàng quan trọng.")),
                OptI("Chằng lại hàng giữa biển", "Ai ra buộc dây?", 0f,
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.Smart, "Mira buộc chuẩn kỹ thuật.", "Dây chằng, hai vòng, siết chéo."),
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.SlipUp, "Bob buộc dây như buộc giày.", "Xong! Chắc chắn... chắc là chắc.", cargoDelta: -1)),
                OptI("Mặc kệ", "Hàng rung thì kệ nó.", 0f,
                    E(CrewId.Chen, FlagCondition.Always, DecisionOutcome.Normal, "Chen chèn thùng bằng... chính mình ngồi lên.", "Ngồi lên là hết rung."),
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.SlipUp, "Hàng nảy trên boong.", "Nghe tiếng gì đó vỡ nhẹ...", cargoDelta: -1))
                );

            var i06 = Inc("I-06", "Máy móc hỏng", "Một tiếng 'kịch' dưới khoang, rồi máy khựng lại.", "Mira đọc được sơ đồ máy; Bob có bật 'sửa tạm' trước.",
                new[] { Trig(worn: true), Trig(tempRepair: true, mult: 3) },
                OptI("Sửa chuẩn", "Ngừng để sửa đúng cách, mất thời gian.", -1f,
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.Smart, "Mira sửa nhanh và đúng.", "Sơ đồ 3B: thay rơ-le số hai."),
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.Normal, "Bob vặn ốc chậm rãi.", "Không gì là không thể vặn."),
                    E(CrewId.Chen, FlagCondition.Always, DecisionOutcome.Normal, "Chen sửa chắc như trồng cây.", "Từ từ, chắc tay.")),
                OptI("Sửa tạm", "Băng keo và hy vọng.", 0f,
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.Smart, "Bob sửa tạm cực khéo.", "Nó chạy rồi! ừ thì… hơi rung, và có tiếng lạ nữa.", flag: FlagToSet.TemporaryRepair),
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.SlipUp, "Dex sửa tạm làm máy tệ hơn.", "Rõ ràng tôi đã nối đúng dây... dây nào?"))
                );

            var i07 = Inc("I-07", "Tín hiệu cầu cứu", "Một tín hiệu loé giữa biển, không rõ ai.", "Bob thích phiêu lưu; Dex nghĩ nó là bẫy.",
                new[] { Trig() },
                OptI("Bỏ qua", "Không phải việc của ta.", 0f,
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "Ta có lịch trình.", "Chuyện của người ta.")),
                OptI("Đi giúp", "Rẽ hướng đi kiểm tra.", 0f,
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.Smart, "Bob cứu được người, nhận quà bánh.", "Đây, quà nhé! Bánh miếng to lắm!"),
                    E(CrewId.Chen, FlagCondition.Always, DecisionOutcome.SlipUp, "Chen đi sai tọa độ.", "Ủa, dấu chấm trên bản đồ là gì?")),
                OptI("Kiểm tra tín hiệu", "Đọc kỹ nguồn tín hiệu trước.", 0f,
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.Normal, "Mira xác nhận là tin nhắn giả.", "Tín hiệu lặp chu kỳ 7 giây — máy phát."),
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.SlipUp, "Dex quá tin máy phát của 'ai đó'.", "Quá trùng hợp. Ai lại mất tín hiệu đúng lúc cần tiệc?"))
                );

            var iF = Inc("I-F", "Cuộc gọi Chủ tịch", "Điện thoại rung. Màn hình: 'CHỦ TỊCH'.", "Chủ tịch chỉ hỏi một câu: 'Tới được không?'",
                new[] { Trig(carrying: true) },
                OptI("Ép hết tốc lực", "'Tôi hứa tới. Tận cùng tốc độ.'", 0.3f,
                    E(CrewId.Bob, FlagCondition.TemporaryRepairForbidden, DecisionOutcome.Smart, "Bob thiêu máy mà vẫn sống.", "Tới! Tận cùng tốc độ!"),
                    E(CrewId.Bob, FlagCondition.TemporaryRepairRequired, DecisionOutcome.SlipUp, "Máy đang tạm, Bob thiêu luôn.", "Máy từ chối hy sinh cho lời hứa.", flag: FlagToSet.CargoShifted, cargoDelta: -1),
                    E(CrewId.Any, FlagCondition.CargoShiftedRequired, DecisionOutcome.Normal, "Hàng vốn đã xô dịch, ép tốc làm nó tệ thêm.", "Nghe tiếng thùng nghiêng rõ hơn...", flag: FlagToSet.CargoShifted, cargoDelta: -1),
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "Hứa là phải giữ lời.", "Được, tôi ép tốc.")),
                OptI("Giữ nhịp", "'Tôi sẽ tới, giờ như đã báo.'", 0f,
                    E(CrewId.Chen, FlagCondition.Always, DecisionOutcome.Normal, "Chen giữ đúng lời hứa của mình.", "Báo mấy giờ là mấy giờ.")),
                OptI("Xin gia hạn khách", "'Tôi xin thêm 1 giờ, đổi lại hàng nguyên vẹn.'", 0f,
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.Smart, "Mira đàm phán chuẩn lịch sự.", "Xin gia hạn, bồi thường bằng sự chuyên nghiệp."),
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.SlipUp, "Dex xin sai người, sai giờ.", "Gọi nhầm số... của rival?"))
                );

            w.FixedFirstDay = i01;
            w.FixedFinale = iF;
            w.Incidents = new List<IncidentDef> { i02, i03, i04, i05, i06, i07 };
        }
    }
}
