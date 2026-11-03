using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace FIFA19QuickLoader
{
    internal enum OverwriteChoice
    {
        Overwrite,
        Skip,
        Cancel,
    }

    internal sealed class ImportResult
    {
        public List<string> Imported { get; } = new List<string>();
        public List<string> Messages { get; } = new List<string>();
        public bool Cancelled { get; set; }
    }

    internal static class ModImporter
    {
        public static ImportResult Import(IEnumerable<string> paths, string modsDir, Func<string, OverwriteChoice> askOverwrite)
        {
            Directory.CreateDirectory(modsDir);
            var result = new ImportResult();
            foreach (var path in paths)
            {
                if (result.Cancelled) break;
                var ext = Path.GetExtension(path).ToLowerInvariant();
                try
                {
                    if (ext == ".zip") ImportZip(path, modsDir, askOverwrite, result);
                    else if (ModFile.IsModFileName(path)) ImportModFile(path, modsDir, askOverwrite, result);
                    else if (ext == ".rar" || ext == ".7z")
                        result.Messages.Add($"{Path.GetFileName(path)}：rar/7z 请先解压，再导入里面的 .fbmod 文件");
                    else if (ext != ".archive")
                        result.Messages.Add($"{Path.GetFileName(path)}：不是 MOD 文件，已跳过");
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
                {
                    result.Messages.Add($"{Path.GetFileName(path)}：导入失败 - {ex.Message}");
                }
            }
            return result;
        }

        private static void ImportZip(string zipPath, string modsDir, Func<string, OverwriteChoice> askOverwrite, ImportResult result)
        {
            var temp = Path.Combine(Path.GetTempPath(), "FIFA19QuickLoader_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                var mods = new List<string>();
                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in zip.Entries)
                    {
                        // Flatten names: Frosty expects every mod and archive directly in Mods\FIFA19, and it blocks zip-slip.
                        var name = Path.GetFileName(entry.FullName);
                        var ext = Path.GetExtension(name).ToLowerInvariant();
                        if (name.Length == 0 || !(ModFile.IsModFileName(name) || ext == ".archive")) continue;
                        var target = Path.Combine(temp, name);
                        if (File.Exists(target))
                        {
                            result.Messages.Add($"{Path.GetFileName(zipPath)}：压缩包里有重名文件 {name}，已跳过重复项");
                            continue;
                        }
                        entry.ExtractToFile(target);
                        if (ModFile.IsModFileName(name)) mods.Add(target);
                    }
                }
                if (mods.Count == 0)
                {
                    result.Messages.Add($"{Path.GetFileName(zipPath)}：压缩包里没有 .fbmod / .fbcollection 文件");
                    return;
                }
                foreach (var m in mods)
                {
                    if (result.Cancelled) break;
                    ImportModFile(m, modsDir, askOverwrite, result);
                }
            }
            finally
            {
                try { Directory.Delete(temp, true); }
                catch (IOException) { }
            }
        }

        private static void ImportModFile(string source, string modsDir, Func<string, OverwriteChoice> askOverwrite, ImportResult result)
        {
            var name = Path.GetFileName(source);
            var info = ModFile.Read(source);
            if (info.IsForOtherGame)
            {
                result.Messages.Add($"{name}：{info.Problem}，已跳过");
                return;
            }
            if (string.Equals(Path.GetFullPath(Path.GetDirectoryName(source)), Path.GetFullPath(modsDir), StringComparison.OrdinalIgnoreCase))
            {
                result.Imported.Add(name);
                return;
            }

            var target = Path.Combine(modsDir, name);
            if (File.Exists(target))
            {
                var choice = askOverwrite(name);
                if (choice == OverwriteChoice.Cancel) { result.Cancelled = true; return; }
                if (choice == OverwriteChoice.Skip) return;
                foreach (var old in ModFile.GetCompanionFiles(target).ToList()) File.Delete(old);
            }
            foreach (var f in ModFile.GetCompanionFiles(source).ToList())
                File.Copy(f, Path.Combine(modsDir, Path.GetFileName(f)), true);
            result.Imported.Add(name);
        }
    }
}
