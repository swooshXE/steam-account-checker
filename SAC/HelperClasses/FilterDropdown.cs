using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SAC.HelperClasses
{
    // A button that opens a dropdown with checkboxes, so several games or several
    // ban kinds can be picked at once. A plain ComboBox only allows one selection,
    // and the filter needs sets ("CS2 + SCP: SL", "community + game ban").
    class FilterDropDown : Button
    {
        public sealed class Item
        {
            public string Text;
            public object Value;
            public bool Checked;

            public Item(string text, object value, bool isChecked = false)
            {
                Text = text;
                Value = value;
                Checked = isChecked;
            }
        }

        private readonly List<Item> items = new List<Item>();
        private readonly ToolTip tip = new ToolTip();
        private ToolStripDropDown dropDown;

        public string Prefix = string.Empty;
        public string EmptySummary = "all";

        public event EventHandler SelectionChanged;

        public FilterDropDown()
        {
            // The native themed button is the one that matches the rest of the window,
            // but it draws its caption itself: it ignores AutoEllipsis and wraps a long
            // selection onto a second line. The caption is therefore shortened here
            // before it reaches the control.
            FlatStyle = FlatStyle.System;
            TextAlign = ContentAlignment.MiddleLeft;
            Padding = new Padding(8, 0, 4, 0);
            UpdateText();
        }

        public IReadOnlyList<Item> Items => items;

        public Item[] CheckedItems => items.Where(item => item.Checked).ToArray();

        public void SetItems(IEnumerable<Item> newItems)
        {
            items.Clear();

            if (newItems != null)
                items.AddRange(newItems);

            UpdateText();
        }

        public Item Find(object value) => items.FirstOrDefault(item => Equals(item.Value, value));

        // Adds an entry (used for custom AppIDs) and optionally checks it right away.
        public Item AddItem(string text, object value, bool isChecked)
        {
            Item existing = Find(value);

            if (existing != null)
            {
                existing.Text = text;
                existing.Checked |= isChecked;
                UpdateText();
                return existing;
            }

            Item item = new Item(text, value, isChecked);
            items.Add(item);
            UpdateText();
            return item;
        }

        public void Check(object value, bool isChecked)
        {
            Item item = Find(value);

            if (item == null)
                return;

            item.Checked = isChecked;
            UpdateText();
        }

        // Clears every box without firing per-item events; the caller raises one
        // SelectionChanged afterwards.
        public void ClearChecks()
        {
            foreach (Item item in items)
                item.Checked = false;

            UpdateText();
        }

        private string Summary
        {
            get
            {
                string[] checkedText = CheckedItems.Select(item => item.Text).ToArray();
                return checkedText.Length == 0 ? EmptySummary : string.Join(", ", checkedText);
            }
        }

        private void UpdateText()
        {
            string full = string.IsNullOrEmpty(Prefix) ? Summary : Prefix + " " + Summary;

            Text = Shorten(full);
            // the button shows one line, so the full pick is available on hover
            tip.SetToolTip(this, full);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateText();
        }

        // Fits the caption into the button: anything longer is cut and ends with "…",
        // because a wrapped caption would make the button two lines tall.
        private string Shorten(string text)
        {
            int available = Width - Padding.Horizontal - 8;

            if (available < 20 || TextRenderer.MeasureText(text, Font).Width <= available)
                return text;

            int low = 0;
            int high = text.Length;

            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                string candidate = text.Substring(0, middle).TrimEnd() + "…";

                if (TextRenderer.MeasureText(candidate, Font).Width <= available)
                    low = middle;
                else
                    high = middle - 1;
            }

            return text.Substring(0, low).TrimEnd() + "…";
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            ShowPopup();
        }

        private void ShowPopup()
        {
            dropDown?.Dispose();

            CheckedListBox list = new CheckedListBox
            {
                CheckOnClick = true,
                BorderStyle = BorderStyle.None,
                IntegralHeight = false,
                Font = Font,
                Width = Math.Max(Width, 190),
            };

            foreach (Item item in items)
                list.Items.Add(item.Text, item.Checked);

            // ItemHeight is only valid once the list knows its font; before the handle
            // exists it reports 0, which used to produce an invisible zero-height popup.
            int rowHeight = Math.Max(list.ItemHeight, TextRenderer.MeasureText("Ag", list.Font).Height + 2);
            int rows = Math.Min(Math.Max(items.Count, 1), 10);
            list.Height = rows * rowHeight + 4;

            list.ItemCheck += (sender, args) =>
            {
                if (args.Index >= 0 && args.Index < items.Count)
                    items[args.Index].Checked = args.NewValue == CheckState.Checked;

                UpdateText();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            };

            ToolStripControlHost host = new ToolStripControlHost(list)
            {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false,
                Size = list.Size,
            };

            dropDown = new ToolStripDropDown
            {
                Padding = Padding.Empty,
                AutoSize = false,
                DropShadowEnabled = true,
                Size = new Size(list.Width + 2, list.Height + 2),
            };

            dropDown.Items.Add(host);
            dropDown.Show(this, new Point(0, Height), ToolStripDropDownDirection.BelowRight);
        }

        // Small modal used by the "+" button: the AppID the user wants to filter by.
        internal static uint? AskAppId(IWin32Window owner)
        {
            using (Form form = new Form())
            {
                form.Text = "Add game by AppID";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ClientSize = new Size(320, 104);

                Label label = new Label
                {
                    Text = "Steam AppID (for example 700330):",
                    Location = new Point(14, 14),
                    AutoSize = true,
                };

                TextBox input = new TextBox
                {
                    Location = new Point(14, 36),
                    Width = 292,
                };

                Button ok = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Location = new Point(150, 70),
                    Size = new Size(75, 24),
                };

                Button cancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(231, 70),
                    Size = new Size(75, 24),
                };

                form.Controls.Add(label);
                form.Controls.Add(input);
                form.Controls.Add(ok);
                form.Controls.Add(cancel);
                form.AcceptButton = ok;
                form.CancelButton = cancel;

                input.KeyPress += (sender, args) =>
                {
                    if (!char.IsDigit(args.KeyChar) && !char.IsControl(args.KeyChar))
                        args.Handled = true;
                };

                if (form.ShowDialog(owner) != DialogResult.OK)
                    return null;

                return uint.TryParse(input.Text.Trim(), out uint appId) && appId != 0 ? appId : (uint?)null;
            }
        }
    }
}