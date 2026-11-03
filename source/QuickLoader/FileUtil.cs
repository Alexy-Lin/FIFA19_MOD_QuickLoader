using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace FIFA19QuickLoader
{
    internal static class FileUtil
    {
        public static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>Writes via a temp file in the same folder so a crash never leaves a half-written file.</summary>
        public static void WriteAllTextAtomic(string path, string content)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(dir);
            var tmp = Path.Combine(dir, Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(tmp, content, Utf8NoBom);
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        public static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var sb = new StringBuilder(64);
                foreach (var b in sha.ComputeHash(fs)) sb.Append(b.ToString("X2"));
                return sb.ToString();
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteFileW(string path);

        /// <summary>
        /// Removes the "downloaded from the internet" mark (Zone.Identifier stream) from every file under dir.
        /// Without this, a zip extracted by Explorer makes .NET refuse to load FMM's plugin/SDK DLLs.
        /// Returns the number of files unblocked.
        /// </summary>
        public static int UnblockDirectory(string dir)
        {
            var count = 0;
            if (!Directory.Exists(dir)) return 0;
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                if (DeleteFileW(f + ":Zone.Identifier")) count++;
            }
            return count;
        }

        public static bool IsProcessRunning(string name)
        {
            var list = Process.GetProcessesByName(name);
            try { return list.Length > 0; }
            finally { foreach (var p in list) p.Dispose(); }
        }

        public static bool IsValidPackName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim()) return false;
            if (name == "." || name == "..") return false;
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            // '|' and ':' are separators inside FMM's pack strings.
            return name.IndexOfAny(new[] { '|', ':', '"' }) < 0;
        }
    }
}
