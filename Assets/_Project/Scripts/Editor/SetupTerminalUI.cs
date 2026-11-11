using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace AyeAye
{
    // Dựng khung UI Toolkit world-space (PanelSettings WorldSpace + GameObject TerminalMachine) — idempotent:
    // chạy lại không ghi đè nội dung có sẵn, không nhân đôi đối tượng (cùng tinh thần Build Demo).
    public static class SetupTerminalUI
    {
        const string UiFolder = "Assets/_Project/UI/Terminal";
        const string UxmlPath = UiFolder + "/TerminalRoot.uxml";
        const string ThemePath = UiFolder + "/RuntimeTheme.tss";
        const string PanelSettingsPath = UiFolder + "/TerminalPanelSettings.asset";
        const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        const string MachineName = "TerminalMachine";
        const string LegacyUiObjectName = "TerminalUI";
        // Phải khớp MachineWorldWidth/Height trong TerminalView: 19.2x12.8 unit = panel 1920x1280 px @ PPU 100.
        const float MachineWorldWidth = 19.2f;
        const float MachineWorldHeight = 12.8f;

        [MenuItem("Tools/AyeAye/Setup Terminal UI")]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder(UiFolder))
            {
                Debug.LogError("[SetupTerminalUI] Thiếu thư mục " + UiFolder + " (UXML/USS/theme chưa import?).");
                return;
            }

            PanelSettings panel = EnsurePanelSettings();
            if (panel == null) return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EnsureSceneObjects(panel, scene);

            // Tắt Canvas uGUI cũ (CHỈ component Canvas, không tắt GameObject:
            // các MonoBehaviour/coroutine của UI cũ vẫn chạy — Core không bị ảnh hưởng, R1).
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.enabled)
                {
                    canvas.enabled = false;
                    EditorUtility.SetDirty(canvas);
                    Debug.Log("[SetupTerminalUI] Đã tắt Canvas uGUI (component): " + canvas.name);
                }
            }

            // Camera nền đen, ortho (world-space 2D); vị trí/zoom TerminalView tự lo lúc Play.
            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam.clearFlags != CameraClearFlags.SolidColor || cam.backgroundColor != Color.black)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                    EditorUtility.SetDirty(cam);
                    Debug.Log("[SetupTerminalUI] Camera " + cam.name + ": Solid Color đen.");
                }
                if (!cam.orthographic)
                {
                    cam.orthographic = true;
                    EditorUtility.SetDirty(cam);
                    Debug.Log("[SetupTerminalUI] Camera " + cam.name + ": Orthographic.");
                }
                if (cam.transform.position.z > -1f)
                {
                    Vector3 p = cam.transform.position;
                    cam.transform.position = new Vector3(p.x, p.y, -10f); // lùi ra sau để máy (z=0) nằm trong near clip
                    EditorUtility.SetDirty(cam);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupTerminalUI] HOÀN TẤT. Kéo TerminalMachine tới vị trí bạn muốn rồi vào Play, bấm vào máy để zoom.");
        }

        static PanelSettings EnsurePanelSettings()
        {
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
                Debug.Log("[SetupTerminalUI] Đã tạo PanelSettings: " + PanelSettingsPath);
            }

            // Constant Pixel Size, scale 1 — sân khấu 1920x1280 px cố định trong USS.
            if (panel.scaleMode != PanelScaleMode.ConstantPixelSize) panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            if (!Mathf.Approximately(panel.scale, 1f)) panel.scale = 1f;
            if (!Mathf.Approximately(panel.sortingOrder, 10f)) panel.sortingOrder = 10f; // UIDocument vẽ trên Canvas uGUI cũ (đã tắt)
            if (panel.renderMode != PanelRenderMode.WorldSpace)
            {
                panel.renderMode = PanelRenderMode.WorldSpace; // kiến trúc B (duyệt 11/11/2026)
                Debug.Log("[SetupTerminalUI] PanelSettings.renderMode = WorldSpace.");
            }

            ThemeStyleSheet theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null)
                Debug.LogError("[SetupTerminalUI] Thiếu theme " + ThemePath + " — kiểm tra file RuntimeTheme.tss.");
            else if (panel.themeStyleSheet != theme) panel.themeStyleSheet = theme;

            EditorUtility.SetDirty(panel);
            return panel;
        }

        static void EnsureSceneObjects(PanelSettings panel, UnityEngine.SceneManagement.Scene scene)
        {
            // Xoá object kiến trúc A cũ (màn hình 2D) — bản cũ vẫn trong lịch sử git.
            GameObject legacy = GameObject.Find(LegacyUiObjectName);
            if (legacy != null)
            {
                Object.DestroyImmediate(legacy);
                Debug.Log("[SetupTerminalUI] Đã xoá " + LegacyUiObjectName + " (kiến trúc A).");
            }

            GameObject go = GameObject.Find(MachineName);
            bool created = go == null;
            if (created)
            {
                go = new GameObject(MachineName);
                Debug.Log("[SetupTerminalUI] Đã tạo GameObject: " + MachineName + " (kéo đi đâu cũng được).");
            }

            UIDocument document = go.GetComponent<UIDocument>();
            if (document == null) document = go.AddComponent<UIDocument>();
            if (document.visualTreeAsset == null)
            {
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
                Debug.Log("[SetupTerminalUI] UIDocument.visualTreeAsset = " + UxmlPath);
            }
            if (document.panelSettings != panel) document.panelSettings = panel;

            // Container world-space: 19.2x12.8 unit, pivot giữa trùng transform của object.
            if (document.worldSpaceSizeMode != UIDocument.WorldSpaceSizeMode.Fixed)
                document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            if (document.worldSpaceSize != new Vector2(MachineWorldWidth, MachineWorldHeight))
                document.worldSpaceSize = new Vector2(MachineWorldWidth, MachineWorldHeight);

            // Collider (3D, trigger) cho raycast input world-space của PanelInputConfiguration.
            BoxCollider hit = go.GetComponent<BoxCollider>();
            if (hit == null) hit = go.AddComponent<BoxCollider>();
            Vector3 hitSize = new Vector3(MachineWorldWidth, MachineWorldHeight, 0.5f);
            if (hit.size != hitSize) hit.size = hitSize;
            if (!hit.isTrigger) hit.isTrigger = true;

            // Input world-space: raycast từ camera → collider → sự kiện pointer vào UXML.
            if (go.GetComponent<PanelInputConfiguration>() == null) go.AddComponent<PanelInputConfiguration>();

            TerminalView view = go.GetComponent<TerminalView>();
            if (view == null) view = go.AddComponent<TerminalView>();

            if (created || document.visualTreeAsset != null)
                Debug.Log("[SetupTerminalUI] " + MachineName + " sẵn sàng trên scene " + scene.name + ".");
        }
    }
}
