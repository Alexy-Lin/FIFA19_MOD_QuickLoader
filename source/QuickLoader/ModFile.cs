using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FIFA19QuickLoader
{
    /// <summary>Metadata of a mod file in FMM's Mods\FIFA19 folder, read from the .fbmod header only.</summary>
    internal sealed class ModFile
    {
        private const ulong FbModMagic = 72155812747760198UL;
        private const uint MaxFbModVersion = 5;
        private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public string FileName { get; private set; }
        public string FullPath { get; private set; }
        public bool IsCollection { get; private set; }
        public bool HeaderValid { get; private set; }
        public bool IsForOtherGame { get; private set; }
        public string Problem { get; private set; }

        public string Title { get; private set; }
        public string Author { get; private set; }
        public string Category { get; private set; }
        public string Version { get; private set; }
        public string Description { get; private set; }
        public string Link { get; private set; }

        // Frosty decodes header bytes one byte per char; these are used to match FMM's mods.json exactly.
        public string RawTitle { get; private set; }
        public string RawCategory { get; private set; }
        public string RawVersion { get; private set; }

        public long Size { get; private set; }
        public DateTime LastWriteTimeUtc { get; private set; }

        public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Path.GetFileNameWithoutExtension(FileName) : Title;

        public static bool IsModFileName(string name)
        {
            var ext = Path.GetExtension(name);
            return ext.Equals(".fbmod", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".fbcollection", StringComparison.OrdinalIgnoreCase);
        }

        public static List<ModFile> ScanDirectory(string dir)
        {
            var list = new List<ModFile>();
            if (!Directory.Exists(dir)) return list;
            foreach (var path in Directory.EnumerateFiles(dir))
            {
                if (IsModFileName(path)) list.Add(Read(path));
            }
            return list.OrderBy(m => m.DisplayTitle, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        /// <summary>Lists the .fbmod plus its "_NN.archive" companions.</summary>
        public static IEnumerable<string> GetCompanionFiles(string modPath)
        {
            yield return modPath;
            if (!modPath.EndsWith(".fbmod", StringComparison.OrdinalIgnoreCase)) yield break;
            var dir = Path.GetDirectoryName(modPath);
            var stem = Path.GetFileNameWithoutExtension(modPath);
            foreach (var f in Directory.EnumerateFiles(dir, stem + "_*.archive"))
            {
                var suffix = Path.GetFileNameWithoutExtension(f).Substring(stem.Length);
                if (suffix.Length == 3 && char.IsDigit(suffix[1]) && char.IsDigit(suffix[2])) yield return f;
            }
        }

        public static ModFile Read(string path)
        {
            var info = new FileInfo(path);
            var m = new ModFile
            {
                FileName = info.Name,
                FullPath = info.FullName,
                Size = info.Exists ? info.Length : 0,
                LastWriteTimeUtc = info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue,
                IsCollection = info.Extension.Equals(".fbcollection", StringComparison.OrdinalIgnoreCase),
            };
            if (m.IsCollection)
            {
                m.Problem = "MOD 合集（.fbcollection），详情请在 FMM 中查看";
                return m;
            }
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var r = new BinaryReader(fs))
                {
                    if (fs.Length < 8 || r.ReadUInt64() != FbModMagic)
                    {
                        m.Problem = "旧版格式 MOD，详细信息请在 FMM 中查看";
                        return m;
                    }
                    var version = r.ReadUInt32();
                    if (version > MaxFbModVersion)
                    {
                        m.Problem = $"MOD 格式版本 {version} 比当前 FMM 支持的更新";
                        return m;
                    }
                    r.ReadInt64();
                    r.ReadInt32();
                    var profile = Latin1.GetString(r.ReadBytes(r.ReadByte())).TrimEnd('\0');
                    r.ReadInt32();
                    if (!profile.Equals(FrostyConfig.Profile, StringComparison.OrdinalIgnoreCase))
                    {
                        m.Problem = $"这是 {profile} 的 MOD，不是 FIFA 19 的";
                        m.IsForOtherGame = true;
                        return m;
                    }
                    var raw = new List<byte[]>();
                    var count = version >= 5 ? 6 : 5;
                    for (int i = 0; i < count; i++) raw.Add(ReadCString(r));
                    m.RawTitle = Latin1.GetString(raw[0]);
                    m.RawCategory = Latin1.GetString(raw[2]);
                    m.RawVersion = Latin1.GetString(raw[3]);
                    m.Title = Decode(raw[0]);
                    m.Author = Decode(raw[1]);
                    m.Category = Decode(raw[2]);
                    m.Version = Decode(raw[3]);
                    m.Description = Decode(raw[4]);
                    m.Link = count > 5 ? Decode(raw[5]) : "";
                    m.HeaderValid = true;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                m.Problem = "无法读取：" + ex.Message;
            }
            return m;
        }

        private static byte[] ReadCString(BinaryReader r)
        {
            var bytes = new List<byte>();
            while (true)
            {
                var b = r.ReadByte();
                if (b == 0) return bytes.ToArray();
                if (bytes.Count > 1 << 20) throw new IOException("MOD 头部字符串过长，文件可能已损坏");
                bytes.Add(b);
            }
        }

        private static string Decode(byte[] bytes)
        {
            try { return StrictUtf8.GetString(bytes); }
            catch (DecoderFallbackException) { return Latin1.GetString(bytes); }
        }
    }
}
