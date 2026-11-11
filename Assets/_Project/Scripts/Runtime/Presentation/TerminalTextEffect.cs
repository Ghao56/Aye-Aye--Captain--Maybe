using System.Collections;
using TMPro;
using UnityEngine;

namespace AyeAye
{
    // Hiệu ứng gõ chữ cho TMP_Text, bỏ qua được, xử lý đúng rich text (chỉ đếm ký tự hiển thị).
    public class TerminalTextEffect : MonoBehaviour
    {
        public TMP_Text target;

        [Tooltip("TMP_Text riêng cho con trỏ █, nhấp nháy trong lúc gõ và ẩn khi gõ xong; để trống nếu không muốn con trỏ.")]
        public TMP_Text cursorText;

        Coroutine typing;
        int charsPerSecond = 90;
        bool done = true;

        public void SetSpeed(int cps)
        {
            charsPerSecond = cps > 0 ? cps : 90;
        }

        public bool IsTyping
        {
            get { return !done; }
        }

        public void Play(string fullText)
        {
            Stop();
            if (target == null) return;

            done = false;
            target.text = fullText;
            target.maxVisibleCharacters = 0;
            if (cursorText != null) cursorText.gameObject.SetActive(true);
            typing = StartCoroutine(Type(fullText));
        }

        IEnumerator Type(string fullText)
        {
            yield return null; // chờ TMP dựng mesh để textInfo có số liệu

            if (string.IsNullOrEmpty(fullText))
            {
                Finish();
                yield break;
            }

            int total = target.textInfo.characterCount;
            int shown = 0;
            float elapsed = 0f;
            float blinkTimer = 0f;
            while (shown < total)
            {
                elapsed += Time.deltaTime;
                int goal = Mathf.FloorToInt(elapsed * charsPerSecond);
                // Không dùng rich text → mỗi ký tự trong text đều hiển thị được.
                if (goal > shown)
                {
                    shown = Mathf.Min(goal, total);
                    target.maxVisibleCharacters = shown;
                    PositionCursor(shown);
                }
                blinkTimer += Time.deltaTime;
                if (cursorText != null && blinkTimer >= 0.15f)
                {
                    blinkTimer = 0f;
                    cursorText.enabled = !cursorText.enabled;
                }
                yield return null;
            }
            Finish();
        }

        // Đặt con trỏ █ ngay sau ký tự hiển thị cuối cùng; textInfo cho phép theo cả line-wrap.
        void PositionCursor(int shown)
        {
            if (cursorText == null) return;
            TMP_CharacterInfo[] chars = target.textInfo.characterInfo;
            if (chars == null || chars.Length == 0) return;

            // Con trỏ đứng tại vị trí ký tự TIẾP theo sẽ hiện — đúng chỗ chữ đang chạy tới (theo cả line-wrap).
            TMP_CharacterInfo c = chars[Mathf.Clamp(shown, 0, chars.Length - 1)];
            Vector3 anchor = target.transform.TransformPoint(new Vector3(c.origin, c.baseLine, 0f));

            // Bù gốc của chính con trỏ để glyph █ ngồi đúng lên baseline của ký tự.
            if (cursorText.textInfo != null && cursorText.textInfo.characterCount > 0)
            {
                TMP_CharacterInfo cur = cursorText.textInfo.characterInfo[0];
                anchor -= cursorText.transform.TransformVector(new Vector3(cur.origin, cur.baseLine, 0f));
            }
            cursorText.transform.position = anchor;
        }

        // Hiện toàn bộ văn bản ngay (bấm SKIP hoặc lần CONTINUE đầu).
        public void Complete()
        {
            if (target == null) return;
            Stop();
            target.maxVisibleCharacters = int.MaxValue;
            Finish();
        }

        void Finish()
        {
            done = true;
            if (cursorText != null)
            {
                cursorText.enabled = true;
                cursorText.gameObject.SetActive(false);
            }
            typing = null;
        }

        void Stop()
        {
            if (typing != null)
            {
                StopCoroutine(typing);
                typing = null;
            }
        }

        void OnDisable()
        {
            Stop();
            done = true;
        }
    }
}
