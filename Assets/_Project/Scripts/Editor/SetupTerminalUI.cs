using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace AyeAye
{
    // Dựng khung UI Toolkit (PanelSettings + GameObject TerminalUI) — idempotent:
    // chạy lại không ghi đè nội dung có sẵn, không nhân đôi đối tượng (cùng tinh thần Build Demo).
    public static class SetupTerminalUI
    {
        const string UiFolder = "Assets/_Project/UI/Terminal";
        const string UxmlPath = UiFolder + "/TerminalRoot.uxml";
        const string ThemePath = UiFolder + "/RuntimeTheme.tss";
        const string PanelSettingsPath = UiFolder + "/TerminalPanelSettings.asset";
        const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        const string UiObjectName = "TerminalUI";

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

            // Camera nền đen cho letterbox; không đổi thứ khác.
            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam.clearFlags != CameraClearFlags.SolidColor || cam.backgroundColor != Color.black)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                    EditorUtility.SetDirty(cam);
                    Debug.Log("[SetupTerminalUI] Camera " + cam.name + ": Solid Color đen (letterbox).");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupTerminalUI] HOÀN TẤT. Vào Play để kiểm tra khung (GATE B).");
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

            // Constant Pixel Size, scale 1 — sân khấu tự co bằng style.scale trong TerminalView.
            if (panel.scaleMode != PanelScaleMode.ConstantPixelSize) panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            if (!Mathf.Approximately(panel.scale, 1f)) panel.scale = 1f;
            if (panel.sortOrder != 10) panel.sortOrder = 10; // UIDocument vẽ trên Canvas uGUI cũ (đã tắt)

            ThemeStyleSheet theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null)
                Debug.LogError("[SetupTerminalUI] Thiếu theme " + ThemePath + " — kiểm tra file RuntimeTheme.tss.");
            else if (panel.themeStyleSheet != theme) panel.themeStyleSheet = theme;

            EditorUtility.SetDirty(panel);
            return panel;
        }

        static void EnsureSceneObjects(PanelSettings panel, UnityEditor.SceneManagement.Scene scene)
        {
            GameObject go = GameObject.Find(UiObjectName);
            bool created = go == null;
            if (created)
            {
                go = new GameObject(UiObjectName);
                Debug.Log("[SetupTerminalUI] Đã tạo GameObject: " + UiObjectName);
            }

            UIDocument document = go.GetComponent<UIDocument>();
            if (document == null) document = go.AddComponent<UIDocument>();
            if (document.sourceAsset == null)
            {
                document.sourceAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
                Debug.Log("[SetupTerminalUI] UIDocument.sourceAsset = " + UxmlPath);
            }
            if (document.panelSettings != panel) document.panelSettings = panel;

            TerminalView view = go.GetComponent<TerminalView>();
            if (view == null) view = go.AddComponent<TerminalView>();

            if (created || document.sourceAsset != null)
                Debug.Log("[SetupTerminalUI] TerminalUI sẵn sàng trên scene " + scene.name + ".");
        }
    }
}
