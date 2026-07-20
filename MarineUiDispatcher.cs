using System;
using System.Threading;
using System.Windows.Forms;

namespace AvevaIntegration
{
    internal sealed class MarineUiDispatcher : IDisposable
    {
        private readonly Control control;
        private readonly int ownerThreadId;
        private bool disposed;

        internal MarineUiDispatcher()
        {
            ownerThreadId = Thread.CurrentThread.ManagedThreadId;
            control = new Control();
            control.CreateControl();
            if (control.Handle == IntPtr.Zero)
            {
                control.Dispose();
                throw new InvalidOperationException(
                    "Failed to create Marine UI dispatcher handle.");
            }
        }

        internal int OwnerThreadId { get { return ownerThreadId; } }

        internal bool IsOwnerThread
        {
            get
            {
                return Thread.CurrentThread.ManagedThreadId == ownerThreadId;
            }
        }

        internal void VerifyOwnerThread()
        {
            if (!IsOwnerThread)
            {
                throw new InvalidOperationException(
                    "Marine SDK operation attempted on a non-owner thread.");
            }
        }

        internal void Post(MethodInvoker action)
        {
            if (action == null)
            {
                throw new ArgumentNullException("action");
            }
            if (disposed || control.IsDisposed || control.Handle == IntPtr.Zero)
            {
                return;
            }
            if (IsOwnerThread)
            {
                action();
            }
            else
            {
                control.BeginInvoke(action);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            VerifyOwnerThread();
            disposed = true;
            if (!control.IsDisposed)
            {
                control.Dispose();
            }
        }
    }
}
