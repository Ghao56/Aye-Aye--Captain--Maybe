using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace AyeAye
{
    // Tạo toàn bộ demo bằng một lần chạy menu: asset dữ liệu, scene Main, build settings.
    public static class DemoBuilder
    {
        const string ProjectRoot = "Assets/_Project";
        const string FontsFolder = ProjectRoot + "/Fonts";
        const string FontSearchFolder = FontsFolder;
        const string TmpSettingsPath = FontsFolder + "/Resources/TMP Settings.asset";
        const string ConfigPath = ProjectRoot + "/Data/Config/GameConfig.asset";
        const string EndingsPath = ProjectRoot + "/Data/Endings/Endings.asset";
        const string ScenePath = ProjectRoot + "/Scenes/Main.unity";

        static readonly string[] DataFolders =
        {
            ProjectRoot, FontsFolder, ProjectRoot + "/Data", ProjectRoot + "/Data/Config",
            ProjectRoot + "/Data/Crew", ProjectRoot + "/Data/Decisions", ProjectRoot + "/Data/Days",
            ProjectRoot + "/Data/Endings", ProjectRoot + "/Scenes", ProjectRoot + "/Scripts",
            ProjectRoot + "/Scripts/Runtime", ProjectRoot + "/Scripts/Runtime/Data",
            ProjectRoot + "/Scripts/Runtime/Logic", ProjectRoot + "/Scripts/Runtime/Core",
            ProjectRoot + "/Scripts/Runtime/UI", ProjectRoot + "/Scripts/Runtime/Presentation",
            ProjectRoot + "/Scripts/Editor", ProjectRoot + "/Scripts/Tests",
            ProjectRoot + "/Scripts/Tests/EditMode", FontsFolder + "/Resources",
        };

        static readonly Color BackgroundColor = new Color(0.043f, 0.059f, 0.055f, 1f);   // #0B0F0E
        static readonly Color FrameColor = new Color(0.18f, 0.42f, 0.28f, 1f);           // viền xanh lục
        static readonly Color PanelBgColor = new Color(0.05f, 0.075f, 0.065f, 1f);
        static readonly Color GreenColor = new Color(0.608f, 0.910f, 0.643f, 1f);       // #9BE8A4
        static readonly Color AmberColor = new Color(1f, 0.722f, 0.302f, 1f);           // #FFB84D
        static readonly Color RedColor = new Color(1f, 0.353f, 0.302f, 1f);             // #FF5A4D
        static readonly Color ButtonBgColor = new Color(0.059f, 0.125f, 0.094f, 1f);
        static readonly Color TrackColor = new Color(0.10f, 0.23f, 0.16f, 1f);

        [MenuItem("Tools/AyeAye/Build Demo")]
        public static void BuildDemo()
        {
            EnsureFolders();

            // TMP shaders nằm trong TMP Essential Resources (không có trong package source);
            // Shader.Find trả về null trong batchmode nếu chưa import.
            EnsureTmpEssentials();

            // TMP_Settings singleton phải tồn tại trước khi CreateFontAsset đọc nó.
            EnsureTmpSettings(null);
            TMP_FontAsset font = EnsureFontAsset();
            EnsureTmpSettings(font);

            GameConfig config = CreateConfig();
            CrewDefinition[] crews = CreateCrews();
            DayDefinition[] days = CreateDays();
            EndingDefinitions endings = CreateEndings();
            AssetDatabase.SaveAssets();

            // Nạp lại để tham chiếu scene chắc chắn trỏ tới asset trên đĩa.
            config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            endings = AssetDatabase.LoadAssetAtPath<EndingDefinitions>(EndingsPath);
            for (int i = 0; i < crews.Length; i++)
                crews[i] = AssetDatabase.LoadAssetAtPath<CrewDefinition>(CrewPath(i + 1));
            for (int i = 0; i < days.Length; i++)
                days[i] = AssetDatabase.LoadAssetAtPath<DayDefinition>(DayPath(i + 1));

            BuildScene(config, crews, days, endings, font);

            AssetDatabase.Refresh();
            Debug.Log("[DemoBuilder] HOÀN TẤT. Scene: " + ScenePath
                      + " | Font: " + (font != null ? AssetDatabase.GetAssetPath(font) : "CHƯA CÓ (bỏ font .ttf vào " + FontsFolder + " rồi chạy lại menu)"));
        }

        [MenuItem("Tools/AyeAye/Validate Demo")]
        public static void ValidateDemo()
        {
            int issues = 0;

            // 1) Dữ liệu.
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null) { Debug.LogError("[Validate] Thiếu GameConfig tại " + ConfigPath); issues++; }
            else if (Mathf.Abs(config.totalDistance - 3.2f) > 0.0001f || config.deadlineDay != 3)
            { Debug.LogError("[Validate] GameConfig sai giá trị chuẩn (3.2 / deadline 3)."); issues++; }

            CrewDefinition[] crews = new CrewDefinition[4];
            for (int i = 0; i < 4; i++)
            {
                crews[i] = AssetDatabase.LoadAssetAtPath<CrewDefinition>(CrewPath(i + 1));
                if (crews[i] == null) { Debug.LogError("[Validate] Thiếu Crew asset #" + (i + 1)); issues++; }
            }

            DayDefinition[] days = new DayDefinition[3];
            for (int i = 0; i < 3; i++)
            {
                days[i] = AssetDatabase.LoadAssetAtPath<DayDefinition>(DayPath(i + 1));
                if (days[i] == null) { Debug.LogError("[Validate] Thiếu Day asset #" + (i + 1)); issues++; continue; }
                if (days[i].decision == null) { Debug.LogError("[Validate] Day " + days[i].dayNumber + " thiếu decision."); issues++; continue; }
                if (days[i].decision.options == null || days[i].decision.options.Count != 3)
                { Debug.LogError("[Validate] Day " + days[i].dayNumber + " phải có đúng 3 phương án."); issues++; continue; }

                foreach (DecisionOption option in days[i].decision.options)
                {
                    if (option == null || option.entries == null)
                    { Debug.LogError("[Validate] Day " + days[i].dayNumber + " có option rỗng."); issues++; continue; }
                    foreach (CrewOutcomeEntry entry in option.entries)
                    {
                        if (entry == null) { issues++; Debug.LogError("[Validate] Day " + days[i].dayNumber + " có entry null."); continue; }
                        if (string.IsNullOrEmpty(entry.explanation) || string.IsNullOrEmpty(entry.quote))
                        { issues++; Debug.LogError("[Validate] Day " + days[i].dayNumber + " entry thiếu explanation/quote."); }
                        if (entry.cargoDelta != 0 && entry.cargoDelta != -1)
                        { issues++; Debug.LogError("[Validate] Day " + days[i].dayNumber + " cargoDelta chỉ cho phép 0 hoặc -1."); }
                    }
                }
            }

            if (AssetDatabase.LoadAssetAtPath<EndingDefinitions>(EndingsPath) == null)
            { Debug.LogError("[Validate] Thiếu Endings asset."); issues++; }

            // 2) Scene + tham chiếu.
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameManager game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (game == null) { Debug.LogError("[Validate] Scene không có GameManager."); issues++; }
            else
            {
                issues += CheckRef(game.config, "GameManager.config");
                issues += CheckRef(game.endings, "GameManager.endings");
                issues += CheckRef(game.dayCycle, "GameManager.dayCycle");
                issues += CheckRef(game.screenFlow, "GameManager.screenFlow");
                issues += CheckRef(game.screenFeedback, "GameManager.screenFeedback");
                issues += CheckRef(game.header, "GameManager.header");
                issues += CheckRef(game.briefing, "GameManager.briefing");
                issues += CheckRef(game.status, "GameManager.status");
                issues += CheckRef(game.assignment, "GameManager.assignment");
                issues += CheckRef(game.resultOverlay, "GameManager.resultOverlay");
                issues += CheckRef(game.endScreen, "GameManager.endScreen");
                issues += CheckRef(game.progressBar, "GameManager.progressBar");
                issues += CheckRef(game.cakeStatus, "GameManager.cakeStatus");
                if (game.days == null || game.days.Length != 3) { Debug.LogError("[Validate] GameManager.days phải có 3 phần tử."); issues++; }
                else for (int i = 0; i < 3; i++) issues += CheckRef(game.days[i], "GameManager.days[" + i + "]");
                if (game.crews == null || game.crews.Length != 4) { Debug.LogError("[Validate] GameManager.crews phải có 4 phần tử."); issues++; }
                else for (int i = 0; i < 4; i++) issues += CheckRef(game.crews[i], "GameManager.crews[" + i + "]");

                if (game.briefing != null)
                {
                    issues += CheckRef(game.briefing.titleText, "briefing.titleText");
                    issues += CheckRef(game.briefing.bodyText, "briefing.bodyText");
                    issues += CheckRef(game.briefing.typewriter, "briefing.typewriter");
                    issues += CheckRef(game.briefing.continueButton, "briefing.continueButton");
                }

                if (game.status != null)
                {
                    issues += CheckRef(game.status.statusText, "status.statusText");
                    issues += CheckRef(game.status.warningText, "status.warningText");
                    issues += CheckRef(game.status.flagsText, "status.flagsText");
                    issues += CheckRef(game.status.continueButton, "status.continueButton");
                    issues += CheckRef(game.status.backButton, "status.backButton");
                    if (game.status.crewCards == null || game.status.crewCards.Length != 4)
                    { Debug.LogError("[Validate] status.crewCards phải có 4 phần tử."); issues++; }
                    else for (int i = 0; i < 4; i++) issues += CheckRef(game.status.crewCards[i], "status.crewCards[" + i + "]");
                }

                if (game.assignment != null)
                {
                    issues += CheckRef(game.assignment.promptText, "assignment.promptText");
                    issues += CheckRef(game.assignment.previewText, "assignment.previewText");
                    issues += CheckRef(game.assignment.confirmButton, "assignment.confirmButton");
                    issues += CheckRef(game.assignment.endDayButton, "assignment.endDayButton");
                    issues += CheckRef(game.assignment.backButton, "assignment.backButton");
                    issues += CheckRef(game.assignment.editButton, "assignment.editButton");
                    if (game.assignment.optionButtons == null || game.assignment.optionButtons.Length != 3)
                    { Debug.LogError("[Validate] assignment.optionButtons phải có 3 phần tử."); issues++; }
                    else for (int i = 0; i < 3; i++) issues += CheckRef(game.assignment.optionButtons[i], "assignment.optionButtons[" + i + "]");
                    if (game.assignment.crewButtons == null || game.assignment.crewButtons.Length != 4)
                    { Debug.LogError("[Validate] assignment.crewButtons phải có 4 phần tử."); issues++; }
                    else for (int i = 0; i < 4; i++) issues += CheckRef(game.assignment.crewButtons[i], "assignment.crewButtons[" + i + "]");
                }

                if (game.resultOverlay != null)
                {
                    issues += CheckRef(game.resultOverlay.resultLabel, "resultOverlay.resultLabel");
                    issues += CheckRef(game.resultOverlay.explanationText, "resultOverlay.explanationText");
                    issues += CheckRef(game.resultOverlay.quoteText, "resultOverlay.quoteText");
                    issues += CheckRef(game.resultOverlay.distanceText, "resultOverlay.distanceText");
                    issues += CheckRef(game.resultOverlay.nextButton, "resultOverlay.nextButton");
                }

                if (game.endScreen != null)
                {
                    issues += CheckRef(game.endScreen.titleText, "endScreen.titleText");
                    issues += CheckRef(game.endScreen.bodyText, "endScreen.bodyText");
                    issues += CheckRef(game.endScreen.statsText, "endScreen.statsText");
                    issues += CheckRef(game.endScreen.restartButton, "endScreen.restartButton");
                }

                if (game.progressBar != null)
                {
                    issues += CheckRef(game.progressBar.track, "progressBar.track");
                    issues += CheckRef(game.progressBar.marker, "progressBar.marker");
                    issues += CheckRef(game.progressBar.percentText, "progressBar.percentText");
                }

                if (game.header != null)
                {
                    issues += CheckRef(game.header.tripText, "header.tripText");
                    issues += CheckRef(game.header.shipText, "header.shipText");
                    issues += CheckRef(game.header.dayText, "header.dayText");
                    issues += CheckRef(game.header.systemText, "header.systemText");
                    issues += CheckRef(game.header.lampText, "header.lampText");
                }

                if (game.cakeStatus != null)
                {
                    issues += CheckRef(game.cakeStatus.cakeText, "cakeStatus.cakeText");
                }

                CanvasScaler scaler = UnityEngine.Object.FindFirstObjectByType<CanvasScaler>();
                if (scaler == null) { Debug.LogError("[Validate] Canvas thiếu CanvasScaler."); issues++; }
                else if (scaler.referenceResolution != new Vector2(1920f, 1080f) || Mathf.Abs(scaler.matchWidthOrHeight - 0.5f) > 0.001f)
                { Debug.LogError("[Validate] CanvasScaler sai cấu hình (1920x1080, match 0.5)."); issues++; }
            }

            if (issues == 0) Debug.Log("[Validate] OK: dữ liệu, scene, tham chiếu đều hợp lệ.");
        }

        static int CheckRef(object value, string name)
        {
            if (value == null || (value is UnityEngine.Object uo && uo == null))
            {
                Debug.LogError("[Validate] Tham chiếu null: " + name);
                return 1;
            }
            return 0;
        }

        // ---------------------------------------------------------------- assets

        static void EnsureFolders()
        {
            foreach (string folder in DataFolders)
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/') ?? "Assets";
                string leaf = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        static string CrewPath(int index)
        {
            string[] names = { "Crew_Bob", "Crew_Mira", "Crew_Chen", "Crew_Dex" };
            return ProjectRoot + "/Data/Crew/" + names[index - 1] + ".asset";
        }

        static string DecisionPath(int day)
        {
            return ProjectRoot + "/Data/Decisions/Day" + day + "_Decision.asset";
        }

        static string DayPath(int day)
        {
            return ProjectRoot + "/Data/Days/Day" + day + "_Day.asset";
        }

        static TMP_FontAsset EnsureFontAsset()
        {
            Font sourceFont = PickSourceFont();
            if (sourceFont == null)
            {
                Debug.LogError("[DemoBuilder] Không có font trong " + FontsFolder + " và không copy được font hệ thống. "
                               + "Bỏ một file .ttf/.otf hỗ trợ tiếng Việt (ví dụ Roboto Mono) vào thư mục đó rồi chạy lại Tools/AyeAye/Build Demo.");
                return null;
            }

            // Tên asset gắn với font nguồn; nếu người dùng bỏ font mới vào thì tạo lại asset cho font đó.
            string assetName = sourceFont.name + " SDF";
            TMP_FontAsset existing = FindExistingFontAsset(assetName);
            if (existing != null) return existing;

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (fontAsset == null)
            {
                Debug.LogError("[DemoBuilder] Tạo TMP_FontAsset thất bại từ font " + sourceFont.name);
                return null;
            }

            string path = FontsFolder + "/" + sourceFont.name + " SDF.asset";
            AssetDatabase.CreateAsset(fontAsset, path);
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            {
                fontAsset.atlasTextures[0].name = sourceFont.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = sourceFont.name + " Atlas Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[DemoBuilder] Đã tạo font asset: " + path + " (nguồn: " + AssetDatabase.GetAssetPath(sourceFont) + "). "
                      + "Nếu thiếu glyph tiếng Việt, hãy thay font bằng font hỗ trợ tiếng Việt rồi chạy lại.");
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }

        static TMP_FontAsset FindExistingFontAsset(string assetName)
        {
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontsFolder });
            foreach (string guid in guids)
            {
                TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && asset.name == assetName) return asset;
            }
            return null;
        }

        // Font do người dùng bỏ vào được ưu tiên; file do builder tự copy (fallback hệ thống) chỉ dùng khi thiếu.
        static readonly string[] SystemFallbackBaseNames = { "consola", "arial", "segoeui", "tahoma", "times" };

        static Font PickSourceFont()
        {
            string[] guids = AssetDatabase.FindAssets("t:Font", new[] { FontSearchFolder });
            Font fallbackHit = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font == null) continue;
                string baseName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (System.Array.IndexOf(SystemFallbackBaseNames, baseName) < 0) return font;
                if (fallbackHit == null) fallbackHit = font;
            }
            return fallbackHit != null ? fallbackHit : CopySystemFontFallback();
        }

        // Nếu người dùng chưa bỏ font, dùng font hệ thống có dấu tiếng Việt để demo vẫn chạy.
        static Font CopySystemFontFallback()
        {
            string[] candidates = { "consola.ttf", "arial.ttf", "segoeui.ttf", "tahoma.ttf" };
            foreach (string candidate in candidates)
            {
                string systemPath = @"C:\Windows\Fonts\" + candidate;
                if (!File.Exists(systemPath)) continue;
                string destPath = FontsFolder + "/" + Path.GetFileName(candidate);
                try
                {
                    if (!File.Exists(destPath)) File.Copy(systemPath, destPath, false);
                    AssetDatabase.ImportAsset(destPath);
                    Font font = AssetDatabase.LoadAssetAtPath<Font>(destPath);
                    if (font != null)
                    {
                        Debug.LogWarning("[DemoBuilder] Chưa có font trong " + FontsFolder + " — dùng tạm font hệ thống: "
                                         + candidate + ". Nên bỏ font monospace hỗ trợ tiếng Việt vào thư mục đó.");
                        return font;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("[DemoBuilder] Copy font hệ thống thất bại (" + candidate + "): " + e.Message);
                }
            }
            return null;
        }

        static void EnsureTmpEssentials()
        {
            // TMP Essential Resources chứa các shader TMP ("TextMeshPro/Mobile/Distance Field"...);
            // thiếu nó thì CreateFontAsset không tạo được material trong batchmode.
            if (Shader.Find("TextMeshPro/Mobile/Distance Field") != null) return;
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                AssetDatabase.Refresh();
                return;
            }

            // Xóa settings tự tạo từ lần chạy cũ (khi chưa có essentials) để tránh
            // hai asset trùng tên "TMP Settings" trong Resources.
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null)
            {
                AssetDatabase.DeleteAsset(TmpSettingsPath);
            }

            string packagePath = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
            if (!File.Exists(packagePath))
            {
                Debug.LogError("[DemoBuilder] Không tìm thấy TMP Essential Resources tại " + packagePath);
                return;
            }

            // AssetDatabase.ImportPackage hoàn thành bất đồng bộ (bị bỏ qua khi -quit),
            // nên tự giải nén unitypackage (định dạng tar.gz) một cách đồng bộ bằng tar.exe.
            string tempDir = Path.Combine(Path.GetTempPath(), "AyeAyeTmpEssentials");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            var psi = new System.Diagnostics.ProcessStartInfo("tar", "-xf \"" + packagePath + "\" -C \"" + tempDir + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
            };
            using (System.Diagnostics.Process proc = System.Diagnostics.Process.Start(psi))
            {
                string err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    Debug.LogError("[DemoBuilder] Giải nén TMP Essential Resources thất bại: " + err);
                    return;
                }
            }

            CopyUnityPackage(tempDir);
            Directory.Delete(tempDir, true);
            AssetDatabase.Refresh();
            Debug.Log("[DemoBuilder] Đã import TMP Essential Resources (shaders, sprites, settings).");
        }

        // Mỗi thư mục <guid>/ trong unitypackage chứa asset + asset.meta + pathname (đường đích trong project).
        static void CopyUnityPackage(string extractedDir)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (string dir in Directory.GetDirectories(extractedDir))
            {
                string pathnameFile = Path.Combine(dir, "pathname");
                if (!File.Exists(pathnameFile)) continue;
                string destRel = File.ReadAllText(pathnameFile).TrimEnd('\r', '\n');
                if (string.IsNullOrEmpty(destRel)) continue;

                string assetFile = Path.Combine(dir, "asset");
                if (!File.Exists(assetFile)) continue;

                string dest = Path.Combine(projectRoot, destRel.Replace('/', Path.DirectorySeparatorChar));
                string destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                File.Copy(assetFile, dest, true);

                string metaFile = Path.Combine(dir, "asset.meta");
                if (File.Exists(metaFile)) File.Copy(metaFile, dest + ".meta", true);
            }
        }

        static void EnsureTmpSettings(TMP_FontAsset fontAsset)
        {
            // Dùng settings hiện có (ví dụ từ TMP Essential Resources) để tránh hai asset
            // trùng tên "TMP Settings" trong Resources.
            TMP_Settings settings = TMP_Settings.instance;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TMP_Settings>();
                AssetDatabase.CreateAsset(settings, TmpSettingsPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            }
            if (settings == null)
            {
                Debug.LogError("[DemoBuilder] Không tạo được TMP Settings tại " + TmpSettingsPath);
                return;
            }

            if (fontAsset != null && TMP_Settings.defaultFontAsset != fontAsset)
            {
                TMP_Settings.defaultFontAsset = fontAsset;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            Debug.Log("[DemoBuilder] TMP Settings sẵn sàng: " + TmpSettingsPath);
        }

        static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        static GameConfig CreateConfig()
        {
            GameConfig config = CreateOrLoad<GameConfig>(ConfigPath);
            config.totalDistance = 3.2f;
            config.deadlineDay = 3;
            config.normalDistance = 1.0f;
            config.smartDistance = 1.75f;
            config.slipUpDistance = 0.4f;
            config.typewriterCharsPerSecond = 90;
            config.startingCargo = CargoCondition.Intact;
            config.tripCode = "BDAY-0042";
            config.shipName = "S.S. CÓ LẼ";
            config.destinationName = "HÀNH TINH ZORP-9";
            EditorUtility.SetDirty(config);
            return config;
        }

        static CrewDefinition[] CreateCrews()
        {
            CrewDefinition bob = CreateOrLoad<CrewDefinition>(CrewPath(1));
            bob.id = CrewId.Bob; bob.displayName = "BOB"; bob.personalityLabel = "Quá nhiệt tình";
            bob.strengthText = "Việc nặng, việc gấp"; bob.weaknessText = "Việc tinh vi, dễ vỡ";
            bob.portraitGlyph = "O_o";
            bob.defaultNormalLine = "Bob làm việc hăng hái và hoàn thành đúng yêu cầu.";
            bob.defaultQuote = "Xong rồi! Còn gì nữa không?";
            EditorUtility.SetDirty(bob);

            CrewDefinition mira = CreateOrLoad<CrewDefinition>(CrewPath(2));
            mira.id = CrewId.Mira; mira.displayName = "MIRA"; mira.personalityLabel = "Hiểu theo nghĩa đen";
            mira.strengthText = "Quy trình, lệnh rõ ràng"; mira.weaknessText = "Lệnh mơ hồ";
            mira.portraitGlyph = "0_0";
            mira.defaultNormalLine = "Mira thực hiện đúng từng chữ và không có gì bất ngờ.";
            mira.defaultQuote = "Đã thực hiện đúng theo lệnh.";
            EditorUtility.SetDirty(mira);

            CrewDefinition chen = CreateOrLoad<CrewDefinition>(CrewPath(3));
            chen.id = CrewId.Chen; chen.displayName = "CHEN"; chen.personalityLabel = "Lười biếng";
            chen.strengthText = "Việc ít khẩn cấp, ổn định"; chen.weaknessText = "Việc gấp, không bao giờ nhanh";
            chen.portraitGlyph = "-_-";
            chen.defaultNormalLine = "Chen làm chậm rãi nhưng không gây sự cố.";
            chen.defaultQuote = "Xong. Giờ tôi nghỉ được chưa?";
            EditorUtility.SetDirty(chen);

            CrewDefinition dex = CreateOrLoad<CrewDefinition>(CrewPath(4));
            dex.id = CrewId.Dex; dex.displayName = "DEX"; dex.personalityLabel = "Hoang tưởng";
            dex.strengthText = "Phát hiện bất thường"; dex.weaknessText = "Nghi ngờ nhầm, mất thời gian";
            dex.portraitGlyph = "O_O";
            dex.defaultNormalLine = "Dex kiểm tra mọi thứ hai lần rồi hoàn thành việc.";
            dex.defaultQuote = "Tôi vẫn thấy có gì đó lạ.";
            EditorUtility.SetDirty(dex);

            return new[] { bob, mira, chen, dex };
        }

        static DayDefinition[] CreateDays()
        {
            return new[] { CreateDay1(), CreateDay2(), CreateDay3() };
        }

        static DayDefinition CreateDay1()
        {
            DecisionDefinition decision = CreateOrLoad<DecisionDefinition>(DecisionPath(1));
            decision.prompt = "Xác nhận niêm phong thùng bánh trước khi rời bến.";
            decision.options = new List<DecisionOption>
            {
                new DecisionOption
                {
                    label = "Ký xác nhận luôn",
                    description = "Nhanh. Không kiểm tra gì thêm.",
                    entries = new List<CrewOutcomeEntry>
                    {
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Chen, condition = FlagCondition.Always, outcome = DecisionOutcome.Normal,
                            explanation = "Chen ký xác nhận nhanh gọn. Mọi thứ trông ổn, nhưng không ai kiểm tra kỹ.",
                            quote = "Xong. Giờ tôi nghỉ được chưa?",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                    },
                },
                new DecisionOption
                {
                    label = "Kiểm tra thủ công",
                    description = "Kỹ lưỡng nhưng tốn công.",
                    entries = new List<CrewOutcomeEntry>
                    {
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Dex, condition = FlagCondition.Always, outcome = DecisionOutcome.Smart,
                            explanation = "Dex phát hiện dây buộc số 3 bị lỏng và buộc lại chắc chắn. Tàu rời bến suôn sẻ.",
                            quote = "Tôi đã nói là có gì đó không ổn. Giờ thì ổn rồi. Chắc vậy.",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Bob, condition = FlagCondition.Always, outcome = DecisionOutcome.SlipUp,
                            explanation = "Bob siết thêm hai vòng cho chắc. Chiếc bánh bị ép lõm một bên, cả đội mất thời gian tháo ra.",
                            quote = "Chắc chắn là tốt! Tôi đã siết thêm hai vòng nữa!",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                    },
                },
                new DecisionOption
                {
                    label = "Chạy quét tự động",
                    description = "Dùng máy quét của tàu.",
                    entries = new List<CrewOutcomeEntry>
                    {
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Mira, condition = FlagCondition.Always, outcome = DecisionOutcome.Normal,
                            explanation = "Mira chạy quét theo đúng quy trình. Kết quả: không có cảnh báo.",
                            quote = "Quét hoàn tất. Không có cảnh báo, theo đúng từng chữ.",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                    },
                },
            };
            EditorUtility.SetDirty(decision);

            DayDefinition day = CreateOrLoad<DayDefinition>(DayPath(1));
            day.dayNumber = 1;
            day.title = "NGÀY 1 · RỜI BẾN";
            day.briefingText = "BẾN GIAO NHẬN ZORP\n\nThùng bánh đã được chất lên tàu. Trước khi rời bến, nhân viên bến yêu cầu thuyền trưởng xác nhận niêm phong thùng hàng.\n\nMỤC TIÊU HÔM NAY: rời bến an toàn.\nLƯU Ý: dây buộc số 3 đã được thay vội ở bến.";
            day.statusText = "TÀU: ổn định.\nBÁNH: {CARGO}.\n\nDEX: \"Có gì đó không ổn với dây buộc. Tôi cảm thấy nó.\"\nBOB: \"Tôi sẵn sàng làm bất cứ thứ gì!\"\nMIRA: \"Tôi sẽ làm đúng theo chỉ dẫn.\"\nCHEN: \"Có thể làm sau được không?\"";
            day.cargoShiftedWarning = "";
            day.decision = AssetDatabase.LoadAssetAtPath<DecisionDefinition>(DecisionPath(1));
            if (day.decision == null) day.decision = decision;
            EditorUtility.SetDirty(day);
            return day;
        }

        static DayDefinition CreateDay2()
        {
            DecisionDefinition decision = CreateOrLoad<DecisionDefinition>(DecisionPath(2));
            decision.prompt = "Chọn cách đi qua vùng thiên thạch.";
            decision.options = new List<DecisionOption>
            {
                new DecisionOption
                {
                    label = "Vòng theo rìa phải",
                    description = "Chậm hơn, theo dữ liệu cảm biến.",
                    entries = new List<CrewOutcomeEntry>
                    {
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Mira, condition = FlagCondition.Always, outcome = DecisionOutcome.Smart,
                            explanation = "Mira bám đúng tuyến rìa phải theo cảm biến. Tàu đi êm và đúng lịch.",
                            quote = "Tuyến rìa phải. Chính xác từng mét.",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                    },
                },
                new DecisionOption
                {
                    label = "Xuyên thẳng",
                    description = "Nhanh nhất. Rung mạnh.",
                    entries = new List<CrewOutcomeEntry>
                    {
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Bob, condition = FlagCondition.Always, outcome = DecisionOutcome.Smart,
                            explanation = "Bob lao thẳng qua vùng thiên thạch và rút ngắn đường đi. Nhưng thùng bánh bị lệch khỏi vị trí.",
                            quote = "Nhanh hơn mà! Chiếc bánh chỉ... nghiêng một chút thôi.",
                            setFlag = FlagToSet.CargoShifted, cargoDelta = 0,
                        },
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Dex, condition = FlagCondition.Always, outcome = DecisionOutcome.SlipUp,
                            explanation = "Dex đổi hướng ba lần vì tin thiên thạch nhắm vào tàu. Cả đội mất nhiều thời gian.",
                            quote = "Chúng đang đổi hướng theo chúng ta!",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                    },
                },
                new DecisionOption
                {
                    label = "Tắt động cơ, trôi qua",
                    description = "Êm nhưng chậm.",
                    entries = new List<CrewOutcomeEntry>
                    {
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Chen, condition = FlagCondition.Always, outcome = DecisionOutcome.Normal,
                            explanation = "Chen tắt động cơ và để tàu trôi qua. Chậm nhưng yên ổn.",
                            quote = "Trôi cũng là một kỹ năng.",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                    },
                },
            };
            EditorUtility.SetDirty(decision);

            DayDefinition day = CreateOrLoad<DayDefinition>(DayPath(2));
            day.dayNumber = 2;
            day.title = "NGÀY 2 · VÙNG THIÊN THẠCH";
            day.briefingText = "VÙNG THIÊN THẠCH NHỎ\n\nTàu sắp đi qua một vùng thiên thạch nhỏ. Cảm biến cho biết mật độ thiên thạch thấp ở rìa bên phải.\n\nMỤC TIÊU HÔM NAY: vượt vùng thiên thạch mà không chậm lịch trình.";
            day.statusText = "TÀU: động cơ ổn định.\nBÁNH: {CARGO}.\nCẢM BIẾN: rìa phải = MẬT ĐỘ THẤP · giữa = MẬT ĐỘ CAO.\n\nDEX: \"Chúng đang nhắm vào tàu, tôi chắc chắn.\"\nBOB: \"Đi thẳng là nhanh nhất!\"\nMIRA: \"Tôi có thể bám theo cảm biến.\"\nCHEN: \"Hay là cứ để nó trôi.\"";
            day.cargoShiftedWarning = "";
            day.decision = AssetDatabase.LoadAssetAtPath<DecisionDefinition>(DecisionPath(2));
            if (day.decision == null) day.decision = decision;
            EditorUtility.SetDirty(day);
            return day;
        }

        static DayDefinition CreateDay3()
        {
            DecisionDefinition decision = CreateOrLoad<DecisionDefinition>(DecisionPath(3));
            decision.prompt = "Chọn câu lệnh gửi cho đội.";
            decision.options = new List<DecisionOption>
            {
                new DecisionOption
                {
                    label = "“Trả lời trung thực mọi câu hỏi.”",
                    description = "Rõ ràng. Có thể quá rõ ràng.",
                    entries = new List<CrewOutcomeEntry>
                    {
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Mira, condition = FlagCondition.Always, outcome = DecisionOutcome.SlipUp,
                            explanation = "Mira kể tất cả, kể cả “một lượng đường không hợp pháp ở ba hệ sao”. Hải quan giữ tàu để hỏi thêm.",
                            quote = "Ngài bảo trả lời trung thực. Tôi đã trả lời trung thực.",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Dex, condition = FlagCondition.Always, outcome = DecisionOutcome.SlipUp,
                            explanation = "Dex từ chối khai báo vì nghi ngờ hải quan. Cả đội mất thời gian giải thích.",
                            quote = "Tôi không khai gì hết. Đây có thể là bẫy.",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Chen, condition = FlagCondition.Always, outcome = DecisionOutcome.Normal,
                            explanation = "Chen làm thủ tục chậm rãi nhưng đúng quy định. Tàu được cho qua.",
                            quote = "Xong rồi. Từ từ thôi.",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                    },
                },
                new DecisionOption
                {
                    label = "“Khai báo đủ giấy tờ.”",
                    description = "Theo đúng thủ tục.",
                    entries = new List<CrewOutcomeEntry>
                    {
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Mira, condition = FlagCondition.Always, outcome = DecisionOutcome.Smart,
                            explanation = "Mira nộp đầy đủ giấy tờ theo đúng danh mục. Hải quan duyệt rất nhanh.",
                            quote = "Giấy tờ: đủ. Theo đúng danh mục.",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Dex, condition = FlagCondition.Always, outcome = DecisionOutcome.SlipUp,
                            explanation = "Dex từ chối khai báo vì nghi ngờ hải quan. Cả đội mất thời gian giải thích.",
                            quote = "Tôi không khai gì hết. Đây có thể là bẫy.",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Chen, condition = FlagCondition.Always, outcome = DecisionOutcome.Normal,
                            explanation = "Chen làm thủ tục chậm rãi nhưng đúng quy định. Tàu được cho qua.",
                            quote = "Xong rồi. Từ từ thôi.",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                    },
                },
                new DecisionOption
                {
                    label = "“Trả lời ngắn gọn.”",
                    description = "Nhanh, đỡ rườm rà.",
                    entries = new List<CrewOutcomeEntry>
                    {
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Bob, condition = FlagCondition.CargoShiftedForbidden, outcome = DecisionOutcome.Smart,
                            explanation = "Bob trả lời ngắn gọn và nhiệt tình. Hải quan quý anh ấy và cho qua nhanh.",
                            quote = "Có! Không! Cảm ơn!",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Bob, condition = FlagCondition.CargoShiftedRequired, outcome = DecisionOutcome.SlipUp,
                            explanation = "Bob nhiệt tình giúp bê thùng bánh lên bàn kiểm tra và làm rơi. Bánh bị trầy, hải quan giữ tàu lại.",
                            quote = "Tôi giúp được mà! ...Chắc vậy.",
                            setFlag = FlagToSet.None, cargoDelta = -1,
                        },
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Dex, condition = FlagCondition.Always, outcome = DecisionOutcome.SlipUp,
                            explanation = "Dex từ chối khai báo vì nghi ngờ hải quan. Cả đội mất thời gian giải thích.",
                            quote = "Tôi không khai gì hết. Đây có thể là bẫy.",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                        new CrewOutcomeEntry
                        {
                            crew = CrewId.Chen, condition = FlagCondition.Always, outcome = DecisionOutcome.Normal,
                            explanation = "Chen làm thủ tục chậm rãi nhưng đúng quy định. Tàu được cho qua.",
                            quote = "Xong rồi. Từ từ thôi.",
                            setFlag = FlagToSet.None, cargoDelta = 0,
                        },
                    },
                },
            };
            EditorUtility.SetDirty(decision);

            DayDefinition day = CreateOrLoad<DayDefinition>(DayPath(3));
            day.dayNumber = 3;
            day.title = "NGÀY 3 · TRẠM HẢI QUAN";
            day.briefingText = "TRẠM HẢI QUAN GLEEP\n\nTrạm hải quan yêu cầu khai báo hàng hóa. Công thức chiếc bánh được coi là bí mật thương mại.\n\nMỤC TIÊU HÔM NAY: qua trạm mà không bị giữ lại.\nBạn sẽ ra lệnh cho đội bằng MỘT CÂU.";
            day.statusText = "TÀU: đang chờ kiểm tra.\nBÁNH: {CARGO}.\n\nMIRA: \"Tôi sẽ làm đúng từng chữ lệnh.\"\nBOB: \"Để tôi giúp bê!\"\nDEX: \"Hải quan trông đáng ngờ.\"\nCHEN: \"Cứ từ từ.\"";
            day.cargoShiftedWarning = "CẢNH BÁO: thùng hàng đang lệch khỏi vị trí cố định.";
            day.decision = AssetDatabase.LoadAssetAtPath<DecisionDefinition>(DecisionPath(3));
            if (day.decision == null) day.decision = decision;
            EditorUtility.SetDirty(day);
            return day;
        }

        static EndingDefinitions CreateEndings()
        {
            EndingDefinitions endings = CreateOrLoad<EndingDefinitions>(EndingsPath);
            endings.endings = new List<EndingDefinitions.EndingEntry>
            {
                new EndingDefinitions.EndingEntry
                {
                    id = EndingId.PerfectDelivery,
                    title = "GIAO HÀNG HOÀN HẢO",
                    body = "Chiếc bánh đến nơi nguyên vẹn. Người nhận thổi nến thành công và hứa sẽ để lại đánh giá năm sao.",
                },
                new EndingDefinitions.EndingEntry
                {
                    id = EndingId.MinorDamage,
                    title = "HƠI TRẦY, VẪN NGON",
                    body = "Bánh đến đúng hạn nhưng hơi trầy. Người nhận vẫn cười và thổi nến.",
                },
                new EndingDefinitions.EndingEntry
                {
                    id = EndingId.DisasterDelivery,
                    title = "GIAO HÀNG THẢM HỌA",
                    body = "Bánh đến đúng hạn nhưng đã nát. Họ vẫn ăn bằng thìa và cố tỏ ra lịch sự.",
                },
                new EndingDefinitions.EndingEntry
                {
                    id = EndingId.LateButIntact,
                    title = "ĐẸP NHƯNG TRỄ",
                    body = "Bánh nguyên vẹn nhưng đã qua giờ thổi nến. Họ thổi nến muộn và nói không sao. Chắc vậy.",
                },
                new EndingDefinitions.EndingEntry
                {
                    id = EndingId.LateDelivery,
                    title = "GIAO HÀNG TRỄ",
                    body = "Bánh đến sau giờ thổi nến. Chỉ còn lại một lời xin lỗi và một chiếc bánh không còn đẹp.",
                },
            };
            EditorUtility.SetDirty(endings);
            return endings;
        }

        // ---------------------------------------------------------------- scene

        static void BuildScene(GameConfig config, CrewDefinition[] crews, DayDefinition[] days, EndingDefinitions endings, TMP_FontAsset font)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            Camera cam = cameraGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundColor;
            cameraGo.AddComponent<UniversalAdditionalCameraData>();

            GameObject eventSystemGo = new GameObject("EventSystem");
            EventSystem eventSystem = eventSystemGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemGo.AddComponent<StandaloneInputModule>();
#endif

            GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Transform canvasT = canvasGo.transform;

            // ---- Header
            RectTransform headerInner = CreateFramedPanel("HeaderPanel", canvasT,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -64f), Vector2.zero);
            HeaderBarUI header = headerInner.gameObject.AddComponent<HeaderBarUI>();
            header.tripText = CreateText("TripText", headerInner, V(0.01f, 0.15f), V(0.20f, 0.85f), font, 24, GreenColor, TextAlignmentOptions.Left);
            header.shipText = CreateText("ShipText", headerInner, V(0.20f, 0.15f), V(0.45f, 0.85f), font, 24, GreenColor, TextAlignmentOptions.Center);
            header.dayText = CreateText("DayText", headerInner, V(0.45f, 0.15f), V(0.63f, 0.85f), font, 24, AmberColor, TextAlignmentOptions.Center);
            header.systemText = CreateText("SystemText", headerInner, V(0.63f, 0.15f), V(0.82f, 0.85f), font, 22, GreenColor, TextAlignmentOptions.Center);
            header.lampText = CreateText("LampText", headerInner, V(0.82f, 0.15f), V(0.99f, 0.85f), font, 22, GreenColor, TextAlignmentOptions.Right);
            header.glyphTestText = CreateText("GlyphTestText", headerInner, V(0.20f, 0.0f), V(0.82f, 0.16f), font, 14, AmberColor, TextAlignmentOptions.Center);
            header.glyphTestText.text = "ăâêôơư đ àáảãạ ềếểễệ ồốổỗộ";
            header.showGlyphTest = false; // chuỗi test glyph chỉ bật tay trong Inspector khi cần kiểm tra font.

            // ---- Content area (chứa 4 trang)
            RectTransform content = CreateRect("ContentArea", canvasT, V(0f, 0.12f), V(1f, 0.94f));

            // ---- Briefing
            RectTransform briefInner = CreateFramedPanel("BriefingPanel", content, Vector2.zero, Vector2.one, V(24f, 16f), V(-24f, -16f));
            BriefingScreenUI briefing = briefInner.gameObject.AddComponent<BriefingScreenUI>();
            briefing.titleText = CreateText("TitleText", briefInner, V(0.02f, 0.88f), V(0.98f, 1f), font, 28, AmberColor, TextAlignmentOptions.Center);
            briefing.bodyText = CreateText("BodyText", briefInner, V(0.03f, 0.12f), V(0.97f, 0.86f), font, 26, GreenColor, TextAlignmentOptions.TopLeft);
            TMP_Text briefCursor = CreateText("CursorText", briefInner, V(0.03f, 0.02f), V(0.10f, 0.11f), font, 26, GreenColor, TextAlignmentOptions.Left);
            briefCursor.text = "█";
            TerminalTextEffect briefType = briefInner.gameObject.AddComponent<TerminalTextEffect>();
            briefType.target = briefing.bodyText;
            briefType.cursorText = briefCursor;
            briefing.typewriter = briefType;
            briefing.continueButton = CreateButton("ContinueButton", briefInner, V(0.70f, 0.01f), V(0.98f, 0.10f), font, "TIẾP TỤC >", 26);

            // ---- Status
            RectTransform statusInner = CreateFramedPanel("StatusPanel", content, Vector2.zero, Vector2.one, V(24f, 16f), V(-24f, -16f));
            StatusScreenUI status = statusInner.gameObject.AddComponent<StatusScreenUI>();
            status.statusText = CreateText("StatusText", statusInner, V(0.02f, 0.36f), V(0.60f, 1f), font, 24, GreenColor, TextAlignmentOptions.TopLeft);
            status.warningText = CreateText("WarningText", statusInner, V(0.02f, 0.24f), V(0.60f, 0.35f), font, 24, RedColor, TextAlignmentOptions.TopLeft);
            status.warningText.text = "";
            status.flagsText = CreateText("FlagsText", statusInner, V(0.02f, 0.13f), V(0.60f, 0.23f), font, 22, AmberColor, TextAlignmentOptions.Left);
            status.flagsText.text = "";
            status.crewCards = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                status.crewCards[i] = CreateText("CrewCard" + i, statusInner,
                    V(0.005f + i * 0.245f, 0.005f), V(0.245f + i * 0.245f, 0.12f), font, 20, GreenColor, TextAlignmentOptions.TopLeft);
            }
            TMP_Text statusCursor = CreateText("CursorText", statusInner, V(0.62f, 0.02f), V(0.70f, 0.11f), font, 24, GreenColor, TextAlignmentOptions.Left);
            statusCursor.text = "█";
            TerminalTextEffect statusType = statusInner.gameObject.AddComponent<TerminalTextEffect>();
            statusType.target = status.statusText;
            statusType.cursorText = statusCursor;
            status.typewriter = statusType;
            status.continueButton = CreateButton("ContinueButton", statusInner, V(0.70f, 0.13f), V(0.98f, 0.22f), font, "TIẾP TỤC >", 26);
            status.backButton = CreateButton("BackButton", statusInner, V(0.02f, 0.13f), V(0.30f, 0.22f), font, "QUAY LẠI", 26);
            CakeStatusUI cake = statusInner.gameObject.AddComponent<CakeStatusUI>();
            cake.cakeText = CreateText("CakeText", statusInner, V(0.62f, 0.13f), V(0.98f, 1f), font, 24, GreenColor, TextAlignmentOptions.TopLeft);

            // ---- Assignment
            RectTransform assignInner = CreateFramedPanel("AssignmentPanel", content, Vector2.zero, Vector2.one, V(24f, 16f), V(-24f, -16f));
            AssignmentScreenUI assignment = assignInner.gameObject.AddComponent<AssignmentScreenUI>();
            assignment.promptText = CreateText("PromptText", assignInner, V(0.02f, 0.88f), V(0.98f, 1f), font, 26, AmberColor, TextAlignmentOptions.Center);
            assignment.optionButtons = new Button[3];
            assignment.optionLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                assignment.optionButtons[i] = CreateButton("OptionButton" + i, assignInner,
                    V(0.02f + i * 0.33f, 0.55f), V(0.32f + i * 0.33f, 0.85f), font, "PHƯƠNG ÁN", 24);
                assignment.optionLabels[i] = assignment.optionButtons[i].GetComponentInChildren<TMP_Text>();
            }
            assignment.crewButtons = new Button[4];
            assignment.crewLabels = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                assignment.crewButtons[i] = CreateButton("CrewButton" + i, assignInner,
                    V(0.02f + i * 0.245f, 0.27f), V(0.245f + i * 0.245f, 0.53f), font, "THỦY THỦ", 24);
                assignment.crewLabels[i] = assignment.crewButtons[i].GetComponentInChildren<TMP_Text>();
            }
            assignment.previewText = CreateText("PreviewText", assignInner, V(0.02f, 0.15f), V(0.98f, 0.25f), font, 24, AmberColor, TextAlignmentOptions.Center);
            assignment.previewText.text = "";
            assignment.confirmButton = CreateButton("ConfirmButton", assignInner, V(0.30f, 0.02f), V(0.70f, 0.12f), font, "XÁC NHẬN QUYẾT ĐỊNH", 26);
            assignment.endDayButton = CreateButton("EndDayButton", assignInner, V(0.30f, 0.02f), V(0.70f, 0.12f), font, "KẾT THÚC NGÀY >", 26);
            assignment.endDayButton.gameObject.SetActive(false);
            assignment.backButton = CreateButton("BackButton", assignInner, V(0.02f, 0.02f), V(0.27f, 0.12f), font, "QUAY LẠI", 26);
            assignment.editButton = CreateButton("EditButton", assignInner, V(0.73f, 0.02f), V(0.98f, 0.12f), font, "CHỈNH LẠI", 26);
            assignment.editButton.gameObject.SetActive(false);

            // ---- Result overlay + ScreenFeedback
            RectTransform overlayInner = CreateFramedPanel("ResultOverlayPanel", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform overlayOuter = overlayInner.parent as RectTransform;
            ResultOverlayUI overlay = overlayInner.gameObject.AddComponent<ResultOverlayUI>();

            Image flash = CreateRect("FlashImage", overlayInner, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            flash.color = new Color(0.35f, 1f, 0.5f, 0f);
            flash.raycastTarget = false;

            overlay.resultLabel = CreateText("ResultLabel", overlayInner, V(0.02f, 0.80f), V(0.98f, 0.96f), font, 30, GreenColor, TextAlignmentOptions.Center);
            overlay.explanationText = CreateText("ExplanationText", overlayInner, V(0.04f, 0.40f), V(0.96f, 0.78f), font, 26, GreenColor, TextAlignmentOptions.TopLeft);
            overlay.quoteText = CreateText("QuoteText", overlayInner, V(0.04f, 0.20f), V(0.96f, 0.38f), font, 24, AmberColor, TextAlignmentOptions.TopLeft);
            overlay.distanceText = CreateText("DistanceText", overlayInner, V(0.04f, 0.10f), V(0.96f, 0.19f), font, 24, GreenColor, TextAlignmentOptions.Center);
            overlay.nextButton = CreateButton("NextButton", overlayInner, V(0.30f, 0.005f), V(0.70f, 0.09f), font, "NGÀY TIẾP THEO >", 26);

            ScreenFeedback feedback = overlayOuter.gameObject.AddComponent<ScreenFeedback>();
            feedback.shakeTarget = overlayOuter;
            feedback.flashImage = flash;

            // ---- End screen
            RectTransform endInner = CreateFramedPanel("EndPanel", canvasT, V(0f, 0.12f), V(1f, 0.94f), V(160f, 20f), V(-160f, -20f));
            EndScreenUI endScreen = endInner.gameObject.AddComponent<EndScreenUI>();
            endScreen.titleText = CreateText("TitleText", endInner, V(0.02f, 0.78f), V(0.98f, 0.95f), font, 34, GreenColor, TextAlignmentOptions.Center);
            endScreen.bodyText = CreateText("BodyText", endInner, V(0.06f, 0.38f), V(0.94f, 0.76f), font, 26, GreenColor, TextAlignmentOptions.TopLeft);
            endScreen.statsText = CreateText("StatsText", endInner, V(0.06f, 0.14f), V(0.94f, 0.36f), font, 24, AmberColor, TextAlignmentOptions.TopLeft);
            endScreen.restartButton = CreateButton("RestartButton", endInner, V(0.34f, 0.01f), V(0.66f, 0.11f), font, "CHƠI LẠI TỪ ĐẦU", 26);

            // ---- Progress bar
            RectTransform barInner = CreateFramedPanel("ProgressBarPanel", canvasT, V(0f, 0f), V(1f, 0.12f), Vector2.zero, Vector2.zero);
            ProgressBarUI bar = barInner.gameObject.AddComponent<ProgressBarUI>();
            CreateText("StartLabel", barInner, V(0.005f, 0.35f), V(0.10f, 0.65f), font, 22, GreenColor, TextAlignmentOptions.Left).text = "START >";
            CreateText("EndLabel", barInner, V(0.90f, 0.35f), V(0.995f, 0.65f), font, 22, GreenColor, TextAlignmentOptions.Right).text = "> ĐÍCH";
            RectTransform track = CreateRect("Track", barInner, V(0.11f, 0.42f), V(0.89f, 0.58f));
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = TrackColor;
            trackImage.raycastTarget = false;
            RectTransform marker = CreateRect("Marker", track, V(0f, 0.5f), V(0f, 0.5f), V(-12f, -11f), V(12f, 11f));
            Image markerImage = marker.gameObject.AddComponent<Image>();
            markerImage.color = AmberColor;
            markerImage.raycastTarget = false;
            TMP_Text markerText = CreateText("MarkerGlyph", marker, Vector2.zero, Vector2.one, font, 18, BackgroundColor, TextAlignmentOptions.Center);
            markerText.text = "▲";
            bar.track = track;
            bar.marker = marker;
            bar.percentText = CreateText("PercentText", barInner, V(0.11f, 0.08f), V(0.89f, 0.38f), font, 22, GreenColor, TextAlignmentOptions.Center);

            // ---- Scanline (render trên cùng, không nhận click)
            RectTransform scanline = CreateRect("Scanline", canvasT, Vector2.zero, Vector2.one);
            Image scanImage = scanline.gameObject.AddComponent<Image>();
            scanImage.color = new Color(0f, 0f, 0f, 0.05f);
            scanImage.raycastTarget = false;
            feedback.scanlineImage = scanImage;

            // ---- Managers
            GameObject managerGo = new GameObject("--GameManager--");
            GameManager game = managerGo.AddComponent<GameManager>();
            DayCycleManager dayCycle = managerGo.AddComponent<DayCycleManager>();

            game.config = config;
            game.days = days;
            game.crews = crews;
            game.endings = endings;
            game.dayCycle = dayCycle;
            game.screenFlow = CreateFlow(briefing.transform, status.transform, assignment.transform,
                overlayOuter, endInner.parent);
            game.screenFeedback = feedback;
            game.header = header;
            game.briefing = briefing;
            game.status = status;
            game.assignment = assignment;
            game.resultOverlay = overlay;
            game.endScreen = endScreen;
            game.progressBar = bar;
            game.cakeStatus = cake;

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[DemoBuilder] Scene " + (saved ? "đã lưu" : "LƯU THẤT BẠI") + ": " + ScenePath);
        }

        static ScreenFlowController CreateFlow(Transform briefingPanel, Transform statusPanel, Transform assignmentPanel, Transform overlayPanel, Transform endPanel)
        {
            ScreenFlowController flow = new GameObject("--ScreenFlow--").AddComponent<ScreenFlowController>();
            flow.briefingPanel = briefingPanel.parent.gameObject;
            flow.statusPanel = statusPanel.parent.gameObject;
            flow.assignmentPanel = assignmentPanel.parent.gameObject;
            flow.resultOverlayPanel = overlayPanel.gameObject;
            flow.endPanel = endPanel.gameObject;
            // Ẩn các panel không dùng ở frame đầu.
            flow.statusPanel.SetActive(false);
            flow.assignmentPanel.SetActive(false);
            flow.resultOverlayPanel.SetActive(false);
            flow.endPanel.SetActive(false);
            return flow;
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            return CreateRect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        }

        static RectTransform CreateFramedPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform outer = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            Image frame = outer.gameObject.AddComponent<Image>();
            frame.color = FrameColor;
            frame.raycastTarget = false;

            GameObject inner = new GameObject("Inner", typeof(RectTransform));
            RectTransform innerRt = inner.GetComponent<RectTransform>();
            innerRt.SetParent(outer, false);
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(2f, 2f);
            innerRt.offsetMax = new Vector2(-2f, -2f);
            Image bg = inner.AddComponent<Image>();
            bg.color = PanelBgColor;
            bg.raycastTarget = false;
            return innerRt;
        }

        static TMP_Text CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, TMP_FontAsset font, int fontSize, Color color, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return tmp;
        }

        static Button CreateButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, TMP_FontAsset font, string label, int fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image image = go.AddComponent<Image>();
            image.color = ButtonBgColor;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 1f, 0.9f, 1f);
            colors.pressedColor = new Color(0.7f, 0.9f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.5f, 0.47f, 0.55f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            TMP_Text text = CreateText("Label", rt, Vector2.zero, Vector2.one, font, fontSize, GreenColor, TextAlignmentOptions.Center);
            text.margin = new Vector4(8f, 4f, 8f, 4f);
            text.text = label;
            return button;
        }

        static Vector2 V(float x, float y)
        {
            return new Vector2(x, y);
        }
    }
}
