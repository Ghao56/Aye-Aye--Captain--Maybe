using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace AyeAye
{

    public static class DemoData
    {

        public const string Goc = "goc", Vang = "vang", Suong = "suong", Camap = "camap", Tram = "tram";
        static readonly string[] Edges =
        {
            Goc + "|" + Vang + "|1|Clear",
            Goc + "|" + Suong + "|1|Fog",
            Goc + "|" + Camap + "|2|Pirates",
            Goc + "|" + Tram + "|2|Ice",
            Suong + "|" + Camap + "|1|Fog",
            Camap + "|" + Tram + "|1|Storm",
        };

        public static GameConfig CreateConfig()
        {
            return ScriptableObject.CreateInstance<GameConfig>();
        }

        public static List<PortDef> BuildPorts()
        {
            PortDef P(string id, string name, bool hq = false, float x = 0.5f, float y = 0.5f)
            {
                var p = ScriptableObject.CreateInstance<PortDef>();
                p.portId = id; p.displayName = name; p.isHeadquarters = hq;
                p.mapPosition = new Vector2(x, y);
                return p;
            }
            return new List<PortDef>
            {
                P(Goc, "Central Hub", hq: true),
                P(Vang, "Golden Harbor", x: 0.2f, y: 0.25f),
                P(Suong, "Fog Isle", x: 0.2f, y: 0.75f),
                P(Camap, "Shark Wharf", x: 0.8f, y: 0.75f),
                P(Tram, "North Ice Station", x: 0.8f, y: 0.25f),
            };
        }

        public static List<RouteDef> BuildRouteDefs()
        {
            var list = new List<RouteDef>();
            foreach (string edge in Edges)
            {
                string[] p = edge.Split('|');
                var r = ScriptableObject.CreateInstance<RouteDef>();
                r.fromPortId = p[0]; r.toPortId = p[1];
                r.length = int.Parse(p[2]);
                r.hazard = System.Enum.Parse<HazardType>(p[3]);
                list.Add(r);
            }
            return list;
        }
        static RouteOption Opt(string label, params string[] path)
        {
            var opt = new RouteOption { label = label, portPath = new List<string>(path), hazards = new List<HazardType>() };
            float length = 0f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                foreach (string e in Edges)
                {
                    string[] p = e.Split('|');
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

        public static RouteTable BuildRouteTable()
        {
            var rt = ScriptableObject.CreateInstance<RouteTable>();
            rt.pairs = new List<RoutePairEntry>();
            void Add(string from, string to, RouteOption[] opts)
            {
                rt.pairs.Add(new RoutePairEntry { fromPortId = from, toPortId = to, options = new List<RouteOption>(opts) });
                rt.pairs.Add(new RoutePairEntry { fromPortId = to, toPortId = from, options = opts.Select(o =>
                    new RouteOption
                    {
                        label = o.label + " (return)",
                        portPath = o.portPath.AsEnumerable().Reverse().ToList(),
                        hazards = o.hazards.AsEnumerable().Reverse().ToList(),
                        length = o.length,
                    }).ToList() });
            }
            Add(Goc, Vang, new[] { Opt("Direct", Goc, Vang) });
            Add(Goc, Suong, new[] { Opt("Fog Route", Goc, Suong) });
            Add(Goc, Camap, new[] { Opt("Via Fog Isle", Goc, Suong, Camap), Opt("Pirate Sea", Goc, Camap) });
            Add(Goc, Tram, new[] { Opt("Via Ice", Goc, Tram), Opt("Via Shark Wharf", Goc, Camap, Tram) });
            Add(Vang, Suong, new[] { Opt("Via Hub", Vang, Goc, Suong) });
            Add(Vang, Camap, new[] { Opt("Via Hub", Vang, Goc, Camap), Opt("Via Fog Isle", Vang, Goc, Suong, Camap) });
            Add(Vang, Tram, new[] { Opt("Via Hub", Vang, Goc, Tram) });
            Add(Suong, Camap, new[] { Opt("Fog Route", Suong, Camap), Opt("Via Hub", Suong, Goc, Camap) });
            Add(Suong, Tram, new[] { Opt("Via Shark Wharf", Suong, Camap, Tram) });
            Add(Camap, Tram, new[] { Opt("Storm Sea", Camap, Tram), Opt("Via Hub", Camap, Goc, Tram) });
            return rt;
        }

        public static List<ShipDef> BuildShips()
        {
            ShipDef Ship(string id, string name, CrewId captain, string capName, SpeedClass speed, string traits, string port, RoamProfile roam)
            {
                var s = ScriptableObject.CreateInstance<ShipDef>();
                s.shipId = id; s.displayName = name; s.captain = captain; s.captainName = capName;
                s.speedClass = speed; s.traits = traits; s.startPortId = port; s.startCondition = ShipCondition.Good;
                s.roam = roam;
                return s;
            }
            return new List<ShipDef>
            {
                Ship("tau-tia-chop", "CARGO-9", CrewId.Bob, "Bob", SpeedClass.Fast, "Fast · Loud · Shortcut lover", Goc,
                    new RoamProfile { dwellSeconds = 8f, demandWeight = 2f, farBias = 0.6f, wanderWeight = 2f }),
                Ship("tau-rua-vang", "HAULER-4", CrewId.Chen, "Chen", SpeedClass.Slow, "Slow · Sturdy · Calm", Goc,
                    new RoamProfile { dwellSeconds = 30f, demandWeight = 2f, farBias = -0.5f, hubAffinity = 3f, wanderWeight = 1f, backtrackPenalty = 3f }),
                Ship("tau-dung-quy-trinh", "FREIGHT-12", CrewId.Mira, "Mira", SpeedClass.Normal, "By the book · A bit stiff", Goc,
                    new RoamProfile { dwellSeconds = 15f, demandWeight = 2f, hubAffinity = 3f, wanderWeight = 0f }),
                Ship("tau-nghi-ngo", "TRANSIT-3", CrewId.Dex, "Dex", SpeedClass.Normal, "Suspicious · Double-checks", Vang,
                    new RoamProfile { dwellSeconds = 15f, demandWeight = 2f, wanderWeight = 6f }),
            };
        }

        public static List<CrewDefinition> BuildCrews()
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
                C(CrewId.Bob, "Bob", "No one has died yet.", "Speed is power!"),
                C(CrewId.Mira, "Mira", "Following procedure.", "Procedure exists for a reason."),
                C(CrewId.Chen, "Chen", "Stay calm, we'll get there.", "Slow ship, intact cargo."),
                C(CrewId.Dex, "Dex", "Check it once more.", "I doubt everything."),
            };
        }
        static OrderDef Order(string id, string customer, OrderTag tags, string from, string to, float deadline, int revenue, string clue)
        {
            var o = ScriptableObject.CreateInstance<OrderDef>();
            o.orderId = id; o.customerName = customer; o.tags = tags; o.fromPortId = from; o.toPortId = to;
            o.deadlineSeconds = deadline; o.revenue = revenue; o.clue = clue; o.weight = 1;
            return o;
        }

        public static OrderPool BuildOrderPool()
        {
            var pool = ScriptableObject.CreateInstance<OrderPool>();
            pool.fixedFirstDay = new List<OrderDef>
            {
                Order("O-01", "Far Planet Bakery", OrderTag.Fragile, Goc, Vang, 70f, 120, "The crate reads DO NOT SHAKE / KEEP UPRIGHT / DELIVER BEFORE THE CANDLES"),
                Order("O-02", "Le's Flip-Flop Shop No.1", OrderTag.None, Goc, Camap, 85f, 100, "The Fog Isle route is short but... foggy like the boss's mood"),
            };
            pool.pool = new List<OrderDef>
            {
                Order("O-03", "Desert Popsicle", OrderTag.Cold, Goc, Tram, 75f, 90, "Ice cream crossing a storm... bring a bucket"),
                Order("O-04", "Happy Broken Mirror Shop", OrderTag.Fragile, Suong, Camap, 45f, 110, "Broken mirror = 7 years of bad luck. Pad it well."),
                Order("O-05", "Late Pizza Is Free", OrderTag.Urgent, Goc, Vang, 45f, 70, "The shop's name says it all."),
                Order("O-06", "Pebble Collectors Club", OrderTag.None, Vang, Tram, 90f, 100, "Pebbles are in no hurry. The boss is."),
                Order("O-07", "Dolphin Wedding", OrderTag.Fragile | OrderTag.Urgent, Camap, Goc, 90f, 160, "Dolphins don't wait. Fragile cargo even less."),
                Order("O-08", "Unsigned Parcel", OrderTag.None, Suong, Tram, 75f, 130, "Nobody claims responsibility. Especially for the contents."),
            };
            return pool;
        }
        static IncidentDef Inc(string id, string title, string situation, string clue, IncidentTrigger[] triggers, params IncidentOption[] options)
        {
            var d = ScriptableObject.CreateInstance<IncidentDef>();
            d.incidentId = id; d.title = title; d.situationText = situation; d.clueText = clue;
            d.weight = 1; d.triggers = new List<IncidentTrigger>(triggers);
            d.options = new List<IncidentOption>(options);
            return d;
        }
        static IncidentTrigger Trig(HazardType hazard = HazardType.Any, bool fragile = false, bool justArrived = false, bool worn = false, bool tempRepair = false, bool carrying = false, int mult = 1)
        {
            return new IncidentTrigger { requiredHazard = hazard, requireFragileCargo = fragile, requireJustArrivedAtPort = justArrived, requireWornCondition = worn, requireTemporaryRepair = tempRepair, requireCarryingCargo = carrying, weightMultiplier = mult };
        }
        static IncidentOption OptI(string label, string desc, float baseDelta, params IncidentOutcomeEntry[] entries)
        {
            return new IncidentOption { label = label, description = desc, baseProgressDelta = baseDelta, entries = new List<IncidentOutcomeEntry>(entries) };
        }
        static IncidentOutcomeEntry E(CrewId crew, FlagCondition cond, DecisionOutcome outcome, string explanation, string quote, FlagToSet flag = FlagToSet.None, int cargoDelta = 0, bool conditionDown = false, int revenueDelta = 0)
        {
            return new IncidentOutcomeEntry { crew = crew, condition = cond, outcome = outcome, explanation = explanation, quote = quote, setFlag = flag, cargoDelta = cargoDelta, conditionDown = conditionDown, revenueDelta = revenueDelta };
        }

        public static IncidentPool BuildIncidentPool()
        {
            var i01 = Inc("I-01", "Thick Fog", "The shoreline ahead vanishes into the fog.", "Mira has the port signal book; Dex doubts signals.",
                new[] { Trig(hazard: HazardType.Fog) },
                OptI("Hold speed", "Don't slow down.", 0.3f,
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.SlipUp, "Bob charges straight into the fog.", "What fog? Full steam ahead!"),
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "The ship glides through unharmed — this time.", "See? Just keep steady.")),
                OptI("Slow and probe", "Slow down, honk continuously.", 0f,
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "Slow but safe.", "Beep beep... beep..."),
                    E(CrewId.Chen, FlagCondition.Always, DecisionOutcome.Smart, "Chen reads the waves, measures the fog precisely.", "One beat slower, a whole day saved."),
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.Normal, "Dex honks a bit too much.", "Being careful is never wasted.")),
                OptI("Follow port signals", "Recall every port's light patterns.", 0f,
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.Smart, "Mira reads the signal book flawlessly.", "Page 47: two blinks, one long."),
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.SlipUp, "Dex misreads the lights.", "Wait... is that light the secondary dock's?"))
                );
            var i02 = Inc("I-02", "Sudden Storm", "A black cloud mass leans over the bow.", "HAULER-4 has a reinforced hull.",
                new[] { Trig(hazard: HazardType.Storm) },
                OptI("Push through", "Hold course, punch straight.", 0.3f,
                    E(CrewId.Chen, FlagCondition.Always, DecisionOutcome.Smart, "The reinforced hull takes the waves.", "Big waves need a stiff ship."),
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.SlipUp, "Bob drives like it's a motorbike.", "Fly, baby, fly!", conditionDown: true),
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "The ship shakes but makes it.", "Grip the wheel tight.")),
                OptI("Drop anchor", "Anchor until the storm passes.", -0.3f,
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "Wait it out, lose time.", "This too shall pass.")),
                OptI("Arc around", "Sail an arc around the storm's eye.", 0f,
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.Smart, "Mira computes the arc correctly.", "The eye is here, we are here."),
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.SlipUp, "Dex arcs wrong, clips the edge.", "Right idea to arc... the OTHER way!", conditionDown: true))
                );
            var i03 = Inc("I-03", "Pirates Blockade", "Three unflagged small ships block the bow.", "Dex suspected since leaving port.",
                new[] { Trig(hazard: HazardType.Pirates) },
                OptI("Pay the toll", "Hand the pirates a 'toll'.", 0f,
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "Passage granted, 20 revenue lighter.", "Here... think of it as a taxi fare.", revenueDelta: -20),
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.Smart, "Bob haggles like a legend, cheapest toll.", "Ten bucks, tops, grandpa!")),
                OptI("Sprint away", "Full throttle.", 0.3f,
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.Smart, "No ship is faster than CARGO-9.", "Bye-bye, fish school!"),
                    E(CrewId.Chen, FlagCondition.Always, DecisionOutcome.SlipUp, "HAULER-4 racing? Stuck.", "Huh... is that the propeller spinning?")),
                OptI("Bluff and bargain", "Talk the pirates into turning around.", 0f,
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.Smart, "Dex lies with a seasoned face.", "Coast guard right over there."),
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.SlipUp, "Mira cannot lie.", "Hello, we are carrying very precious cargo..."))
                );
            var i04 = Inc("I-04", "Port Jam", "The port is packed, ships queue for miles.", "Bob knows half the dockworkers.",
                new[] { Trig(justArrived: true) },
                OptI("Queue up", "There are rules for order.", 0f,
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "Wait your turn.", "Line up, one by one."),
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.Smart, "Bob cuts the line through friends.", "Hey already! Can we slip in front?")),
                OptI("Call a connection", "Ring a dock contact.", 0.3f,
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.SlipUp, "Dex's contact turns out to... hate Dex.", "You still owe me a night out!")),
                OptI("Use side dock", "Dock at the secondary pier nearby.", 0f,
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.Smart, "Mira has an exact map of the side docks.", "Port diagram, appendix 12."),
                    E(CrewId.Chen, FlagCondition.Always, DecisionOutcome.SlipUp, "Chen misjudges the side dock by a day.", "This side dock... is whose side dock?"))
                );
            var i05 = Inc("I-05", "Rattling Cargo", "Crates on deck rattle like a festival drum.", "Mira lashes per the manual; Chen knows the wedge trick.",
                new[] { Trig(fragile: true) },
                OptI("Slow down", "Ease off to stop the rattle.", 0f,
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "Less rattle, cargo fine.", "Slow and steady, the cargo matters.")),
                OptI("Re-lash at sea", "Who goes out to tie ropes?", 0f,
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.Smart, "Mira lashes to spec.", "Lashing rope, two loops, cross-tighten."),
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.SlipUp, "Bob ties knots like shoelaces.", "Done! Solid... probably solid.", cargoDelta: -1)),
                OptI("Ignore it", "Let it rattle.", 0f,
                    E(CrewId.Chen, FlagCondition.Always, DecisionOutcome.Normal, "Chen wedges the crate by... sitting on it.", "Sit on it and the rattle stops."),
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.SlipUp, "Cargo bounces across the deck.", "Was that a soft crack?", cargoDelta: -1))
                );
            var i06 = Inc("I-06", "Engine Failure", "A 'clunk' below deck, then the engine stalls.", "Mira can read engine diagrams; Bob used 'quick fix' earlier.",
                new[] { Trig(worn: true), Trig(tempRepair: true, mult: 3) },
                OptI("Proper repair", "Stop and fix it right, costs time.", -1f,
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.Smart, "Mira repairs fast and correctly.", "Diagram 3B: replace relay two."),
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.Normal, "Bob turns screws at a leisurely pace.", "Nothing is unscrewable."),
                    E(CrewId.Chen, FlagCondition.Always, DecisionOutcome.Normal, "Chen repairs as solid as planting a tree.", "Slowly, firmly.")),
                OptI("Quick fix", "Duct tape and hope.", 0f,
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.Smart, "Bob's quick fix is a work of art.", "It runs! Well... it shakes, and there's a new noise.", flag: FlagToSet.TemporaryRepair),
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.SlipUp, "Dex's quick fix makes it worse.", "I clearly connected the right wire... which wire?"))
                );
            var i07 = Inc("I-07", "Distress Signal", "A signal flashes out at sea — sender unknown.", "Bob loves adventure; Dex calls it a trap.",
                new[] { Trig() },
                OptI("Ignore", "Not our business.", 0f,
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "We have a schedule.", "Their problem, not ours.")),
                OptI("Go help", "Change course to check.", 0f,
                    E(CrewId.Bob, FlagCondition.Always, DecisionOutcome.Smart, "Bob rescues them and gets cake.", "Here, a gift! Big cake slice!"),
                    E(CrewId.Chen, FlagCondition.Always, DecisionOutcome.SlipUp, "Chen sails to the wrong coordinates.", "Wait, what was that dot on the map?")),
                OptI("Verify first", "Study the signal source first.", 0f,
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.Normal, "Mira confirms it is a fake.", "The signal loops every 7 seconds — a machine."),
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.SlipUp, "Dex trusts 'someone's' transmitter too much.", "Too convenient. Who loses a signal exactly at party time?"))
                );
            var iF = Inc("I-F", "The Chairman Calls", "The phone buzzes. Screen: 'CHAIRMAN'.", "The chairman asks one question: 'Will you make it?'",
                new[] { Trig(carrying: true) },
                OptI("Full speed, promise", "'I promise. Top speed.'", 0.3f,
                    E(CrewId.Bob, FlagCondition.TemporaryRepairForbidden, DecisionOutcome.Smart, "Bob burns the engine and survives.", "Making it! Top speed!"),
                    E(CrewId.Bob, FlagCondition.TemporaryRepairRequired, DecisionOutcome.SlipUp, "The engine is patched; Bob burns it anyway.", "The engine refuses to die for a promise.", flag: FlagToSet.CargoShifted, cargoDelta: -1),
                    E(CrewId.Any, FlagCondition.CargoShiftedRequired, DecisionOutcome.Normal, "The cargo already shifted; speed makes it worse.", "The crates lean audibly further...", flag: FlagToSet.CargoShifted, cargoDelta: -1),
                    E(CrewId.Any, FlagCondition.Always, DecisionOutcome.Normal, "A promise is a promise.", "Fine, full speed.")),
                OptI("Keep the schedule", "'I will arrive as reported.'", 0f,
                    E(CrewId.Chen, FlagCondition.Always, DecisionOutcome.Normal, "Chen keeps his word exactly.", "Reported time is arrival time.")),
                OptI("Ask for an extension", "'One more hour, cargo stays intact.'", 0f,
                    E(CrewId.Mira, FlagCondition.Always, DecisionOutcome.Smart, "Mira negotiates politely to the letter.", "Requesting an extension, compensated with professionalism."),
                    E(CrewId.Dex, FlagCondition.Always, DecisionOutcome.SlipUp, "Dex calls the wrong person at the wrong time.", "Wrong number... the rival's?"))
                );
            var pool = ScriptableObject.CreateInstance<IncidentPool>();
            pool.fixedFirstDay = i01;
            pool.fixedFinale = iF;
            pool.incidents = new List<IncidentDef> { i02, i03, i04, i05, i06, i07 };
            return pool;
        }

        public static readonly List<string> OrderCustomers = new List<string>
        {
            "Far Planet Bakery", "Le's Flip-Flop Shop No.1", "Desert Popsicle", "Happy Broken Mirror Shop",
            "Late Pizza Is Free", "Pebble Collectors Club", "Dolphin Wedding", "Unsigned Parcel",
            "Space Noodle Stand", "Roasted Chicken Feet Depot", "Grandpa Seven's House", "Bottle Buyback Station",
            "Food Cookbook Store", "Lifebuoy Warehouse No.3", "Uncle Five's Rope Shop", "Fish Sauce Trading Co.",
            "Coral Sand Ltd.", "Shark Dentist", "Tropical Fish Farm",
            "Galaxy Night Market", "Red Planet Chicken Ranch", "Cu Lac Pharmacy", "Tattered Sail Workshop",
            "Woodlouse Fan Club",
        };

        public static string OrderClue(OrderTag tags, HazardType hazard, System.Random rng)
        {
            var tagText = tags switch
            {
                var t when (t & OrderTag.Fragile) != 0 => FragileClues[(int)(rng.NextDouble() * FragileClues.Count) % FragileClues.Count],
                var t when (t & OrderTag.Cold) != 0 => ColdClues[(int)(rng.NextDouble() * ColdClues.Count) % ColdClues.Count],
                var t when (t & OrderTag.Urgent) != 0 => UrgentClues[(int)(rng.NextDouble() * UrgentClues.Count) % UrgentClues.Count],
                _ => PlainClues[(int)(rng.NextDouble() * PlainClues.Count) % PlainClues.Count],
            };
            var hazardText = hazard switch
            {
                HazardType.Fog => "That route is often foggy.",
                HazardType.Storm => "Rumor says there's a storm there.",
                HazardType.Pirates => "Unflagged ships roam that area.",
                HazardType.Ice => "Ice drifts are heavy this season.",
                _ => "",
            };
            return hazardText.Length > 0 ? tagText + " " + hazardText : tagText;
        }
        static readonly List<string> FragileClues = new List<string>
        {
            "Customer warns: very fragile, handle gently.",
            "The crate scrawls: DO NOT SHAKE.",
        };
        static readonly List<string> ColdClues = new List<string>
        {
            "If it melts, it's gone — move fast.",
            "The customer asks upfront: does the ship have refrigeration?",
        };
        static readonly List<string> UrgentClues = new List<string>
        {
            "The customer keeps calling — tight deadline.",
            "The shop's name says it all: late is free.",
        };
        static readonly List<string> PlainClues = new List<string>
        {
            "Regular cargo, nothing special. Probably.",
            "The customer is unnervingly calm. Lower your guard.",
        };

        public static EndingDefinitions BuildEndings()
        {
            var endings = ScriptableObject.CreateInstance<EndingDefinitions>();
            EndingDefinitions.EndingEntry En(EndingId id, string title, string body) =>
                new EndingDefinitions.EndingEntry { id = id, title = title, body = body };
            endings.endings = new List<EndingDefinitions.EndingEntry>
            {
                En(EndingId.E1_Empire, "The \"Great Boss\" Empire",
                    "Not a single late order. Every ship sings on deck. At least one captain tears up with emotion — all signs point to Bob, but Bob swears it's sea-spray allergies."),
                En(EndingId.E2_Renowned, "A Company of Renown",
                    "Only one order late. Customers come back to order again; Dex still doubts: \"Orders this good? Careful, it's a trap.\""),
                En(EndingId.E3_Survived, "Survived the Month",
                    "Two orders late, still delivered enough. Barely broke even. Chen texts: \"I'm on my way. Don't ask how far.\""),
                En(EndingId.E4_Debt, "Drowning in Debt",
                    "Too many late orders — the company owes it all. Bob proposes: \"Sell the ships, boss. Any of them. Except mine.\""),
                En(EndingId.E5_Bankrupt, "Bankrupt",
                    "Too late, too soon — the fleet barely delivered anything. The whole crew demands wages in... cake. A multi-tier birthday cake. Already broken."),
            };
            return endings;
        }
    }
}

