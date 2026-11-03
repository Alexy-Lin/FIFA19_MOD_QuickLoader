using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace FIFA19QuickLoader
{
    /// <summary>
    /// Editor for fifasetup.ini. Option values and their meanings mirror FIFASetup\fifaconfig.exe; a key is only
    /// written when the user actually changes it, and unknown values found in the file are kept as-is.
    /// </summary>
    internal static class DisplaySettingsDialog
    {
        private static readonly Regex SizePattern = new Regex(@"^\s*(\d{3,5})\s*[x×X*,，]\s*(\d{3,5})", RegexOptions.Compiled);

        private sealed class Choice
        {
            public string Value;
            public string Text;
            public override string ToString() => Text;
        }

        private sealed class Row
        {
            public string Key;
            public string Default;
            public ComboBox Combo;
            public string Initial;
        }

        private static Choice C(string value, string text) => new Choice { Value = value, Text = text };

        // Defaults are what fifaconfig.exe assumes when a key is missing.
        private static readonly (string key, string label, string def, Choice[] choices, string hint)[] Options =
        {
            ("DIRECTX_SELECT", "DirectX 版本", "1", new[] { C("0", "自动"), C("1", "DirectX 11"), C("2", "DirectX 12") },
                "DX12 在部分显卡上更流畅，遇到闪退或黑屏请改回 DX11。"),
            ("RENDERINGQUALITY", "画面质量", "0", new[] { C("0", "自动"), C("1", "低"), C("2", "中"), C("3", "高"), C("4", "极高") },
                "越高画面越好、帧数越低。"),
            ("MSAA_LEVEL", "MSAA 抗锯齿", "1", new[] { C("0", "自动"), C("1", "关闭"), C("2", "2x"), C("3", "4x") },
                "倍数越高边缘越平滑，但更吃显卡。"),
            ("WAITFORVSYNC", "帧率限制", "0", new[] { C("0", "不限帧率"), C("1", "锁定 60 帧"), C("2", "锁定 30 帧") },
                "锁 60 帧可减少画面撕裂，配置较低时可选 30 帧。"),
            ("AUDIO_MIX_MODE", "声道模式", "0", new[] { C("0", "立体声"), C("1", "5.1 声道"), C("2", "7.1 声道") },
                "按你的耳机或音箱选择。"),
            ("VOICECHAT", "语音聊天", "1", new[] { C("1", "开启"), C("0", "关闭") },
                "离线游玩可关闭。"),
            ("CONTROLLER_DEFAULT", "手柄按键图标", "0", new[] { C("0", "字母按键（Xbox：A/B/X/Y）"), C("1", "数字按键（其他手柄：1/2/3/4）") },
                "游戏内显示的按键提示样式。"),
            ("CONFIG_APP_LOCALE", "游戏语言", "en-US", new[]
                {
                    C("zh-CN", "简体中文"), C("zh-HK", "繁體中文"), C("en-US", "English US"), C("ja-JP", "日本語"),
                    C("de-DE", "Deutsch"), C("fr-FR", "Français"), C("it-IT", "Italiano"), C("es-ES", "Español España"),
                    C("es-MX", "Español México"), C("pt-BR", "Português Brasil"), C("pt-PT", "Português Portugal"),
                    C("nl-NL", "Nederlands"), C("pl-PL", "Polski"), C("ru-RU", "Pусский"), C("tr-TR", "Türkçe"),
                    C("cs-CZ", "Čeština"), C("da-DK", "Dansk"), C("no", "Norsk"), C("sv-SE", "Svenska"), C("ar-SA", "العربية"),
                },
                "游戏界面和解说的语言（需要游戏里装有对应语言包）。"),
        };

        private static readonly string[] DisplayGroup = { "DIRECTX_SELECT" };
        private static readonly string[] QualityGroup = { "RENDERINGQUALITY", "MSAA_LEVEL", "WAITFORVSYNC" };
        private static readonly string[] OtherGroup = { "AUDIO_MIX_MODE", "VOICECHAT", "CONTROLLER_DEFAULT", "CONFIG_APP_LOCALE" };

        /// <summary>Returns true when the settings were saved.</summary>
        public static bool Show(IWin32Window owner, GameSettings settings)
        {
            var modes = GameSettings.GetMonitorResolutions(out var desktop);
            float scale;
            using (var probe = new Form()) using (var g = probe.CreateGraphics()) scale = Math.Max(1f, g.DpiX / 96f);
            int S(int px) => (int)Math.Round(px * scale);

            using (var form = new Form
            {
                Text = "游戏设置（画面 / 画质 / 音频 / 语言）",
                Font = new Font("Microsoft YaHei UI", 10F),
                AutoScaleMode = AutoScaleMode.None,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(S(14)),
            })
            {
                var comboWidth = S(320);
                var hintWidth = S(360);
                var rows = new List<Row>();

                TableLayoutPanel NewGrid() => new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill, Padding = new Padding(S(4)) };
                Label Caption(string t) => new Label { Text = t, AutoSize = true, MinimumSize = new Size(S(130), 0), Margin = new Padding(S(3), S(9), S(10), S(3)) };
                Label Hint(string t) => new Label
                {
                    Text = t, AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(hintWidth, 0),
                    Margin = new Padding(S(10), S(9), S(3), S(3)), Font = new Font("Microsoft YaHei UI", 9F),
                };
                GroupBox Group(string title, TableLayoutPanel grid)
                {
                    var box = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, Padding = new Padding(S(8)), Margin = new Padding(0, 0, 0, S(10)) };
                    box.Controls.Add(grid);
                    return box;
                }
                void AddRow(TableLayoutPanel grid, string key)
                {
                    var o = Options.First(x => x.key == key);
                    var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = comboWidth, Margin = new Padding(S(3), S(5), S(3), S(3)) };
                    combo.Items.AddRange(o.choices);
                    var raw = settings.Get(key);
                    var current = (raw ?? o.def).Trim().Trim('"');
                    var match = o.choices.FirstOrDefault(c => c.Value.Equals(current, StringComparison.OrdinalIgnoreCase));
                    if (match == null)
                    {
                        match = C(current, $"保持当前值（{current}）");
                        combo.Items.Add(match);
                    }
                    combo.SelectedItem = match;
                    var r = grid.RowCount++;
                    grid.Controls.Add(Caption(o.label + "："), 0, r);
                    grid.Controls.Add(combo, 1, r);
                    grid.Controls.Add(Hint(o.hint), 2, r);
                    rows.Add(new Row { Key = key, Default = o.def, Combo = combo, Initial = match.Value });
                }

                // ---- display ----
                var display = NewGrid();
                var cmbRes = new ComboBox { Width = comboWidth, DropDownStyle = ComboBoxStyle.DropDown, Margin = new Padding(S(3), S(5), S(3), S(3)) };
                foreach (var m in modes) cmbRes.Items.Add(Format(m) + (m == desktop ? "（当前桌面）" : ""));
                var initialRes = Format(new Size(settings.Width, settings.Height));
                var resMatch = cmbRes.Items.Cast<string>().FirstOrDefault(i => i == initialRes || i.StartsWith(initialRes + "（"));
                if (resMatch != null) cmbRes.SelectedItem = resMatch; else cmbRes.Text = initialRes;
                display.RowCount = 1;
                display.Controls.Add(Caption("分辨率："), 0, 0);
                display.Controls.Add(cmbRes, 1, 0);
                display.Controls.Add(Hint("可从列表选，也可直接输入，如 2560x1440。全屏只能用显示器支持的分辨率。"), 2, 0);

                var cmbMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = comboWidth, Margin = new Padding(S(3), S(5), S(3), S(3)) };
                foreach (DisplayMode m in Enum.GetValues(typeof(DisplayMode))) cmbMode.Items.Add(GameSettings.ModeName(m));
                var initialMode = settings.Mode;
                cmbMode.SelectedIndex = (int)initialMode;
                display.RowCount = 2;
                display.Controls.Add(Caption("显示模式："), 0, 1);
                display.Controls.Add(cmbMode, 1, 1);
                display.Controls.Add(Hint("无边框窗口切屏方便，分辨率与桌面一致时最清晰。"), 2, 1);
                foreach (var k in DisplayGroup) AddRow(display, k);

                var quality = NewGrid();
                foreach (var k in QualityGroup) AddRow(quality, k);
                var other = NewGrid();
                foreach (var k in OtherGroup) AddRow(other, k);

                var note = new Label
                {
                    AutoSize = true,
                    MaximumSize = new Size(comboWidth + hintWidth + S(140), 0),
                    ForeColor = Color.DimGray,
                    Font = new Font("Microsoft YaHei UI", 9F),
                    Margin = new Padding(S(3), 0, S(3), S(6)),
                    Text = "这些就是游戏自带“FIFASetup\\fifaconfig.exe”里的选项，修改后重启游戏生效。只会写入你改动过的项目。\r\n" +
                           "设置文件：" + settings.FilePath + (settings.Exists ? "" : "（尚不存在，保存时创建）") + "\r\n" +
                           "首次保存会自动备份为 fifasetup.ini.quickloader.bak。",
                };

                var ok = new Button { Text = "保存", AutoSize = true, Padding = new Padding(S(14), S(2), S(14), S(2)) };
                var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(S(14), S(2), S(14), S(2)) };
                var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);

                var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
                layout.Controls.Add(Group("显示", display));
                layout.Controls.Add(Group("画质与帧率", quality));
                layout.Controls.Add(Group("音频、手柄与语言", other));
                layout.Controls.Add(note);
                layout.Controls.Add(buttons);
                form.Controls.Add(layout);
                form.AcceptButton = ok;
                form.CancelButton = cancel;

                ok.Click += (s, e) =>
                {
                    var m = SizePattern.Match(cmbRes.Text);
                    int w = 0, h = 0;
                    if (!m.Success ||
                        !int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out w) ||
                        !int.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out h) ||
                        w < 640 || h < 480 || w > 16384 || h > 16384)
                    {
                        MessageBox.Show(form, "分辨率格式不对，请输入“宽x高”，例如 1920x1080。", form.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    var mode = (DisplayMode)cmbMode.SelectedIndex;
                    if (mode == DisplayMode.Fullscreen && modes.Count > 0 && !modes.Contains(new Size(w, h)) &&
                        MessageBox.Show(form, $"显示器的模式列表里没有 {w}x{h}，全屏时游戏可能黑屏或自动改回其他分辨率。\r\n\r\n仍然保存吗？",
                            form.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;

                    try
                    {
                        if (Format(new Size(w, h)) != initialRes || !settings.Exists)
                        {
                            settings.Width = w;
                            settings.Height = h;
                        }
                        if (mode != initialMode || !settings.Exists) settings.Mode = mode;
                        foreach (var r in rows)
                        {
                            var value = ((Choice)r.Combo.SelectedItem).Value;
                            if (value != r.Initial) settings.Set(r.Key, value);
                        }
                        settings.Save();
                    }
                    catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
                    {
                        MessageBox.Show(form, "保存失败：\r\n" + ex.Message, form.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    form.DialogResult = DialogResult.OK;
                };
                return form.ShowDialog(owner) == DialogResult.OK;
            }
        }

        public static string Format(Size s) => s.Width.ToString(CultureInfo.InvariantCulture) + "x" + s.Height.ToString(CultureInfo.InvariantCulture);
    }
}
