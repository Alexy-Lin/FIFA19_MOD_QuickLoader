using System.Drawing;
using System.Windows.Forms;

namespace FIFA19QuickLoader
{
    internal static class InputDialog
    {
        /// <summary>Returns the entered text, or null when cancelled.</summary>
        public static string Show(IWin32Window owner, string title, string prompt, string initial)
        {
            using (var form = new Form
            {
                Text = title,
                Font = new Font("Microsoft YaHei UI", 9F),
                AutoScaleMode = AutoScaleMode.Font,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12),
            })
            {
                var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
                var box = new TextBox { Text = initial ?? "", Width = 380 };
                var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, AutoSize = true };
                var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
                var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);
                layout.Controls.Add(new Label { Text = prompt, AutoSize = true, MaximumSize = new Size(380, 0) });
                layout.Controls.Add(box);
                layout.Controls.Add(buttons);
                form.Controls.Add(layout);
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                form.Shown += (s, e) => { box.SelectAll(); box.Focus(); };
                return form.ShowDialog(owner) == DialogResult.OK ? box.Text : null;
            }
        }
    }
}
