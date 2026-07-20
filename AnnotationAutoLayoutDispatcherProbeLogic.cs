namespace AvevaIntegration
{
    internal static class AnnotationAutoLayoutDispatcherProbeLogic
    {
        internal static bool Passed(
            int startThreadId, int ownerThreadId, int callbackThreadId,
            bool startIsThreadPool, bool callbackIsThreadPool,
            bool workerIsThreadPool, int workerThreadId)
        {
            return startThreadId == ownerThreadId &&
                callbackThreadId == ownerThreadId &&
                callbackThreadId == startThreadId &&
                !startIsThreadPool && !callbackIsThreadPool &&
                workerIsThreadPool && workerThreadId != callbackThreadId;
        }
    }
}
