using System.Collections;
using TMPro;
using UnityEngine;
namespace AyeAye
{

    public class TerminalTextEffect : MonoBehaviour
    {

        public TMP_Text target;
        [Tooltip("Dedicated TMP_Text for the █ cursor — blinks while typing, hides when done; leave empty for no cursor.")]

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
            yield return null; 
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
        void PositionCursor(int shown)
        {
            if (cursorText == null) return;
            TMP_CharacterInfo[] chars = target.textInfo.characterInfo;
            if (chars == null || chars.Length == 0) return;
            TMP_CharacterInfo c = chars[Mathf.Clamp(shown, 0, chars.Length - 1)];
            Vector3 anchor = target.transform.TransformPoint(new Vector3(c.origin, c.baseLine, 0f));
            if (cursorText.textInfo != null && cursorText.textInfo.characterCount > 0)
            {
                TMP_CharacterInfo cur = cursorText.textInfo.characterInfo[0];
                anchor -= cursorText.transform.TransformVector(new Vector3(cur.origin, cur.baseLine, 0f));
            }
            cursorText.transform.position = anchor;
        }

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

