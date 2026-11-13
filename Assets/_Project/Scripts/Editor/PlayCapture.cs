// Công cụ chụp Play mode để xác minh UI G3 (tạm thời — chủ dự án yêu cầu chụp màn hình Play).
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class PlayCapture
{
    const string Dir = "C:/Users/Admin/AppData/Local/Temp/opencode/aa-shots";
    static int frame;

    public static void Capture()
    {
        Directory.CreateDirectory(Dir);
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    // Build Demo (idempotent) + Validate rồi chụp — dùng cho lượt có regenerate asset.
    public static void BuildValidateCapture()
    {
        AyeAye.DemoBuilder.BuildDemo();
        AyeAye.DemoBuilder.Validate();
        Capture();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        frame++;
        if (frame == 40) ForceMachineOn();
        if (frame == 90) Shot("fix-tab-orders");
        if (frame == 94) InvokeSelectTab(1);
        if (frame == 130) Shot("fix-tab-fleet");
        if (frame >= 134) { EditorApplication.isPlaying = false; EditorApplication.update -= Tick; EditorApplication.Exit(0); }
    }

    static void ForceMachineOn()
    {
        var machine = Object.FindObjectOfType<AyeAye.TerminalView>();
        var m = machine.GetType().GetMethod("FinishIntro", BindingFlags.NonPublic | BindingFlags.Instance);
        m.Invoke(machine, null);
        Debug.Log("[PlayCapture] machine on");
    }

    static void InvokeSelectTab(int i)
    {
        var hud = Object.FindObjectOfType<AyeAye.TerminalHud>();
        var m = hud.GetType().GetMethod("SelectTab", BindingFlags.NonPublic | BindingFlags.Instance);
        m.Invoke(hud, new object[] { i });
        Debug.Log($"[PlayCapture] tab {i}");
    }

    static void Shot(string name)
    {
        var cam = Camera.main;
        int w = 1920, h = 1080;
        var rt = new RenderTexture(w, h, 24);
        var oldTarget = cam.targetTexture;
        var oldActive = RenderTexture.active;
        cam.targetTexture = rt;
        cam.aspect = (float)w / h;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        File.WriteAllBytes($"{Dir}/{name}.png", tex.EncodeToPNG());
        cam.targetTexture = oldTarget;
        RenderTexture.active = oldActive;
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);
        Debug.Log($"[PlayCapture] shot {name}");
    }
}
