using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GamingKeypressOverlay.Localization;
using GamingKeypressOverlay.Overlay;
using GamingKeypressOverlay.Settings;

namespace GamingKeypressOverlay.Win32
{
    /// <summary>
    /// Free-form color editor. Every value can be typed as #RRGGBB or selected
    /// with the native Windows color picker.
    /// </summary>
    internal sealed class ColorCustomizationDialog : Form
    {
        private readonly AppSettings _settings;
        private readonly Dictionary<string, TextBox> _inputs = new();
        private readonly Dictionary<string, Panel> _previews = new();

        private readonly ComboBox _presetBox;

        private static readonly (string Key, string EnglishLabel, string FrenchLabel, string DefaultValue)[] Fields =
        {
            ("Background", "Background", "Arrière-plan", "#101218"),
            ("Surface", "Panels / mouse", "Panneaux / souris", "#181C24"),
            ("IdleKey", "Idle keys", "Touches inactives", "#2A303B"),
            ("PressedKey", "Pressed keys", "Touches enfoncées", "#00D4FF"),
            ("Text", "Text", "Texte", "#F3F7FA"),
            ("PressedText", "Pressed-key text", "Texte des touches enfoncées", "#071014"),
            ("Primary", "Primary borders", "Bordures principales", "#00D4FF"),
            ("Secondary", "Secondary color", "Couleur secondaire", "#FF4FA3"),
            ("Accent", "Accent / glow", "Accent / lueur", "#8A5CFF")
        };

        private static readonly (string Name, string[] Colors)[] Presets =
        {
            ("Cyan Night", new[] { "#0B1018", "#141D29", "#243247", "#00D9FF", "#F3FAFF", "#061018", "#00D9FF", "#4B7BFF", "#7A5CFF" }),
            ("Violet Pulse", new[] { "#100B18", "#211633", "#38274F", "#B66CFF", "#FAF4FF", "#180B24", "#B66CFF", "#FF4FA3", "#7C5CFF" }),
            ("Ember", new[] { "#160D09", "#2A1710", "#493025", "#FF6B35", "#FFF5EC", "#241006", "#FF8A3D", "#FF3D68", "#FFC857" }),
            ("Forest Signal", new[] { "#09130F", "#13261D", "#254334", "#48E08B", "#F0FFF7", "#06150D", "#48E08B", "#28B8A6", "#B5E550" }),
            ("Monochrome", new[] { "#0E0F11", "#1A1C20", "#30343A", "#E6E8EB", "#FFFFFF", "#111214", "#D7DADE", "#8E949C", "#B8BDC4" }),
            ("Heroic Orange", new[] { "#101317", "#23272D", "#343A40", "#F99E1A", "#F2F2F2", "#111317", "#F99E1A", "#00A5E2", "#FFFFFF" }),
            ("Tactical Ops", new[] { "#0E100B", "#1C2117", "#343B2B", "#B6D53C", "#E7E2D2", "#111408", "#9EB33B", "#D08C32", "#6E7D4B" }),
            ("Midnight Gold", new[] { "#080B10", "#121923", "#263241", "#F4C430", "#F8FAFC", "#151000", "#F4C430", "#2D7DD2", "#FFEA80" }),
            ("Neon Storm", new[] { "#120C27", "#21153E", "#382B59", "#8B5CF6", "#F7F2FF", "#110925", "#7C4DFF", "#27D8FF", "#FF4FB8" })
        };

        public ColorCustomizationDialog(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            Text = UiText.Get("Customize Colors", "Personnaliser les couleurs");
            ClientSize = new Size(590, 555);
            MinimumSize = new Size(540, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(24, 27, 34);
            ForeColor = Color.White;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 1,
                RowCount = 4
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(new Label
            {
                AutoSize = true,
                Text = UiText.Get(
                    "Choose a matching preset, then fine-tune any HEX color (#RRGGBB).",
                    "Choisissez une palette assortie, puis ajustez chaque couleur HEX (#RRGGBB)."),
                Margin = new Padding(0, 0, 0, 14),
                ForeColor = Color.Gainsboro
            }, 0, 0);

            var presetRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 12)
            };
            presetRow.Controls.Add(new Label
            {
                Text = UiText.Get("Color preset:", "Palette prédéfinie :"),
                AutoSize = true,
                Margin = new Padding(0, 7, 10, 0),
                ForeColor = Color.White
            });
            _presetBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 210
            };
            foreach (var preset in Presets)
                _presetBox.Items.Add(preset.Name);
            _presetBox.SelectedIndex = 0;
            presetRow.Controls.Add(_presetBox);
            root.Controls.Add(presetRow, 0, 1);

            var colors = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 4,
                RowCount = Fields.Length,
                Margin = new Padding(0)
            };
            colors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            colors.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            colors.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            colors.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));

            for (int row = 0; row < Fields.Length; row++)
            {
                var field = Fields[row];
                AddColorRow(colors, row, field.Key,
                    UiText.Get(field.EnglishLabel, field.FrenchLabel), GetSetting(field.Key));
            }
            _presetBox.SelectedIndexChanged += (_, _) => ApplyPreset();
            root.Controls.Add(colors, 0, 2);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 12, 0, 0)
            };

            var applyButton = CreateButton(UiText.Get("Apply", "Appliquer"), 95);
            applyButton.Click += (_, _) => ApplyAndClose();

            buttons.Controls.Add(applyButton);
            root.Controls.Add(buttons, 0, 3);

            AcceptButton = applyButton;
            Controls.Add(root);
        }

        private void AddColorRow(TableLayoutPanel table, int row, string key, string label, string value)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            var labelControl = new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = Color.White
            };
            var input = new TextBox
            {
                Text = value,
                CharacterCasing = CharacterCasing.Upper,
                MaxLength = 7,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            var preview = new Panel
            {
                Width = 34,
                Height = 24,
                Anchor = AnchorStyles.None,
                BorderStyle = BorderStyle.FixedSingle
            };
            var pickerButton = CreateButton(UiText.Get("Choose…", "Choisir…"), 86);
            pickerButton.Tag = key;
            pickerButton.Click += PickColor;

            _inputs[key] = input;
            _previews[key] = preview;
            input.TextChanged += (_, _) => UpdatePreview(key);

            table.Controls.Add(labelControl, 0, row);
            table.Controls.Add(input, 1, row);
            table.Controls.Add(preview, 2, row);
            table.Controls.Add(pickerButton, 3, row);
            UpdatePreview(key);
        }

        private static Button CreateButton(string text, int width) => new()
        {
            Text = text,
            Width = width,
            Height = 30,
            FlatStyle = FlatStyle.System,
            Margin = new Padding(6, 0, 0, 0)
        };

        private void PickColor(object sender, EventArgs e)
        {
            if (sender is not Button button || button.Tag is not string key)
                return;

            using var picker = new ColorDialog
            {
                FullOpen = true,
                AnyColor = true,
                Color = StyleManager.TryParseHexColor(_inputs[key].Text, out Color current)
                    ? current
                    : Color.White
            };

            if (picker.ShowDialog(this) == DialogResult.OK)
                _inputs[key].Text = StyleManager.NormalizeHexColor(picker.Color);
        }

        private void UpdatePreview(string key)
        {
            bool valid = StyleManager.TryParseHexColor(_inputs[key].Text, out Color color);
            _previews[key].BackColor = valid ? color : Color.FromArgb(120, 25, 25);
            _inputs[key].ForeColor = valid ? SystemColors.WindowText : Color.Firebrick;
        }

        private void ApplyAndClose()
        {
            foreach (var field in Fields)
            {
                if (!StyleManager.TryParseHexColor(_inputs[field.Key].Text, out Color color))
                {
                    MessageBox.Show(this,
                        UiText.Get(
                            $"The value '{_inputs[field.Key].Text}' for '{field.EnglishLabel}' is not a valid HEX color (#RRGGBB).",
                            $"La valeur '{_inputs[field.Key].Text}' pour '{field.FrenchLabel}' n’est pas une couleur HEX valide (#RRGGBB)."),
                        UiText.Get("Invalid Color", "Couleur invalide"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _inputs[field.Key].Focus();
                    return;
                }

                SetSetting(field.Key, StyleManager.NormalizeHexColor(color));
            }

            _settings.UseCustomColors = true;
            _settings.Style = "Custom";
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ApplyPreset()
        {
            int index = _presetBox.SelectedIndex;
            if (index < 0 || index >= Presets.Length)
                return;

            for (int i = 0; i < Fields.Length; i++)
                _inputs[Fields[i].Key].Text = Presets[index].Colors[i];
        }

        private string GetSetting(string key) => key switch
        {
            "Background" => _settings.CustomBackgroundColor,
            "Surface" => _settings.CustomSurfaceColor,
            "IdleKey" => _settings.CustomIdleKeyColor,
            "PressedKey" => _settings.CustomPressedKeyColor,
            "Text" => _settings.CustomTextColor,
            "PressedText" => _settings.CustomPressedTextColor,
            "Primary" => _settings.CustomPrimaryColor,
            "Secondary" => _settings.CustomSecondaryColor,
            "Accent" => _settings.CustomAccentColor,
            _ => "#000000"
        };

        private void SetSetting(string key, string value)
        {
            switch (key)
            {
                case "Background": _settings.CustomBackgroundColor = value; break;
                case "Surface": _settings.CustomSurfaceColor = value; break;
                case "IdleKey": _settings.CustomIdleKeyColor = value; break;
                case "PressedKey": _settings.CustomPressedKeyColor = value; break;
                case "Text": _settings.CustomTextColor = value; break;
                case "PressedText": _settings.CustomPressedTextColor = value; break;
                case "Primary": _settings.CustomPrimaryColor = value; break;
                case "Secondary": _settings.CustomSecondaryColor = value; break;
                case "Accent": _settings.CustomAccentColor = value; break;
            }
        }
    }

    internal sealed class NativeWindowOwner : IWin32Window
    {
        public NativeWindowOwner(IntPtr handle) => Handle = handle;
        public IntPtr Handle { get; }
    }
}
