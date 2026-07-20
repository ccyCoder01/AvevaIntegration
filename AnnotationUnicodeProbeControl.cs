using System.Collections;
using System.Drawing;
using System.Windows.Forms;
using Aveva.PDMS.PMLNet;

namespace AvevaIntegration
{
    [PMLNetCallable()]
    public class AnnotationUnicodeProbeControl : UserControl
    {
        private readonly Button startButton;
        private readonly Button cancelButton;
        private readonly Button openLogButton;
        private readonly Button closeButton;

        [PMLNetCallable()]
        public event PMLNetDelegate.PMLNetEventHandler StartRequested;

        [PMLNetCallable()]
        public event PMLNetDelegate.PMLNetEventHandler CancelRequested;

        [PMLNetCallable()]
        public event PMLNetDelegate.PMLNetEventHandler OpenLogRequested;

        [PMLNetCallable()]
        public event PMLNetDelegate.PMLNetEventHandler CloseRequested;

        [PMLNetCallable()]
        public AnnotationUnicodeProbeControl()
        {
            BackColor = SystemColors.Control;

            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.Padding = new Padding(4);
            table.ColumnCount = 2;
            table.RowCount = 3;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50.0f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50.0f));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28.0f));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 50.0f));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 50.0f));

            Label title = new Label();
            title.Dock = DockStyle.Fill;
            title.Font = SystemFonts.MessageBoxFont;
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.Text = "\u6807\u6CE8\u81EA\u52A8\u6392\u7248";
            table.Controls.Add(title, 0, 0);
            table.SetColumnSpan(title, 2);

            startButton = CreateButton("\u5F00\u59CB\u6392\u7248");
            cancelButton = CreateButton("\u53D6\u6D88");
            openLogButton = CreateButton("\u67E5\u770B\u65E5\u5FD7");
            closeButton = CreateButton("\u5173\u95ED");

            startButton.Click += new System.EventHandler(StartButtonClick);
            cancelButton.Click += new System.EventHandler(CancelButtonClick);
            openLogButton.Click += new System.EventHandler(OpenLogButtonClick);
            closeButton.Click += new System.EventHandler(CloseButtonClick);

            table.Controls.Add(startButton, 0, 1);
            table.Controls.Add(cancelButton, 1, 1);
            table.Controls.Add(openLogButton, 0, 2);
            table.Controls.Add(closeButton, 1, 2);
            Controls.Add(table);

            startButton.Enabled = true;
            cancelButton.Enabled = false;
            openLogButton.Enabled = true;
            closeButton.Enabled = true;
        }

        private Button CreateButton(string text)
        {
            Button button = new Button();
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(4);
            button.Font = SystemFonts.MessageBoxFont;
            button.Text = text;
            return button;
        }

        private void StartButtonClick(object sender, System.EventArgs args)
        {
            RaiseEvent(StartRequested);
        }

        private void CancelButtonClick(object sender, System.EventArgs args)
        {
            RaiseEvent(CancelRequested);
        }

        private void OpenLogButtonClick(object sender, System.EventArgs args)
        {
            RaiseEvent(OpenLogRequested);
        }

        private void CloseButtonClick(object sender, System.EventArgs args)
        {
            RaiseEvent(CloseRequested);
        }

        private static void RaiseEvent(
            PMLNetDelegate.PMLNetEventHandler eventHandler)
        {
            if (eventHandler != null)
            {
                eventHandler(new ArrayList());
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
        public void Assign(AnnotationUnicodeProbeControl that)
        {
            // Unicode probe control has no transferable state.
        }
    }
}
