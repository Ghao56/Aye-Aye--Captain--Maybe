using UnityEngine;

namespace AyeAye
{
    // Điểm khởi động: giữ dữ liệu, tạo GameState, kết nối manager và UI. Không chứa logic thắng/thua.
    public class GameManager : MonoBehaviour
    {
        [Header("Data")]
        public GameConfig config;
        public DayDefinition[] days = new DayDefinition[0];
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

        public DayDefinition GetDay(int dayNumber)
        {
            if (days == null) return null;
            for (int i = 0; i < days.Length; i++)
                if (days[i] != null && days[i].dayNumber == dayNumber) return days[i];
            return null;
        }

        bool ValidateReferences()
        {
            bool ok = Check(config, "GameConfig")
                      && Check(days != null && days.Length == 3, "days (cần đúng 3 DayDefinition)")
                      && Check(crews != null && crews.Length == 4, "crews (cần đúng 4 CrewDefinition)");
            if (days != null)
                for (int i = 0; i < days.Length; i++) ok &= Check(days[i], "days[" + i + "]");
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
