using System.Drawing;
using System.Windows.Forms;
using Aveva.PDMS.PMLNet;

namespace AvevaIntegration
{
    [PMLNetCallable()]
    public class AnnotationUnicodeProbeControl : UserControl
    {
        private readonly Label label;

        [PMLNetCallable()]
        public AnnotationUnicodeProbeControl()
        {
            BackColor = Color.LightGray;
            BorderStyle = BorderStyle.FixedSingle;

            label = new Label();
            label.Dock = DockStyle.Fill;
            label.AutoSize = false;
            label.BackColor = Color.LightYellow;
            label.ForeColor = Color.Black;
            label.BorderStyle = BorderStyle.FixedSingle;
            label.TextAlign = ContentAlignment.TopLeft;
            label.Padding = new Padding(8, 8, 0, 0);
            label.Font = SystemFonts.MessageBoxFont;
            label.UseCompatibleTextRendering = true;
            label.Text = "\u6807\u6CE8\u81EA\u52A8\u6392\u7248";
            Controls.Add(label);
        }

        [PMLNetCallable()]
        public void Assign(AnnotationUnicodeProbeControl that)
        {
            // Unicode probe control has no transferable state.
        }
    }
}
