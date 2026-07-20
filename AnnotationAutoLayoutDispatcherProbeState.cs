using System;
using System.Threading;

namespace AvevaIntegration
{
    internal sealed class AnnotationAutoLayoutDispatcherProbeState
    {
        internal string ProbeId;
        internal string State = "IDLE";
        internal int StartThreadId = 0;
        internal bool StartIsThreadPool = false;
        internal string StartSynchronizationContext = "NULL";
        internal int WorkerThreadId = 0;
        internal bool WorkerIsThreadPool = false;
        internal int CallbackThreadId = 0;
        internal bool CallbackIsThreadPool = false;
        internal string CallbackSynchronizationContext = "NULL";
        internal int EventThreadId = 0;
        internal bool EventIsThreadPool = false;
        internal int OwnerThreadId = 0;
        internal DateTime StartUtc = new DateTime(0);
        internal DateTime CompletedUtc = new DateTime(0);
        internal string Error = string.Empty;
        internal bool Passed = false;

        internal string GetStatus()
        {
            return "PROBE_ID=" + Safe(ProbeId) +
                "|STATE=" + Safe(State) +
                "|OWNER_THREAD_ID=" + OwnerThreadId.ToString() +
                "|START_THREAD_ID=" + StartThreadId.ToString() +
                "|START_IS_THREAD_POOL=" + Bool(StartIsThreadPool) +
                "|WORKER_THREAD_ID=" + WorkerThreadId.ToString() +
                "|WORKER_IS_THREAD_POOL=" + Bool(WorkerIsThreadPool) +
                "|CALLBACK_THREAD_ID=" + CallbackThreadId.ToString() +
                "|CALLBACK_IS_THREAD_POOL=" + Bool(CallbackIsThreadPool) +
                "|EVENT_THREAD_ID=" + EventThreadId.ToString() +
                "|EVENT_IS_THREAD_POOL=" + Bool(EventIsThreadPool) +
                "|START_SYNC_CONTEXT=" + Safe(StartSynchronizationContext) +
                "|CALLBACK_SYNC_CONTEXT=" + Safe(CallbackSynchronizationContext) +
                "|START_UTC=" + StartUtc.ToString("o") +
                "|COMPLETED_UTC=" + CompletedUtc.ToString("o") +
                "|PASSED=" + Bool(Passed) +
                "|ERROR=" + Safe(Error);
        }

        private static string Bool(bool value) { return value ? "true" : "false"; }
        private static string Safe(string value)
        {
            return (value ?? string.Empty).Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
