using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace AyeAye
{
    // V3: Build Demo dựng dữ liệu mẫu mục 5 của SRS v3 (FR-0905). Font/TMP dựng trước ở đây;
    // phần asset dữ liệu (config, crews, ports/routes, orders, incidents, endings) và wiring scene
    // được bổ sung theo từng bước triển khai v3 — menu phải luôn idempotent.
    public static class DemoBuilder
    {
        const string ProjectRoot = "Assets/_Project";
        const string FontsFolder = ProjectRoot + "/Fonts";
        const string FontSearchFolder = FontsFolder;
        const string TmpSettingsPath = FontsFolder + "/Resources/TMP Settings.asset";
        const string DataRoot = ProjectRoot + "/Data";

        static readonly string[] DataFolders =
        {
            ProjectRoot, FontsFolder, DataRoot, DataRoot + "/Config",
            DataRoot + "/Crew", DataRoot + "/World", DataRoot + "/Orders",
            DataRoot + "/Incidents", DataRoot + "/Endings",
            DataRoot + "/Ports", DataRoot + "/Routes", DataRoot + "/Ships", DataRoot + "/Crews",
            ProjectRoot + "/Scenes", ProjectRoot + "/Scripts",
            ProjectRoot + "/Scripts/Runtime", ProjectRoot + "/Scripts/Runtime/Data",
            ProjectRoot + "/Scripts/Runtime/Logic", ProjectRoot + "/Scripts/Runtime/Core",
            ProjectRoot + "/Scripts/Runtime/UI", ProjectRoot + "/Scripts/Runtime/Presentation",
            ProjectRoot + "/Scripts/Editor", ProjectRoot + "/Scripts/Tests",
            ProjectRoot + "/Scripts/Tests/EditMode", FontsFolder + "/Resources",
        };

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

            // Dữ liệu mẫu SRS §5 thành assets (FR-0905) — nguồn duy nhất DemoData, chạy lại không phá dữ liệu.
            EnsureDataAssets();

            AssetDatabase.SaveAssets();
            Debug.Log("[DemoBuilder] Font/TMP + dữ liệu mẫu §5 sẵn sàng (Assets/_Project/Data). Chạy Tools/AyeAye/Validate để kiểm tra.");
        }

        // ---------------------------------------------------------------- dữ liệu mẫu §5

        static void EnsureDataAssets()
        {
            // GameConfig: chỉ tạo khi thiếu — không đè số cân bằng đã chỉnh (idempotent thật sự).
            string configPath = DataRoot + "/Config/GameConfig.asset";
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(configPath) == null)
                CreateAssetAt(DemoData.CreateConfig(), configPath, "GameConfig");

            // Cảng + cạnh + bảng tuyến
            foreach (var p in DemoData.BuildPorts())
                RebuildAsset("Ports/Port_" + p.portId + ".asset", p);
            foreach (var e in DemoData.BuildRouteDefs())
                RebuildAsset("Routes/Route_" + e.fromPortId + "-" + e.toPortId + ".asset", e);
            RebuildAsset("Routes/RouteTable.asset", DemoData.BuildRouteTable());

            // Tàu + thuyền trưởng (tham chiếu qua từng asset riêng)
            foreach (var s in DemoData.BuildShips())
                RebuildAsset("Ships/Ship_" + s.shipId + ".asset", s);
            foreach (var c in DemoData.BuildCrews())
                RebuildAsset("Crews/Crew_" + c.id + ".asset", c);

            // Đơn + pool (pool trỏ tới asset riêng, không copy nội tuyến)
            var orders = DemoData.BuildOrderPool();
            var fixedOrders = new List<OrderDef>();
            foreach (var o in orders.fixedFirstDay) fixedOrders.Add(RebuildAsset("Orders/Order_" + o.orderId + ".asset", o));
            var poolOrders = new List<OrderDef>();
            foreach (var o in orders.pool) poolOrders.Add(RebuildAsset("Orders/Order_" + o.orderId + ".asset", o));
            var orderPool = AssetDatabase.LoadAssetAtPath<OrderPool>(DataRoot + "/Orders/OrderPool.asset");
            if (orderPool == null)
                orderPool = CreateAssetAt(ScriptableObject.CreateInstance<OrderPool>(), DataRoot + "/Orders/OrderPool.asset", "OrderPool");
            orderPool.fixedFirstDay = fixedOrders;
            orderPool.pool = poolOrders;
            EditorUtility.SetDirty(orderPool);

            // Sự cố + pool
            var incidents = DemoData.BuildIncidentPool();
            var fixedFirst = RebuildAsset("Incidents/Incident_" + incidents.fixedFirstDay.incidentId + ".asset", incidents.fixedFirstDay);
            var fixedFinale = RebuildAsset("Incidents/Incident_" + incidents.fixedFinale.incidentId + ".asset", incidents.fixedFinale);
            var poolIncidents = new List<IncidentDef>();
            foreach (var d in incidents.incidents) poolIncidents.Add(RebuildAsset("Incidents/Incident_" + d.incidentId + ".asset", d));
            var incidentPool = AssetDatabase.LoadAssetAtPath<IncidentPool>(DataRoot + "/Incidents/IncidentPool.asset");
            if (incidentPool == null)
                incidentPool = CreateAssetAt(ScriptableObject.CreateInstance<IncidentPool>(), DataRoot + "/Incidents/IncidentPool.asset", "IncidentPool");
            incidentPool.fixedFirstDay = fixedFirst;
            incidentPool.fixedFinale = fixedFinale;
            incidentPool.incidents = poolIncidents;
            EditorUtility.SetDirty(incidentPool);

            // 5 đoạn kết
            RebuildAsset("Endings/Endings.asset", DemoData.BuildEndings());
        }

        // Dựng lại nội dung asset từ nguồn DemoData (CopySerialized) — idempotent.
        // GameConfig là ngoại lệ duy nhất: chỉ tạo khi thiếu, giữ nguyên số đã chỉnh.
        static T RebuildAsset<T>(string relPath, T fresh) where T : ScriptableObject
        {
            string path = DataRoot + "/" + relPath;
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
                return CreateAssetAt(fresh, path, System.IO.Path.GetFileNameWithoutExtension(relPath));
            EditorUtility.CopySerialized(fresh, existing);
            ScriptableObject.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static T CreateAssetAt<T>(T asset, string path, string assetName) where T : ScriptableObject
        {
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ---------------------------------------------------------------- Tools/AyeAye/Validate

        [MenuItem("Tools/AyeAye/Validate")]
        public static void Validate()
        {
            var config = LoadOne<GameConfig>(DataRoot + "/Config");
            var ports = LoadAll<PortDef>(DataRoot + "/Ports");
            var edges = LoadAll<RouteDef>(DataRoot + "/Routes");
            var routes = LoadOne<RouteTable>(DataRoot + "/Routes");
            var orderPool = LoadOne<OrderPool>(DataRoot + "/Orders");
            var incidentPool = LoadOne<IncidentPool>(DataRoot + "/Incidents");
            var ships = LoadAll<ShipDef>(DataRoot + "/Ships");
            var crews = LoadAll<CrewDefinition>(DataRoot + "/Crews");

            var errors = DemoValidator.Validate(config, routes, orderPool,
                incidentPool != null ? incidentPool.incidents : null,
                incidentPool != null ? incidentPool.fixedFirstDay : null,
                incidentPool != null ? incidentPool.fixedFinale : null,
                ships, crews, ports: ports, edges: edges);

            if (errors.Count == 0)
                Debug.Log($"[Validate] OK: {ports.Count} cảng, {edges.Count} cạnh, {ships.Count} tàu, " +
                          $"{orderPool.fixedFirstDay.Count + orderPool.pool.Count} đơn, " +
                          $"{incidentPool.incidents.Count + 2} sự cố (kể cả I-01/I-F), bảng outcome đủ, route nối được.");
            else
                foreach (string e in errors)
                    Debug.LogError("[Validate] " + e);
        }

        static T LoadOne<T>(string folder) where T : ScriptableObject
        {
            var all = LoadAll<T>(folder);
            return all.Count > 0 ? all[0] : null;
        }

        static List<T> LoadAll<T>(string folder) where T : ScriptableObject
        {
            var list = new List<T>();
            if (!AssetDatabase.IsValidFolder(folder)) return list;
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
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

        static TMP_FontAsset EnsureFontAsset()
        {
            Font sourceFont = PickSourceFont();
            if (sourceFont == null)
            {
                Debug.LogError("[DemoBuilder] Không có font trong " + FontsFolder + " và không copy được font hệ thống. "
                               + "Bỏ một file .ttf/.otf hỗ trợ tiếng Việt (ví dụ JetBrains Mono) vào thư mục đó rồi chạy lại Tools/AyeAye/Build Demo.");
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
    }
}
