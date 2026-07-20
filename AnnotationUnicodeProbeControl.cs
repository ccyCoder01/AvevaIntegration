using System;
using System.Collections;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using Aveva.PDMS.PMLNet;

namespace AvevaIntegration
{
    [PMLNetCallable()]
    public class AnnotationAutoLayoutStatusControl : UserControl
    {
        private readonly Label drawingValue;
        private readonly Label stateValue;
        private readonly Label stageValue;
        private readonly Label batchValue;
        private readonly Label movesValue;
        private readonly Label runValue;
        private readonly Label progressText;
        private readonly Label messageValue;
        private readonly ProgressBar progressBar;
        private readonly Button startButton;
        private readonly Button cancelButton;

        [PMLNetCallable()]
        public event PMLNetDelegate.PMLNetEventHandler StartRequested;

        [PMLNetCallable()]
        public event PMLNetDelegate.PMLNetEventHandler CancelRequested;

        [PMLNetCallable()]
        public AnnotationAutoLayoutStatusControl()
        {
            BackColor = SystemColors.Control;
            Padding = new Padding(8);
            AutoScroll = true;
            AutoScaleMode = AutoScaleMode.Font;
            Dock = DockStyle.Fill;
            MinimumSize = new Size(400, 240);
            MaximumSize = new Size(560, 360);
            Size = new Size(480, 280);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.Padding = new Padding(4);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 156.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));

            TableLayoutPanel title = new TableLayoutPanel();
            title.Dock = DockStyle.Fill;
            title.ColumnCount = 1;
            title.RowCount = 2;
            title.RowStyles.Add(new RowStyle(SizeType.Percent, 62.0f));
            title.RowStyles.Add(new RowStyle(SizeType.Percent, 38.0f));
            Label titleLabel = CreateLabel("\u6807\u6CE8\u81EA\u52A8\u6392\u7248", 16, FontStyle.Bold);
            drawingValue = CreateValueLabel("\u5F53\u524D\u56FE\u7EB8\uFF1A\u672A\u68C0\u67E5");
            title.Controls.Add(titleLabel, 0, 0);
            title.Controls.Add(drawingValue, 0, 1);
            root.Controls.Add(title, 0, 0);

            TableLayoutPanel summary = new TableLayoutPanel();
            summary.Dock = DockStyle.Fill;
            summary.ColumnCount = 2;
            summary.RowCount = 5;
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72.0f));
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
            AddSummaryRow(summary, 0, "\u72B6\u6001\uFF1A", out stateValue);
            AddSummaryRow(summary, 1, "\u9636\u6BB5\uFF1A", out stageValue);
            AddSummaryRow(summary, 2, "\u6279\u6B21\uFF1A", out batchValue);
            AddSummaryRow(summary, 3, "Moves\uFF1A", out movesValue);
            AddSummaryRow(summary, 4, "Run ID\uFF1A", out runValue);
            root.Controls.Add(summary, 0, 1);

            TableLayoutPanel progress = new TableLayoutPanel();
            progress.Dock = DockStyle.Fill;
            progress.ColumnCount = 1;
            progress.RowCount = 2;
            progress.RowStyles.Add(new RowStyle(SizeType.Percent, 62.0f));
            progress.RowStyles.Add(new RowStyle(SizeType.Percent, 38.0f));
            progressBar = new ProgressBar();
            progressBar.Dock = DockStyle.Fill;
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressText = CreateValueLabel("0 / 0");
            progress.Controls.Add(progressBar, 0, 0);
            progress.Controls.Add(progressText, 0, 1);
            root.Controls.Add(progress, 0, 2);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.WrapContents = false;
            startButton = CreateButton("\u5F00\u59CB\u6392\u7248", 150);
            cancelButton = CreateButton("\u53D6\u6D88", 100);
            startButton.Click += new EventHandler(StartButtonClick);
            cancelButton.Click += new EventHandler(CancelButtonClick);
            actions.Controls.Add(startButton);
            actions.Controls.Add(cancelButton);
            root.Controls.Add(actions, 0, 3);

            Panel messagePanel = new Panel();
            messagePanel.Dock = DockStyle.Fill;
            messageValue = CreateValueLabel(string.Empty);
            messagePanel.Controls.Add(messageValue);
            root.Controls.Add(messagePanel, 0, 4);
            Controls.Add(root);

            SetStartEnabled(true);
            SetCancelEnabled(false);
            SetStatus("STATE=IDLE | STAGE=READY | BATCH=0/0 | MOVES=0/0 | DRAWING=NOT_CHECKED | RUN_ID= | MESSAGE=READY", "READY", "0/0", "DRAWING=NOT_CHECKED");
            Trace.WriteLine("UI_CONTROL_CREATED | type=AnnotationAutoLayoutStatusControl | build=20260720.1");
        }

        private Label CreateLabel(string text, float size, FontStyle style)
        {
            Label label = CreateValueLabel(text);
            label.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, size, style);
            return label;
        }

        private Label CreateValueLabel(string text)
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Text = text;
            label.Margin = new Padding(2);
            label.AutoEllipsis = true;
            return label;
        }

        private Button CreateButton(string text, int width)
        {
            Button button = new Button();
            button.Width = width;
            button.Height = 32;
            button.Margin = new Padding(4, 4, 8, 4);
            button.Text = text;
            button.Font = SystemFonts.MessageBoxFont;
            return button;
        }

        private void AddSummaryRow(TableLayoutPanel table, int row, string name, out Label value)
        {
            table.Controls.Add(CreateValueLabel(name), 0, row);
            value = CreateValueLabel(string.Empty);
            table.Controls.Add(value, 1, row);
        }

        private void StartButtonClick(object sender, EventArgs args)
        {
            RaiseEvent(StartRequested);
        }

        private void CancelButtonClick(object sender, EventArgs args)
        {
            RaiseEvent(CancelRequested);
        }

        private static void RaiseEvent(PMLNetDelegate.PMLNetEventHandler handler)
        {
            if (handler != null)
            {
                handler(new ArrayList());
            }
        }

        [PMLNetCallable()]
        public void SetStartEnabled(bool enabled)
        {
            startButton.Enabled = enabled;
        }

        [PMLNetCallable()]
        public void SetCancelEnabled(bool enabled)
        {
            cancelButton.Enabled = enabled;
        }

        [PMLNetCallable()]
        public void SetStatus(string status, string stage, string batch, string drawing)
        {
            string state = GetField(status, "STATE");
            string moves = GetField(status, "MOVES");
            string runId = GetField(status, "RUN_ID");
            string message = GetField(status, "MESSAGE");
            stateValue.Text = MapState(state);
            stageValue.Text = MapStage(stage);
            batchValue.Text = Display(batch);
            movesValue.Text = Display(moves);
            runValue.Text = Display(runId);
            drawingValue.Text = Display(drawing);
            messageValue.Text = MapMessage(state, message);
            int completed;
            int total;
            if (!TrySplitPair(moves, out completed, out total))
            {
                TrySplitPair(batch, out completed, out total);
            }
            if (total < 0) total = 0;
            if (completed < 0) completed = 0;
            if (completed > total && total > 0) completed = total;
            int percentage = total > 0 ? (completed * 100) / total : 0;
            if (percentage < 0) percentage = 0;
            if (percentage > 100) percentage = 100;
            progressBar.Maximum = 100;
            progressBar.Value = percentage;
            progressText.Text = (total > 0 ? completed.ToString() + " / " + total.ToString() : "0 / 0") +
                "  " + percentage.ToString() + "%";
            if (string.Equals(state, "COMPLETE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(state, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                progressBar.Value = progressBar.Maximum;
                progressText.Text = (total > 0 ? total.ToString() + " / " + total.ToString() : "0 / 0") + "  100%";
            }
        }

        private static string GetField(string text, string key)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string marker = key + "=";
            int start = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return string.Empty;
            start += marker.Length;
            int end = text.IndexOf(" | ", start, StringComparison.Ordinal);
            return end < 0 ? text.Substring(start) : text.Substring(start, end - start);
        }

        private static bool TrySplitPair(string text, out int first, out int second)
        {
            first = 0;
            second = 0;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Split('/');
            return parts.Length == 2 && int.TryParse(parts[0], out first) && int.TryParse(parts[1], out second);
        }

        private static string Display(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }

        private static string MapState(string value)
        {
            if (string.Equals(value, "RUNNING", StringComparison.OrdinalIgnoreCase)) return "\u6B63\u5728\u8FD0\u884C";
            if (string.Equals(value, "COMPLETE", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "COMPLETED", StringComparison.OrdinalIgnoreCase)) return "\u5DF2\u5B8C\u6210";
            if (string.Equals(value, "FAILED", StringComparison.OrdinalIgnoreCase)) return "\u6267\u884C\u5931\u8D25";
            if (string.Equals(value, "CANCELLED", StringComparison.OrdinalIgnoreCase)) return "\u5DF2\u53D6\u6D88";
            return "\u51C6\u5907\u5C31\u7EEA";
        }

        private static string MapStage(string value)
        {
            if (string.IsNullOrEmpty(value) || string.Equals(value, "READY", StringComparison.OrdinalIgnoreCase)) return "\u7B49\u5F85\u5F00\u59CB";
            if (string.Equals(value, "Preparing", StringComparison.OrdinalIgnoreCase)) return "\u6B63\u5728\u51C6\u5907";
            if (string.Equals(value, "WaitingAlgorithm", StringComparison.OrdinalIgnoreCase)) return "\u6B63\u5728\u7B49\u5F85\u6392\u7248\u7B97\u6CD5";
            if (string.Equals(value, "MovingText", StringComparison.OrdinalIgnoreCase)) return "\u6B63\u5728\u79FB\u52A8\u6587\u5B57";
            if (string.Equals(value, "PreviewingBatch", StringComparison.OrdinalIgnoreCase)) return "\u6B63\u5728\u68C0\u67E5\u539F\u59CB\u51E0\u4F55";
            if (string.Equals(value, "ApplyingBatch", StringComparison.OrdinalIgnoreCase)) return "\u6B63\u5728\u66F4\u65B0\u4E0B\u5212\u7EBF\u548C\u5F15\u7EBF";
            if (string.Equals(value, "VerifyingBatch", StringComparison.OrdinalIgnoreCase)) return "\u6B63\u5728\u9A8C\u8BC1\u6392\u7248\u7ED3\u679C";
            if (string.Equals(value, "Completed", StringComparison.OrdinalIgnoreCase)) return "\u6392\u7248\u5B8C\u6210";
            return Display(value);
        }

        private static string MapMessage(string state, string message)
        {
            if (string.Equals(state, "COMPLETE", StringComparison.OrdinalIgnoreCase) || string.Equals(state, "COMPLETED", StringComparison.OrdinalIgnoreCase)) return "\u6392\u7248\u5B8C\u6210\uFF0C\u53EF\u4EE5\u4FDD\u5B58\u56FE\u7EB8";
            if (string.Equals(state, "FAILED", StringComparison.OrdinalIgnoreCase)) return "\u6267\u884C\u5931\u8D25\uFF1A" + Display(message);
            if (string.Equals(state, "CANCELLED", StringComparison.OrdinalIgnoreCase)) return "\u5DF2\u53D6\u6D88";
            return Display(message);
        }

        [PMLNetCallable()]
        public void Assign(AnnotationAutoLayoutStatusControl that)
        {
        }
    }
}
