using UnityEngine;

namespace AyeAye
{
    // Bật/tắt trang; chỉ một trang hiển thị tại một thời điểm. Overlay và End screen quản lý riêng.
    public class ScreenFlowController : MonoBehaviour
    {
        public GameObject briefingPanel;
        public GameObject statusPanel;
        public GameObject assignmentPanel;
        public GameObject resultOverlayPanel;
        public GameObject endPanel;

        public void ShowStep(ScreenStep step)
        {
            SetActive(briefingPanel, step == ScreenStep.Briefing);
            SetActive(statusPanel, step == ScreenStep.Status);
            SetActive(assignmentPanel, step == ScreenStep.Assignment);
        }

        public void ShowResultOverlay(bool visible)
        {
            SetActive(resultOverlayPanel, visible);
        }

        public void ShowEndScreen()
        {
            HideAll();
            SetActive(endPanel, true);
        }

        public void HideAll()
        {
            SetActive(briefingPanel, false);
            SetActive(statusPanel, false);
            SetActive(assignmentPanel, false);
            SetActive(resultOverlayPanel, false);
            SetActive(endPanel, false);
        }

        // Dùng khi Restart: đóng tất cả để vòng lặp ngày dựng lại từ đầu.
        public void ResetAll()
        {
            HideAll();
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }
    }
}
