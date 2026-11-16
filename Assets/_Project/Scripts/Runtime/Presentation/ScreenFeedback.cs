using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace AyeAye
{

    public class ScreenFeedback : MonoBehaviour
    {
        [Tooltip("Shake target (RectTransform of the result panel).")]

        public RectTransform shakeTarget;
        [Tooltip("Full-screen Image flashed green on Smart.")]

        public Image flashImage;
        [Tooltip("Full-screen scanline Image (raycast off); toggled by the debug flag.")]

        public Image scanlineImage;
        [Tooltip("Enable/disable the scanline (debug flag).")]

        public bool scanlineEnabled = true;
        [Header("Effect parameters (no magic numbers in code)")]

        public float shakeAmplitudePixels = 6f;

        public float shakeDuration = 0.25f;

        public float flashDuration = 0.3f;
        static readonly Color SlipUpColor = new Color(1f, 0.353f, 0.302f, 0.55f);
        static readonly Color SmartColor = new Color(0.353f, 1f, 0.5f, 0.35f);
        Coroutine running;
        Vector2 shakeBasePosition;
        bool shaking;
        void Awake()
        {
            ApplyScanline();
        }
        void ApplyScanline()
        {
            if (scanlineImage != null) scanlineImage.enabled = scanlineEnabled;
        }

        public void PlayFeedback(DecisionOutcome outcome)
        {
            ResetVisuals();
            if (outcome == DecisionOutcome.Normal) return; 
            if (outcome == DecisionOutcome.SlipUp && shakeTarget != null)
            {
                shakeBasePosition = shakeTarget.anchoredPosition;
                shaking = true;
                running = StartCoroutine(Shake());
            }
            else if (outcome == DecisionOutcome.Smart && flashImage != null)
            {
                running = StartCoroutine(Flash(SmartColor));
            }
        }
        IEnumerator Shake()
        {
            float elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float offset = (elapsed < shakeDuration) ? shakeAmplitudePixels : 0f;
                float dir = Mathf.PingPong(elapsed * 40f, 2f) - 1f;
                shakeTarget.anchoredPosition = shakeBasePosition + new Vector2(dir * offset, 0f);
                yield return null;
            }
            Restore();
        }
        IEnumerator Flash(Color color)
        {
            flashImage.color = color;
            float elapsed = 0f;
            Color baseColor = color;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                Color c = baseColor;
                c.a = baseColor.a * (1f - elapsed / flashDuration);
                flashImage.color = c;
                yield return null;
            }
            Restore();
        }

        public void ResetVisuals()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
            Restore();
        }
        void Restore()
        {
            if (shaking && shakeTarget != null)
            {
                shakeTarget.anchoredPosition = shakeBasePosition;
                shaking = false;
            }
            if (flashImage != null)
            {
                Color c = flashImage.color;
                c.a = 0f;
                flashImage.color = c;
            }
            ApplyScanline();
        }
    }
}

