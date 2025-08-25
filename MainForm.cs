using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.Threading;

// Fix ambiguous Timer names:
using WinFormsTimer = System.Windows.Forms.Timer;
using ThreadingTimer = System.Threading.Timer;

namespace SoundBoard
{
    public class MainForm : Form
    {
        // ====== UI Top ======
        private TableLayoutPanel grid;
        private TrackBar volumeTrack;
        private Label volumeLbl;

        private Button stopAllBtn;
        private Button reloadBtn;

        private Label deviceLbl;
        private ComboBox deviceCombo;

        private Label rowsLbl;
        private NumericUpDown rowsUpDown;
        private Label colsLbl;
        private NumericUpDown colsUpDown;

        private Label fontLbl;
        private NumericUpDown fontUpDown;

        private Label padLbl;
        private NumericUpDown padUpDown;
        private Button applyGridBtn;

        // ====== Config / Paths ======
        private string configPath;
        private AppConfig config = AppConfig.CreateDefault();

        // ====== File watcher with debounce ======
        private FileSystemWatcher? watcher;
        private ThreadingTimer? watcherDebounceTimer;
        private volatile bool suppressWatcherEvents = false;
        private volatile bool suppressSaves = false;

        // ====== Audio ======
        private MMDeviceEnumerator? mmEnum;
        private List<MMDevice> renderDevices = new();
        private MMDevice? selectedDevice;
        private bool suppressDeviceComboEvent = false;

        // ====== Playback ======
        private readonly List<IWavePlayer> activePlayers = new();
        private readonly Dictionary<int, IWavePlayer> padExclusivePlayers = new();

        public MainForm()
        {
            Text = "PXE SoundBoard";
            Width = 1200;
            Height = 800;
            KeyPreview = true;

            // Resolve config.json next to EXE (or CWD)
            var exeDir = AppContext.BaseDirectory;
            var exeConfig = Path.Combine(exeDir, "config.json");
            var cwdConfig = Path.Combine(Environment.CurrentDirectory, "config.json");
            configPath = File.Exists(exeConfig) ? exeConfig :
                         File.Exists(cwdConfig) ? cwdConfig :
                         exeConfig;

            // ===== Top Panel =====
            var top = new Panel { Dock = DockStyle.Top, Height = 90 };

            volumeLbl = new Label { Text = "Volume: 100%", AutoSize = true, Left = 10, Top = 12 };
            volumeTrack = new TrackBar { Minimum = 0, Maximum = 100, Value = 100, TickFrequency = 10, Width = 220, Left = 100, Top = 5 };
            volumeTrack.Scroll += (_, __) => volumeLbl.Text = $"Volume: {volumeTrack.Value}%";

            stopAllBtn = new Button { Text = "Stop All", Left = 330, Top = 10, Width = 100, Height = 28 };
            stopAllBtn.Click += (_, __) => StopAll();

            reloadBtn = new Button { Text = "Reload Config", Left = 440, Top = 10, Width = 120, Height = 28 };
            reloadBtn.Click += (_, __) => { LoadConfig(); BuildGrid(); };

            deviceLbl = new Label { Text = "Output:", AutoSize = true, Left = 580, Top = 12 };
            deviceCombo = new ComboBox { Left = 640, Top = 8, Width = 330, DropDownStyle = ComboBoxStyle.DropDownList };
            deviceCombo.SelectedIndexChanged += DeviceCombo_SelectedIndexChanged;

            rowsLbl = new Label { Text = "Rows:", AutoSize = true, Left = 10, Top = 55 };
            rowsUpDown = new NumericUpDown { Left = 60, Top = 50, Width = 60, Minimum = 1, Maximum = 16, Value = 4 };

            colsLbl = new Label { Text = "Cols:", AutoSize = true, Left = 130, Top = 55 };
            colsUpDown = new NumericUpDown { Left = 175, Top = 50, Width = 60, Minimum = 1, Maximum = 16, Value = 4 };

            fontLbl = new Label { Text = "Font:", AutoSize = true, Left = 245, Top = 55 };
            fontUpDown = new NumericUpDown { Left = 285, Top = 50, Width = 60, Minimum = 8, Maximum = 48, Value = 12 };

            padLbl = new Label { Text = "Padding:", AutoSize = true, Left = 355, Top = 55 };
            padUpDown = new NumericUpDown { Left = 420, Top = 50, Width = 60, Minimum = 0, Maximum = 30, Value = 6 };

            applyGridBtn = new Button { Text = "Apply Grid", Left = 490, Top = 48, Width = 110, Height = 28 };
            applyGridBtn.Click += (_, __) =>
            {
                config.GridRows = (int)rowsUpDown.Value;
                config.GridCols = (int)colsUpDown.Value;
                config.ButtonFontSize = (int)fontUpDown.Value;
                config.ButtonPadding = (int)padUpDown.Value;
                SaveConfig();
                BuildGrid();
            };

            top.Controls.Add(volumeLbl);
            top.Controls.Add(volumeTrack);
            top.Controls.Add(stopAllBtn);
            top.Controls.Add(reloadBtn);
            top.Controls.Add(deviceLbl);
            top.Controls.Add(deviceCombo);
            top.Controls.Add(rowsLbl);
            top.Controls.Add(rowsUpDown);
            top.Controls.Add(colsLbl);
            top.Controls.Add(colsUpDown);
            top.Controls.Add(fontLbl);
            top.Controls.Add(fontUpDown);
            top.Controls.Add(padLbl);
            top.Controls.Add(padUpDown);
            top.Controls.Add(applyGridBtn);

            // ===== Grid =====
            grid = new TableLayoutPanel { Dock = DockStyle.Fill };

            Controls.Add(grid);
            Controls.Add(top);

            // Init
            LoadConfig();
            InitDevices();
            BuildGrid();
            SetupWatcher();

            KeyDown += MainForm_KeyDown;
            Text = $"PXE SoundBoard — {configPath}";

            FormClosing += (_, __) =>
            {
                StopAll();
                watcher?.Dispose();
                watcherDebounceTimer?.Dispose();
                mmEnum?.Dispose();
            };
        }

        // ===================== Devices ======================
        private void InitDevices()
        {
            try
            {
                suppressDeviceComboEvent = true;
                mmEnum ??= new MMDeviceEnumerator();
                renderDevices = mmEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();

                deviceCombo.Items.Clear();
                foreach (var d in renderDevices) deviceCombo.Items.Add(d.FriendlyName);

                if (renderDevices.Count > 0)
                {
                    int idx = 0;
                    if (!string.IsNullOrWhiteSpace(config.SelectedOutputDeviceId))
                    {
                        var ix = renderDevices.FindIndex(d => d.ID.Equals(config.SelectedOutputDeviceId, StringComparison.OrdinalIgnoreCase));
                        if (ix >= 0) idx = ix;
                    }
                    deviceCombo.SelectedIndex = Math.Clamp(idx, 0, renderDevices.Count - 1);
                    selectedDevice = renderDevices[deviceCombo.SelectedIndex];
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Audio device init error:\n{ex.Message}", "Audio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                suppressDeviceComboEvent = false;
            }
        }

        private void DeviceCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (suppressDeviceComboEvent) return;
            if (deviceCombo.SelectedIndex >= 0 && deviceCombo.SelectedIndex < renderDevices.Count)
            {
                selectedDevice = renderDevices[deviceCombo.SelectedIndex];
                config.SelectedOutputDeviceId = selectedDevice?.ID;
                SaveConfig();
            }
        }

        // ===================== Watcher ======================
        private void SetupWatcher()
        {
            try
            {
                watcher?.Dispose();
                watcherDebounceTimer?.Dispose();

                var dir = Path.GetDirectoryName(configPath)!;
                var file = Path.GetFileName(configPath);

                watcher = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Attributes
                };
                watcher.Changed += Watcher_Changed;
                watcher.EnableRaisingEvents = true;

                watcherDebounceTimer = new ThreadingTimer(_ =>
                {
                    if (suppressWatcherEvents) return;
                    try
                    {
                        BeginInvoke((Action)(() =>
                        {
                            LoadConfig();
                            BuildGrid();
                        }));
                    }
                    catch { }
                }, null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Watcher setup error:\n{ex.Message}", "Watcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (suppressWatcherEvents) return;
            watcherDebounceTimer?.Change(200, System.Threading.Timeout.Infinite);
        }

        // ===================== Config ======================
        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    config = AppConfig.CreateDefault();
                    suppressWatcherEvents = true;
                    File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    var json = File.ReadAllText(configPath);
                    config = JsonSerializer.Deserialize<AppConfig>(json) ?? AppConfig.CreateDefault();
                }

                if (config.GridRows < 1) config.GridRows = 4;
                if (config.GridCols < 1) config.GridCols = 4;
                if (config.ButtonFontSize < 8) config.ButtonFontSize = 12;
                if (config.ButtonPadding < 0) config.ButtonPadding = 6;

                rowsUpDown.Value = Math.Clamp(config.GridRows, (int)rowsUpDown.Minimum, (int)rowsUpDown.Maximum);
                colsUpDown.Value = Math.Clamp(config.GridCols, (int)colsUpDown.Minimum, (int)colsUpDown.Maximum);
                fontUpDown.Value = Math.Clamp(config.ButtonFontSize, (int)fontUpDown.Minimum, (int)fontUpDown.Maximum);
                padUpDown.Value = Math.Clamp(config.ButtonPadding, (int)padUpDown.Minimum, (int)padUpDown.Maximum);

                if (selectedDevice == null || (config.SelectedOutputDeviceId != selectedDevice?.ID))
                    InitDevices();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Config load error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                config = AppConfig.CreateDefault();
            }
            finally
            {
                suppressWatcherEvents = false;
            }
        }

        private void SaveConfig()
        {
            if (suppressSaves) return;
            try
            {
                suppressWatcherEvents = true;
                suppressSaves = true;
                File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Config save error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                suppressSaves = false;
                var t = new WinFormsTimer { Interval = 150 };
                t.Tick += (_, __) => { suppressWatcherEvents = false; t.Stop(); t.Dispose(); };
                t.Start();
            }
        }

        // ===================== Grid ======================
        private void BuildGrid()
        {
            grid.Controls.Clear();
            grid.ColumnStyles.Clear();
            grid.RowStyles.Clear();

            grid.ColumnCount = config.GridCols;
            grid.RowCount = config.GridRows;

            for (int c = 0; c < grid.ColumnCount; c++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / grid.ColumnCount));
            for (int r = 0; r < grid.RowCount; r++)
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / grid.RowCount));

            padExclusivePlayers.Clear();

            int cells = grid.RowCount * grid.ColumnCount;
            var pads = (config.Pads ?? new List<SoundPad>()).Take(cells).ToList();
            while (pads.Count < cells) pads.Add(new SoundPad());
            config.Pads = pads;

            for (int i = 0; i < cells; i++)
            {
                var pad = pads[i];

                var btn = new Button
                {
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", config.ButtonFontSize, FontStyle.Bold),
                    Text = PadLabel(pad, i),
                    Tag = (pad, index: i),
                    BackColor = ColorTranslator.FromHtml(string.IsNullOrWhiteSpace(pad.Color) ? "#2d2d2d" : pad.Color),
                    ForeColor = Color.White,
                    Margin = new Padding(config.ButtonPadding)
                };
                btn.Click += PadButton_Click;
                btn.MouseUp += PadButton_MouseUp; // right-click opens editor
                grid.Controls.Add(btn, i % grid.ColumnCount, i / grid.ColumnCount);
            }
        }

        private static string PadLabel(SoundPad pad, int index)
        {
            string name = string.IsNullOrWhiteSpace(pad.Label) ? $"Pad {index + 1}" : pad.Label;
            string hk = string.IsNullOrWhiteSpace(pad.Hotkey) ? "" : $"\n[{pad.Hotkey}]";
            string mode = string.IsNullOrWhiteSpace(pad.Mode) ? "" : $"\n<{pad.Mode}>";
            return name + hk + mode;
        }

        // ===================== Per-Pad ======================
        private void PadButton_Click(object? sender, EventArgs e)
        {
            if (sender is not Button btn) return;
            var (pad, index) = ((SoundPad pad, int index))btn.Tag!;
            PlayPad(pad, index);
        }

        private void PadButton_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || sender is not Button btn) return;
            var (pad, index) = ((SoundPad pad, int index))btn.Tag!;

            using var dlg = new PadEditorForm(pad);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                // The pad object is edited in-place
                SaveConfig();
                UpdatePadButton(btn, pad, index);
            }
        }

        private void UpdatePadButton(Button btn, SoundPad pad, int index)
        {
            btn.Text = PadLabel(pad, index);
            btn.Font = new Font("Segoe UI", config.ButtonFontSize, FontStyle.Bold);
            btn.BackColor = ColorTranslator.FromHtml(string.IsNullOrWhiteSpace(pad.Color) ? "#2d2d2d" : pad.Color);
            btn.Margin = new Padding(config.ButtonPadding);
        }

        // ===================== Hotkeys (focus-only) ======================
        private void MainForm_KeyDown(object? sender, KeyEventArgs e)
        {
            string PressedToString()
            {
                var parts = new List<string>();
                if (e.Control) parts.Add("Ctrl");
                if (e.Alt) parts.Add("Alt");
                if (e.Shift) parts.Add("Shift");
                parts.Add(e.KeyCode.ToString());
                return string.Join("+", parts);
            }

            var pressed = NormalizeHotkey(PressedToString());

            for (int i = 0; i < (config.Pads?.Count ?? 0); i++)
            {
                var pad = config.Pads![i];
                if (string.IsNullOrWhiteSpace(pad.Hotkey)) continue;
                if (NormalizeHotkey(pad.Hotkey) == pressed)
                {
                    PlayPad(pad, i);
                    e.Handled = true;
                    break;
                }
            }
        }

        private static string NormalizeHotkey(string hk)
        {
            return string.Join("+", hk.Split('+', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()))
                .ToLowerInvariant();
        }

        // ===================== Playback ======================
        private void PlayPad(SoundPad pad, int index)
        {
            if (string.IsNullOrWhiteSpace(pad.FilePath) || !File.Exists(pad.FilePath))
            {
                MessageBox.Show($"Το αρχείο δεν βρέθηκε:\n{pad.FilePath}", "Missing file", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            float masterVol = volumeTrack.Value / 100f;
            float padVol = (pad.Volume <= 0 ? 1f : Math.Clamp(pad.Volume, 0f, 1f));
            float finalVol = Math.Clamp(masterVol * padVol, 0f, 1f);

            try
            {
                if (string.Equals(pad.Mode, "Cut", StringComparison.OrdinalIgnoreCase))
                {
                    if (padExclusivePlayers.TryGetValue(index, out var existing))
                    {
                        try { existing.Stop(); existing.Dispose(); } catch { }
                        padExclusivePlayers.Remove(index);
                    }

                    var reader = new AudioFileReader(pad.FilePath) { Volume = finalVol };
                    var output = CreateOutput();
                    output.Init(reader);
                    output.Play();
                    padExclusivePlayers[index] = output;

                    output.PlaybackStopped += (_, __) =>
                    {
                        try { output.Dispose(); } catch { }
                        try { reader.Dispose(); } catch { }
                        padExclusivePlayers.Remove(index);
                    };
                }
                else
                {
                    var reader = new AudioFileReader(pad.FilePath) { Volume = finalVol };
                    var output = CreateOutput();
                    output.Init(reader);
                    lock (activePlayers) activePlayers.Add(output);
                    output.Play();
                    output.PlaybackStopped += (_, __) =>
                    {
                        try { output.Dispose(); } catch { }
                        try { reader.Dispose(); } catch { }
                        lock (activePlayers) activePlayers.Remove(output);
                    };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Playback error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private IWavePlayer CreateOutput()
        {
            if (selectedDevice != null)
            {
                // Route to selected WASAPI device (e.g., VoiceMeeter Input / AUX / VAIO3)
                return new WasapiOut(selectedDevice, AudioClientShareMode.Shared, true, 80);
            }
            return new WaveOutEvent { DesiredLatency = 80 };
        }

        private void StopAll()
        {
            lock (activePlayers)
            {
                foreach (var p in activePlayers.ToList())
                {
                    try { p.Stop(); p.Dispose(); } catch { }
                }
                activePlayers.Clear();
            }

            foreach (var kv in padExclusivePlayers.ToList())
            {
                try { kv.Value.Stop(); kv.Value.Dispose(); } catch { }
                padExclusivePlayers.Remove(kv.Key);
            }
        }
    }

    // ===================== Models ======================
    public class AppConfig
    {
        public int GridRows { get; set; } = 4;
        public int GridCols { get; set; } = 4;
        public int ButtonFontSize { get; set; } = 12;
        public int ButtonPadding { get; set; } = 6;

        public List<SoundPad>? Pads { get; set; } = new();
        public string? SelectedOutputDeviceId { get; set; }

        public static AppConfig CreateDefault()
        {
            return new AppConfig
            {
                GridRows = 4,
                GridCols = 4,
                ButtonFontSize = 12,
                ButtonPadding = 6,
                Pads = new List<SoundPad>
                {
                    new SoundPad { Label = "Intro", FilePath = @"C:\Sounds\intro.wav", Hotkey="1", Color="#3b82f6", Mode = "Cut", Volume = 1.0f },
                    new SoundPad { Label = "Clap", FilePath = @"C:\Sounds\clap.mp3", Hotkey="2", Color="#16a34a", Mode = "Overlap", Volume = 0.9f },
                    new SoundPad { Label = "Boo", FilePath = @"C:\Sounds\boo.mp3", Hotkey="3", Color="#dc2626", Mode = "Overlap", Volume = 0.8f },
                    new SoundPad { Label = "Jingle", FilePath = @"C:\Sounds\jingle.wav", Hotkey="4", Color="#9333ea", Mode = "Cut", Volume = 1.0f },
                }
            };
        }
    }

    public class SoundPad
    {
        public string? Label { get; set; }
        public string? FilePath { get; set; }
        public string? Hotkey { get; set; }       // e.g., "Ctrl+1"
        public string? Mode { get; set; }         // "Cut" | "Overlap"
        public float Volume { get; set; } = 1.0f; // 0..1
        public string? Color { get; set; } = "#2d2d2d";
    }
}
