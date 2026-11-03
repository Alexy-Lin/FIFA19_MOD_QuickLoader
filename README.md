# FIFA 19 MOD 快速加载器（中文）

用中文界面管理并启动 FIFA 19 的 Frosty MOD：导入、排序、启动，一个窗口搞定。

**[⬇ 下载完整包](https://github.com/Alexy-Lin/FIFA19_MOD_QuickLoader/releases/latest)**（约 38 MB，已含内置 FMM 和运行库，解压即用）

> [English version ↓](#fifa-19-mod-quick-loader-english)

## 解决的痛点

| 以前 | 现在 |
|---|---|
| 每次进游戏都要开 Extreme Injector 手动注入 DLL | 自带免注入代理 DLL，游戏启动时自动加载 |
| 得先装 Frosty Mod Manager，还要指定目录、敲命令行 | 内置 FMM，解压就能用，所有操作都在中文界面里 |
| 每次玩都得先打开 FMM，等它把 MOD 列表加载出来，再点 Launch | 不用开 FMM，在工具里点一次"启动游戏"就直接进游戏 |
| 改分辨率、画质要另开 `fifaconfig.exe` 手改 ini | 顶部"游戏设置"弹窗，只写入你改动过的项 |

## 使用方法

1. **下载解压**：右键 zip → 属性 → 勾选"解除锁定" → 确定，再解压。
   不勾会弹"Windows 已保护你的电脑"；已经弹了的话点"更多信息 → 仍要运行"。
2. **双击 `FIFA19QuickLoader.exe`**。游戏装在 `C:\Program Files` 这类目录时，右键选择"以管理员身份运行"。
3. **设置游戏目录**：工具会自动查找 `FIFA19.exe`，找不到就手动选，设置后自动记住。
4. **首次安装代理**：提示时点"是"。工具只把游戏目录里的 `amd_ags_x64.dll` 改名备份、再放代理 DLL，**不修改 `FIFA19.exe`**；随时可以点"还原原版 DLL"完全复原。
5. **导入 MOD**：把 `.fbmod`、`.fbcollection` 或 `.zip` 拖进窗口，也可以点"导入 MOD…"。
6. **启用和排序**：左侧选中 MOD →"加入方案 →"；右侧勾选启用，用上移/下移调整顺序，**下面的覆盖上面的**。
7. **启动游戏**：点"▶ 启动游戏"。
   - 底部显示"⚡ 数据已是最新"：秒启，**全程不打开 FMM**。
   - 底部显示"需要生成"：只在**第一次**使用这个方案、或之后增删/换过 MOD 时出现。这时后台调用 FMM 生成一次 MOD 数据（首次约 5～15 分钟，和 FMM 自己生成的速度一样），完成后自动进游戏；之后数据没变就不会再生成，仍然是秒启。

> 说明：FMM 自己也是生成过一次数据后就快了，本工具的优势是**不用开 FMM**，不是靠跳过生成来省时间。

## 系统要求

Windows 10/11 x64 + FIFA 19 PC 版 + .NET Framework 4.7.2（Win10 1803 以后自带）。
MOD 需要的 5 个游戏 DLL 由游戏提供；代理要用的 `CryptBase.dll` 会自动从内置 FMM 复制。

## 说明

- 工具只改 FMM 的配置、并在游戏目录放一个代理 DLL，**不动游戏本体**；游戏更新后启动出错时，点"重新生成并启动"。
- 依赖开源项目 Frosty Toolsuite（GPLv3）的 FMM。FMM 约 50 MB，不放进 Git 仓库，完整包见 [Releases](https://github.com/Alexy-Lin/FIFA19_MOD_QuickLoader/releases/latest)。
- 源码在 `source\`：`QuickLoader` 是 C# WinForms（.NET Framework 4.7.2，`dotnet build -c Release`），`proxy` 是 C 代理 DLL（MSVC x64，运行 `Build.cmd`）。
- 仅供单机离线游玩使用，请自行准备游戏和 MOD 文件。

---

# FIFA 19 MOD Quick Loader (English)

A Chinese-language GUI for managing and launching FIFA 19 Frosty MODs: import, order, launch — all from one window.

**[⬇ Download the full package](https://github.com/Alexy-Lin/FIFA19_MOD_QuickLoader/releases/latest)** (~38 MB, includes the bundled FMM and runtime, unzip and run)

> The application UI is in Chinese; the on-screen labels are given below in English with the original Chinese label in parentheses.

## Why use it

| Before | Now |
|---|---|
| Manually injecting the DLL with Extreme Injector on every launch | A bundled no-injection proxy DLL that is loaded automatically when the game starts |
| Installing Frosty Mod Manager, pointing it at the game folder and typing command lines | FMM is bundled — unzip and run, everything happens in one GUI |
| Opening FMM every single time and waiting for the mod list to load before pressing Launch | FMM never has to be opened — press "Launch Game" and you're in |
| Editing ini files through `fifaconfig.exe` to change resolution or graphics | A "Game Settings" dialog that only writes the values you actually changed |

## Usage

1. **Download and unzip** — Right-click the zip → Properties → check "Unblock" → OK, then unzip. If you skip this, Windows shows "Windows protected your PC"; if it already did, click "More info → Run anyway".
2. **Double-click `FIFA19QuickLoader.exe`**. If the game is installed under `C:\Program Files`, right-click it and choose "Run as administrator".
3. **Set the game folder** — The tool searches for `FIFA19.exe` automatically; pick it manually if it can't find it. Your choice is remembered.
4. **Install the proxy (first run only)** — Click "Yes" when prompted. The tool only renames the game's `amd_ags_x64.dll` to a backup and drops in the proxy DLL; it **never modifies `FIFA19.exe`**. Click "还原原版 DLL" (Restore original DLL) at any time to undo it completely.
5. **Import MODs** — Drag `.fbmod`, `.fbcollection` or `.zip` files into the window, or click "导入 MOD…" (Import MOD…).
6. **Enable and order** — Select a mod on the left → "加入方案 →" (Add to profile →). On the right, tick the mods you want to enable and use the up/down buttons to reorder them — **lower entries override higher ones**.
7. **Launch** — Click "▶ 启动游戏" (Launch Game).
   - If the status bar says "⚡ MOD 数据已是最新" (mod data is up to date): instant launch, **FMM is never opened**.
   - If it says "需要生成" (generation needed): this happens only the **first time** you use a profile, or after you add, remove or replace mods. The loader then runs FMM in the background to generate the mod data once (about 5–15 minutes on the first run — the same speed as FMM itself), and the game starts automatically when it's done. Later launches reuse that data and stay instant.

> Note: FMM is already fast by itself once the mod data has been generated. The advantage of this tool is that **you never have to open FMM** — not that generation is skipped.

## Requirements

Windows 10/11 x64 + FIFA 19 (PC) + .NET Framework 4.7.2 (bundled with Windows 10 1803 and later).
The five game DLLs the mods need are provided by the game; `CryptBase.dll` for the proxy is copied from the bundled FMM automatically.

## Notes

- The tool only edits FMM's configuration and places one proxy DLL in the game folder — **the game itself is untouched**. If launches break after a game update, click "重新生成并启动" (Regenerate and launch).
- Built on Frosty Toolsuite's FMM (GPLv3). FMM is about 50 MB and is not committed to the Git repository; get the full package from [Releases](https://github.com/Alexy-Lin/FIFA19_MOD_QuickLoader/releases/latest).
- Source lives in `source\`: `QuickLoader` is the C# WinForms app (.NET Framework 4.7.2, `dotnet build -c Release`), and `proxy` is the C proxy DLL (MSVC x64, run `Build.cmd`).
- For single-player offline use only; supply your own game and mod files.
