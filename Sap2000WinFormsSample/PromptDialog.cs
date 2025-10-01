using System;
using System.Drawing;
using System.Windows.Forms;

namespace Sap2000WinFormsSample
{
    public static class PromptDialog
    {
        public static string Show(string title, string prompt)
        {
            using (var form = new Form())
            {
                form.Text = title;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(420, 170);

                var lbl = new Label
                {
                    AutoSize = false,
                    Text = prompt,
                    Left = 12,
                    Top = 12,
                    Width = form.ClientSize.Width - 24,
                    Height = 70
                };

                var txt = new TextBox
                {
                    Left = 12,
                    Top = lbl.Bottom + 8,
                    Width = form.ClientSize.Width - 24,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
                };

                var btnOk = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK
                };

                var btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel
                };

                int buttonTop = txt.Bottom + 12;
                btnOk.SetBounds(form.ClientSize.Width - 170, buttonTop, 75, 28);
                btnCancel.SetBounds(form.ClientSize.Width - 85, buttonTop, 75, 28);

                btnOk.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
                btnCancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;

                form.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
                txt.Focus();
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                var result = form.ShowDialog();
                return result == DialogResult.OK ? txt.Text.Trim() : null;
            }
        }
    }
}
