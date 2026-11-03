using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FIFA19QuickLoader
{
    internal sealed class LaunchPlan
    {
        public bool CanFastLaunch { get; set; }
        public string Reason { get; set; }
        public string DataDirectory { get; set; }
        public List<string> MissingMods { get; } = new List<string>();
        public int EnabledCount { get; set; }
        /// <summary>Non-null when existing ModData lacks the links to the original game data and must be rebuilt.</summary>
        public string BrokenLinks { get; set; }
    }

    internal static class GameLauncher
    {
        public const string GameProcessName = "FIFA19";
        public const string FmmProcessName = "FrostyModManager";

        /// <summary>
        /// Mirrors FrostyModExecutor.Run: generated data is reusable when mods.json matches the enabled list
        /// and neither the game patch nor any mod file changed afterwards. Anything uncertain falls back to FMM.
        /// </summary>
        public static LaunchPlan Evaluate(FrostyConfig cfg, ModPack pack, string modsDir, IReadOnlyDictionary<string, ModFile> library)
        {
            var plan = new LaunchPlan { DataDirectory = Path.Combine(cfg.GamePath, "ModData", pack.Name) };
            var enabled = new List<string>();
            foreach (var e in pack.Entries.Where(e => e.Enabled))
            {
                if (File.Exists(Path.Combine(modsDir, e.FileName))) enabled.Add(e.FileName);
                else plan.MissingMods.Add(e.FileName);
            }
            plan.EnabledCount = enabled.Count;

            plan.BrokenLinks = CheckDataLinks(cfg.GamePath, plan.DataDirectory);
            if (plan.BrokenLinks != null)
                return Slow(plan, "MOD 数据缺少指向游戏原始数据的链接，需要重建");

            if (cfg.PlatformLaunchingEnabled)
                return Slow(plan, "FMM 里开启了“通过平台启动”，需交给 FMM 处理");

            var patchDir = Path.Combine(plan.DataDirectory, "Patch");
            var modsJson = Path.Combine(patchDir, "mods.json");
            if (!File.Exists(modsJson) || !File.Exists(Path.Combine(patchDir, "layout.toc")) ||
                !File.Exists(Path.Combine(patchDir, "initfs_Win32")) ||
                !Directory.Exists(Path.Combine(plan.DataDirectory, "Data")))
                return Slow(plan, "这个方案还没有生成过 MOD 数据");

            List<object> generated;
            try
            {
                generated = Json.Parse(File.ReadAllText(modsJson)) as List<object>;
            }
            catch (Exception ex) when (ex is FormatException || ex is IOException)
            {
                generated = null;
            }
            if (generated == null) return Slow(plan, "已生成的 mods.json 无法读取");

            if (generated.Count != enabled.Count) return Slow(plan, "启用的 MOD 有变化");
            for (int i = 0; i < enabled.Count; i++)
            {
                var g = generated[i] as JsonObject;
                if (g == null || g.GetString("file_name") != enabled[i]) return Slow(plan, "启用的 MOD 或顺序有变化");
                if (library.TryGetValue(enabled[i], out var mod) && mod.HeaderValid &&
                    (g.GetString("name") != mod.RawTitle || g.GetString("version") != mod.RawVersion ||
                     g.GetString("category") != mod.RawCategory))
                    return Slow(plan, $"MOD“{mod.DisplayTitle}”的版本信息变了");
            }

            var generatedAt = File.GetLastWriteTimeUtc(modsJson);
            var gameLayout = Path.Combine(cfg.GamePath, "Patch", "layout.toc");
            if (File.Exists(gameLayout) && File.GetLastWriteTimeUtc(gameLayout) > generatedAt)
                return Slow(plan, "游戏本体更新过，需要重新生成");
            foreach (var name in enabled)
            {
                foreach (var f in ModFile.GetCompanionFiles(Path.Combine(modsDir, name)))
                {
                    if (File.GetLastWriteTimeUtc(f) > generatedAt)
                        return Slow(plan, $"MOD 文件“{name}”在上次生成后被替换过");
                }
            }

            plan.CanFastLaunch = true;
            plan.Reason = "MOD 数据已是最新，可直接秒启";
            return plan;
        }

        private static LaunchPlan Slow(LaunchPlan plan, string reason)
        {
            plan.CanFastLaunch = false;
            plan.Reason = reason;
            return plan;
        }

        public static Process LaunchDirect(FrostyConfig cfg, LaunchPlan plan)
        {
            var exe = Path.Combine(cfg.GamePath, GameProcessName + ".exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("找不到游戏程序。", exe);
            if (cfg.CommandLineArgs.IndexOf("-dataPath", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("FMM 启动参数里已经有 -dataPath，请在 FMM 的选项中删掉它。");
            var args = "-dataPath \"" + plan.DataDirectory.TrimEnd('\\') + "\"";
            if (cfg.CommandLineArgs.Length > 0) args += " " + cfg.CommandLineArgs;
            var psi = new ProcessStartInfo(exe, args)
            {
                WorkingDirectory = cfg.GamePath,
                UseShellExecute = false,
            };
            return Process.Start(psi);
        }

        public static Process LaunchViaFmm(string fmmDirectory, string packName)
        {
            var exe = Path.Combine(fmmDirectory, FmmProcessName + ".exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("找不到 FrostyModManager.exe。", exe);
            var psi = new ProcessStartInfo(exe, "-launch \"" + packName + "\"")
            {
                WorkingDirectory = fmmDirectory,
                UseShellExecute = false,
            };
            return Process.Start(psi);
        }

        /// <summary>
        /// FMM only creates the Data/Update links and the per-catalog .cas links when the profile folder does not
        /// exist yet ("New installation"). If the folder already exists without them (moved game, denied UAC, copied
        /// ModData), FMM never repairs it and the game exits shortly after start. Returns null when the data is
        /// absent (FMM will build it properly) or complete; otherwise a description of the first problem.
        /// </summary>
        public static string CheckDataLinks(string gameDir, string dataDir)
        {
            if (!Directory.Exists(dataDir)) return null;
            try
            {
                if (!IsLinkedDirectory(Path.Combine(dataDir, "Data")))
                    return "缺少 Data 链接";
                if (Directory.Exists(Path.Combine(gameDir, "Update")) && !IsLinkedDirectory(Path.Combine(dataDir, "Update")))
                    return "缺少 Update 链接";

                var gamePatch = Path.Combine(gameDir, "Patch");
                if (Directory.Exists(gamePatch))
                {
                    var root = Path.GetFullPath(gamePatch).TrimEnd('\\') + "\\";
                    foreach (var cas in Directory.EnumerateFiles(gamePatch, "*.cas", SearchOption.AllDirectories))
                    {
                        var rel = Path.GetFullPath(cas).Substring(root.Length);
                        var target = Path.Combine(dataDir, "patch", rel);
                        if (!File.Exists(target) || (File.GetAttributes(target) & FileAttributes.ReparsePoint) == 0)
                            return "缺少原始数据链接 patch\\" + rel;
                    }
                }
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return "无法检查链接：" + ex.Message;
            }
        }

        private static bool IsLinkedDirectory(string path)
        {
            var info = new DirectoryInfo(path);
            if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) == 0) return false;
            try
            {
                // Touch the target so a dangling link (e.g. pointing at the old game folder) counts as missing.
                using (var e = Directory.EnumerateFileSystemEntries(path).GetEnumerator()) e.MoveNext();
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Deletes a whole profile folder so FMM regenerates it from scratch. Links are removed without following
        /// them, so the original game data they point to is never touched.
        /// </summary>
        public static void DeleteProfileData(string gameDir, string packName)
        {
            var modData = Path.GetFullPath(Path.Combine(gameDir, "ModData"));
            var dir = Path.GetFullPath(Path.Combine(modData, packName));
            if (!dir.StartsWith(modData + "\\", StringComparison.OrdinalIgnoreCase) || dir.Length <= modData.Length + 1)
                throw new InvalidOperationException("方案目录路径异常，已取消。");
            if (Directory.Exists(dir)) DeleteTreeNoFollow(new DirectoryInfo(dir));
        }

        private static void DeleteTreeNoFollow(DirectoryInfo dir)
        {
            if ((dir.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                dir.Delete(false);
                return;
            }
            foreach (var entry in dir.EnumerateFileSystemInfos())
            {
                if (entry is DirectoryInfo sub)
                {
                    DeleteTreeNoFollow(sub);
                }
                else
                {
                    if ((entry.Attributes & FileAttributes.ReadOnly) != 0) entry.Attributes &= ~FileAttributes.ReadOnly;
                    entry.Delete();
                }
            }
            dir.Delete(false);
        }

        /// <summary>Removes generated data so FMM is forced to rebuild it on the next launch.</summary>
        public static void DeleteGeneratedData(string gameDir, string packName)
        {
            var modData = Path.GetFullPath(Path.Combine(gameDir, "ModData"));
            var dir = Path.GetFullPath(Path.Combine(modData, packName));
            if (!dir.StartsWith(modData + "\\", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("方案目录路径异常，已取消。");
            var modsJson = Path.Combine(dir, "Patch", "mods.json");
            if (File.Exists(modsJson)) File.Delete(modsJson);
        }
    }
}
