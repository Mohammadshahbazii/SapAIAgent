using System;
using System.Drawing;
using System.Windows.Forms;

namespace Sap2000WinFormsSample
{
    public static class PromptDialog
    {
        public static string Show(string title, string prompt)
        {
            return ShowInternal(title, null, prompt, multiline: false);
        }

        public static string ShowClarification(string title, string agentMessage, string question)
        {
            return ShowInternal(title, agentMessage, question, multiline: true);
        }

        private static string ShowInternal(string title, string context, string prompt, bool multiline)
        {
            using (var form = new Form())
            {
                form.Text = title;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(460, multiline ? 260 : 180);

                int currentTop = 12;

                if (!string.IsNullOrWhiteSpace(context))
                {
                    var contextLabel = new Label
                    {
                        AutoSize = false,
                        Text = context,
                        Left = 12,
                        Top = currentTop,
                        Width = form.ClientSize.Width - 24,
                        Height = 70
                    };
                    contextLabel.Font = new Font(contextLabel.Font, FontStyle.Italic);
                    form.Controls.Add(contextLabel);
                    currentTop = contextLabel.Bottom + 10;
                }

                var promptLabel = new Label
                {
                    AutoSize = false,
                    Text = prompt,
                    Left = 12,
                    Top = currentTop,
                    Width = form.ClientSize.Width - 24,
                    Height = 50
                };
                form.Controls.Add(promptLabel);

                var txt = new TextBox
                {
                    Left = 12,
                    Top = promptLabel.Bottom + 8,
                    Width = form.ClientSize.Width - 24,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                    Multiline = multiline,
                    Height = multiline ? 90 : 24,
                    ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None
                };
                form.Controls.Add(txt);

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

                form.Controls.Add(btnOk);
                form.Controls.Add(btnCancel);

                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                txt.Focus();

                var result = form.ShowDialog();
                return result == DialogResult.OK ? txt.Text.Trim() : null;
            }
        }
    }
}
