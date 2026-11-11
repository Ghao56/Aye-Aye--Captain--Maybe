using TMPro;
using UnityEngine;

namespace AyeAye
{
    // Thanh tiến trình: chỉ đọc progress/totalDistance, không tự tính quãng đường.
    public class ProgressBarUI : MonoBehaviour
    {
        public RectTransform track;
        public RectTransform marker;
        public TMP_Text percentText;

        Coroutine tween;
        float displayedPercent;

        void Awake()
        {
            ResetBar();
        }

        // Hiển thị quãng đường tích lũy (clamp 100%); nếu có thời gian thì nội suy mượt.
        public void AnimateTo(float progress, float totalDistance, float seconds)
        {
            float target = ClampPercent(progress, totalDistance);
            if (tween != null) StopCoroutine(tween);
            if (seconds <= 0f)
            {
                displayedPercent = target;
                ApplyVisuals();
                return;
            }
            float from = displayedPercent;
            tween = StartCoroutine(Tween(from, target, seconds));
        }

        System.Collections.IEnumerator Tween(float from, float to, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                displayedPercent = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
                ApplyVisuals();
                yield return null;
            }
            displayedPercent = to;
            ApplyVisuals();
            tween = null;
        }

        // Restart: về 0 ngay và dứt coroutine cũ.
        public void ResetBar()
        {
            if (tween != null)
            {
                StopCoroutine(tween);
                tween = null;
            }
            displayedPercent = 0f;
            ApplyVisuals();
        }

        void ApplyVisuals()
        {
            if (marker != null)
            {
                float width = track != null ? track.rect.width : 0f;
                marker.anchoredPosition = new Vector2(width * displayedPercent, marker.anchoredPosition.y);
            }
            if (percentText != null)
            {
                percentText.text = Mathf.RoundToInt(displayedPercent * 100f) + "%";
            }
        }

        static float ClampPercent(float progress, float totalDistance)
        {
            if (totalDistance <= 0f) return 1f;
            return Mathf.Clamp01(progress / totalDistance);
        }
    }
}
