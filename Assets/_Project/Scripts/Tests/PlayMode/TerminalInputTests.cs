using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AyeAye;

namespace AyeAye.Tests.PlayMode
{
    // Repro tự động cho lỗi "bấm máy không zoom": mở Main.unity, xếp chuột thật qua Input System
    // bấm vào tâm collider máy, chờ intro xong. Đỏ = click không tới panel (đúng lỗi người dùng gặp).
    public class TerminalInputTests
    {
        [UnityTest]
        public IEnumerator Click_Machine_CompletesIntro()
        {
            SceneManager.LoadScene("Assets/_Project/Scenes/Main.unity");
            yield return null;
            yield return null;

            var view = Object.FindFirstObjectByType<TerminalView>();
            Assert.NotNull(view, "Thiếu TerminalView trên scene Main.");
            var cam = Camera.main;
            Assert.NotNull(cam, "Thiếu Camera.main.");
            var col = view.GetComponentInChildren<Collider>();
            Assert.NotNull(col, "Thiếu collider trên máy.");

            Vector3 screen = cam.WorldToScreenPoint(col.bounds.center);
            Debug.Log($"[Test] Click tại screen={screen}, tâm máy world={col.bounds.center}");

            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                // Nhấn + nhả: mỗi InputSystem.Update() xử lý một sự kiện trong hàng đợi.
                var press = new MouseState { position = new Vector2(screen.x, screen.y) }
                    .WithButton(MouseButton.Left);
                InputSystem.QueueStateEvent(mouse, press);
                InputSystem.Update();

                yield return null;

                var release = new MouseState { position = new Vector2(screen.x, screen.y) };
                InputSystem.QueueStateEvent(mouse, release);
                InputSystem.Update();

                int frame = 0;
                while (frame < 600 && !view.IsMachineOn)
                {
                    yield return null;
                    InputSystem.Update();
                    frame++;
                }
                Assert.IsTrue(view.IsMachineOn,
                    $"Click chuột không làm máy bật sau {frame} frame — PointerDown chưa tới panel.");
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
            }
        }
    }
}
