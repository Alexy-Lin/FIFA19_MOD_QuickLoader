using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace FIFA19QuickLoader
{
    internal enum DisplayMode
    {
        Fullscreen,
        Borderless,
        Windowed,
    }

    /// <summary>
    /// Reads and writes Documents\FIFA 19\fifasetup.ini (the file FIFASetup\fifaconfig.exe edits).
    /// Unknown keys, their order and the file's BOM are preserved; only the keys we change are touched.
    /// </summary>
    internal sealed class GameSettings
    {
        private const string BackupSuffix = ".quickloader.bak";
        private readonly List<string> lines;
        private readonly bool hadBom;

        public string FilePath { get; }
        public bool Exists { get; }

        private GameSettings(string path, List<string> lines, bool hadBom, bool exists)
        {
            FilePath = path;
            this.lines = lines;
            this.hadBom = hadBom;
            Exists = exists;
        }

        public static string ResolvePath(string gameDir)
        {
            var folder = "FIFA 19";
            var setupConfig = Path.Combine(gameDir ?? "", "FIFASetup", "config.ini");
            try
            {
                if (File.Exists(setupConfig))
                {
                    foreach (var line in File.ReadAllLines(setupConfig))
                    {
                        var kv = SplitLine(line);
                        if (kv != null && kv.Item1.Equals("SETTING_FOLDER", StringComparison.OrdinalIgnoreCase))
                        {
                            var v = kv.Item2.Trim().Trim('\'', '"').Trim();
                            if (v.Length > 0 && v.IndexOfAny(Path.GetInvalidFileNameChars()) < 0) folder = v;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docs, folder, "fifasetup.ini");
        }

        public static GameSettings Load(string gameDir)
        {
            var path = ResolvePath(gameDir);
            if (!File.Exists(path))
            {
                // Same keys fifaconfig.exe writes; the game falls back to them when the file is missing.
                var defaults = new List<string>
                {
                    "AUDIO_MIX_MODE = 0", "CONFIG_APP_LOCALE = zh-CN", "CONTROLLER_DEFAULT = 0", "DIRECTX_SELECT = 1",
                    "FULLSCREEN = 1", "MSAA_LEVEL = 1", "RENDERINGQUALITY = 3", "RESOLUTIONHEIGHT = 1080",
                    "RESOLUTIONWIDTH = 1920", "VOICECHAT = 0", "WAITFORVSYNC = 1", "WINDOWED_BORDERLESS = 0",
                    "WINDOWTITLE = \"FIFA 19\"", "",
                };
                return new GameSettings(path, defaults, true, false);
            }
            var bytes = File.ReadAllBytes(path);
            var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var text = new UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
            var list = text.Replace("\r\n", "\n").Split('\n').ToList();
            return new GameSettings(path, list, bom, true);
        }

        private static Tuple<string, string> SplitLine(string line)
        {
            var i = line.IndexOf('=');
            if (i <= 0 || line.TrimStart().StartsWith(";")) return null;
            return Tuple.Create(line.Substring(0, i).Trim(), line.Substring(i + 1).Trim());
        }

        public string Get(string key)
        {
            foreach (var line in lines)
            {
                var kv = SplitLine(line);
                if (kv != null && kv.Item1.Equals(key, StringComparison.OrdinalIgnoreCase)) return kv.Item2;
            }
            return null;
        }

        public int GetInt(string key, int fallback) =>
            int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        public void Set(string key, string value)
        {
            var newLine = key + " = " + value;
            for (int i = 0; i < lines.Count; i++)
            {
                var kv = SplitLine(lines[i]);
                if (kv != null && kv.Item1.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = newLine;
                    return;
                }
            }
            // Keep the trailing blank line(s) at the end of the file.
            var insertAt = lines.Count;
            while (insertAt > 0 && lines[insertAt - 1].Trim().Length == 0) insertAt--;
            lines.Insert(insertAt, newLine);
        }

        public int Width
        {
            get => GetInt("RESOLUTIONWIDTH", 1920);
            set => Set("RESOLUTIONWIDTH", value.ToString(CultureInfo.InvariantCulture));
        }

        public int Height
        {
            get => GetInt("RESOLUTIONHEIGHT", 1080);
            set => Set("RESOLUTIONHEIGHT", value.ToString(CultureInfo.InvariantCulture));
        }

        public DisplayMode Mode
        {
            get
            {
                if (GetInt("FULLSCREEN", 1) != 0) return DisplayMode.Fullscreen;
                return GetInt("WINDOWED_BORDERLESS", 0) != 0 ? DisplayMode.Borderless : DisplayMode.Windowed;
            }
            set
            {
                Set("FULLSCREEN", value == DisplayMode.Fullscreen ? "1" : "0");
                Set("WINDOWED_BORDERLESS", value == DisplayMode.Borderless ? "1" : "0");
            }
        }

        public static string ModeName(DisplayMode mode)
        {
            switch (mode)
            {
                case DisplayMode.Fullscreen: return "全屏";
                case DisplayMode.Borderless: return "无边框窗口";
                default: return "窗口";
            }
        }

        public void Save()
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var backup = FilePath + BackupSuffix;
            if (File.Exists(FilePath) && !File.Exists(backup)) File.Copy(FilePath, backup);

            var payload = new UTF8Encoding(false).GetBytes(string.Join("\r\n", lines));
            byte[] bytes;
            if (hadBom)
            {
                bytes = new byte[payload.Length + 3];
                bytes[0] = 0xEF; bytes[1] = 0xBB; bytes[2] = 0xBF;
                Buffer.BlockCopy(payload, 0, bytes, 3, payload.Length);
            }
            else bytes = payload;

            if (File.Exists(FilePath) && (File.GetAttributes(FilePath) & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(FilePath, File.GetAttributes(FilePath) & ~FileAttributes.ReadOnly);
            var tmp = FilePath + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            if (File.Exists(FilePath))
            {
                try { File.Replace(tmp, FilePath, null); }
                catch (IOException)
                {
                    // File.Replace can fail on sync-managed folders (OneDrive); fall back to an overwrite.
                    File.Copy(tmp, FilePath, true);
                    File.Delete(tmp);
                }
            }
            else File.Move(tmp, FilePath);
        }

        // ---------- monitor modes ----------

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        private const int ENUM_CURRENT_SETTINGS = -1;

        /// <summary>Resolutions the primary monitor supports (32-bit), largest first, plus its current/native mode.</summary>
        public static List<System.Drawing.Size> GetMonitorResolutions(out System.Drawing.Size current)
        {
            var set = new HashSet<System.Drawing.Size>();
            var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
            current = System.Drawing.Size.Empty;
            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm))
                current = new System.Drawing.Size(dm.dmPelsWidth, dm.dmPelsHeight);
            for (int i = 0; i < 1000; i++)
            {
                dm = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
                if (!EnumDisplaySettings(null, i, ref dm)) break;
                if (dm.dmBitsPerPel >= 32 && dm.dmPelsWidth >= 800 && dm.dmPelsHeight >= 600)
                    set.Add(new System.Drawing.Size(dm.dmPelsWidth, dm.dmPelsHeight));
            }
            if (!current.IsEmpty) set.Add(current);
            return set.OrderByDescending(s => s.Width).ThenByDescending(s => s.Height).ToList();
        }
    }
}
