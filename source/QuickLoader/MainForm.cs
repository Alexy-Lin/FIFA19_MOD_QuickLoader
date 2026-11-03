using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FIFA19QuickLoader
{
    internal sealed class MainForm : Form
    {
        private const string AppTitle = "FIFA 19 MOD 快速加载器";
        private static readonly string ProxyPromptDismissedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FIFA19QuickLoader", "proxy-install-prompt-dismissed");

        private readonly string appDir;
        private readonly string fmmDir;
        private FrostyConfig cfg;
        private ProxyManager proxy;
        private string proxyLoadError;
        private List<ModFile> library = new List<ModFile>();
        private Dictionary<string, ModFile> libraryByName = new Dictionary<string, ModFile>(StringComparer.OrdinalIgnoreCase);
        private bool dirty;
        private bool suppressEvents;
        private bool busy;

        private readonly Label lblGame = new Label { AutoSize = true };
        private readonly Label lblFmm = new Label { AutoSize = true };
        private readonly Label lblProxy = new Label { AutoSize = true };
        private readonly Button btnChangeGame = new Button { Text = "更改游戏目录…", AutoSize = true };
        private readonly Button btnDisplay = new Button { Text = "游戏设置…", AutoSize = true };
        private readonly Button btnProxyInstall = new Button { Text = "安装免注入代理", AutoSize = true };
        private readonly Button btnProxyUninstall = new Button { Text = "还原原版 DLL", AutoSize = true };
        private readonly Button btnProxyReinstall = new Button { Text = "强制重装代理", AutoSize = true };

        private readonly TextBox txtSearch = new TextBox { Dock = DockStyle.Fill };
        private readonly ListView lvLibrary = NewListView(false);
        private readonly Button btnImport = new Button { Text = "导入 MOD…", AutoSize = true };
        private readonly Button btnAdd = new Button { Text = "加入方案 →", AutoSize = true };
        private readonly Button btnDeleteMod = new Button { Text = "删除 MOD 文件", AutoSize = true };
        private readonly Button btnOpenMods = new Button { Text = "打开 MOD 文件夹", AutoSize = true };

        private readonly ComboBox cmbPack = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly Button btnNewPack = new Button { Text = "新建", AutoSize = true };
        private readonly Button btnCopyPack = new Button { Text = "复制", AutoSize = true };
        private readonly Button btnDeletePack = new Button { Text = "删除", AutoSize = true };
        private readonly ListView lvPack = NewListView(true);
        private readonly Button btnTop = new Button { Text = "置顶", AutoSize = true };
        private readonly Button btnUp = new Button { Text = "↑ 上移", AutoSize = true };
        private readonly Button btnDown = new Button { Text = "↓ 下移", AutoSize = true };
        private readonly Button btnBottom = new Button { Text = "置底", AutoSize = true };
        private readonly Button btnRemove = new Button { Text = "移出方案", AutoSize = true };

        private readonly TextBox txtDetails = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = SystemColors.Window,
        };
        private readonly Label lblPlan = new Label { AutoSize = true, Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold) };
        private readonly Label lblStatus = new Label { AutoSize = true, ForeColor = Color.DimGray };
        private readonly Button btnRefresh = new Button { Text = "刷新", AutoSize = true };
        private readonly Button btnSave = new Button { Text = "保存", AutoSize = true };
        private readonly Button btnRebuild = new Button { Text = "重新生成并启动", AutoSize = true };
        private readonly Button btnLaunch = new Button
        {
            Text = "▶ 启动游戏", AutoSize = true, Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold), Padding = new Padding(16, 4, 16, 4),
        };

        public MainForm(string appDirectory)
        {
            appDir = appDirectory;
            fmmDir = Path.Combine(appDir, "FMM");
            Text = AppTitle;
            Font = new Font("Microsoft YaHei UI", 9F);
            AutoScaleMode = AutoScaleMode.None;
            using (var g = CreateGraphics()) scale = Math.Max(1f, g.DpiX / 96f);
            var work = Screen.FromPoint(Cursor.Position).WorkingArea;
            ClientSize = new Size(S(1180), S(760));
            Size = new Size(Math.Min(Width, work.Width), Math.Min(Height, work.Height));
            MinimumSize = new Size(Math.Min(S(900), work.Width), Math.Min(S(600), work.Height));
            StartPosition = FormStartPosition.Manual;
            Location = new Point(work.X + (work.Width - Width) / 2, work.Y + (work.Height - Height) / 2);
            AllowDrop = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException) { }

            BuildLayout();
            WireEvents();
        }

        private readonly float scale;

        private int S(int logicalPixels) => (int)Math.Round(logicalPixels * scale);

        /// <summary>Gives the name column whatever width the other columns leave free.</summary>
        private static void FitNameColumn(ListView lv, int nameColumn)
        {
            var others = 0;
            for (int i = 0; i < lv.Columns.Count; i++)
                if (i != nameColumn) others += lv.Columns[i].Width;
            var w = lv.ClientSize.Width - others - 4;
            if (w > 80) lv.Columns[nameColumn].Width = w;
        }

        private static ListView NewListView(bool checkBoxes) => new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            CheckBoxes = checkBoxes,
            Dock = DockStyle.Fill,
            GridLines = false,
        };

        private static FlowLayoutPanel Flow(params Control[] controls)
        {
            var f = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
            f.Controls.AddRange(controls);
            return f;
        }

        private void BuildLayout()
        {
            var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(8, 6, 8, 0) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.Controls.Add(Flow(lblGame, btnChangeGame, btnDisplay), 0, 0);
            header.Controls.Add(lblFmm, 0, 1);
            header.Controls.Add(Flow(lblProxy, btnProxyInstall, btnProxyUninstall, btnProxyReinstall), 0, 2);
            foreach (var l in new[] { lblGame, lblFmm, lblProxy }) l.Margin = new Padding(3, 7, 3, 3);

            lvLibrary.Columns.Add("MOD 名称", S(260));
            lvLibrary.Columns.Add("版本", S(80));
            lvLibrary.Columns.Add("作者", S(110));
            lvLibrary.Columns.Add("分类", S(110));
            lvLibrary.Resize += (s, e) => FitNameColumn(lvLibrary, 0);
            var libLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
            libLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            libLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            libLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            libLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            libLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            libLayout.Controls.Add(new Label { Text = "搜索：", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, 0);
            libLayout.Controls.Add(txtSearch, 1, 0);
            libLayout.Controls.Add(lvLibrary, 0, 1);
            libLayout.SetColumnSpan(lvLibrary, 2);
            var libButtons = Flow(btnImport, btnAdd, btnDeleteMod, btnOpenMods);
            libLayout.Controls.Add(libButtons, 0, 2);
            libLayout.SetColumnSpan(libButtons, 2);
            var grpLib = new GroupBox { Text = "MOD 库（可直接把 .fbmod / .zip 拖进窗口导入）", Dock = DockStyle.Fill, Padding = new Padding(6) };
            grpLib.Controls.Add(libLayout);

            lvPack.Columns.Add("顺序", S(56));
            lvPack.Columns.Add("MOD 名称", S(280));
            lvPack.Columns.Add("状态", S(170));
            lvPack.Resize += (s, e) => FitNameColumn(lvPack, 1);
            var packLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
            packLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            packLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            packLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            packLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            packLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            packLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            packLayout.Controls.Add(new Label { Text = "方案：", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, 0);
            var packTop = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(0) };
            packTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            packTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            packTop.Controls.Add(cmbPack, 0, 0);
            packTop.Controls.Add(Flow(btnNewPack, btnCopyPack, btnDeletePack), 1, 0);
            packLayout.Controls.Add(packTop, 1, 0);
            packLayout.Controls.Add(lvPack, 0, 1);
            packLayout.SetColumnSpan(lvPack, 2);
            var packButtons = Flow(btnTop, btnUp, btnDown, btnBottom, btnRemove);
            packLayout.Controls.Add(packButtons, 0, 2);
            packLayout.SetColumnSpan(packButtons, 2);
            var hint = new Label
            {
                Text = "勾选 = 启用。列表从上到下依次应用，两个 MOD 改了同一内容时，下面的覆盖上面的。",
                AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(3, 2, 3, 3),
            };
            packLayout.Controls.Add(hint, 0, 3);
            packLayout.SetColumnSpan(hint, 2);
            var grpPack = new GroupBox { Text = "启用方案", Dock = DockStyle.Fill, Padding = new Padding(6) };
            grpPack.Controls.Add(packLayout);

            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterWidth = 6 };
            split.Panel1.Controls.Add(grpLib);
            split.Panel2.Controls.Add(grpPack);
            Load += (s, e) => split.SplitterDistance = split.Width / 2;

            var grpDetails = new GroupBox { Text = "MOD 详情", Dock = DockStyle.Fill, Padding = new Padding(6) };
            grpDetails.Controls.Add(txtDetails);

            var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 2, Padding = new Padding(8, 4, 8, 8) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var planBox = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            planBox.Controls.Add(lblPlan);
            planBox.Controls.Add(lblStatus);
            bottom.Controls.Add(planBox, 0, 0);
            var actions = Flow(btnRefresh, btnSave, btnRebuild, btnLaunch);
            actions.Dock = DockStyle.None;
            actions.Anchor = AnchorStyles.Right;
            actions.WrapContents = false;
            foreach (Control c in actions.Controls) c.Anchor = AnchorStyles.None;
            bottom.Controls.Add(actions, 1, 0);

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8, 4, 8, 0) };
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 75));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            main.Controls.Add(split, 0, 0);
            main.Controls.Add(grpDetails, 0, 1);

            Controls.Add(main);
            Controls.Add(bottom);
            Controls.Add(header);
            AcceptButton = btnLaunch;
        }

        private void WireEvents()
        {
            Shown += (s, e) => InitialLoad();
            Activated += (s, e) => CheckExternalChanges();
            FormClosing += OnFormClosing;
            DragEnter += (s, e) => e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) && !busy ? DragDropEffects.Copy : DragDropEffects.None;
            DragDrop += (s, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] files) ImportFiles(files); };

            btnChangeGame.Click += (s, e) => ChangeGameDirectory();
            btnDisplay.Click += (s, e) => EditDisplaySettings();
            btnProxyInstall.Click += (s, e) => ProxyAction(true);
            btnProxyUninstall.Click += (s, e) => ProxyAction(false);
            btnProxyReinstall.Click += (s, e) => ForceReinstallProxy();

            txtSearch.TextChanged += (s, e) => PopulateLibrary();
            lvLibrary.SelectedIndexChanged += (s, e) => { if (lvLibrary.SelectedItems.Count > 0) ShowDetails(lvLibrary.SelectedItems[0].Tag as ModFile, null); };
            lvLibrary.DoubleClick += (s, e) => AddSelectedToPack();
            btnImport.Click += (s, e) => BrowseImport();
            btnAdd.Click += (s, e) => AddSelectedToPack();
            btnDeleteMod.Click += (s, e) => DeleteSelectedMods();
            btnOpenMods.Click += (s, e) => OpenModsFolder();

            cmbPack.SelectedIndexChanged += (s, e) =>
            {
                if (suppressEvents || cfg == null) return;
                cfg.SelectedPack = (string)cmbPack.SelectedItem;
                MarkDirty();
                PopulatePack();
            };
            btnNewPack.Click += (s, e) => CreatePack(false);
            btnCopyPack.Click += (s, e) => CreatePack(true);
            btnDeletePack.Click += (s, e) => DeleteCurrentPack();

            lvPack.ItemChecked += (s, e) =>
            {
                if (suppressEvents) return;
                ((PackEntry)e.Item.Tag).Enabled = e.Item.Checked;
                MarkDirty();
                UpdatePlan();
            };
            lvPack.SelectedIndexChanged += (s, e) =>
            {
                if (lvPack.SelectedItems.Count == 0) return;
                var entry = (PackEntry)lvPack.SelectedItems[0].Tag;
                libraryByName.TryGetValue(entry.FileName, out var mod);
                ShowDetails(mod, entry.FileName);
            };
            lvPack.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) RemoveSelectedFromPack(); };
            btnTop.Click += (s, e) => MoveSelected(int.MinValue);
            btnUp.Click += (s, e) => MoveSelected(-1);
            btnDown.Click += (s, e) => MoveSelected(1);
            btnBottom.Click += (s, e) => MoveSelected(int.MaxValue);
            btnRemove.Click += (s, e) => RemoveSelectedFromPack();

            btnRefresh.Click += (s, e) =>
            {
                if (dirty && !Confirm("有未保存的修改，刷新会丢弃它们。继续吗？")) return;
                ReloadAll();
            };
            btnSave.Click += (s, e) => TrySave();
            btnLaunch.Click += async (s, e) => await LaunchAsync(false);
            btnRebuild.Click += async (s, e) => await LaunchAsync(true);
        }

        // ---------- loading ----------

        private void InitialLoad()
        {
            try
            {
                var unblocked = FileUtil.UnblockDirectory(appDir);
                if (unblocked > 0) SetStatus($"已为 {unblocked} 个工具文件解除“来自互联网”锁定。");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                SetStatus("解除文件锁定失败（如生成 MOD 出错，请右键工具包 zip → 属性 → 解除锁定后重新解压）：" + ex.Message);
            }

            try
            {
                proxy = new ProxyManager(appDir);
            }
            catch (Exception ex) when (ex is IOException || ex is FormatException)
            {
                proxyLoadError = ex.Message;
            }

            if (!File.Exists(Path.Combine(fmmDir, GameLauncher.FmmProcessName + ".exe")))
            {
                ShowError("工具目录下缺少内置的 FMM（FMM\\FrostyModManager.exe）。\r\n请重新解压完整的工具包。");
                Close();
                return;
            }

            if (!FrostyConfig.HasValidGamePath())
            {
                var detected = FrostyConfig.DetectGameDirectory();
                if (detected != null)
                {
                    if (Confirm($"已自动找到 FIFA19.exe：\r\n{Path.Combine(detected, GameLauncher.GameProcessName + ".exe")}\r\n\r\n使用这个游戏目录吗？"))
                        ApplyGameDirectory(detected);
                    else
                        PromptGameDirectory(detected);
                }
                else
                {
                    ShowInfo("首次使用：没有自动找到 FIFA19.exe，请选择 FIFA 19 的游戏程序。\r\n\r\n选择后会自动记住，以后无需重复设置。");
                    PromptGameDirectory();
                }
            }
            ReloadAll();
            PromptToInstallProxyIfNeeded();
        }

        private void PromptGameDirectory(string suggestedDirectory = null)
        {
            var initialDirectory = Directory.Exists(suggestedDirectory) ? suggestedDirectory
                : Directory.Exists(cfg?.GamePath) ? cfg.GamePath
                : "";
            using (var dlg = new OpenFileDialog
            {
                Title = "请选择 FIFA 19 的游戏程序 FIFA19.exe",
                Filter = "FIFA 19 (FIFA19.exe)|FIFA19.exe",
                InitialDirectory = initialDirectory,
                CheckFileExists = true,
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK) ApplyGameDirectory(Path.GetDirectoryName(dlg.FileName));
            }
        }

        private void ApplyGameDirectory(string dir)
        {
            if (FileUtil.IsProcessRunning(GameLauncher.FmmProcessName))
            {
                ShowError("FMM 正在运行，请先关闭它再设置游戏目录。");
                return;
            }
            try
            {
                FrostyConfig.SetGamePath(dir);
                SetStatus("游戏目录已设置为 " + dir);
            }
            catch (Exception ex) when (ex is IOException || ex is FormatException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
            {
                ShowError("设置游戏目录失败：\r\n" + ex.Message);
            }
        }

        private void RefreshDisplayButton()
        {
            try
            {
                var s = GameSettings.Load(cfg?.GamePath);
                btnDisplay.Text = $"游戏设置（{s.Width}x{s.Height} {GameSettings.ModeName(s.Mode)}" + (s.Exists ? "" : "，默认") + "）…";
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                btnDisplay.Text = "游戏设置…";
            }
        }

        private void EditDisplaySettings()
        {
            GameSettings settings;
            try
            {
                settings = GameSettings.Load(cfg?.GamePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                ShowError("读取游戏设置失败：\r\n" + ex.Message);
                return;
            }
            if (!DisplaySettingsDialog.Show(this, settings)) return;
            RefreshDisplayButton();
            SetStatus(FileUtil.IsProcessRunning(GameLauncher.GameProcessName)
                ? $"游戏设置已保存（{settings.Width}x{settings.Height} {GameSettings.ModeName(settings.Mode)}），游戏正在运行，重启游戏后生效。"
                : $"游戏设置已保存：{settings.Width}x{settings.Height} {GameSettings.ModeName(settings.Mode)}。");
        }

        private void ChangeGameDirectory()
        {
            if (dirty && !Confirm("有未保存的修改，更改游戏目录会丢弃它们。继续吗？")) return;
            PromptGameDirectory();
            ReloadAll();
        }

        private void ReloadAll()
        {
            try
            {
                cfg = FrostyConfig.Load();
            }
            catch (Exception ex) when (ex is IOException || ex is FormatException || ex is UnauthorizedAccessException)
            {
                cfg = null;
                ShowError("读取 FMM 配置失败：\r\n" + ex.Message);
            }
            dirty = false;
            RefreshHeader();
            RefreshLibrary();
            PopulatePacks();
            UpdateTitle();
            UpdateButtons();
        }

        private string ModsDir => cfg?.GetModsDirectory(fmmDir);

        private void RefreshHeader()
        {
            lblGame.Text = cfg == null ? "游戏目录：（未设置）" : "游戏目录：" + cfg.GamePath;
            RefreshDisplayButton();
            var exe = Path.Combine(fmmDir, GameLauncher.FmmProcessName + ".exe");
            var ver = File.Exists(exe) ? FileVersionInfo.GetVersionInfo(exe).FileVersion : "缺失";
            lblFmm.Text = $"MOD 生成引擎：内置 Frosty Mod Manager {ver}（在后台自动运行，无需单独打开）";
            RefreshProxyStatus();
        }

        private void RefreshProxyStatus()
        {
            btnProxyInstall.Visible = btnProxyUninstall.Visible = btnProxyReinstall.Visible = false;
            if (proxy == null)
            {
                lblProxy.Text = "免注入代理：" + proxyLoadError;
                lblProxy.ForeColor = Color.Firebrick;
                return;
            }
            if (cfg == null || !File.Exists(Path.Combine(cfg.GamePath, GameLauncher.GameProcessName + ".exe")))
            {
                lblProxy.Text = "免注入代理：找不到游戏程序 FIFA19.exe，请点“更改游戏目录…”重新选择";
                lblProxy.ForeColor = Color.Firebrick;
                return;
            }
            try
            {
                var state = proxy.GetState(cfg.GamePath, out var detail);
                var missingGameDlls = proxy.MissingGameDlls(cfg.GamePath).ToList();
                var missingCryptBase = proxy.IsCryptBaseMissing(cfg.GamePath);
                var text = "免注入代理：" + detail;
                if (missingGameDlls.Count > 0)
                    text += "\r\n  ⚠ 缺少游戏自带 DLL：" + string.Join("、", missingGameDlls) + "（请在 EA App/Origin 中修复游戏文件）";
                if (missingCryptBase)
                    text += "\r\n  ⚠ 缺少 CryptBase.dll（点击右侧按钮自动安装）";
                lblProxy.Text = text;
                lblProxy.ForeColor = state == ProxyState.Installed && missingGameDlls.Count == 0 && !missingCryptBase ? Color.ForestGreen
                    : state == ProxyState.Unknown ? Color.Firebrick : Color.DarkOrange;
                btnProxyInstall.Text = state == ProxyState.Installed ? "补装 CryptBase.dll" : "安装免注入代理";
                btnProxyInstall.Visible = state == ProxyState.NotInstalled ||
                                          (state == ProxyState.Installed && missingCryptBase);
                btnProxyUninstall.Visible = state == ProxyState.Installed;
                btnProxyReinstall.Visible = true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                lblProxy.Text = "免注入代理：无法读取游戏目录 - " + ex.Message;
                lblProxy.ForeColor = Color.Firebrick;
            }
        }

        private void RefreshLibrary()
        {
            library = cfg == null ? new List<ModFile>() : ModFile.ScanDirectory(ModsDir);
            libraryByName = new Dictionary<string, ModFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in library) libraryByName[m.FileName] = m;
            PopulateLibrary();
        }

        private void PopulateLibrary()
        {
            var keyword = txtSearch.Text.Trim();
            lvLibrary.BeginUpdate();
            lvLibrary.Items.Clear();
            foreach (var m in library)
            {
                if (keyword.Length > 0 && !Matches(m, keyword)) continue;
                var item = new ListViewItem(m.DisplayTitle) { Tag = m, ToolTipText = m.FileName };
                item.SubItems.Add(m.Version ?? "");
                item.SubItems.Add(m.Author ?? "");
                item.SubItems.Add(m.Category ?? "");
                if (m.IsForOtherGame) item.ForeColor = Color.Firebrick;
                else if (!m.HeaderValid) item.ForeColor = Color.DimGray;
                lvLibrary.Items.Add(item);
            }
            lvLibrary.EndUpdate();
            var grp = lvLibrary.Parent?.Parent as GroupBox;
            if (grp != null) grp.Text = $"MOD 库（共 {library.Count} 个，可直接把 .fbmod / .zip 拖进窗口导入）";
        }

        private static bool Matches(ModFile m, string keyword) =>
            new[] { m.DisplayTitle, m.FileName, m.Author, m.Category }
                .Any(s => s != null && s.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) >= 0);

        private ModPack CurrentPack => cfg?.FindPack(cfg.SelectedPack);

        private void PopulatePacks()
        {
            suppressEvents = true;
            cmbPack.Items.Clear();
            if (cfg != null)
            {
                foreach (var p in cfg.Packs) cmbPack.Items.Add(p.Name);
                cmbPack.SelectedItem = CurrentPack?.Name;
            }
            suppressEvents = false;
            PopulatePack();
        }

        private void PopulatePack(IEnumerable<PackEntry> keepSelected = null)
        {
            var selected = new HashSet<PackEntry>(keepSelected ?? Enumerable.Empty<PackEntry>());
            suppressEvents = true;
            lvPack.BeginUpdate();
            lvPack.Items.Clear();
            var pack = CurrentPack;
            if (pack != null)
            {
                var n = 1;
                foreach (var e in pack.Entries)
                {
                    libraryByName.TryGetValue(e.FileName, out var mod);
                    var item = new ListViewItem((n++).ToString()) { Tag = e, Checked = e.Enabled, ToolTipText = e.FileName };
                    item.SubItems.Add(mod?.DisplayTitle ?? Path.GetFileNameWithoutExtension(e.FileName));
                    string status;
                    if (mod == null) { status = "⚠ 文件缺失（启动时会跳过）"; item.ForeColor = Color.Firebrick; }
                    else if (mod.IsForOtherGame) { status = "⚠ 不是 FIFA 19 的 MOD"; item.ForeColor = Color.Firebrick; }
                    else status = mod.IsCollection ? "合集" : (mod.HeaderValid ? "正常" : "旧格式");
                    item.SubItems.Add(status);
                    item.Selected = selected.Contains(e);
                    lvPack.Items.Add(item);
                }
            }
            lvPack.EndUpdate();
            suppressEvents = false;
            UpdatePlan();
        }

        private void ShowDetails(ModFile mod, string missingName)
        {
            var sb = new StringBuilder();
            if (mod == null)
            {
                sb.AppendLine($"文件：{missingName}");
                sb.AppendLine("⚠ MOD 库里找不到这个文件。它可能被删除或改名了；启动时 FMM 会跳过它。可以“移出方案”，或重新导入。");
            }
            else
            {
                sb.AppendLine($"名称：{mod.DisplayTitle}");
                if (!string.IsNullOrEmpty(mod.Version)) sb.AppendLine($"版本：{mod.Version}");
                if (!string.IsNullOrEmpty(mod.Author)) sb.AppendLine($"作者：{mod.Author}");
                if (!string.IsNullOrEmpty(mod.Category)) sb.AppendLine($"分类：{mod.Category}");
                sb.AppendLine($"文件：{mod.FileName}（{mod.Size / 1048576.0:0.0} MB，修改于 {mod.LastWriteTimeUtc.ToLocalTime():yyyy-MM-dd HH:mm}）");
                if (!string.IsNullOrEmpty(mod.Link)) sb.AppendLine($"链接：{mod.Link}");
                if (mod.Problem != null) sb.AppendLine("说明：" + mod.Problem);
                var usedBy = cfg?.Packs.Where(p => p.Entries.Any(e => e.FileName.Equals(mod.FileName, StringComparison.OrdinalIgnoreCase)))
                    .Select(p => p.Name).ToList();
                if (usedBy != null && usedBy.Count > 0) sb.AppendLine("所在方案：" + string.Join("、", usedBy));
                if (!string.IsNullOrWhiteSpace(mod.Description))
                {
                    sb.AppendLine();
                    sb.AppendLine(mod.Description.Replace("\r\n", "\n").Replace("\n", "\r\n"));
                }
            }
            txtDetails.Text = sb.ToString();
        }

        private void UpdatePlan()
        {
            var pack = CurrentPack;
            if (cfg == null || pack == null)
            {
                lblPlan.Text = "未读取到 FMM 配置";
                lblPlan.ForeColor = Color.Firebrick;
                return;
            }
            var plan = GameLauncher.Evaluate(cfg, pack, ModsDir, libraryByName);
            var count = $"方案“{pack.Name}”启用 {plan.EnabledCount} 个 MOD";
            if (plan.MissingMods.Count > 0) count += $"（{plan.MissingMods.Count} 个文件缺失）";
            if (plan.CanFastLaunch)
            {
                lblPlan.Text = $"⚡ {count}：MOD 数据已是最新，点“启动游戏”秒启";
                lblPlan.ForeColor = Color.ForestGreen;
            }
            else
            {
                lblPlan.Text = $"⚙ {count}：{plan.Reason}，启动时会自动调用 FMM 生成（可能需要几分钟）";
                lblPlan.ForeColor = Color.DarkOrange;
            }
        }

        private void SetStatus(string text) => lblStatus.Text = text;

        private void MarkDirty()
        {
            dirty = true;
            UpdateTitle();
        }

        private void UpdateTitle() => Text = AppTitle + (dirty ? " *（未保存）" : "");

        private void UpdateButtons()
        {
            var ok = cfg != null && !busy;
            foreach (var c in new Control[]
            {
                btnImport, btnAdd, btnDeleteMod, btnOpenMods, cmbPack, btnNewPack, btnCopyPack, btnDeletePack, lvPack, lvLibrary,
                btnTop, btnUp, btnDown, btnBottom, btnRemove, btnSave, btnLaunch, btnRebuild, btnProxyInstall, btnProxyUninstall, btnProxyReinstall,
            })
                c.Enabled = ok;
            btnRefresh.Enabled = btnChangeGame.Enabled = !busy;
        }

        private void CheckExternalChanges()
        {
            if (busy || cfg == null || !cfg.ChangedOnDisk) return;
            if (dirty)
            {
                SetStatus("⚠ FMM 配置已被其他程序修改。保存会失败，请先点“刷新”（会丢弃本工具里未保存的修改）。");
                return;
            }
            ReloadAll();
            SetStatus("检测到 FMM 配置有变化，已自动刷新。");
        }

        // ---------- library actions ----------

        private void BrowseImport()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "导入 MOD",
                Filter = "FIFA 19 MOD (*.fbmod;*.fbcollection;*.zip)|*.fbmod;*.fbcollection;*.zip|所有文件 (*.*)|*.*",
                Multiselect = true,
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK) ImportFiles(dlg.FileNames);
            }
        }

        private void ImportFiles(string[] files)
        {
            if (cfg == null || busy) return;
            ImportResult result;
            Cursor = Cursors.WaitCursor;
            try
            {
                result = ModImporter.Import(files, ModsDir, name =>
                {
                    var r = MessageBox.Show(this, $"MOD 库里已有“{name}”。\r\n\r\n是 = 覆盖    否 = 跳过    取消 = 停止导入",
                        AppTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    return r == DialogResult.Yes ? OverwriteChoice.Overwrite : r == DialogResult.No ? OverwriteChoice.Skip : OverwriteChoice.Cancel;
                });
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            RefreshLibrary();
            var pack = CurrentPack;
            var added = 0;
            if (pack != null)
            {
                foreach (var name in result.Imported)
                {
                    if (pack.Entries.Any(e => e.FileName.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
                    pack.Entries.Add(new PackEntry(name, true));
                    added++;
                }
            }
            if (added > 0) MarkDirty();
            PopulatePack();

            var msg = $"导入 {result.Imported.Count} 个 MOD" + (added > 0 ? $"，其中 {added} 个已加入方案“{pack.Name}”并启用" : "");
            SetStatus(msg + "。");
            if (result.Messages.Count > 0) MessageBox.Show(this, msg + "\r\n\r\n" + string.Join("\r\n", result.Messages), AppTitle);
        }

        private List<ModFile> SelectedLibraryMods() => lvLibrary.SelectedItems.Cast<ListViewItem>().Select(i => (ModFile)i.Tag).ToList();

        private void AddSelectedToPack()
        {
            var pack = CurrentPack;
            if (pack == null) return;
            var added = new List<PackEntry>();
            foreach (var m in SelectedLibraryMods())
            {
                if (m.IsForOtherGame) continue;
                if (pack.Entries.Any(e => e.FileName.Equals(m.FileName, StringComparison.OrdinalIgnoreCase))) continue;
                var entry = new PackEntry(m.FileName, true);
                pack.Entries.Add(entry);
                added.Add(entry);
            }
            if (added.Count == 0)
            {
                SetStatus("选中的 MOD 已在方案里（或不是 FIFA 19 的 MOD）。");
                return;
            }
            MarkDirty();
            PopulatePack(added);
            SetStatus($"已把 {added.Count} 个 MOD 加入方案“{pack.Name}”末尾（优先级最高）。");
        }

        private void DeleteSelectedMods()
        {
            var mods = SelectedLibraryMods();
            if (mods.Count == 0) return;
            if (!Confirm($"确定要从硬盘删除这 {mods.Count} 个 MOD 文件吗？\r\n\r\n" +
                         string.Join("\r\n", mods.Select(m => "· " + m.DisplayTitle)) +
                         "\r\n\r\n它们也会从所有方案里移除。此操作不可撤销。")) return;
            var errors = new List<string>();
            foreach (var m in mods)
            {
                try
                {
                    foreach (var f in ModFile.GetCompanionFiles(m.FullPath).ToList()) File.Delete(f);
                    foreach (var p in cfg.Packs) p.Entries.RemoveAll(e => e.FileName.Equals(m.FileName, StringComparison.OrdinalIgnoreCase));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    errors.Add(m.FileName + "：" + ex.Message);
                }
            }
            MarkDirty();
            RefreshLibrary();
            PopulatePack();
            TrySave();
            if (errors.Count > 0) ShowError("部分文件删除失败：\r\n" + string.Join("\r\n", errors));
        }

        private void OpenModsFolder()
        {
            Directory.CreateDirectory(ModsDir);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + ModsDir + "\"") { UseShellExecute = true });
        }

        // ---------- pack actions ----------

        private void CreatePack(bool copy)
        {
            var source = CurrentPack;
            var name = InputDialog.Show(this, copy ? "复制方案" : "新建方案",
                copy ? $"为“{source?.Name}”的副本起个名字：" : "新方案的名字（例如：比赛画面、全面补丁）：",
                copy ? source?.Name + " 副本" : "");
            if (name == null) return;
            name = name.Trim();
            if (!FileUtil.IsValidPackName(name))
            {
                ShowError("名字不能为空，也不能包含 \\ / : * ? \" < > | 等字符。");
                return;
            }
            if (cfg.FindPack(name) != null)
            {
                ShowError("已经有同名方案了。");
                return;
            }
            var pack = copy && source != null ? source.Clone(name) : new ModPack(name);
            cfg.Packs.Add(pack);
            cfg.SelectedPack = name;
            MarkDirty();
            PopulatePacks();
        }

        private void DeleteCurrentPack()
        {
            var pack = CurrentPack;
            if (pack == null) return;
            if (cfg.Packs.Count <= 1)
            {
                ShowError("至少要保留一个方案。");
                return;
            }
            if (!Confirm($"确定删除方案“{pack.Name}”吗？\r\nMOD 文件本身不会被删除。")) return;
            cfg.Packs.Remove(pack);
            cfg.SelectedPack = cfg.Packs[0].Name;
            MarkDirty();
            PopulatePacks();
            SetStatus($"已删除方案“{pack.Name}”。它以前生成的数据仍在游戏目录 ModData\\{pack.Name}，可手动删除。");
        }

        private List<PackEntry> SelectedPackEntries() =>
            lvPack.SelectedItems.Cast<ListViewItem>().OrderBy(i => i.Index).Select(i => (PackEntry)i.Tag).ToList();

        private void MoveSelected(int delta)
        {
            var pack = CurrentPack;
            var selected = SelectedPackEntries();
            if (pack == null || selected.Count == 0) return;
            var list = pack.Entries;
            var set = new HashSet<PackEntry>(selected);
            if (delta == int.MinValue || delta == int.MaxValue)
            {
                var rest = list.Where(e => !set.Contains(e)).ToList();
                list.Clear();
                if (delta == int.MinValue) { list.AddRange(selected); list.AddRange(rest); }
                else { list.AddRange(rest); list.AddRange(selected); }
            }
            else if (delta < 0)
            {
                for (int i = 1; i < list.Count; i++)
                    if (set.Contains(list[i]) && !set.Contains(list[i - 1])) Swap(list, i, i - 1);
            }
            else
            {
                for (int i = list.Count - 2; i >= 0; i--)
                    if (set.Contains(list[i]) && !set.Contains(list[i + 1])) Swap(list, i, i + 1);
            }
            MarkDirty();
            PopulatePack(selected);
            lvPack.SelectedItems.Cast<ListViewItem>().FirstOrDefault()?.EnsureVisible();
        }

        private static void Swap(List<PackEntry> list, int a, int b)
        {
            var t = list[a];
            list[a] = list[b];
            list[b] = t;
        }

        private void RemoveSelectedFromPack()
        {
            var pack = CurrentPack;
            var selected = SelectedPackEntries();
            if (pack == null || selected.Count == 0) return;
            foreach (var e in selected) pack.Entries.Remove(e);
            MarkDirty();
            PopulatePack();
            SetStatus($"已从方案移出 {selected.Count} 个 MOD（文件仍在 MOD 库里）。");
        }

        // ---------- save / launch ----------

        private bool TrySave()
        {
            if (cfg == null) return false;
            if (FileUtil.IsProcessRunning(GameLauncher.FmmProcessName))
            {
                ShowError("FMM 正在运行。它关闭时会用自己的设置覆盖配置文件。\r\n请先关闭 FMM，再保存。");
                return false;
            }
            try
            {
                cfg.Save();
                dirty = false;
                UpdateTitle();
                SetStatus($"已保存（{DateTime.Now:HH:mm:ss}）。FMM 下次打开时也会看到这些修改。");
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                ShowError("保存失败：\r\n" + ex.Message);
                return false;
            }
        }

        private async Task LaunchAsync(bool forceRebuild)
        {
            if (cfg == null || busy) return;
            var pack = CurrentPack;
            if (pack == null) return;
            if (FileUtil.IsProcessRunning(GameLauncher.GameProcessName))
            {
                ShowError("FIFA 19 已经在运行了。");
                return;
            }
            if (FileUtil.IsProcessRunning(GameLauncher.FmmProcessName))
            {
                ShowError("请先关闭 FMM 窗口，再从本工具启动。");
                return;
            }

            RefreshProxyStatus();
            if (!IsProxyInstalled() &&
                !Confirm("免注入代理没有安装，MOD 需要的 DLL 不会被自动加载（游戏可能直接退出或 MOD 不生效）。\r\n\r\n仍然要启动吗？"))
                return;

            var needSave = dirty;
            if (!cfg.DefaultProfileReady)
            {
                // FMM's -launch shows its profile picker unless FIFA19 is the default profile.
                cfg.EnsureDefaultProfile();
                needSave = true;
            }
            if (needSave && !TrySave()) return;

            RefreshLibrary();
            var plan = GameLauncher.Evaluate(cfg, pack, ModsDir, libraryByName);
            if (plan.MissingMods.Count > 0 &&
                !Confirm("以下已启用的 MOD 文件找不到，会被跳过：\r\n" + string.Join("\r\n", plan.MissingMods) + "\r\n\r\n继续启动吗？"))
                return;
            if (plan.BrokenLinks != null &&
                !Confirm($"方案“{pack.Name}”已生成的 MOD 数据不完整（{plan.BrokenLinks}），直接启动游戏会马上退出。\r\n" +
                         "常见原因：游戏目录换过位置，或上次生成时拒绝了管理员权限。\r\n\r\n" +
                         "将删除这个方案的 MOD 数据，交给 FMM 重新生成（需要几分钟）。\r\n" +
                         "FMM 会先弹出英文提示，随后请求一次管理员权限（UAC）用于创建到游戏原始数据的链接，请务必点“是”。\r\n\r\n继续吗？"))
                return;

            busy = true;
            UpdateButtons();
            try
            {
                if (plan.BrokenLinks != null)
                {
                    GameLauncher.DeleteProfileData(cfg.GamePath, pack.Name);
                    await LaunchThroughFmmAsync(pack.Name, false);
                }
                else if (plan.CanFastLaunch && !forceRebuild) await LaunchDirectAsync(plan);
                else await LaunchThroughFmmAsync(pack.Name, forceRebuild);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
            {
                ShowError("启动失败：\r\n" + ex.Message);
            }
            finally
            {
                busy = false;
                ReloadAll();
                UpdateButtons();
            }
        }

        private async Task LaunchDirectAsync(LaunchPlan plan)
        {
            SetStatus("正在启动 FIFA 19…");
            using (var p = GameLauncher.LaunchDirect(cfg, plan))
            {
                var exited = await Task.Run(() => p.WaitForExit(10000));
                if (exited)
                {
                    ShowError($"FIFA 19 启动后很快退出了（退出码 {p.ExitCode}）。\r\n可以试试“重新生成并启动”，或检查免注入代理和游戏目录的 DLL。");
                    return;
                }
            }
            SetStatus($"游戏已启动（{DateTime.Now:HH:mm:ss}，秒启模式，未打开 FMM）。");
            WindowState = FormWindowState.Minimized;
        }

        private async Task LaunchThroughFmmAsync(string packName, bool forceRebuild)
        {
            if (forceRebuild)
            {
                if (!Confirm("将强制 FMM 重新生成这个方案的 MOD 数据，需要几分钟。继续吗？")) return;
                GameLauncher.DeleteGeneratedData(cfg.GamePath, packName);
            }
            SetStatus("FMM 正在生成 MOD 数据并启动游戏…会弹出 FMM 的进度窗口，请不要关闭它。完成后 FMM 会自动退出。");
            var started = DateTime.Now;
            using (var fmm = GameLauncher.LaunchViaFmm(fmmDir, packName))
            {
                await Task.Run(() => fmm.WaitForExit());
            }
            var gameUp = await Task.Run(() =>
            {
                for (int i = 0; i < 30; i++)
                {
                    if (FileUtil.IsProcessRunning(GameLauncher.GameProcessName)) return true;
                    System.Threading.Thread.Sleep(500);
                }
                return false;
            });
            var minutes = (DateTime.Now - started).TotalMinutes;
            var linkProblem = GameLauncher.CheckDataLinks(cfg.GamePath, Path.Combine(cfg.GamePath, "ModData", packName));
            if (linkProblem != null)
            {
                if (gameUp) SetStatus("警告：MOD 数据链接不完整，游戏可能很快退出。");
                ShowError($"FMM 生成的 MOD 数据仍不完整（{linkProblem}）。\r\n" +
                          "多半是 FMM 请求管理员权限时点了“否”。请再点一次“启动游戏”，在 UAC 弹窗中选择“是”；\r\n" +
                          "或者右键本工具选择“以管理员身份运行”后再启动。");
                return;
            }
            if (gameUp)
            {
                SetStatus($"游戏已由 FMM 启动（用时 {minutes:0.0} 分钟）。下次 MOD 不变时可直接秒启。");
                WindowState = FormWindowState.Minimized;
            }
            else
            {
                ShowError("FMM 已退出，但没有检测到游戏进程。\r\n可能是生成出错或被取消了，请查看 FMM 弹出的提示；必要时直接运行工具目录下的 FMM\\FrostyModManager.exe，手动点 Launch 查看详细日志。");
            }
        }

        // ---------- proxy ----------

        private bool IsProxyInstalled()
        {
            if (proxy == null || cfg == null) return false;
            try { return proxy.GetState(cfg.GamePath, out _) == ProxyState.Installed; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return false; }
        }

        private void ProxyAction(bool install)
        {
            if (proxy == null || cfg == null) return;
            if (FileUtil.IsProcessRunning(GameLauncher.GameProcessName))
            {
                ShowError("请先关闭游戏。");
                return;
            }
            var proxyAlreadyInstalled = false;
            if (install)
            {
                try { proxyAlreadyInstalled = proxy.GetState(cfg.GamePath, out _) == ProxyState.Installed; }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    ShowError("读取代理状态失败：\r\n" + ex.Message);
                    return;
                }
            }
            var prompt = install
                ? proxyAlreadyInstalled
                    ? $"将在 {cfg.GamePath} 补装缺少的 CryptBase.dll。\r\n不会更改已有的 DLL 或代理文件。继续吗？"
                    : $"将在 {cfg.GamePath} 安装免注入代理：\r\n· 原版 amd_ags_x64.dll 备份为 amd_ags_x64_orig.dll\r\n· 放入代理 DLL，游戏启动时自动加载 MOD 需要的 DLL\r\n· 如果缺少 CryptBase.dll，从内置 FMM 复制一份；已有文件不会覆盖\r\n\r\n不修改 FIFA19.exe，可随时还原。继续吗？"
                : $"将还原 {cfg.GamePath} 里的原版 amd_ags_x64.dll。\r\n如果 CryptBase.dll 是本工具安装且未被修改，也会一并移除；原先已有或后来修改过的文件会保留。\r\n还原后需要重新用 Extreme Injector 注入。继续吗？";
            if (!Confirm(prompt)) return;
            PerformProxyAction(install);
        }

        private void ForceReinstallProxy()
        {
            if (proxy == null || cfg == null) return;
            if (FileUtil.IsProcessRunning(GameLauncher.GameProcessName))
            {
                ShowError("请先关闭游戏。");
                return;
            }
            if (!Confirm($"将在 {cfg.GamePath} 强制重装免注入代理（备用修复手段）：\r\n" +
                         "· 用原版重新生成 amd_ags_x64_orig.dll 备份\r\n" +
                         "· 无论当前状态如何，重新写入代理 amd_ags_x64.dll\r\n" +
                         "· 缺少或不一致的 CryptBase.dll 用内置版本替换\r\n" +
                         "· 被覆盖的未知文件会改名保留为 *.quickloader-时间.bak，不会直接删除\r\n\r\n" +
                         "适用于：状态显示已安装但 MOD 不生效、文件被杀毒软件或游戏修复改动等情况。继续吗？"))
                return;
            try
            {
                var actions = proxy.ForceReinstall(cfg.GamePath);
                SetStatus("免注入代理已强制重装。");
                ShowInfo("强制重装完成：\r\n\r\n· " + string.Join("\r\n· ", actions));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
            {
                var hint = ex is UnauthorizedAccessException ? "\r\n\r\n没有写入权限：请右键本工具 →“以管理员身份运行”。" : "";
                ShowError("强制重装失败：\r\n" + ex.Message + hint);
            }
            RefreshProxyStatus();
        }

        private void PerformProxyAction(bool install)
        {
            if (proxy == null || cfg == null) return;
            try
            {
                if (install) proxy.Install(cfg.GamePath);
                else proxy.Uninstall(cfg.GamePath);
                SetStatus(install ? "免注入代理已安装。" : "已还原原版 DLL。");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
            {
                var hint = ex is UnauthorizedAccessException ? "\r\n\r\n没有写入权限：请右键本工具 →“以管理员身份运行”。" : "";
                ShowError((install ? "安装失败：" : "还原失败：") + "\r\n" + ex.Message + hint);
            }
            RefreshProxyStatus();
        }

        private void PromptToInstallProxyIfNeeded()
        {
            if (proxy == null || cfg == null || File.Exists(ProxyPromptDismissedPath) ||
                !File.Exists(Path.Combine(cfg.GamePath, GameLauncher.GameProcessName + ".exe")))
                return;

            ProxyState state;
            try { state = proxy.GetState(cfg.GamePath, out _); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                SetStatus("检查免注入代理状态失败：" + ex.Message);
                return;
            }
            if (state != ProxyState.NotInstalled) return;

            var result = MessageBox.Show(this,
                $"尚未安装免注入代理。\r\n\r\n安装会先备份原版 amd_ags_x64.dll，再放入代理；如果缺少 CryptBase.dll，会从内置 FMM 复制（已有文件不会覆盖）。不会修改 FIFA19.exe，之后可随时还原。\r\n\r\n现在安装吗？\r\n选择“否”后不再自动询问，仍可随时点“安装免注入代理”。游戏目录受保护时请以管理员身份运行本工具。",
                AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (result == DialogResult.Yes)
            {
                PerformProxyAction(true);
                return;
            }

            try
            {
                FileUtil.WriteAllTextAtomic(ProxyPromptDismissedPath, DateTime.UtcNow.ToString("O"));
                SetStatus("已跳过自动安装提示；需要时可点“安装免注入代理”。");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                ShowError("无法保存“以后不再询问”设置；本次仍已跳过安装，下次启动时可能再次提示。\r\n" + ex.Message);
            }
        }

        // ---------- misc ----------

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (busy)
            {
                if (!Confirm("FMM 还在生成/启动中，关闭本工具不会中断它。确定关闭吗？")) e.Cancel = true;
                return;
            }
            if (!dirty) return;
            var r = MessageBox.Show(this, "有未保存的修改，要保存吗？", AppTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel || (r == DialogResult.Yes && !TrySave())) e.Cancel = true;
        }

        private bool Confirm(string text) =>
            MessageBox.Show(this, text, AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        private void ShowInfo(string text) =>
            MessageBox.Show(this, text, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);

        private void ShowError(string text) =>
            MessageBox.Show(this, text, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
