using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SoundBoard
{
    public class PadEditorForm : Form
    {
        private readonly SoundPad pad;

        private TextBox txtLabel;
        private TextBox txtFile;
        private Button btnBrowse;

        private RadioButton rbCut;
        private RadioButton rbOverlap;

        private TextBox txtHotkey;
        private Button btnCaptureHotkey;
        private Button btnClearHotkey;

        private NumericUpDown volUpDown;

        private Panel colorPanel;
        private Button btnPickColor;

        private Button btnOk;
        private Button btnCancel;

        public PadEditorForm(SoundPad pad)
        {
            this.pad = pad;

            Text = "Pad Settings";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false; MaximizeBox = false;
            Width = 520; Height = 360;

            int leftLabel = 16;
            int leftInput = 140;
            int line = 18;
            int y = 16;

            // Label
            var lblLabel = new Label { Left = leftLabel, Top = y, Text = "Text (Label):", AutoSize = true };
            txtLabel = new TextBox { Left = leftInput, Top = y - 2, Width = 320 };
            y += line + 18;

            // File
            var lblFile = new Label { Left = leftLabel, Top = y, Text = "Audio File:", AutoSize = true };
            txtFile = new TextBox { Left = leftInput, Top = y - 2, Width = 260 };
            btnBrowse = new Button { Left = leftInput + 265, Top = y - 4, Width = 55, Height = 24, Text = "..." };
            btnBrowse.Click += (_, __) =>
            {
                using var ofd = new OpenFileDialog
                {
                    Filter = "Audio files|*.wav;*.mp3;*.aiff;*.wma;*.m4a;*.flac|All files|*.*"
                };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    txtFile.Text = ofd.FileName;
                }
            };
            y += line + 18;

            // Mode
            var lblMode = new Label { Left = leftLabel, Top = y, Text = "Mode:", AutoSize = true };
            rbCut = new RadioButton { Left = leftInput, Top = y - 2, Text = "Cut", AutoSize = true };
            rbOverlap = new RadioButton { Left = leftInput + 80, Top = y - 2, Text = "Overlap", AutoSize = true };
            y += line + 18;

            // Hotkey
            var lblHotkey = new Label { Left = leftLabel, Top = y, Text = "Hotkey:", AutoSize = true };
            txtHotkey = new TextBox { Left = leftInput, Top = y - 2, Width = 160, ReadOnly = true };
            btnCaptureHotkey = new Button { Left = leftInput + 165, Top = y - 4, Width = 85, Height = 24, Text = "Capture" };
            btnClearHotkey = new Button { Left = leftInput + 255, Top = y - 4, Width = 70, Height = 24, Text = "Clear" };
            btnCaptureHotkey.Click += BtnCaptureHotkey_Click;
            btnClearHotkey.Click += (_, __) => { txtHotkey.Text = ""; };
            y += line + 18;

            // Volume
            var lblVol = new Label { Left = leftLabel, Top = y, Text = "Volume (0..1):", AutoSize = true };
            volUpDown = new NumericUpDown { Left = leftInput, Top = y - 4, Width = 80, DecimalPlaces = 2, Minimum = 0, Maximum = 1, Increment = 0.05M };
            y += line + 18;

            // Color
            var lblColor = new Label { Left = leftLabel, Top = y, Text = "Color:", AutoSize = true };
            colorPanel = new Panel { Left = leftInput, Top = y - 4, Width = 40, Height = 22, BorderStyle = BorderStyle.FixedSingle };
            btnPickColor = new Button { Left = leftInput + 50, Top = y - 4, Width = 85, Height = 24, Text = "Pick…" };
            btnPickColor.Click += (_, __) =>
            {
                using var cd = new ColorDialog();
                if (cd.ShowDialog(this) == DialogResult.OK)
                {
                    colorPanel.BackColor = cd.Color;
                }
            };
            y += line + 28;

            // Buttons
            btnOk = new Button { Text = "OK", Left = Width - 200, Top = Height - 90, Width = 75, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Left = Width - 115, Top = Height - 90, Width = 75, DialogResult = DialogResult.Cancel };
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[]
            {
                lblLabel, txtLabel,
                lblFile, txtFile, btnBrowse,
                lblMode, rbCut, rbOverlap,
                lblHotkey, txtHotkey, btnCaptureHotkey, btnClearHotkey,
                lblVol, volUpDown,
                lblColor, colorPanel, btnPickColor,
                btnOk, btnCancel
            });

            // Load current pad values
            txtLabel.Text = pad.Label ?? "";
            txtFile.Text = pad.FilePath ?? "";
            rbCut.Checked = string.Equals(pad.Mode, "Cut", StringComparison.OrdinalIgnoreCase);
            rbOverlap.Checked = !rbCut.Checked;
            txtHotkey.Text = pad.Hotkey ?? "";
            volUpDown.Value = (decimal)Math.Clamp(pad.Volume, 0f, 1f);

            try
            {
                colorPanel.BackColor = string.IsNullOrWhiteSpace(pad.Color) ? ColorTranslator.FromHtml("#2d2d2d") : ColorTranslator.FromHtml(pad.Color!);
            }
            catch { colorPanel.BackColor = ColorTranslator.FromHtml("#2d2d2d"); }

            // Save back on OK
            btnOk.Click += (_, __) =>
            {
                pad.Label = string.IsNullOrWhiteSpace(txtLabel.Text) ? null : txtLabel.Text;
                pad.FilePath = string.IsNullOrWhiteSpace(txtFile.Text) ? null : txtFile.Text;
                pad.Mode = rbCut.Checked ? "Cut" : "Overlap";
                pad.Hotkey = string.IsNullOrWhiteSpace(txtHotkey.Text) ? null : txtHotkey.Text;
                pad.Volume = (float)volUpDown.Value;
                pad.Color = ColorTranslator.ToHtml(colorPanel.BackColor);
            };
        }

        private void BtnCaptureHotkey_Click(object? sender, EventArgs e)
        {
            using var hk = new HotkeyCaptureDialog(txtHotkey.Text);
            if (hk.ShowDialog(this) == DialogResult.OK)
            {
                txtHotkey.Text = hk.HotkeyString;
            }
        }

        // Small dialog to capture hotkey with modifiers
        private class HotkeyCaptureDialog : Form
        {
            private Label lbl;
            public string HotkeyString { get; private set; } = "";

            public HotkeyCaptureDialog(string? current)
            {
                Width = 420; Height = 160;
                Text = "Press hotkey (Ctrl/Alt/Shift + Key)";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MinimizeBox = false; MaximizeBox = false;
                KeyPreview = true;

                lbl = new Label { Left = 15, Top = 20, Width = 370, Text = string.IsNullOrWhiteSpace(current) ? "Press keys…" : $"Current: {current}" };
                var clear = new Button { Text = "Clear", Left = 130, Width = 75, Top = 60 };
                var ok = new Button { Text = "OK", Left = 220, Width = 75, Top = 60, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancel", Left = 310, Width = 75, Top = 60, DialogResult = DialogResult.Cancel };

                clear.Click += (_, __) => { HotkeyString = ""; lbl.Text = "Cleared"; };
                Controls.Add(lbl); Controls.Add(clear); Controls.Add(ok); Controls.Add(cancel);

                this.KeyDown += HotkeyDialog_KeyDown;
            }

            private void HotkeyDialog_KeyDown(object? sender, KeyEventArgs e)
            {
                var parts = new System.Collections.Generic.List<string>();
                if (e.Control) parts.Add("Ctrl");
                if (e.Alt) parts.Add("Alt");
                if (e.Shift) parts.Add("Shift");
                parts.Add(e.KeyCode.ToString());
                HotkeyString = string.Join("+", parts);
                lbl.Text = HotkeyString;
                e.Handled = true;
            }
        }
    }
}
