# FIFA 19 MOD 快速加载器（中文）

用中文界面管理并启动 FIFA 19 的 Frosty MOD：导入、排序、启动，一个窗口搞定。

**[⬇ 下载完整包](https://github.com/Alexy-Lin/FIFA19_MOD_QuickLoader/releases/latest)**（约 38 MB，已含内置 FMM 和运行库，解压即用）

## 解决的痛点

| 以前 | 现在 |
|---|---|
| 每次进游戏都要开 Extreme Injector 手动注入 DLL | 自带免注入代理 DLL，游戏启动时自动加载 |
| 得先装 Frosty Mod Manager，还要指定目录、敲命令行 | 内置 FMM，解压就能用，所有操作都在中文界面里 |
| 每次启动都等 FMM 重新生成 MOD 数据，几分钟起步 | MOD 没变直接秒启，变了才在后台重新生成 |
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
   - 底部显示"⚡ 数据已是最新"：秒启。
   - 底部显示"需要生成"：后台运行 FMM 生成数据，首次约 5～15 分钟，完成后自动进入游戏。

## 系统要求

Windows 10/11 x64 + FIFA 19 PC 版 + .NET Framework 4.7.2（Win10 1803 以后自带）。
MOD 需要的 5 个游戏 DLL 由游戏提供；代理要用的 `CryptBase.dll` 会自动从内置 FMM 复制。

## 说明

- 工具只改 FMM 的配置、并在游戏目录放一个代理 DLL，**不动游戏本体**；游戏更新后启动出错时，点"重新生成并启动"。
- 依赖开源项目 Frosty Toolsuite（GPLv3）的 FMM。FMM 约 50 MB，不放进 Git 仓库，完整包见 [Releases](https://github.com/Alexy-Lin/FIFA19_MOD_QuickLoader/releases/latest)。
- 源码在 `source\`：`QuickLoader` 是 C# WinForms（.NET Framework 4.7.2，`dotnet build -c Release`），`proxy` 是 C 代理 DLL（MSVC x64，运行 `Build.cmd`）。
- 仅供单机离线游玩使用，请自行准备游戏和 MOD 文件。
