using UnityEngine;

namespace AyeAye
{
    // Điểm khởi động: giữ dữ liệu, tạo GameState, kết nối manager và UI. Không chứa logic thắng/thua.
    public class GameManager : MonoBehaviour
    {
        [Header("Data")]
        public GameConfig config;
        public EventPool eventPool;
        public CrewDefinition[] crews = new CrewDefinition[0];
        public EndingDefinitions endings;

        [Header("Core")]
        public DayCycleManager dayCycle;
        public ScreenFlowController screenFlow;
        public ScreenFeedback screenFeedback;

        [Header("Screens & Widgets")]
        public HeaderBarUI header;
        public BriefingScreenUI briefing;
        public StatusScreenUI status;
        public AssignmentScreenUI assignment;
        public ResultOverlayUI resultOverlay;
        public EndScreenUI endScreen;
        public ProgressBarUI progressBar;
        public CakeStatusUI cakeStatus;

        public GameState State { get; private set; }

        public GameConfig Config
        {
            get { return config; }
        }

        void Start()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            State = new GameState(config);

            dayCycle.Init(this);

            dayCycle.BeginDay();
        }

        public CrewDefinition GetCrew(CrewId id)
        {
            if (crews == null) return null;
            for (int i = 0; i < crews.Length; i++)
                if (crews[i] != null && crews[i].id == id) return crews[i];
            return null;
        }

        // Bốc event cho ngày hiện tại, đúng 1 lần rồi cache (QUAY LẠI giữa các trang không bốc lại).
        public DayDefinition GetCurrentEvent()
        {
            if (State.CurrentEvent != null) return State.CurrentEvent;

            DayDefinition picked;
            if (State.CurrentDay == 1) picked = eventPool.openingDay;
            else if (State.CurrentDay == Config.deadlineDay && eventPool.fixedFinale != null) picked = eventPool.fixedFinale;
            else picked = EventScheduler.Pick(eventPool.events, State.UsedEvents, State, State.Rng);

            if (picked == null)
            {
                Debug.LogError("[GameManager] Không bốc được event cho ngày " + State.CurrentDay);
                return null;
            }

            State.CurrentEvent = picked;
            if (!State.UsedEvents.Contains(picked)) State.UsedEvents.Add(picked);
            Debug.Log("[Event] Ngày " + State.CurrentDay + ": " + picked.eventId + " (seed " + State.Seed + ")");
            return picked;
        }

        bool ValidateReferences()
        {
            bool ok = Check(config, "GameConfig")
                      && Check(eventPool, "EventPool")
                      && Check(eventPool == null || eventPool.openingDay != null, "eventPool.openingDay")
                      && Check(crews != null && crews.Length == 4, "crews (cần đúng 4 CrewDefinition)");
            if (eventPool != null)
            {
                ok &= Check(eventPool.events != null && eventPool.events.Count >= 1, "eventPool.events (cần ít nhất 1 event)");
                if (eventPool.events != null)
                    for (int i = 0; i < eventPool.events.Count; i++) ok &= Check(eventPool.events[i], "eventPool.events[" + i + "]");
            }
            if (crews != null)
                for (int i = 0; i < crews.Length; i++) ok &= Check(crews[i], "crews[" + i + "]");

            ok &= Check(endings, "EndingDefinitions")
                 && Check(dayCycle, "DayCycleManager")
                 && Check(screenFlow, "ScreenFlowController")
                 && Check(screenFeedback, "ScreenFeedback")
                 && Check(header, "HeaderBarUI")
                 && Check(briefing, "BriefingScreenUI")
                 && Check(status, "StatusScreenUI")
                 && Check(assignment, "AssignmentScreenUI")
                 && Check(resultOverlay, "ResultOverlayUI")
                 && Check(endScreen, "EndScreenUI")
                 && Check(progressBar, "ProgressBarUI")
                 && Check(cakeStatus, "CakeStatusUI");
            return ok;
        }

        static bool Check(object value, string name)
        {
            // UnityEngine.Object hỏng so sánh == null khác object thường, xử lý cả hai.
            if (value == null || (value is UnityEngine.Object uo && uo == null))
            {
                Debug.LogError("[GameManager] Thiếu tham chiếu bắt buộc: " + name);
                return false;
            }
            return true;
        }
    }
}
