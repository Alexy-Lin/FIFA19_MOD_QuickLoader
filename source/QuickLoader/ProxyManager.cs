using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FIFA19QuickLoader
{
    internal enum ProxyState
    {
        NotInstalled,
        Installed,
        Unknown,
    }

    /// <summary>Installs/restores the amd_ags_x64.dll proxy that replaces Extreme Injector.</summary>
    internal sealed class ProxyManager
    {
        public const string DllName = "amd_ags_x64.dll";
        public const string BackupName = "amd_ags_x64_orig.dll";

        public static readonly string[] InjectedDlls =
        {
            "sysdll_Win64_retail.dll",
            "CardsDLL_Win64_retail.dll",
            "FootballCompEng_Win64_retail.dll",
            "powdll_Win64_retail.dll",
            "CryptBase.dll",
            "dbdata.dll",
        };

        private readonly string packagedProxy;
        private readonly string packagedCryptBase;
        private readonly string originalHash;
        private readonly string proxyHash;
        private readonly string cryptBaseHash;

        private const string CryptBaseName = "CryptBase.dll";
        private const string CryptBaseMarkerName = "CryptBase.dll.quickloader";

        public ProxyManager(string appDirectory)
        {
            var dir = Path.Combine(appDirectory, "proxy");
            packagedProxy = Path.Combine(dir, DllName);
            packagedCryptBase = Path.Combine(appDirectory, "FMM", "ThirdParty", CryptBaseName);
            var manifestPath = Path.Combine(dir, "proxy-manifest.json");
            if (!File.Exists(packagedProxy) || !File.Exists(manifestPath))
                throw new FileNotFoundException("工具目录下缺少 proxy\\amd_ags_x64.dll 或 proxy-manifest.json，请重新解压完整工具包。");
            if (!File.Exists(packagedCryptBase))
                throw new FileNotFoundException("工具目录下缺少 FMM\\ThirdParty\\CryptBase.dll，请重新解压完整工具包。");
            var manifest = Json.Parse(File.ReadAllText(manifestPath)) as JsonObject
                           ?? throw new FormatException("proxy-manifest.json 格式不正确。");
            originalHash = manifest.GetString("OriginalAmdSHA256")?.ToUpperInvariant();
            proxyHash = manifest.GetString("ProxySHA256")?.ToUpperInvariant();
            if (string.IsNullOrEmpty(originalHash) || string.IsNullOrEmpty(proxyHash))
                throw new FormatException("proxy-manifest.json 缺少哈希值。");
            cryptBaseHash = FileUtil.Sha256(packagedCryptBase);
        }

        public ProxyState GetState(string gameDir, out string detail)
        {
            var installed = Path.Combine(gameDir, DllName);
            var backup = Path.Combine(gameDir, BackupName);
            if (!File.Exists(installed))
            {
                detail = "游戏目录里没有 amd_ags_x64.dll，游戏文件可能不完整";
                return ProxyState.Unknown;
            }
            var current = FileUtil.Sha256(installed);
            var backupHash = File.Exists(backup) ? FileUtil.Sha256(backup) : null;
            if (current == proxyHash && backupHash == originalHash)
            {
                detail = "已安装（启动游戏时自动加载 MOD 所需 DLL，无需注入器）";
                return ProxyState.Installed;
            }
            if (current == originalHash && backupHash == null)
            {
                detail = "未安装（目前仍需 Extreme Injector 注入）";
                return ProxyState.NotInstalled;
            }
            if (current == originalHash && backupHash == originalHash)
            {
                detail = "未安装（留有一份原始备份，可直接安装）";
                return ProxyState.NotInstalled;
            }
            detail = "状态异常：amd_ags_x64.dll 不是原版也不是本工具的代理（可能是其他工具或游戏修复改过）";
            return ProxyState.Unknown;
        }

        public IEnumerable<string> MissingGameDlls(string gameDir) =>
            InjectedDlls.Where(n => n != CryptBaseName && !File.Exists(Path.Combine(gameDir, n)));

        public bool IsCryptBaseMissing(string gameDir) =>
            !File.Exists(Path.Combine(gameDir, CryptBaseName));

        public void Install(string gameDir)
        {
            EnsurePackagedProxyIntact();
            EnsurePackagedCryptBaseIntact();
            var state = GetState(gameDir, out var detail);
            if (state == ProxyState.Unknown) throw new InvalidOperationException(detail + "。为安全起见未做任何修改。");

            var installed = Path.Combine(gameDir, DllName);
            var backup = Path.Combine(gameDir, BackupName);
            var createdBackup = false;
            var createdCryptBase = false;
            try
            {
                if (state == ProxyState.NotInstalled && !File.Exists(backup))
                {
                    File.Copy(installed, backup);
                    createdBackup = true;
                }
                if (IsCryptBaseMissing(gameDir))
                {
                    var cryptBase = Path.Combine(gameDir, CryptBaseName);
                    File.Copy(packagedCryptBase, cryptBase);
                    createdCryptBase = true;
                    if (FileUtil.Sha256(cryptBase) != cryptBaseHash)
                        throw new IOException("复制后的 CryptBase.dll 校验失败。");
                    FileUtil.WriteAllTextAtomic(GetCryptBaseMarkerPath(gameDir), cryptBaseHash);
                }
                if (state == ProxyState.NotInstalled)
                {
                    File.Copy(packagedProxy, installed, true);
                    if (FileUtil.Sha256(installed) != proxyHash) throw new IOException("复制后的代理文件校验失败。");
                }
            }
            catch
            {
                if (state == ProxyState.NotInstalled && File.Exists(backup) && FileUtil.Sha256(backup) == originalHash)
                {
                    File.Copy(backup, installed, true);
                    if (createdBackup) File.Delete(backup);
                }
                if (createdCryptBase)
                {
                    var cryptBase = Path.Combine(gameDir, CryptBaseName);
                    if (File.Exists(cryptBase) && FileUtil.Sha256(cryptBase) == cryptBaseHash)
                        File.Delete(cryptBase);
                    var marker = GetCryptBaseMarkerPath(gameDir);
                    if (File.Exists(marker)) File.Delete(marker);
                }
                throw;
            }
        }

        public void Uninstall(string gameDir)
        {
            var installed = Path.Combine(gameDir, DllName);
            var backup = Path.Combine(gameDir, BackupName);
            if (!File.Exists(backup) || FileUtil.Sha256(backup) != originalHash)
                throw new InvalidOperationException("找不到可信的原始备份 amd_ags_x64_orig.dll，为安全起见未做任何修改。");
            if (File.Exists(installed))
            {
                var current = FileUtil.Sha256(installed);
                if (current != proxyHash && current != originalHash)
                    throw new InvalidOperationException("amd_ags_x64.dll 已被其他工具改动，为安全起见未覆盖。");
            }
            File.Copy(backup, installed, true);
            File.Delete(backup);

            var cryptBase = Path.Combine(gameDir, CryptBaseName);
            var marker = GetCryptBaseMarkerPath(gameDir);
            if (File.Exists(marker) && File.Exists(cryptBase))
            {
                var markerHash = File.ReadAllText(marker).Trim();
                if (string.Equals(markerHash, cryptBaseHash, StringComparison.OrdinalIgnoreCase) &&
                    FileUtil.Sha256(cryptBase) == cryptBaseHash)
                {
                    File.Delete(cryptBase);
                    File.Delete(marker);
                }
            }
            else if (File.Exists(marker) && !File.Exists(cryptBase))
            {
                File.Delete(marker);
            }
        }

        /// <summary>
        /// Rewrites the proxy, the original backup and CryptBase.dll regardless of the current state. Requires a
        /// trusted original amd_ags_x64.dll (current file or backup); any unrecognised file that would be
        /// overwritten is kept as "*.quickloader-&lt;time&gt;.bak". Returns a human-readable list of actions.
        /// </summary>
        public List<string> ForceReinstall(string gameDir)
        {
            EnsurePackagedProxyIntact();
            EnsurePackagedCryptBaseIntact();
            var actions = new List<string>();
            var installed = Path.Combine(gameDir, DllName);
            var backup = Path.Combine(gameDir, BackupName);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

            var currentHash = File.Exists(installed) ? FileUtil.Sha256(installed) : null;
            var backupHash = File.Exists(backup) ? FileUtil.Sha256(backup) : null;
            string originalSource = backupHash == originalHash ? backup : currentHash == originalHash ? installed : null;
            if (originalSource == null)
                throw new InvalidOperationException(
                    "游戏目录里找不到原版 amd_ags_x64.dll（本身和备份 amd_ags_x64_orig.dll 都不是原版），无法安全重装。\r\n" +
                    "请先在 EA App/Origin 中修复游戏文件，或从其他完整的 FIFA 19 复制原版 amd_ags_x64.dll 到游戏目录，再试一次。");

            // 1. Make sure the backup is the trusted original (taken before the main DLL is overwritten).
            if (backupHash != originalHash)
            {
                if (backupHash != null)
                {
                    var kept = KeepAside(backup, stamp);
                    actions.Add($"原备份 {BackupName} 不是原版，已改名保留为 {Path.GetFileName(kept)}");
                }
                File.Copy(originalSource, backup);
                if (FileUtil.Sha256(backup) != originalHash) throw new IOException("写入原版备份后校验失败。");
                actions.Add($"已重新备份原版到 {BackupName}");
            }
            else actions.Add($"原版备份 {BackupName} 校验正常");

            // 2. Rewrite the proxy.
            if (currentHash != null && currentHash != originalHash && currentHash != proxyHash)
            {
                var kept = KeepAside(installed, stamp);
                actions.Add($"原来的 {DllName} 既不是原版也不是代理，已改名保留为 {Path.GetFileName(kept)}");
            }
            ClearReadOnly(installed);
            File.Copy(packagedProxy, installed, true);
            if (FileUtil.Sha256(installed) != proxyHash)
            {
                File.Copy(backup, installed, true);
                throw new IOException("写入代理后校验失败，已恢复为原版 amd_ags_x64.dll。");
            }
            actions.Add($"已重新写入代理 {DllName}");

            // 3. CryptBase.dll
            var cryptBase = Path.Combine(gameDir, CryptBaseName);
            var marker = GetCryptBaseMarkerPath(gameDir);
            if (!File.Exists(cryptBase))
            {
                File.Copy(packagedCryptBase, cryptBase);
                FileUtil.WriteAllTextAtomic(marker, cryptBaseHash);
                actions.Add("已补装 CryptBase.dll");
            }
            else if (FileUtil.Sha256(cryptBase) != cryptBaseHash)
            {
                var kept = KeepAside(cryptBase, stamp);
                File.Copy(packagedCryptBase, cryptBase);
                // The previous file was not ours, so uninstall must not delete the replacement either.
                if (File.Exists(marker)) File.Delete(marker);
                actions.Add($"CryptBase.dll 与内置版本不一致，已替换（旧文件保留为 {Path.GetFileName(kept)}）");
            }
            else actions.Add("CryptBase.dll 校验正常");
            if (FileUtil.Sha256(cryptBase) != cryptBaseHash) throw new IOException("写入 CryptBase.dll 后校验失败。");

            var missing = MissingGameDlls(gameDir).ToList();
            if (missing.Count > 0)
                actions.Add("⚠ 仍缺少游戏自带 DLL：" + string.Join("、", missing) + "（需在 EA App/Origin 中修复游戏文件）");
            return actions;
        }

        private static string KeepAside(string path, string stamp)
        {
            var target = path + ".quickloader-" + stamp + ".bak";
            for (int i = 2; File.Exists(target); i++) target = path + ".quickloader-" + stamp + "-" + i + ".bak";
            ClearReadOnly(path);
            File.Move(path, target);
            return target;
        }

        private static void ClearReadOnly(string path)
        {
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        }

        private static string GetCryptBaseMarkerPath(string gameDir) =>
            Path.Combine(gameDir, CryptBaseMarkerName);

        private void EnsurePackagedProxyIntact()
        {
            if (FileUtil.Sha256(packagedProxy) != proxyHash)
                throw new InvalidOperationException("工具包里的代理 DLL 校验失败，请重新下载或用 source\\proxy\\Build.cmd 重新编译。");
        }

        private void EnsurePackagedCryptBaseIntact()
        {
            if (FileUtil.Sha256(packagedCryptBase) != cryptBaseHash)
                throw new InvalidOperationException("工具包里的 CryptBase.dll 校验失败，请重新解压完整工具包。");
        }
    }
}
