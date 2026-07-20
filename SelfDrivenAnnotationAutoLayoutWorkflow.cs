using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace AvevaIntegration
{
    internal sealed class SelfDrivenAnnotationAutoLayoutWorkflow
    {
        private readonly DemoEntry entry;
        private readonly MarineUiDispatcher dispatcher;
        private readonly string username;
        private readonly string projectName;
        private readonly string extraParamsJson;
        private readonly string runId;
        private readonly string runDirectory;
        private readonly string logPath;
        private readonly ManualResetEvent cancelEvent =
            new ManualResetEvent(false);
        private readonly object stateLock = new object();
        private string stage = "Idle";
        private string message = "准备就绪";
        private string taskId;
        private string dxfPath;
        private string resultJsonPath;
        private string drawingIdentity;
        private int batchStart;
        private int batchNumber;
        private int totalBatches;
        private int moveCount;
        private bool finished;
        private bool failed;
        private bool uiStepScheduled;
        private bool uiStepRunning;
        private bool terminalStateReached;
        private string errorDetail;

        internal SelfDrivenAnnotationAutoLayoutWorkflow(
            DemoEntry entry,
            MarineUiDispatcher dispatcher,
            string username,
            string projectName,
            string extraParamsJson)
        {
            this.entry = entry;
            this.dispatcher = dispatcher;
            this.username = string.IsNullOrEmpty(username)
                ? "AVEVA" : username;
            this.projectName = string.IsNullOrEmpty(projectName)
                ? "Default" : projectName;
            this.extraParamsJson = string.IsNullOrEmpty(extraParamsJson)
                ? "{}" : extraParamsJson;
            DateTime now = DateTime.Now;
            runId = now.ToString(
                "yyyyMMdd_HHmmss_fff",
                CultureInfo.InvariantCulture);
            runDirectory = Path.Combine(
                Path.Combine("D:\\DXF\\AutoLayout", "current_drawing"),
                runId);
            logPath = Path.Combine(runDirectory, "auto-layout.log.txt");
        }

        internal bool IsFinished
        {
            get { lock (stateLock) { return finished; } }
        }

        internal string Status()
        {
            lock (stateLock)
            {
                string state = failed ? "FAILED" : finished
                    ? "COMPLETE" : "RUNNING";
                return "STATE=" + state +
                    " | STAGE=" + stage +
                    " | BATCH=" + batchNumber + "/" + totalBatches +
                    " | MOVES=" + moveCount +
                    " | DRAWING=" + (drawingIdentity ?? string.Empty) +
                    " | RUN_ID=" + runId +
                    " | LOG=" + logPath +
                    " | MESSAGE=" + (failed ? errorDetail : message);
            }
        }

        internal string StartOnOwnerThread()
        {
            dispatcher.VerifyOwnerThread();
            try
            {
                Directory.CreateDirectory(runDirectory);
                drawingIdentity = entry.GetCurrentDrawingIdentityOnOwnerThread();
                dxfPath = Path.Combine(runDirectory, "source.dxf");
                resultJsonPath = Path.Combine(
                    runDirectory, "algorithm_result.json");
                Log("STAGE=Preparing | THREAD=" +
                    Thread.CurrentThread.ManagedThreadId);
                if (!entry.HasCurrentDrawingOnOwnerThread())
                {
                    return FailOnOwnerThread("no current drawing");
                }
                string exportResult = entry.ExportCurrentDrawingToDxf(dxfPath);
                if (!exportResult.StartsWith("SUCCESS",
                    StringComparison.Ordinal))
                {
                    return FailOnOwnerThread("DXF export failed | " + exportResult);
                }
                stage = "Uploading";
                message = "正在上传算法服务...";
                PublishOnOwnerThread();
                QueueBackgroundHttp();
                return Status();
            }
            catch (Exception ex)
            {
                return FailOnOwnerThread(ex.Message);
            }
        }

        internal string Cancel()
        {
            lock (stateLock)
            {
                if (terminalStateReached)
                {
                    return Status();
                }
                message = "正在取消...";
            }
            cancelEvent.Set();
            PublishOnOwnerThread();
            return Status();
        }

        internal string OpenLog()
        {
            try
            {
                if (File.Exists(logPath))
                {
                    Process.Start(logPath);
                }
                return "SUCCESS: " + logPath;
            }
            catch (Exception ex)
            {
                return "ERROR: cannot open log: " + ex.Message + " | LOG=" + logPath;
            }
        }

        private void QueueBackgroundHttp()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Log("BACKGROUND_ONLY=true | THREAD=" +
                        Thread.CurrentThread.ManagedThreadId);
                    AlgorithmServiceClient client =
                        new AlgorithmServiceClient(
                            AlgorithmServiceConfig.LoadBaseUrl());
                    taskId = client.UploadAlgorithmTask(
                        dxfPath,
                        username,
                        projectName,
                        extraParamsJson);
                    if (IsError(taskId))
                    {
                        PostFailure("algorithm upload failed | " + taskId);
                        return;
                    }
                    PostStage("WaitingAlgorithm", "算法处理中...");
                    while (!cancelEvent.WaitOne(0, false))
                    {
                        string status = client.QueryAlgorithmTask(
                            taskId,
                            resultJsonPath);
                        Log("BACKGROUND_ONLY=true | ALGORITHM_STATUS=" + status);
                        if (IsError(status))
                        {
                            PostFailure("algorithm query failed | " + status);
                            return;
                        }
                        if (string.Equals(status, "SUCCESS",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            dispatcher.Post(OnAlgorithmResultOnOwnerThread);
                            return;
                        }
                        if (cancelEvent.WaitOne(2000, false))
                        {
                            PostCancelled();
                            return;
                        }
                    }
                    PostCancelled();
                }
                catch (Exception ex)
                {
                    PostFailure("background algorithm stage failed | " +
                        ex.GetType().FullName + ": " + ex.Message);
                }
            });
        }

        private void OnAlgorithmResultOnOwnerThread()
        {
            dispatcher.VerifyOwnerThread();
            try
            {
                if (IsCancellationRequested())
                {
                    CompleteCancelledOnOwnerThread();
                    return;
                }
                int[] plan = DemoEntry.GetAlgorithmPlanCounts(resultJsonPath);
                moveCount = plan[0];
                totalBatches = AnnotationAutoLayoutPlan.CreateBatchSizes(
                    moveCount).Count;
                stage = "ReadyToApply";
                message = "算法结果已保存，准备分批应用";
                PublishOnOwnerThread();
                ScheduleNextUiStep();
            }
            catch (Exception ex)
            {
                FailOnOwnerThread("result plan failed | " + ex.Message);
            }
        }

        private void ScheduleNextUiStep()
        {
            dispatcher.VerifyOwnerThread();
            if (uiStepScheduled || terminalStateReached)
            {
                return;
            }
            uiStepScheduled = true;
            Log("event=NEXT_BATCH_SCHEDULED | batch=" +
                (batchStart / AnnotationAutoLayoutPlan.BatchSize + 1) +
                "/" + totalBatches + " | batch_start=" + batchStart +
                " | operation_in_flight=false");
            Log("event=MAIN_THREAD_POSTED | stage=" + stage);
            dispatcher.PostDeferred(ApplyNextBatchOnOwnerThread);
        }

        private void ApplyNextBatchOnOwnerThread()
        {
            dispatcher.VerifyOwnerThread();
            if (uiStepRunning)
            {
                return;
            }
            uiStepScheduled = false;
            uiStepRunning = true;
            try
            {
                Log("event=NEXT_BATCH_STARTED | batch=" +
                    (batchStart / AnnotationAutoLayoutPlan.BatchSize + 1) +
                    "/" + totalBatches + " | stage=MovingText");
                if (IsCancellationRequested())
                {
                    CompleteCancelledOnOwnerThread();
                    return;
                }
                if (!entry.HasCurrentDrawingOnOwnerThread() ||
                    !string.Equals(
                        drawingIdentity,
                        entry.GetCurrentDrawingIdentityOnOwnerThread(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    FailOnOwnerThread("drawing identity changed; apply stopped");
                    return;
                }
                if (batchStart >= moveCount)
                {
                    CompleteOnOwnerThread();
                    return;
                }
                batchNumber = batchStart / AnnotationAutoLayoutPlan.BatchSize + 1;
                stage = "ApplyingBatch";
                message = "正在处理第 " + batchNumber + "/" + totalBatches + " 批";
                PublishOnOwnerThread();

                string textResult = entry.ApplyAlgorithmMovesBatch(
                    resultJsonPath,
                    batchStart,
                    AnnotationAutoLayoutPlan.BatchSize);
                if (!IsSuccess(textResult))
                {
                    FailOnOwnerThread("text move failed | " + textResult);
                    return;
                }
                entry.RepaintOnOwnerThread("AFTER_TEXT_MOVE");
                string geometryResult =
                    entry.ApplyAlgorithmAnnotationGeometryBatch(
                        resultJsonPath,
                        dxfPath,
                        batchStart,
                        AnnotationAutoLayoutPlan.BatchSize);
                if (!IsSuccess(geometryResult))
                {
                    FailOnOwnerThread("geometry apply failed | " + geometryResult);
                    return;
                }
                entry.RepaintOnOwnerThread("AFTER_GEOMETRY_APPLY");
                stage = "VerifyingBatch";
                Log("event=VERIFY_STARTED | batch=" + batchNumber + "/" +
                    totalBatches);
                string previewResult =
                    entry.PreviewAlgorithmAnnotationGeometryBatch(
                        resultJsonPath,
                        dxfPath,
                        batchStart,
                        AnnotationAutoLayoutPlan.BatchSize);
                if (!IsSuccessfulAppliedVerification(previewResult))
                {
                    FailOnOwnerThread("preview failed | " + previewResult);
                    return;
                }
                Log("event=VERIFY_COMPLETED | batch=" + batchNumber + "/" +
                    totalBatches + " | result=" +
                    previewResult.Replace("\r", " ").Replace("\n", " "));
                Log("event=VERIFY_ACCEPTED | batch=" + batchNumber + "/" +
                    totalBatches + " | already_applied=true");
                batchStart += AnnotationAutoLayoutPlan.BatchSize;
                if (batchStart < moveCount)
                {
                    batchNumber = batchStart / AnnotationAutoLayoutPlan.BatchSize + 1;
                    stage = "MovingText";
                    message = "正在处理第 " + batchNumber + "/" +
                        totalBatches + " 批";
                    PublishOnOwnerThread();
                    ScheduleNextUiStep();
                }
                else
                {
                    PublishOnOwnerThread();
                    CompleteOnOwnerThread();
                }
            }
            catch (Exception ex)
            {
                FailOnOwnerThread("batch failed | " + ex.Message);
            }
            finally
            {
                uiStepRunning = false;
            }
        }

        private void PostStage(string nextStage, string nextMessage)
        {
            dispatcher.Post(delegate
            {
                dispatcher.VerifyOwnerThread();
                stage = nextStage;
                message = nextMessage;
                PublishOnOwnerThread();
            });
        }

        private void PostFailure(string detail)
        {
            dispatcher.Post(delegate { FailOnOwnerThread(detail); });
        }

        private void PostCancelled()
        {
            dispatcher.Post(CompleteCancelledOnOwnerThread);
        }

        private string FailOnOwnerThread(string detail)
        {
            dispatcher.VerifyOwnerThread();
            failed = true;
            finished = true;
            terminalStateReached = true;
            errorDetail = detail;
            stage = "Failed";
            Log("FINAL_STATUS=FAILED | " + detail);
            PublishOnOwnerThread();
            return Status();
        }

        private void CompleteCancelledOnOwnerThread()
        {
            dispatcher.VerifyOwnerThread();
            finished = true;
            terminalStateReached = true;
            stage = "Cancelled";
            message = "已取消";
            Log("FINAL_STATUS=CANCELLED");
            PublishOnOwnerThread();
        }

        private void CompleteOnOwnerThread()
        {
            dispatcher.VerifyOwnerThread();
            finished = true;
            terminalStateReached = true;
            stage = "Completed";
            message = "排版完成，可以保存图纸";
            Log("FINAL_STATUS=SUCCESS");
            PublishOnOwnerThread();
        }

        private void PublishOnOwnerThread()
        {
            dispatcher.VerifyOwnerThread();
            entry.PublishAnnotationAutoLayoutStatusOnOwnerThread(
                Status(),
                stage,
                batchNumber + "/" + totalBatches,
                message,
                !finished,
                finished,
                failed ? errorDetail : string.Empty,
                drawingIdentity,
                runId);
        }

        private bool IsCancellationRequested()
        {
            return cancelEvent.WaitOne(0, false);
        }

        private static bool IsError(string value)
        {
            return value == null || value.StartsWith("ERROR:",
                StringComparison.Ordinal);
        }

        private static bool IsSuccess(string value)
        {
            return value != null && value.StartsWith("SUCCESS",
                StringComparison.Ordinal);
        }

        private static bool IsSuccessfulAppliedVerification(string value)
        {
            if (!IsSuccess(value))
            {
                return false;
            }
            int processed;
            int ready;
            int alreadyApplied;
            int failed;
            int inconclusive;
            return TryGetResultInt(value, "processed", out processed) &&
                TryGetResultInt(value, "ready", out ready) &&
                TryGetResultInt(value, "already_applied", out alreadyApplied) &&
                TryGetResultInt(value, "failed", out failed) &&
                TryGetResultInt(value, "inconclusive", out inconclusive) &&
                processed > 0 && failed == 0 && inconclusive == 0 &&
                alreadyApplied == processed;
        }

        private static bool TryGetResultInt(
            string value,
            string key,
            out int result)
        {
            result = 0;
            string marker = key + "=";
            int start = value.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }
            start += marker.Length;
            int end = value.IndexOf(" | ", start, StringComparison.Ordinal);
            string text = end < 0
                ? value.Substring(start)
                : value.Substring(start, end - start);
            return int.TryParse(text, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out result);
        }

        private void Log(string value)
        {
            try
            {
                Directory.CreateDirectory(runDirectory);
                using (StreamWriter writer = new StreamWriter(
                    logPath, true, new System.Text.UTF8Encoding(false)))
                {
                    writer.WriteLine(DateTime.Now.ToString("o",
                        CultureInfo.InvariantCulture) + " | " + value);
                }
            }
            catch
            {
            }
        }
    }
}
