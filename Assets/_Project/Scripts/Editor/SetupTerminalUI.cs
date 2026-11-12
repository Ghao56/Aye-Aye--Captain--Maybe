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
        const string MachineName = "Terminal";
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

            // Xoá hẳn Canvas uGUI cũ (cả GameObject) — UI Toolkit đã thay thế toàn bộ UI game
            // (Bước 2), không còn luồng nào dùng uGUI. Bỏ qua đối tượng có UIDocument (panel này).
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.GetComponent<UIDocument>() != null) continue;
                Debug.Log("[SetupTerminalUI] Đã xoá Canvas uGUI cũ (cả GameObject): " + canvas.name);
                Object.DestroyImmediate(canvas.gameObject);
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
            if (!Mathf.Approximately(panel.sortingOrder, 10f)) panel.sortingOrder = 10f; // thứ tự vẽ panel world-space
            // renderMode đang internal ở 6000.3.3f1 → đặt qua SerializedObject
            // (field serial hoá m_RenderMode có sẵn; PanelRenderMode.WorldSpace = 1 theo docs 6000.3).
            SerializedObject so = new SerializedObject(panel);
            SerializedProperty renderMode = so.FindProperty("m_RenderMode");
            if (renderMode != null)
            {
                if (renderMode.intValue != 1)
                {
                    renderMode.intValue = 1; // WorldSpace
                    so.ApplyModifiedProperties();
                    Debug.Log("[SetupTerminalUI] PanelSettings m_RenderMode = WorldSpace.");
                }
            }
            else
            {
                Debug.LogError("[SetupTerminalUI] PanelSettings không có m_RenderMode — bản Unity không hỗ trợ World Space?");
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
            GameObject oldNamed = GameObject.Find(MachineName) != null ? null : GameObject.Find("TerminalMachine");
            if (oldNamed != null)
            {
                Object.DestroyImmediate(oldNamed);
                Debug.Log("[SetupTerminalUI] Đã xoá TerminalMachine (đổi tên thành Terminal).");
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

            // Container world-space: Dynamic — bám nội dung USS 1920x1280 px = 19.2x12.8 unit @ PPU 100,
            // GO scale thì scale cả panel. Ép Fixed theo scale gây mất click (xem TerminalView.FitPanelToMachine).
            if (document.worldSpaceSizeMode != UIDocument.WorldSpaceSizeMode.Dynamic)
                document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Dynamic;

            // Collider (3D, trigger) cho raycast input world-space của PanelInputConfiguration — chỉ giữ 1.
            BoxCollider[] colliders = go.GetComponents<BoxCollider>();
            for (int i = 1; i < colliders.Length; i++)
            {
                Object.DestroyImmediate(colliders[i]);
                Debug.Log("[SetupTerminalUI] Đã xoá BoxCollider trùng trên " + MachineName + ".");
            }
            BoxCollider hit = colliders.Length > 0 ? colliders[0] : go.AddComponent<BoxCollider>();
            Vector3 hitSize = new Vector3(MachineWorldWidth, MachineWorldHeight, 0.5f);
            if (hit.size != hitSize) hit.size = hitSize;
            if (!hit.isTrigger) hit.isTrigger = true;

            // Input world-space: raycast từ camera → collider → sự kiện pointer vào UXML.
            if (go.GetComponent<PanelInputConfiguration>() == null) go.AddComponent<PanelInputConfiguration>();

            EnsurePreviewImage(go);

            TerminalView view = go.GetComponent<TerminalView>();
            if (view == null) view = go.AddComponent<TerminalView>();

            TerminalHud hud = go.GetComponent<TerminalHud>();
            if (hud == null) go.AddComponent<TerminalHud>();

            if (created || document.visualTreeAsset != null)
                Debug.Log("[SetupTerminalUI] " + MachineName + " sẵn sàng trên scene " + scene.name + ".");
        }

        // Lớp xem trước: ảnh máy dính trên object để kéo vị trí trong Scene view.
        // TerminalView tự ẩn nó khi Play (ảnh thật do panel vẽ — tránh chồng đôi).
        const string PreviewTexSrc = "Assets/Image/Terminal.png";
        const string PreviewTexPath = UiFolder + "/TerminalPreview.png";
        const float PreviewPpu = 80f; // 1536/80 = 19.2, 1024/80 = 12.8 → khớp đúng panel world-space

        static void EnsurePreviewImage(GameObject go)
        {
            if (!System.IO.File.Exists(PreviewTexPath))
            {
                System.IO.File.Copy(PreviewTexSrc, PreviewTexPath, false);
                AssetDatabase.ImportAsset(PreviewTexPath);
                Debug.Log("[SetupTerminalUI] Đã copy ảnh xem trước: " + PreviewTexPath);
            }
            TextureImporter importer = AssetImporter.GetAtPath(PreviewTexPath) as TextureImporter;
            if (importer != null && (importer.spriteImportMode != SpriteImportMode.Single || !Mathf.Approximately(importer.spritePixelsPerUnit, PreviewPpu)))
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PreviewPpu;
                importer.SaveAndReimport();
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PreviewTexPath);
            if (sprite == null)
            {
                Debug.LogError("[SetupTerminalUI] Không nạp được sprite xem trước " + PreviewTexPath);
                return;
            }

            Transform existing = go.transform.Find("ImagePreview");
            GameObject preview = existing != null ? existing.gameObject : new GameObject("ImagePreview");
            if (existing == null) preview.transform.SetParent(go.transform, false);
            preview.transform.localPosition = Vector3.zero; // ảnh phải nằm đúng tâm — nơi panel đặt UI
            SpriteRenderer sr = preview.GetComponent<SpriteRenderer>();
            if (sr == null) sr = preview.AddComponent<SpriteRenderer>();
            if (sr.sprite != sprite) sr.sprite = sprite;
        }
    }
}
