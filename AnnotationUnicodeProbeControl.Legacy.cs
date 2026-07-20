using System.Windows.Forms;
using Aveva.PDMS.PMLNet;

namespace AvevaIntegration
{
    [PMLNetCallable()]
    public class AnnotationUnicodeProbeControl : AnnotationAutoLayoutStatusControl
    {
        [PMLNetCallable()]
        public AnnotationUnicodeProbeControl()
            : base()
        {
        }

        [PMLNetCallable()]
        public void Assign(AnnotationUnicodeProbeControl that)
        {
        }
    }
}
