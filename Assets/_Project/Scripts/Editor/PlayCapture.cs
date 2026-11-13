// Công cụ chụp Play mode để xác minh UI (tạm thời — chủ dự án yêu cầu chụp màn hình Play từ Start đến Ending).
// Chạy: menu hoặc -executeMethod PlayCapture.BuildValidateCapture (batch mode).
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class PlayCapture
{
    const string Dir = "C:/Users/Admin/AppData/Local/Temp/opencode/aa-shots";
    static int frame;

    // Trạng thái ván lấy qua reflection từ TerminalHud — driver tự chơi thay người (G2 báo cáo Play).
    static AyeAye.GameState state;
    static AyeAye.SimWorld world;
    static System.Collections.Generic.List<AyeAye.CrewDefinition> crews;

    public static void Capture()
    {
        Directory.CreateDirectory(Dir);
        // Bật cờ TRƯỚC khi vào Play: domain reload khi vào Play mất subscription update —
        // PlayCaptureBoot ([InitializeOnLoad]) sẽ đăng ký lại Tick sau reload.
        Debug.Log("[PlayCapture] Capture: đặt cờ + vào Play");
        SessionState.SetBool("aa-play-capture", true);
        frame = 0;
        machineOn = false; started = false; endingQueued = false;
        state = null; world = null; crews = null;
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity");
        EditorApplication.isPlaying = true;
    }

    // Build Demo (idempotent) + Validate rồi chụp — dùng cho lượt có regenerate asset.
    public static void BuildValidateCapture()
    {
        AyeAye.DemoBuilder.BuildDemo();
        AyeAye.DemoBuilder.Validate();
        Capture();
    }

    internal static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        frame++;
        if (frame % 300 == 0) Debug.Log($"[PlayCapture] tick {frame} machineOn={machineOn} started={started}"
            + (state != null ? $" t={state.ElapsedSeconds:0}s giao={state.DeliveredCount} trễ={state.LateCount} chờ={state.PendingIncidents.Count} canAdvance={state.CanAdvance}" : ""));
        if (!machineOn) // chờ scene dựng xong rồi bật máy (batchmode: vài chục frame đầu chưa có object)
        {
            var machine = Object.FindObjectOfType<AyeAye.TerminalView>();
            if (machine == null) return;
            machine.GetType().GetMethod("FinishIntro", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(machine, null);
            // UI world-space không vào cam.Render() mặc định → dựng camera ortho riêng trước mặt máy để chụp.
            var preview = machine.GetComponentInChildren<SpriteRenderer>();
            if (preview == null) { Debug.LogError("[PlayCapture] không thấy SpriteRenderer máy"); }
            else
            {
                var b = preview.bounds;
                var camGo = new GameObject("CaptureCam");
                shotCam = camGo.AddComponent<Camera>();
                shotCam.orthographic = true;
                shotCam.clearFlags = CameraClearFlags.SolidColor;
                shotCam.backgroundColor = Color.black;
                shotCam.aspect = 1920f / 1080f;
                shotCam.orthographicSize = Mathf.Max(b.extents.y, b.extents.x / shotCam.aspect);
                shotCam.transform.position = b.center - Vector3.forward * 10f;
                Debug.Log($"[PlayCapture] capture cam bounds={b.center} {b.size}");
            }
            shotRt = new RenderTexture(1920, 1080, 24);
            machineOn = true;
            Debug.Log("[PlayCapture] machine on");
            return;
        }
        // batchmode không bao giờ có window focus → tính năng pause-mất-focus (G2) sẽ dừng đồng hồ vĩnh viễn.
        // Driver gỡ pause mỗi tick (đúng hành vi game trên máy có focus thật).
        if (hudRef != null && focusPaused != null) focusPaused.SetValue(hudRef, false);
        if (pendingShot != null && ++shotFrames >= 4)
        {
            SaveShot(pendingShot);
            pendingShot = null;
            if (endingQueued)
            {
                Debug.Log($"[PlayCapture] Ending {state.FinalEnding} — giao {state.DeliveredCount}, trễ {state.LateCount}, doanh thu {state.Revenue}, {state.ElapsedSeconds:0}s");
                Stop(0);
                return;
            }
        }
        if (!started) // chụp màn BẮT ĐẦU rồi mới bấm BẮT ĐẦU khi HUD đã bind (state == null → còn ở màn BẮT ĐẦU)
        {
            var hud = Object.FindObjectOfType<AyeAye.TerminalHud>();
            if (hud == null) return;
            if (!startShotTaken) { startShotTaken = true; Shot("g2-start"); return; } // chờ pendingShot xong (4 tick)
            if (pendingShot != null) return;
            hud.GetType().GetMethod("StartNewRun", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hud, null);
            hudRef = hud;
            focusPaused = typeof(AyeAye.TerminalHud).GetField("focusPaused", BindingFlags.NonPublic | BindingFlags.Instance);
            Time.timeScale = 10f; // chỉ dùng khi chụp: rút ngắn thời gian chạy, không đổi cấu hình cân bằng
            started = true;
            startFrame = frame;
            Debug.Log("[PlayCapture] BẮT ĐẦU");
            return;
        }
        int f = frame - startFrame;
        if (f == 30) { InvokeSelectTab(0); Shot("g2-tab-orders"); } // chụp lưu ~4 tick sau — tab vẫn 0
        if (f == 40) InvokeSelectTab(1);
        if (f == 80) Shot("g2-tab-fleet"); // lưu khi vẫn ở tab ĐỘI TÀU
        if (f == 90) InvokeSelectTab(2);
        if (f == 110) Shot("g2-tab-incidents"); // lưu khi đã ở tab SỰ CỐ
        if (f >= 110)
        {
            Drive(); // thay người chơi: nhận đơn / điều tàu / xử lý sự cố
            if (state != null && state.IsFinished)
            {
                if (!endingQueued) { endingQueued = true; Shot("g2-ending"); } // chụp xong mới Stop (xem pendingShot)
            }
            else if (state != null && state.ElapsedSeconds > 1500) { Debug.LogError($"[PlayCapture] {state.ElapsedSeconds:0}s game chưa tới Ending"); Stop(1); }
            else if (frame > 2000000) { Debug.LogError("[PlayCapture] quá 2000000 frame chưa tới Ending"); Stop(1); }
        }
    }

    static bool machineOn, started;
    static int startFrame;
    static RenderTexture shotRt;
    static string pendingShot;
    static int shotFrames;
    static Camera shotCam;
    static bool endingQueued;
    static bool startShotTaken;
    static bool incidentShotTaken;
    static AyeAye.TerminalHud hudRef;
    static FieldInfo focusPaused;

    static void Stop(int code)
    {
        SessionState.SetBool("aa-play-capture", false);
        if (shotRt != null)
        {
            shotRt.Release();
            shotRt = null;
        }
        Time.timeScale = 1f;
        if (shotCam != null) Object.Destroy(shotCam.gameObject);
        EditorApplication.isPlaying = false;
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }

    static void InvokeSelectTab(int i)
    {
        var hud = Object.FindObjectOfType<AyeAye.TerminalHud>();
        var m = hud.GetType().GetMethod("SelectTab", BindingFlags.NonPublic | BindingFlags.Instance);
        m.Invoke(hud, new object[] { i });
        Debug.Log($"[PlayCapture] tab {i}");
    }

    // Driver tự chơi: xử lý sự cố → nhận đơn (chỉ đơn về kịp hạn) → điều tàu theo tuyến nhanh nhất.
    // Cùng nhịp với chính sách "đọc manh mối" của BalanceProbe — không đụng UI.
    static void Drive()
    {
        var hud = Object.FindObjectOfType<AyeAye.TerminalHud>();
        if (hud == null) return;
        if (state == null)
        {
            var f = typeof(AyeAye.TerminalHud).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
            state = (AyeAye.GameState)f.GetValue(hud);
            if (state == null) return;
            var pool = AyeAye.DemoData.BuildOrderPool();
            var incidents = AyeAye.DemoData.BuildIncidentPool();
            world = new AyeAye.SimWorld
            {
                Pool = pool, Routes = AyeAye.DemoData.BuildRouteTable(),
                Incidents = incidents.incidents,
                FixedFirstDay = incidents.fixedFirstDay, FixedFinale = incidents.fixedFinale,
            };
            crews = AyeAye.DemoData.BuildCrews();
        }

        // 1. Sự cố chờ → chụp 1 lần tab SỰ CỐ có sự cố thật, rồi chọn phương án đầu (thay nút tab SỰ CỐ).
        if (state.PendingIncidents.Count > 0)
        {
            if (!incidentShotTaken && pendingShot == null)
            {
                incidentShotTaken = true;
                InvokeSelectTab(2);
                Shot("g2-incident-active"); // hold ~5 tick — đồng hồ dừng đúng khi có sự cố chờ
                return;
            }
            if (pendingShot != null) return;
            var p = state.PendingIncidents[0];
            var resolved = AyeAye.IncidentResolver.Resolve(p.Incident, p.Incident.options[0], state, p.Context.Ship, crews);
            AyeAye.IncidentResolver.Apply(resolved, state, p.Context.Ship);
            AyeAye.Simulation.ApplyProgressDelta(state, p.Context.Ship, resolved.ProgressDelta);
            state.PendingIncidents.RemoveAt(0);
            return;
        }

        // 2. Nhận đơn còn về kịp hạn (tàu rảnh: tới cảng lấy hàng + đi tuyến nhanh nhất ≤ thời gian còn lại).
        foreach (var o in state.Orders.FindAll(x => x.Status == AyeAye.OrderStatus.InInbox))
        {
            float haul = FastestSeconds(o.FromPortId, o.ToPortId);
            if (haul == float.MaxValue) continue;
            bool reachable = false;
            foreach (var ship in state.Ships)
            {
                if (ship.Status != AyeAye.ShipStatus.Idle || !string.IsNullOrEmpty(ship.OrderId)) continue;
                if (FastestSeconds(ship.PortId, o.FromPortId) + haul <= o.DeadlineAtSeconds - state.ElapsedSeconds)
                { reachable = true; break; }
            }
            if (reachable && AyeAye.CommandValidator.CanAccept(state, o.Id))
                AyeAye.OrderActions.Accept(state, o.Id);
        }

        // 3. Điều tàu: đơn đã nhận chưa gán → tàu đứng ở cảng đi, tuyến nhanh nhất (MAKE IT SO áp ngay).
        foreach (var o in state.Orders.FindAll(x => x.Status == AyeAye.OrderStatus.Accepted && string.IsNullOrEmpty(x.AssignedShipId)))
        {
            var options = world.Routes.GetOptions(o.FromPortId, o.ToPortId);
            if (options == null) continue;
            AyeAye.ShipState best = null;
            int bestIdx = -1;
            float bestLen = float.MaxValue;
            foreach (var ship in state.Ships)
            {
                if (ship.Status != AyeAye.ShipStatus.Idle || !string.IsNullOrEmpty(ship.OrderId)) continue;
                for (int i = 0; i < options.Count; i++)
                    if (options[i].length < bestLen && AyeAye.CommandValidator.ValidateAssign(state, world.Routes, ship.Id, o.Id, i) == null)
                    { best = ship; bestIdx = i; bestLen = options[i].length; }
            }
            if (best != null) AyeAye.FleetActions.Assign(state, world.Routes, best.Id, o.Id, bestIdx);
        }

        // 4. Đọc manh mối: tàu rỗi chạy về cảng lấy hàng của đơn đã nhận chưa có tàu (như chính sách BalanceProbe).
        var demand = state.Orders.Find(o => o.Status == AyeAye.OrderStatus.Accepted && string.IsNullOrEmpty(o.AssignedShipId));
        if (demand == null) return;
        foreach (var ship in state.Ships)
        {
            if (ship.Status != AyeAye.ShipStatus.Idle || !string.IsNullOrEmpty(ship.OrderId)) continue;
            if (ship.PortId == demand.FromPortId) continue;
            var options = world.Routes.GetOptions(ship.PortId, demand.FromPortId);
            if (options == null || options.Count == 0) continue;
            int best = 0;
            for (int i = 1; i < options.Count; i++)
                if (options[i].length < options[best].length) best = i;
            AyeAye.FleetActions.Reposition(state, world.Routes, ship.Id, demand.FromPortId, best);
        }
    }

    static float FastestSeconds(string from, string to)
    {
        var options = world.Routes.GetOptions(from, to);
        float bestLen = float.MaxValue;
        if (options != null)
            foreach (var opt in options)
                if (opt.length < bestLen) bestLen = opt.length;
        return bestLen == float.MaxValue ? float.MaxValue
            : bestLen / Mathf.Max(0.0001f, state.Config.SpeedUnitsPerSecond(AyeAye.SpeedClass.Fast));
    }

    static void Shot(string name)
    {
        pendingShot = name; // chờ 4 frame để panel kịp repaint vào targetTexture
        shotFrames = 0;
    }

    static void SaveShot(string name)
    {
        if (shotCam == null || shotRt == null) { Debug.LogError("[PlayCapture] không có camera/RT để chụp"); return; }
        var old = shotCam.targetTexture;
        shotCam.targetTexture = shotRt;
        shotCam.Render();
        shotCam.targetTexture = old;
        RenderTexture.active = shotRt;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        tex.Apply();
        File.WriteAllBytes($"{Dir}/{name}.png", tex.EncodeToPNG());
        RenderTexture.active = null;
        Object.DestroyImmediate(tex);
        Debug.Log($"[PlayCapture] shot {name}");
    }
}

// Đăng ký lại Tick sau domain reload của Play mode (xem PlayCapture.Capture).
[InitializeOnLoad]
public static class PlayCaptureBoot
{
    static PlayCaptureBoot()
    {
        if (SessionState.GetBool("aa-play-capture", false))
        {
            Debug.Log("[PlayCapture] boot: đăng ký Tick");
            EditorApplication.update += PlayCapture.Tick;
        }
    }
}
