using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Drawing;
using System.Collections.Generic;

public class DoubleBufferedListView : ListView
{
    public DoubleBufferedListView()
    {
        this.DoubleBuffered = true;
    }
}

public class DoubleBufferedListViewDisabled : ListView
{
    public DoubleBufferedListViewDisabled()
    {
        this.DoubleBuffered = true;
    }
}

public class CPHInline
{
    private HttpListener _listener;
    private HttpListener _localFileListener;
    private DoubleBufferedListView listView;
    private DoubleBufferedListViewDisabled listViewDisabled;

    public bool Execute()
    {
        // Form
        Form form = new Form { Size = new Size(550, 550), BackColor = Color.FromArgb(108, 11, 169), Padding = new Padding(1), FormBorderStyle = FormBorderStyle.None };
        form.Text = "Overlay(er) v1.0.0";
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(
            Screen.PrimaryScreen.Bounds.Width / 2 - form.ClientSize.Width / 2,
            Screen.PrimaryScreen.Bounds.Height / 2 - form.ClientSize.Height / 2);
        form.FormClosing += (sender, e) =>
        {
            DialogResult result = MessageBox.Show("Are you sure you want to close the application?", "Confirm Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.No)
            {
                e.Cancel = true; // Cancel the form closing event
            }
            _listener?.Stop();
        };
        // Connect Button
        Button connectButton = new Button { Text = "Start Server", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(108, 11, 169), ForeColor = Color.White };
        connectButton.FlatAppearance.BorderColor = connectButton.BackColor;
        // Disconnect Button
        Button disconnectButton = new Button { Text = "Stop Server", Dock = DockStyle.Fill, Enabled = false, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(108, 11, 169), ForeColor = Color.White };
        disconnectButton.FlatAppearance.BorderColor = connectButton.BackColor;
        // Minimize Button
        Button miniButton = new Button { Text = "_", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(108, 11, 169), ForeColor = Color.White };
        miniButton.FlatAppearance.BorderColor = connectButton.BackColor;
        miniButton.Click += (sender, e) =>
        {
            form.WindowState = FormWindowState.Minimized;
        };
        // Close Button
        Button closeButton = new Button { Text = "x", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(255, 0, 0), ForeColor = Color.White };
        closeButton.FlatAppearance.BorderColor = connectButton.BackColor;
        closeButton.Click += (sender, e) =>
        {
            Application.Exit();
        };
        // Help Button
        Button helpButton = new Button { Text = "?", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(108, 11, 169), ForeColor = Color.White };
        helpButton.FlatAppearance.BorderColor = connectButton.BackColor;
        helpButton.Click += (sender, e) =>
        {
            ShowHelpPopup();
        };
        // OBS URL
        TextBox urlBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.Black, Text = "Enter URL here", TextAlign = HorizontalAlignment.Center, Font = new Font("Arial", 14) };
        urlBox.BorderStyle = BorderStyle.None;
        urlBox.ForeColor = Color.Gray;
        urlBox.Text = "Not Connected";
        // listViewDisabled
        listViewDisabled = new DoubleBufferedListViewDisabled();
        listViewDisabled.MultiSelect = false;
        listViewDisabled.Dock = DockStyle.Fill;
        listViewDisabled.View = View.Details;
        listViewDisabled.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        listViewDisabled.BorderStyle = BorderStyle.FixedSingle;
        listViewDisabled.BackColor = Color.FromArgb(60, 60, 60);
        listViewDisabled.ForeColor = Color.White;
        listViewDisabled.BorderStyle = BorderStyle.FixedSingle;
        listViewDisabled.Columns.Add("NAME", 75, HorizontalAlignment.Left);
        listViewDisabled.Columns.Add("URL", 250, HorizontalAlignment.Left);
        listViewDisabled.Columns.Add("Height", -2, HorizontalAlignment.Left);
        listViewDisabled.Columns.Add("Width", -2, HorizontalAlignment.Left);
        listViewDisabled.Columns.Add("Top", -2, HorizontalAlignment.Left);
        listViewDisabled.Columns.Add("Left", -2, HorizontalAlignment.Left);
        listViewDisabled.FullRowSelect = true;
        listViewDisabled.GridLines = false;
        listViewDisabled.OwnerDraw = true;

        listViewDisabled.DrawColumnHeader += (sender, e) =>
        {
            e.DrawBackground();
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(108, 11, 169)))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            e.Graphics.DrawString(e.Header.Text, listViewDisabled.Font, Brushes.White, e.Bounds, new StringFormat());
        };
        // DrawItem event for listViewDisabled
        listViewDisabled.DrawItem += (sender, e) =>
        {
            e.DrawBackground();
            if ((e.State & ListViewItemStates.Selected) == ListViewItemStates.Selected)
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(108, 11, 169)))
                {
                    e.Graphics.FillRectangle(brush, e.Bounds);
                }
                using (SolidBrush brush = new SolidBrush(Color.White))
                {
                    // Check if the text in the first column exceeds the character limit
                    string firstColumnText = e.Item.Text;
                    if (firstColumnText.Length > 10)
                    {
                        // Truncate the text to 10 characters and add ellipsis
                        firstColumnText = firstColumnText.Substring(0, 10) + "...";
                    }

                    e.Graphics.DrawString(firstColumnText, listViewDisabled.Font, brush, e.Bounds);
                }
            }
            else
            {
                using (SolidBrush brush = new SolidBrush(Color.White))
                {
                    // Check if the text in the first column exceeds the character limit
                    string firstColumnText = e.Item.Text;
                    if (firstColumnText.Length > 10)
                    {
                        // Truncate the text to 10 characters and add ellipsis
                        firstColumnText = firstColumnText.Substring(0, 10) + "...";
                    }

                    e.Graphics.DrawString(firstColumnText, listViewDisabled.Font, brush, e.Bounds);
                }
            }

            // Draw subitems
            int subItemX = e.Bounds.Left + listViewDisabled.Columns[0].Width;
            for (int subItemIndex = 1; subItemIndex < e.Item.SubItems.Count; subItemIndex++)
            {
                Rectangle subItemBounds = new Rectangle(subItemX, e.Bounds.Top, listViewDisabled.Columns[subItemIndex].Width, e.Bounds.Height);
                string subItemText = e.Item.SubItems[subItemIndex].Text;

                // Measure the width of the text and check if it exceeds the column width
                if (TextRenderer.MeasureText(subItemText, listViewDisabled.Font).Width > subItemBounds.Width)
                {
                    // Calculate the available width for the ellipsis
                    int ellipsisWidth = TextRenderer.MeasureText("...", listViewDisabled.Font).Width;

                    // Truncate the text to fit within the available width with ellipsis
                    while (subItemText.Length > 0 && TextRenderer.MeasureText(subItemText + "...", listViewDisabled.Font).Width > subItemBounds.Width)
                    {
                        subItemText = subItemText.Substring(0, subItemText.Length - 1);
                    }

                    // Append the ellipsis to the truncated text
                    subItemText += "...";
                }

                using (SolidBrush brush = new SolidBrush(Color.White))
                {
                    e.Graphics.DrawString(subItemText, listViewDisabled.Font, brush, subItemBounds);
                }

                subItemX += listViewDisabled.Columns[subItemIndex].Width;
            }
        };

        // listViewEnabled
        listView = new DoubleBufferedListView();
        listView.MultiSelect = false;
        listView.Dock = DockStyle.Fill;
        listView.View = View.Details;
        listView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        listView.BorderStyle = BorderStyle.FixedSingle;
        listView.BackColor = Color.FromArgb(60, 60, 60);
        listView.ForeColor = Color.White;
        listView.Columns.Add("NAME", 75, HorizontalAlignment.Left);
        listView.Columns.Add("URL", 250, HorizontalAlignment.Left);
        listView.Columns.Add("Height", -2, HorizontalAlignment.Left);
        listView.Columns.Add("Width", -2, HorizontalAlignment.Left);
        listView.Columns.Add("Top", -2, HorizontalAlignment.Left);
        listView.Columns.Add("Left", -2, HorizontalAlignment.Left);
        listView.FullRowSelect = true;
        listView.GridLines = false;
        listView.OwnerDraw = true;

        listView.DrawColumnHeader += (sender, e) =>
        {
            e.DrawBackground();
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(108, 11, 169)))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            e.Graphics.DrawString(e.Header.Text, listView.Font, Brushes.White, e.Bounds, new StringFormat());
        };

        listView.DrawItem += (sender, e) =>
        {
            e.DrawBackground();
            if ((e.State & ListViewItemStates.Selected) == ListViewItemStates.Selected)
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(108, 11, 169)))
                {
                    e.Graphics.FillRectangle(brush, e.Bounds);
                }
                using (SolidBrush brush = new SolidBrush(Color.White))
                {
                    // Check if the text in the first column exceeds the character limit
                    string firstColumnText = e.Item.Text;
                    if (firstColumnText.Length > 10)
                    {
                        // Truncate the text to 10 characters and add ellipsis
                        firstColumnText = firstColumnText.Substring(0, 10) + "...";
                    }

                    e.Graphics.DrawString(firstColumnText, listView.Font, brush, e.Bounds);
                }
            }
            else
            {
                using (SolidBrush brush = new SolidBrush(Color.White))
                {
                    // Check if the text in the first column exceeds the character limit
                    string firstColumnText = e.Item.Text;
                    if (firstColumnText.Length > 10)
                    {
                        // Truncate the text to 10 characters and add ellipsis
                        firstColumnText = firstColumnText.Substring(0, 10) + "...";
                    }

                    e.Graphics.DrawString(firstColumnText, listView.Font, brush, e.Bounds);
                }
            }

            // Draw subitems
            int subItemX = e.Bounds.Left + listView.Columns[0].Width;
            for (int subItemIndex = 1; subItemIndex < e.Item.SubItems.Count; subItemIndex++)
            {
                Rectangle subItemBounds = new Rectangle(subItemX, e.Bounds.Top, listView.Columns[subItemIndex].Width, e.Bounds.Height);
                string subItemText = e.Item.SubItems[subItemIndex].Text;

                // Measure the width of the text and check if it exceeds the column width
                if (TextRenderer.MeasureText(subItemText, listView.Font).Width > subItemBounds.Width)
                {
                    // Calculate the available width for the ellipsis
                    int ellipsisWidth = TextRenderer.MeasureText("...", listView.Font).Width;

                    // Truncate the text to fit within the available width with ellipsis
                    while (subItemText.Length > 0 && TextRenderer.MeasureText(subItemText + "...", listView.Font).Width > subItemBounds.Width)
                    {
                        subItemText = subItemText.Substring(0, subItemText.Length - 1);
                    }

                    // Append the ellipsis to the truncated text
                    subItemText += "...";
                }

                using (SolidBrush brush = new SolidBrush(Color.White))
                {
                    e.Graphics.DrawString(subItemText, listView.Font, brush, subItemBounds);
                }

                subItemX += listView.Columns[subItemIndex].Width;
            }
        };

        // Create the context menu for the listView
        ContextMenu listViewContextMenu = new ContextMenu();
        MenuItem disableMenuItem = new MenuItem("Disable");
        disableMenuItem.Click += (sender, e) =>
        {
            if (listView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an item to disable.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // Get the selected item from the listView
            ListViewItem selectedItem = listView.SelectedItems[0];
            // Move the item to the listViewDisabled
            listView.Items.Remove(selectedItem);
            listViewDisabled.Items.Add(selectedItem);

            listViewDisabled.Columns[0].Width = 75;
            listViewDisabled.Columns[1].Width = 250;
            listViewDisabled.Columns[2].Width = -2;
            listViewDisabled.Columns[3].Width = -2;
            listViewDisabled.Columns[4].Width = -2;
            listViewDisabled.Columns[5].Width = -2;
        };
        MenuItem removeMenuItem = new MenuItem("Remove");
        removeMenuItem.Click += (sender, e) =>
        {
            // Call the RemoveSelectedItem method to handle the removal
            RemoveSelectedItem();
        };
        listViewContextMenu.MenuItems.Add(disableMenuItem);
        listViewContextMenu.MenuItems.Add(new MenuItem("-")); // Separator
        listViewContextMenu.MenuItems.Add(removeMenuItem);
        listView.ContextMenu = listViewContextMenu;
        // Create the context menu for the listViewDisabled
        ContextMenu listViewDisabledContextMenu = new ContextMenu();
        MenuItem enableMenuItem = new MenuItem("Enable");
        enableMenuItem.Click += (sender, e) =>
        {
            if (listViewDisabled.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an item to enable.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // Get the selected item from the listView
            ListViewItem selectedItem = listViewDisabled.SelectedItems[0];
            listViewDisabled.Items.Remove(selectedItem);
            listView.Items.Add(selectedItem);
            listView.Columns[0].Width = 75;
            listView.Columns[1].Width = 250;
            listView.Columns[2].Width = -2;
            listView.Columns[3].Width = -2;
            listView.Columns[4].Width = -2;
            listView.Columns[5].Width = -2;
        };
        MenuItem removeMenuItem2 = new MenuItem("Remove");
        removeMenuItem2.Click += (sender, e) =>
        {
            // Call the RemoveSelectedItem method to handle the removal
            RemoveSelectedItem();
        };
        listViewDisabledContextMenu.MenuItems.Add(enableMenuItem);
        listViewDisabledContextMenu.MenuItems.Add(new MenuItem("-")); // Separator
        listViewDisabledContextMenu.MenuItems.Add(removeMenuItem2);
        listViewDisabled.ContextMenu = listViewDisabledContextMenu;
        // Add Textbox for Name
        TextBox nameAddBox = new TextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White };
        nameAddBox.BorderStyle = BorderStyle.FixedSingle;
        // Add Textbox for URL
        TextBox urlAddBox = new TextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White };
        urlAddBox.BorderStyle = BorderStyle.FixedSingle;
        // Add Textbox for Height
        TextBox heightAddBox = new TextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White };
        heightAddBox.BorderStyle = BorderStyle.FixedSingle;
        // Add Textbox for Width
        TextBox widthAddBox = new TextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White };
        widthAddBox.BorderStyle = BorderStyle.FixedSingle;
        // Add Textbox for Top
        TextBox topAddBox = new TextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White };
        topAddBox.BorderStyle = BorderStyle.FixedSingle;
        // Add Textbox for Left
        TextBox leftAddBox = new TextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White };
        leftAddBox.BorderStyle = BorderStyle.FixedSingle;
        // Add Button
        Button addButton = new Button { Text = "Add", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(108, 11, 169), ForeColor = Color.White };
        addButton.FlatAppearance.BorderColor = connectButton.BackColor;
        // Remove Button
        Button removeButton = new Button { Text = "Remove", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(108, 11, 169), ForeColor = Color.White };
        removeButton.FlatAppearance.BorderColor = connectButton.BackColor;
        // Up Button
        Button upButton = new Button { Text = "⬆", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(108, 11, 169), ForeColor = Color.White };
        upButton.FlatAppearance.BorderColor = connectButton.BackColor;
        // Down Button
        Button downButton = new Button { Text = "⬇", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(108, 11, 169), ForeColor = Color.White };
        downButton.FlatAppearance.BorderColor = connectButton.BackColor;
        // Repopulate textboxes
        listViewDisabled.SelectedIndexChanged += (sender, args) =>
        {
            PopulateTextBoxesFromSelectedItem(listViewDisabled, nameAddBox, urlAddBox, heightAddBox, widthAddBox, topAddBox, leftAddBox);
        };
        listView.SelectedIndexChanged += (sender, args) =>
        {
            PopulateTextBoxesFromSelectedItem(listView, nameAddBox, urlAddBox, heightAddBox, widthAddBox, topAddBox, leftAddBox);
        };
        // Form Layout
        TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
        tableLayoutPanel.Dock = DockStyle.Fill;
        tableLayoutPanel.RowCount = 6;
        tableLayoutPanel.ColumnCount = 2;
        tableLayoutPanel.BackColor = Color.FromArgb(30, 30, 30);
        tableLayoutPanel.Padding = new Padding(3);
        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));
        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        // Title Panel Layout
        TableLayoutPanel titlePanel = new TableLayoutPanel();
        titlePanel.Dock = DockStyle.Fill;
        titlePanel.RowCount = 1;
        titlePanel.ColumnCount = 4; // Increase column count to 4
        titlePanel.Padding = new Padding(0);
        titlePanel.Margin = new Padding(0);
        titlePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        titlePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30F));
        titlePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30F));
        titlePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30F));

        // Title Label
        Label titleLabel = new Label { Dock = DockStyle.Fill, BackColor = Color.FromArgb(108, 11, 169), ForeColor = Color.White, Padding = new Padding(0), Margin = new Padding(3, 3, 3, 3) };
        titleLabel.Text = "Overlay(er) v1.0.0";
        titleLabel.Font = new Font("Arial", 10, FontStyle.Bold);
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // Enabled Label
        Label listViewLabel = new Label { Dock = DockStyle.Fill, BackColor = Color.FromArgb(108, 11, 169), ForeColor = Color.White, Padding = new Padding(0), Margin = new Padding(3, 3, 3, 3) };
        listViewLabel.Text = "Enabled";
        listViewLabel.Font = new Font(titleLabel.Font.FontFamily, listViewLabel.Font.Size, FontStyle.Bold);
        listViewLabel.TextAlign = ContentAlignment.MiddleLeft;
        // Disabled Label
        Label listViewDisabledLabel = new Label { Dock = DockStyle.Fill, BackColor = Color.FromArgb(108, 11, 169), ForeColor = Color.White, Padding = new Padding(0), Margin = new Padding(3, 3, 3, 3) };
        listViewDisabledLabel.Text = "Disabled";
        listViewDisabledLabel.Font = new Font(listViewDisabledLabel.Font.FontFamily, listViewDisabledLabel.Font.Size, FontStyle.Bold);
        listViewDisabledLabel.TextAlign = ContentAlignment.MiddleLeft;
        // Name Label
        Label overlayName = new Label { Dock = DockStyle.Fill, BackColor = Color.Transparent, ForeColor = Color.White, Padding = new Padding(0), Margin = new Padding(3, 3, 3, 3) };
        overlayName.Text = "Name: ";
        overlayName.Font = new Font(overlayName.Font.FontFamily, overlayName.Font.Size, FontStyle.Bold);
        overlayName.TextAlign = ContentAlignment.MiddleLeft;
        // Name Label
        Label overlayURL = new Label { Dock = DockStyle.Fill, BackColor = Color.Transparent, ForeColor = Color.White, Padding = new Padding(0), Margin = new Padding(3, 3, 3, 3) };
        overlayURL.Text = "URL: ";
        overlayURL.Font = new Font(overlayURL.Font.FontFamily, overlayURL.Font.Size, FontStyle.Bold);
        overlayURL.TextAlign = ContentAlignment.MiddleLeft;
        // Name Label
        Label overlayHeight = new Label { Dock = DockStyle.Fill, BackColor = Color.Transparent, ForeColor = Color.White, Padding = new Padding(0), Margin = new Padding(3, 3, 3, 3) };
        overlayHeight.Text = "Height: ";
        overlayHeight.Font = new Font(overlayURL.Font.FontFamily, overlayURL.Font.Size, FontStyle.Bold);
        overlayHeight.TextAlign = ContentAlignment.MiddleLeft;
        // Name Label
        Label overlayWidth = new Label { Dock = DockStyle.Fill, BackColor = Color.Transparent, ForeColor = Color.White, Padding = new Padding(0), Margin = new Padding(3, 3, 3, 3) };
        overlayWidth.Text = "Width: ";
        overlayWidth.Font = new Font(overlayWidth.Font.FontFamily, overlayWidth.Font.Size, FontStyle.Bold);
        overlayWidth.TextAlign = ContentAlignment.MiddleLeft;
        // Name Label
        Label overlayTop = new Label { Dock = DockStyle.Fill, BackColor = Color.Transparent, ForeColor = Color.White, Padding = new Padding(0), Margin = new Padding(3, 3, 3, 3) };
        overlayTop.Text = "Top (y): ";
        overlayTop.Font = new Font(overlayTop.Font.FontFamily, overlayTop.Font.Size, FontStyle.Bold);
        overlayTop.TextAlign = ContentAlignment.MiddleLeft;
        // Name Label
        Label overlayLeft = new Label { Dock = DockStyle.Fill, BackColor = Color.Transparent, ForeColor = Color.White, Padding = new Padding(0), Margin = new Padding(3, 3, 3, 3) };
        overlayLeft.Text = "Left (x): ";
        overlayLeft.Font = new Font(overlayLeft.Font.FontFamily, overlayLeft.Font.Size, FontStyle.Bold);
        overlayLeft.TextAlign = ContentAlignment.MiddleLeft;
        // Draggable Form
        Point lastLocation = Point.Empty;
        titleLabel.MouseDown += (sender, e) =>
        {
            lastLocation = e.Location;
        };
        titleLabel.MouseMove += (sender, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                form.Left += e.X - lastLocation.X;
                form.Top += e.Y - lastLocation.Y;
            }
        };
        titleLabel.MouseUp += (sender, e) =>
        {
            lastLocation = Point.Empty;
        };
        // Button Layout Panel
        TableLayoutPanel buttonPanel = new TableLayoutPanel();
        buttonPanel.Dock = DockStyle.Fill;
        buttonPanel.RowCount = 1;
        buttonPanel.ColumnCount = 2;
        buttonPanel.Padding = new Padding(0);
        buttonPanel.Margin = new Padding(0);
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanel.SetColumnSpan(buttonPanel, 2);
        // Overlay Layout Panel
        TableLayoutPanel overlayPanel = new TableLayoutPanel();
        overlayPanel.Dock = DockStyle.Fill;
        overlayPanel.RowCount = 4;
        overlayPanel.ColumnCount = 2;
        overlayPanel.Padding = new Padding(0);
        overlayPanel.Margin = new Padding(0);
        overlayPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        overlayPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        overlayPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        overlayPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        overlayPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        overlayPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30F));
        // Move Buttons Layout Panel
        TableLayoutPanel movePanel = new TableLayoutPanel();
        movePanel.Dock = DockStyle.Fill;
        movePanel.RowCount = 2;
        movePanel.ColumnCount = 1;
        movePanel.Padding = new Padding(0);
        movePanel.Margin = new Padding(0);
        movePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        movePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        movePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30F));
        // Name/Url Layout Panel
        TableLayoutPanel addUrlPanel = new TableLayoutPanel();
        addUrlPanel.Dock = DockStyle.Fill;
        addUrlPanel.RowCount = 2;
        addUrlPanel.ColumnCount = 4;
        addUrlPanel.Padding = new Padding(0);
        addUrlPanel.Margin = new Padding(0);
        addUrlPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        addUrlPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        addUrlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45F));
        addUrlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        addUrlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38F));
        addUrlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        // Height/Position Layout Panel
        TableLayoutPanel settingsUrlPanel = new TableLayoutPanel();
        settingsUrlPanel.Dock = DockStyle.Fill;
        settingsUrlPanel.RowCount = 2;
        settingsUrlPanel.ColumnCount = 4;
        settingsUrlPanel.Padding = new Padding(0);
        settingsUrlPanel.Margin = new Padding(0);
        settingsUrlPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        settingsUrlPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        settingsUrlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55F));
        settingsUrlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        settingsUrlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55F));
        settingsUrlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        // TableLayoutPanel Controls
        tableLayoutPanel.Controls.Add(titlePanel, 0, 0);
        tableLayoutPanel.SetColumnSpan(titlePanel, 3);
        tableLayoutPanel.Controls.Add(buttonPanel, 0, 1);
        tableLayoutPanel.Controls.Add(urlBox, 0, 2);
        tableLayoutPanel.SetColumnSpan(urlBox, 2);
        tableLayoutPanel.Controls.Add(overlayPanel, 0, 3);
        tableLayoutPanel.SetColumnSpan(overlayPanel, 2);
        tableLayoutPanel.Controls.Add(addUrlPanel, 0, 4);
        tableLayoutPanel.SetColumnSpan(addUrlPanel, 2);
        tableLayoutPanel.Controls.Add(addButton, 0, 5);
        tableLayoutPanel.Controls.Add(removeButton, 1, 5);
        // Name/URL Panel Controls
        addUrlPanel.Controls.Add(overlayName, 0, 0);
        addUrlPanel.Controls.Add(nameAddBox, 1, 0);
        addUrlPanel.Controls.Add(overlayURL, 2, 0);
        addUrlPanel.Controls.Add(urlAddBox, 3, 0);
        addUrlPanel.Controls.Add(settingsUrlPanel, 0, 1);
        addUrlPanel.SetColumnSpan(settingsUrlPanel, 4);
        // Add the new text boxes
        settingsUrlPanel.Controls.Add(overlayHeight, 0, 0);
        settingsUrlPanel.Controls.Add(heightAddBox, 1, 0);
        settingsUrlPanel.Controls.Add(overlayWidth, 2, 0);
        settingsUrlPanel.Controls.Add(widthAddBox, 3, 0);

        settingsUrlPanel.Controls.Add(overlayTop, 0, 1);
        settingsUrlPanel.Controls.Add(topAddBox, 1, 1);
        settingsUrlPanel.Controls.Add(overlayLeft, 2, 1);
        settingsUrlPanel.Controls.Add(leftAddBox, 3, 1);
        // Title Panel Controls
        titlePanel.Controls.Add(titleLabel, 0, 0);
        titlePanel.Controls.Add(helpButton, 1, 0);
        titlePanel.Controls.Add(miniButton, 2, 0);
        titlePanel.Controls.Add(closeButton, 3, 0);
        // Overlay Panel Controls
        overlayPanel.Controls.Add(listViewLabel, 0, 0);
        overlayPanel.Controls.Add(listView, 0, 1);
        overlayPanel.Controls.Add(listViewDisabledLabel, 0, 2);
        overlayPanel.SetColumnSpan(listViewDisabledLabel, 2);
        overlayPanel.Controls.Add(listViewDisabled, 0, 3);
        overlayPanel.SetColumnSpan(listViewDisabled, 2);
        overlayPanel.Controls.Add(movePanel, 1, 0);
        overlayPanel.SetRowSpan(movePanel, 2);
        // Move Button Panel Controls
        movePanel.Controls.Add(upButton, 0, 0);
        movePanel.Controls.Add(downButton, 0, 1);
        // Connection Button Panel Controls
        buttonPanel.Controls.Add(connectButton, 0, 0);
        buttonPanel.Controls.Add(disconnectButton, 1, 0);
        // Up Button Click Event
        upButton.Click += (sender, args) =>
        {
            if (listView.SelectedItems.Count > 0)
            {
                int selectedIndex = listView.SelectedIndices[0];
                if (selectedIndex > 0)
                {
                    ListViewItem selectedItem = listView.SelectedItems[0];
                    listView.Items.Remove(selectedItem);
                    listView.Items.Insert(selectedIndex - 1, selectedItem);
                }
            }
        };
        // Down Button Click Event
        downButton.Click += (sender, args) =>
        {
            if (listView.SelectedItems.Count > 0)
            {
                int selectedIndex = listView.SelectedIndices[0];
                if (selectedIndex < listView.Items.Count - 1)
                {
                    ListViewItem selectedItem = listView.SelectedItems[0];
                    listView.Items.Remove(selectedItem);
                    listView.Items.Insert(selectedIndex + 1, selectedItem);
                }
            }
        };
        // Add Button Click Event
        addButton.Click += (sender, args) =>
        {
            if (string.IsNullOrWhiteSpace(nameAddBox.Text))
            {
                MessageBox.Show("Please enter a name to add to the list.", "Empty Name");
            }
            else if (string.IsNullOrWhiteSpace(urlAddBox.Text))
            {
                MessageBox.Show("Please enter a URL to add to the list.", "Empty URL");
            }
            else if (!urlAddBox.Text.ToLower().Contains("http://") && !urlAddBox.Text.ToLower().Contains("https://") && !urlAddBox.Text.ToLower().Contains("file:///"))
            {
                MessageBox.Show("Please enter a valid URL starting with 'https:// or file:///'", "Invalid URL");
            }
            else if (!string.IsNullOrEmpty(heightAddBox.Text) && !heightAddBox.Text.Contains("%") && !heightAddBox.Text.ToLower().Contains("px"))
            {
                MessageBox.Show("Please enter a valid height value containing '%' or 'px'", "Invalid Height");
            }
            else if (!string.IsNullOrEmpty(widthAddBox.Text) && !widthAddBox.Text.Contains("%") && !widthAddBox.Text.ToLower().Contains("px"))
            {
                MessageBox.Show("Please enter a valid width value containing '%' or 'px'", "Invalid Width");
            }
            else if (!string.IsNullOrEmpty(topAddBox.Text) && !topAddBox.Text.Contains("%") && !topAddBox.Text.ToLower().Contains("px"))
            {
                MessageBox.Show("Please enter a valid top value containing '%' or 'px'", "Invalid Top");
            }
            else if (!string.IsNullOrEmpty(leftAddBox.Text) && !leftAddBox.Text.Contains("%") && !leftAddBox.Text.ToLower().Contains("px"))
            {
                MessageBox.Show("Please enter a valid left value containing '%' or 'px'", "Invalid Left");
            }
            else
            {
                string name = nameAddBox.Text;
                ListViewItem existingItem = listView.FindItemWithText(name);
                ListViewItem existingDisabledItem = listViewDisabled.FindItemWithText(name);

                if (existingItem != null)
                {
                    // An item with the same name already exists in listView, so update the existing item
                    existingItem.SubItems[1].Text = urlAddBox.Text;
                    existingItem.SubItems[2].Text = string.IsNullOrWhiteSpace(heightAddBox.Text) ? "100%" : heightAddBox.Text;
                    existingItem.SubItems[3].Text = string.IsNullOrWhiteSpace(widthAddBox.Text) ? "100%" : widthAddBox.Text;
                    existingItem.SubItems[4].Text = string.IsNullOrWhiteSpace(topAddBox.Text.ToLower()) ? "0px" : topAddBox.Text.ToLower();
                    existingItem.SubItems[5].Text = string.IsNullOrWhiteSpace(leftAddBox.Text.ToLower()) ? "0px" : leftAddBox.Text.ToLower();
                }
                else if (existingDisabledItem != null)
                {
                    // An item with the same name already exists in listViewDisabled, so update the existing item
                    existingDisabledItem.SubItems[1].Text = urlAddBox.Text;
                    existingDisabledItem.SubItems[2].Text = string.IsNullOrWhiteSpace(heightAddBox.Text) ? "100%" : heightAddBox.Text;
                    existingDisabledItem.SubItems[3].Text = string.IsNullOrWhiteSpace(widthAddBox.Text) ? "100%" : widthAddBox.Text;
                    existingDisabledItem.SubItems[4].Text = string.IsNullOrWhiteSpace(topAddBox.Text.ToLower()) ? "0px" : topAddBox.Text.ToLower();
                    existingDisabledItem.SubItems[5].Text = string.IsNullOrWhiteSpace(leftAddBox.Text.ToLower()) ? "0px" : leftAddBox.Text.ToLower();
                }
                else
                {

                    // Create a new ListViewItem
                    ListViewItem newItem = new ListViewItem();
                    // Set the Name as the first column
                    newItem.Text = nameAddBox.Text;
                    // Add the URL as a subitem
                    newItem.SubItems.Add(urlAddBox.Text);
                    // Add the height, width, top, and left values to the ListViewItem
                    newItem.SubItems.Add(string.IsNullOrWhiteSpace(heightAddBox.Text) ? "100%" : heightAddBox.Text);
                    newItem.SubItems.Add(string.IsNullOrWhiteSpace(widthAddBox.Text) ? "100%" : widthAddBox.Text);
                    newItem.SubItems.Add(string.IsNullOrWhiteSpace(topAddBox.Text.ToLower()) ? "0px" : topAddBox.Text.ToLower());
                    newItem.SubItems.Add(string.IsNullOrWhiteSpace(leftAddBox.Text.ToLower()) ? "0px" : leftAddBox.Text.ToLower());
                    // Add the new ListViewItem to the listView
                    listView.Items.Add(newItem);
                    listView.Columns[0].Width = 75;
                    listView.Columns[1].Width = 250;
                    listView.Columns[2].Width = -2;
                    listView.Columns[3].Width = -2;
                    listView.Columns[4].Width = -2;
                    listView.Columns[5].Width = -2;
                }

                // Clear the nameAddBox, urlAddBox, heightAddBox, widthAddBox, topAddBox, and leftAddBox
                nameAddBox.Text = "";
                urlAddBox.Text = "";
                heightAddBox.Text = "";
                widthAddBox.Text = "";
                topAddBox.Text = "";
                leftAddBox.Text = "";
            }
        };
        // Remove Button Click Event
        removeButton.Click += (sender, e) =>
        {
            if (listView.SelectedItems.Count > 0)
            {
                var confirmResult = MessageBox.Show("Are you sure to delete this URL from the list?", "Confirm Removal", MessageBoxButtons.YesNo);
                if (confirmResult == DialogResult.Yes)
                {
                    foreach (ListViewItem item in listView.SelectedItems)
                    {
                        listView.Items.Remove(item);
                    }
                }
            }
            if (listViewDisabled.SelectedItems.Count > 0)
            {
                var confirmResult = MessageBox.Show("Are you sure to delete this URL from the list?", "Confirm Removal", MessageBoxButtons.YesNo);
                if (confirmResult == DialogResult.Yes)
                {
                    foreach (ListViewItem item in listViewDisabled.SelectedItems)
                    {
                        listViewDisabled.Items.Remove(item);
                    }
                }
            }
        };
        // Delete Item Click Event Listview
        listView.KeyDown += (sender, args) =>
        {
            if (args.KeyCode == Keys.Delete)
            {
                if (MessageBox.Show("Are you sure you want to delete the selected items?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    foreach (ListViewItem item in listView.SelectedItems)
                    {
                        listView.Items.Remove(item);
                    }
                }
            }
        };
        // Delete Item Click Event
        listViewDisabled.KeyDown += (sender, args) =>
        {
            if (args.KeyCode == Keys.Delete)
            {
                if (MessageBox.Show("Are you sure you want to delete the selected items?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    foreach (ListViewItem item in listViewDisabled.SelectedItems)
                    {
                        listViewDisabled.Items.Remove(item);
                    }
                }
            }
        };
        // Connect Button Click Event
        connectButton.Click += (sender, args) =>
        {
            disconnectButton.Enabled = true;
            connectButton.Enabled = false;

            string url = StartServer();
            urlBox.ForeColor = Color.White;
            urlBox.Text = "Add to OBS: " + url;
        };
        // Disconnect Button Click Event
        disconnectButton.Click += (sender, args) =>
        {
            disconnectButton.Enabled = false;
            connectButton.Enabled = true;
            _listener?.Stop();
            StopServer();
            urlBox.ForeColor = Color.Gray;
            urlBox.Text = "Not connected";
        };
        form.Controls.Add(tableLayoutPanel);
        // Load the data from the JSON file on form load
        form.Load += (sender, args) =>
        {
            // Check and create "overlayer" folder if it doesn't exist
            string overlayerFolderPath = "overlayer";
            if (!Directory.Exists(overlayerFolderPath))
            {
                Directory.CreateDirectory(overlayerFolderPath);
            }

            // Check and create "listview.json" file if it doesn't exist
            string jsonFilePath = Path.Combine(overlayerFolderPath, "listview.json");
            if (!File.Exists(jsonFilePath))
            {
                // Create an empty JSON file
                File.WriteAllText(jsonFilePath, "{}");
            }

            if (File.Exists("overlayer/listview.json"))
            {
                string json = File.ReadAllText("overlayer/listview.json");
                if (!string.IsNullOrEmpty(json))
                {
                    ListViewData data = JsonConvert.DeserializeObject<ListViewData>(json);
                    if (data != null && data.Enabled != null && data.Disabled != null)
                    {
                        // Load the enabled items into the listView control
                        foreach (var itemData in data.Enabled)
                        {
                            ListViewItem item = new ListViewItem(itemData["Name"]);
                            item.SubItems.Add(itemData["URL"]);
                            item.SubItems.Add(itemData["Height"]);
                            item.SubItems.Add(itemData["Width"]);
                            item.SubItems.Add(itemData["Top"]);
                            item.SubItems.Add(itemData["Left"]);
                            listView.Items.Add(item);
                        }
                        // Load the disabled items into the listViewDisabled control
                        foreach (var itemData in data.Disabled)
                        {
                            ListViewItem item = new ListViewItem(itemData["Name"]);
                            item.SubItems.Add(itemData["URL"]);
                            item.SubItems.Add(itemData["Height"]);
                            item.SubItems.Add(itemData["Width"]);
                            item.SubItems.Add(itemData["Top"]);
                            item.SubItems.Add(itemData["Left"]);
                            listViewDisabled.Items.Add(item);
                            // Handle the Resize event to adjust column widths
                            int nameColumnWidth = CalculateMaxNameColumnWidth(listViewDisabled);
                            listViewDisabled.Columns[0].Width = nameColumnWidth;
                            listViewDisabled.Columns[1].Width = nameColumnWidth;
                        }
                        listView.Columns[0].Width = 75;
                        listView.Columns[1].Width = 250;
                        listView.Columns[2].Width = -2;
                        listView.Columns[3].Width = -2;
                        listView.Columns[4].Width = -2;
                        listView.Columns[5].Width = -2;

                        listViewDisabled.Columns[0].Width = 75;
                        listViewDisabled.Columns[1].Width = 250;
                        listViewDisabled.Columns[2].Width = -2;
                        listViewDisabled.Columns[3].Width = -2;
                        listViewDisabled.Columns[4].Width = -2;
                        listViewDisabled.Columns[5].Width = -2;
                    }
                }
            }
        };
        form.FormClosing += (sender, args) =>
        {
            // Get the items in the listView control
            var enabledItems = new List<Dictionary<string, string>>();
            for (int i = 0; i < listView.Items.Count; i++)
            {
                var itemData = new Dictionary<string, string>
                {
                    { "Name", listView.Items[i].Text },
                    { "URL", listView.Items[i].SubItems[1].Text },
                    { "Height", listView.Items[i].SubItems[2].Text },
                    { "Width", listView.Items[i].SubItems[3].Text },
                    { "Top", listView.Items[i].SubItems[4].Text },
                    { "Left", listView.Items[i].SubItems[5].Text }
                };
                enabledItems.Add(itemData);
            }
            // Get the items in the listViewDisabled control
            var disabledItems = new List<Dictionary<string, string>>();
            for (int i = 0; i < listViewDisabled.Items.Count; i++)
            {
                var itemData = new Dictionary<string, string>
                {
                    { "Name", listViewDisabled.Items[i].Text },
                    { "URL", listViewDisabled.Items[i].SubItems[1].Text },
                    { "Height", listViewDisabled.Items[i].SubItems[2].Text },
                    { "Width", listViewDisabled.Items[i].SubItems[3].Text },
                    { "Top", listViewDisabled.Items[i].SubItems[4].Text },
                    { "Left", listViewDisabled.Items[i].SubItems[5].Text }
                };
                disabledItems.Add(itemData);
            }
            // Create an object to store both sets of items
            var jsonObject = new { Enabled = enabledItems, Disabled = disabledItems };
            // Serialize the object to JSON
            string json = JsonConvert.SerializeObject(jsonObject);
            // Write the JSON to the listview.json file
            File.WriteAllText("overlayer/listview.json", json);
        };
        form.ShowDialog();
        return true;
    }

    private string StartServer()
    {
        _listener = new HttpListener();
        string url = "http://localhost:42069/";
        _listener.Prefixes.Add(url);
        _listener.Start();
        _listener.BeginGetContext(OnContext, _listener);

        bool hasLocalFiles = false;
        foreach (ListViewItem item in listView.Items)
        {
            if (item.SubItems[1].Text.StartsWith("file:///"))
            {
                hasLocalFiles = true;
                break;
            }
        }

        if (hasLocalFiles)
        {
            _localFileListener = new HttpListener();
            string localFileUrl = "http://localhost:42070/localfile/";
            _localFileListener.Prefixes.Add(localFileUrl);
            _localFileListener.Start();
            _localFileListener.BeginGetContext(OnLocalFileContext, _localFileListener);
        }

        return url;
    }

    private void StopServer()
    {
        if (_listener != null)
        {
            _listener.Stop();
            _listener.Close();
            _listener = null;
        }
        if (_localFileListener != null)
        {
            _localFileListener.Stop();
            _localFileListener.Close();
            _localFileListener = null;
        }
    }

    private void OnContext(IAsyncResult ar)
    {
        try
        {
            HttpListener listener = (HttpListener)ar.AsyncState;
            if (listener == null || !listener.IsListening) return;
            HttpListenerContext context = listener.EndGetContext(ar);
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            string htmlContent = "<!DOCTYPE html><html><head><meta charset=\"UTF-8\"><title>My Page</title><style>iframe {position: absolute;border: none;}</style></head><body>";
            foreach (ListViewItem item in listView.Items)
            {
                htmlContent += "<iframe src='";
                if (item.SubItems[1].Text.StartsWith("file:///"))
                {
                    Uri fileUri = new Uri(item.SubItems[1].Text);
                    string localFilePath = fileUri.LocalPath; // Extract local file path using Uri class
                    string directoryPath = Path.GetFullPath(Path.GetDirectoryName(localFilePath)); // Extract and convert directory path to absolute path using Path class
                    string[] fileNames = Directory.GetFiles(directoryPath);
                    string fileNamez = Path.GetFileName(localFilePath);
                    foreach (string fileName in fileNames)
                    {
                        if (!_localFilePaths.ContainsKey(Path.GetFileName(fileName)))
                        {
                            _localFilePaths.Add(Path.GetFileName(fileName), new List<string>());
                        }
                        _localFilePaths[Path.GetFileName(fileName)].Add(directoryPath);
                        try
                        {
                            byte[] fileBytes = File.ReadAllBytes(Path.Combine(directoryPath, fileName));
                            // do something with fileBytes
                        }
                        catch (Exception ex)
                        {
                            CPH.LogVerbose($"Error reading file {Path.Combine(directoryPath, fileName)}: {ex.Message}");
                        }
                    }
                    htmlContent += "http://localhost:42070/localfile/" + fileNamez + "?id=" + Guid.NewGuid().ToString();

                }
                else
                {
                    htmlContent += item.SubItems[1].Text;
                }
                htmlContent += "' style='";
                if (!string.IsNullOrEmpty(item.SubItems[2].Text))
                {
                    htmlContent += "height: " + item.SubItems[2].Text + ";";
                }
                if (!string.IsNullOrEmpty(item.SubItems[3].Text))
                {
                    htmlContent += "width: " + item.SubItems[3].Text + ";";
                }
                if (!string.IsNullOrEmpty(item.SubItems[4].Text))
                {
                    htmlContent += "top: " + item.SubItems[4].Text + ";";
                }
                if (!string.IsNullOrEmpty(item.SubItems[5].Text))
                {
                    htmlContent += "left: " + item.SubItems[5].Text + ";";
                }
                htmlContent += "overflow: hidden;' scrolling='no'></iframe>";
            }
            htmlContent += "</body></html>";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(htmlContent);
            response.ContentType = "text/html";
            response.ContentLength64 = buffer.Length;
            if (response != null && response.OutputStream != null)
            {
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.Close();
            }
            if (listener != null && listener.IsListening)
            {
                listener.BeginGetContext(OnContext, listener);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error in OnLocalFileContext method: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Dictionary<string, List<string>> _localFilePaths = new Dictionary<string, List<string>>();

    private void OnLocalFileContext(IAsyncResult ar)
    {
        try
        {
            HttpListener listener = (HttpListener)ar.AsyncState;
            if (listener == null || !listener.IsListening) return;
            HttpListenerContext context = listener.EndGetContext(ar);
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            // Extract the local file path from the request URL
            string fileName = Uri.UnescapeDataString(request.Url.LocalPath.Substring("/localfile/".Length));
            List<string> directoryPaths;
            if (_localFilePaths.TryGetValue(Path.GetFileName(fileName), out directoryPaths))
            {
                string localFilePath = null;
                foreach (string directoryPath in directoryPaths)
                {
                    string candidateFilePath = Path.Combine(directoryPath, fileName);
                    if (File.Exists(candidateFilePath))
                    {
                        localFilePath = candidateFilePath;
                        break;
                    }
                }
                if (localFilePath != null)
                {
                    try
                    {
                        using (FileStream fs = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            byte[] buffer = new byte[fs.Length];
                            fs.Read(buffer, 0, buffer.Length);
                            response.ContentType = MimeMapping.GetMimeMapping(localFilePath);
                            response.ContentLength64 = buffer.Length;
                            response.OutputStream.Write(buffer, 0, buffer.Length);
                        }
                    }
                    catch (IOException ex)
                    {
                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        string fileInUseContent = $"Error reading file {localFilePath}: {ex.Message}";
                        byte[] fileInUseBuffer = Encoding.UTF8.GetBytes(fileInUseContent);
                        response.ContentType = "text/plain";
                        response.ContentLength64 = fileInUseBuffer.Length;
                        response.OutputStream.Write(fileInUseBuffer, 0, fileInUseBuffer.Length);
                    }
                }
                else
                {
                    // Send a 404 error if the file does not exist
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    string notFoundContent = "File not found";
                    byte[] notFoundBuffer = Encoding.UTF8.GetBytes(notFoundContent);
                    response.ContentType = "text/plain";
                    response.ContentLength64 = notFoundBuffer.Length;
                    response.OutputStream.Write(notFoundBuffer, 0, notFoundBuffer.Length);
                }
            }
            else if (request.Url.LocalPath == "/localfile/")
            {
                // Serve the local files directory listing
                StringBuilder htmlContent = new StringBuilder();
                htmlContent.Append("<html><body><ul>");
                foreach (string innerFileName in _localFilePaths.Keys)
                {
                    htmlContent.AppendFormat("<li><a href=\"/localfile/{0}\">{0}</a></li>", Uri.EscapeDataString(innerFileName));
                }
                htmlContent.Append("</ul></body></html>");
                byte[] buffer = Encoding.UTF8.GetBytes(htmlContent.ToString());
                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            else
            {
                // Send a 404 error if the file does not exist
                response.StatusCode = (int)HttpStatusCode.NotFound;
                string notFoundContent = "File not found";
                byte[] notFoundBuffer = Encoding.UTF8.GetBytes(notFoundContent);
                response.ContentType = "text/plain";
                response.ContentLength64 = notFoundBuffer.Length;
                response.OutputStream.Write(notFoundBuffer, 0, notFoundBuffer.Length);
            }
            response.Close();
            if (listener != null && listener.IsListening)
            {
                listener.BeginGetContext(OnLocalFileContext, listener);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error in OnLocalFileContext method: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Update the ListViewData class
    public class ListViewData
    {
        public List<Dictionary<string, string>> Enabled { get; set; }
        public List<Dictionary<string, string>> Disabled { get; set; }
    }

    private void RemoveSelectedItem()
    {
        if (listView.SelectedItems.Count > 0)
        {
            foreach (ListViewItem item in listView.SelectedItems)
            {
                listView.Items.Remove(item);
            }
        }

        if (listViewDisabled.SelectedItems.Count > 0)
        {
            foreach (ListViewItem item in listViewDisabled.SelectedItems)
            {
                listViewDisabled.Items.Remove(item);
            }
        }
    }

    private void PopulateTextBoxesFromSelectedItem(ListView listView, TextBox nameBox, TextBox urlBox, TextBox heightBox, TextBox widthBox, TextBox topBox, TextBox leftBox)
    {
        if (listView.SelectedItems.Count > 0)
        {
            ListViewItem selectedItem = listView.SelectedItems[0];
            nameBox.Text = selectedItem.Text;
            urlBox.Text = selectedItem.SubItems[1].Text;
            heightBox.Text = selectedItem.SubItems[2].Text;
            widthBox.Text = selectedItem.SubItems[3].Text;
            topBox.Text = selectedItem.SubItems[4].Text;
            leftBox.Text = selectedItem.SubItems[5].Text;
        }
        else
        {
            nameBox.Text = "";
            urlBox.Text = "";
            heightBox.Text = "";
            widthBox.Text = "";
            topBox.Text = "";
            leftBox.Text = "";
        }
    }

    private int CalculateMaxNameColumnWidth(ListView listView)
    {
        int maxWidth = 0;
        using (Graphics g = listView.CreateGraphics())
        {
            foreach (ListViewItem item in listView.Items)
            {
                int itemWidth = (int)g.MeasureString(item.Text, listView.Font).Width;
                if (itemWidth > maxWidth)
                {
                    maxWidth = itemWidth;
                }
            }
            foreach (ListViewItem item in listViewDisabled.Items)
            {
                int itemWidth = (int)g.MeasureString(item.Text, listViewDisabled.Font).Width;
                if (itemWidth > maxWidth)
                {
                    maxWidth = itemWidth;
                }
            }
        }
        return maxWidth;
    }

    private void ShowHelpPopup()
    {
        string helpText = "Overlay(er) v1.0.0 Help\n\n" +
            "1. Add URL: Enter a name and URL, then click 'Add'.\n" +
            "2. Remove URL: Select an item and click 'Remove'.\n" +
            "3. Enable/Disable: Right-click an item to enable or disable it.\n" +
            "4. Start Server: Click 'Start Server' to begin hosting.\n" +
            "5. Stop Server: Click 'Stop Server' to stop hosting.\n" +
            "6. OBS Setup: Add a browser source with the URL shown when the server is running.\n" +
            "7. Please note that some URLs may not work due to the limitations of the C# HttpListener.\n" +
            "8. If you encounter any issues, please report them to the developer. (twitter.com/dreadedzombietv)";

        MessageBox.Show(helpText, "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

}