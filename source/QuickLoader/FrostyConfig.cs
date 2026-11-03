using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FIFA19QuickLoader
{
    internal sealed class PackEntry
    {
        public PackEntry(string fileName, bool enabled)
        {
            FileName = fileName;
            Enabled = enabled;
        }

        public string FileName { get; }
        public bool Enabled { get; set; }
    }

    internal sealed class ModPack
    {
        public ModPack(string name) { Name = name; }
        public string Name { get; set; }
        public List<PackEntry> Entries { get; } = new List<PackEntry>();

        public ModPack Clone(string newName)
        {
            var p = new ModPack(newName);
            p.Entries.AddRange(Entries.Select(e => new PackEntry(e.FileName, e.Enabled)));
            return p;
        }
    }

    /// <summary>Reads/writes %LOCALAPPDATA%\Frosty\manager_config.json (the file FMM itself uses).</summary>
    internal sealed class FrostyConfig
    {
        public const string Profile = "FIFA19";

        private JsonObject root;
        private DateTime loadedWriteTimeUtc;

        public static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frosty", "manager_config.json");

        public List<ModPack> Packs { get; } = new List<ModPack>();

        public string SelectedPack { get; set; }

        public string GamePath { get; private set; }

        public string CommandLineArgs { get; private set; }

        public bool PlatformLaunchingEnabled { get; private set; }

        public string CustomModsDirectory { get; private set; }

        public bool DefaultProfileReady { get; private set; }

        public bool ChangedOnDisk =>
            File.Exists(ConfigPath) && File.GetLastWriteTimeUtc(ConfigPath) != loadedWriteTimeUtc;

        public static FrostyConfig Load()
        {
            var path = ConfigPath;
            if (!File.Exists(path))
                throw new FileNotFoundException("找不到 FMM 配置文件。请点“更改游戏目录…”设置 FIFA 19 所在位置。", path);

            var cfg = new FrostyConfig();
            cfg.loadedWriteTimeUtc = File.GetLastWriteTimeUtc(path);
            cfg.root = Json.Parse(File.ReadAllText(path, FileUtil.Utf8NoBom)) as JsonObject
                       ?? throw new FormatException("FMM 配置文件格式不正确。");

            var game = cfg.root.GetObject("Games")?.GetObject(Profile)
                       ?? throw new InvalidDataException("FMM 配置里没有 FIFA19。请点“更改游戏目录…”设置 FIFA 19 所在位置。");
            cfg.GamePath = game.GetString("GamePath");
            if (string.IsNullOrWhiteSpace(cfg.GamePath) || !Path.IsPathRooted(cfg.GamePath))
                throw new InvalidDataException("FMM 配置里的 FIFA19 游戏路径无效。");
            cfg.GamePath = Path.GetFullPath(cfg.GamePath).TrimEnd('\\');

            var options = game.GetObject("Options");
            cfg.SelectedPack = options?.GetString("SelectedPack");
            cfg.CommandLineArgs = (options?.GetString("CommandLineArgs") ?? "").Trim();
            cfg.PlatformLaunchingEnabled = options?.GetBool("PlatformLaunchingEnabled") == true;

            var global = cfg.root.GetObject("GlobalOptions");
            cfg.CustomModsDirectory = global?.GetString("CustomModsDirectory") ?? "";
            cfg.DefaultProfileReady = global?.GetBool("UseDefaultProfile") == true &&
                                      string.Equals(global.GetString("DefaultProfile"), Profile, StringComparison.OrdinalIgnoreCase);

            var packs = game.GetObject("Packs");
            if (packs != null)
            {
                foreach (var name in packs.Keys)
                {
                    var pack = new ModPack(name);
                    var raw = packs[name] as string ?? "";
                    foreach (var item in raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var idx = item.LastIndexOf(':');
                        if (idx <= 0 || !bool.TryParse(item.Substring(idx + 1), out var enabled))
                            throw new InvalidDataException($"方案“{name}”里有无法识别的条目：{item}");
                        pack.Entries.Add(new PackEntry(item.Substring(0, idx), enabled));
                    }
                    cfg.Packs.Add(pack);
                }
            }
            if (cfg.Packs.Count == 0) cfg.Packs.Add(new ModPack("Default"));
            if (cfg.FindPack(cfg.SelectedPack) == null) cfg.SelectedPack = cfg.Packs[0].Name;
            return cfg;
        }

        /// <summary>True when the FMM config exists and points FIFA19 at a folder that contains FIFA19.exe.</summary>
        public static bool HasValidGamePath()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return false;
                var root = Json.Parse(File.ReadAllText(ConfigPath, FileUtil.Utf8NoBom)) as JsonObject;
                var path = root?.GetObject("Games")?.GetObject(Profile)?.GetString("GamePath");
                return IsGameDirectory(path);
            }
            catch (Exception ex) when (ex is IOException || ex is FormatException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static bool IsGameDirectory(string dir) =>
            !string.IsNullOrWhiteSpace(dir) && Path.IsPathRooted(dir) && File.Exists(Path.Combine(dir, "FIFA19.exe"));

        /// <summary>Checks EA/Origin registry entries and common library folders without a recursive disk scan.</summary>
        public static string DetectGameDirectory()
        {
            var candidates = new List<string>();
            foreach (var key in new[]
            {
                @"SOFTWARE\WOW6432Node\EA Sports\FIFA 19",
                @"SOFTWARE\EA Sports\FIFA 19",
                @"SOFTWARE\WOW6432Node\EA Games\FIFA 19",
                @"SOFTWARE\EA Games\FIFA 19",
                @"SOFTWARE\WOW6432Node\Origin Games\FIFA 19",
            })
            {
                try
                {
                    using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key))
                    {
                        var dir = (k?.GetValue("Install Dir") as string)?.TrimEnd('\\');
                        if (!string.IsNullOrWhiteSpace(dir)) candidates.Add(dir);
                    }
                }
                catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is IOException)
                {
                }
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            candidates.AddRange(new[]
            {
                Path.Combine(programFiles, "EA Games", "FIFA 19"),
                Path.Combine(programFilesX86, "EA Games", "FIFA 19"),
                Path.Combine(programFiles, "Origin Games", "FIFA 19"),
                Path.Combine(programFilesX86, "Origin Games", "FIFA 19"),
                @"C:\FIFA19",
            });

            var commonLibraryFolders = new[]
            {
                "FIFA 19",
                "FIFA19",
                Path.Combine("EA Games", "FIFA 19"),
                Path.Combine("Origin Games", "FIFA 19"),
                Path.Combine("Games", "FIFA 19"),
                Path.Combine("SteamLibrary", "steamapps", "common", "FIFA 19"),
            };
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                    candidates.AddRange(commonLibraryFolders.Select(folder => Path.Combine(drive.RootDirectory.FullName, folder)));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                }
            }

            return candidates.FirstOrDefault(IsGameDirectory);
        }

        /// <summary>
        /// Writes the FIFA19 game path into FMM's config, creating the config when FMM has never run.
        /// Only touches Games.FIFA19.GamePath; everything else is preserved.
        /// </summary>
        public static void SetGamePath(string gameDir)
        {
            if (!IsGameDirectory(gameDir)) throw new InvalidOperationException("所选文件夹里没有 FIFA19.exe。");
            var path = ConfigPath;
            JsonObject root = null;
            if (File.Exists(path))
            {
                root = Json.Parse(File.ReadAllText(path, FileUtil.Utf8NoBom)) as JsonObject
                       ?? throw new FormatException("FMM 配置文件格式不正确。");
                var backup = path + ".quickloader.bak";
                if (!File.Exists(backup)) File.Copy(path, backup);
            }
            root = root ?? new JsonObject();
            var game = root.GetOrCreateObject("Games").GetOrCreateObject(Profile);
            game["GamePath"] = Path.GetFullPath(gameDir).TrimEnd('\\');
            game.GetOrCreateObject("Options");
            game.GetOrCreateObject("Packs");
            var global = root.GetOrCreateObject("GlobalOptions");
            global["UseDefaultProfile"] = true;
            global["DefaultProfile"] = Profile;
            FileUtil.WriteAllTextAtomic(path, Json.Write(root));
        }

        public ModPack FindPack(string name) =>
            name == null ? null : Packs.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        public string GetModsDirectory(string fmmDirectory)
        {
            var baseDir = string.IsNullOrWhiteSpace(CustomModsDirectory)
                ? Path.Combine(fmmDirectory, "Mods")
                : Path.Combine(fmmDirectory, CustomModsDirectory);
            return Path.GetFullPath(Path.Combine(baseDir, Profile));
        }

        /// <summary>FMM's "-launch" only skips the profile picker when FIFA19 is the default profile.</summary>
        public void EnsureDefaultProfile()
        {
            var global = root.GetOrCreateObject("GlobalOptions");
            global["UseDefaultProfile"] = true;
            global["DefaultProfile"] = Profile;
            DefaultProfileReady = true;
        }

        public void Save()
        {
            if (ChangedOnDisk)
                throw new IOException("FMM 配置文件在本工具打开后被其他程序修改过（可能是 FMM）。请点“刷新”后再操作。");

            var game = root.GetObject("Games").GetObject(Profile);
            var packs = new JsonObject();
            foreach (var p in Packs)
                packs[p.Name] = string.Join("|", p.Entries.Select(e => e.FileName + ":" + (e.Enabled ? "True" : "False")));
            game["Packs"] = packs;
            game.GetOrCreateObject("Options")["SelectedPack"] = SelectedPack;

            var path = ConfigPath;
            var backup = path + ".quickloader.bak";
            if (!File.Exists(backup)) File.Copy(path, backup);
            FileUtil.WriteAllTextAtomic(path, Json.Write(root));
            loadedWriteTimeUtc = File.GetLastWriteTimeUtc(path);
        }
    }
}
