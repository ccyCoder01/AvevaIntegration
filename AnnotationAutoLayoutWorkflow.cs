using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Aveva.Marine.Drafting;

namespace AvevaIntegration
{
    internal sealed class AnnotationAutoLayoutWorkflow
    {
        internal const int BatchSize = AnnotationAutoLayoutPlan.BatchSize;
        private readonly DemoEntry entry;
        private readonly string username;
        private readonly string projectName;
        private readonly string extraParamsJson;
        private readonly string runId;
        private readonly string runDirectory;
        private readonly string logPath;
        private string stage;
        private string taskId;
        private string dxfPath;
        private string resultJsonPath;
        private string drawingName;
        private int batchStart;
        private int batchNumber;
        private int totalBatches;
        private int moveCount;
        private int skippedCount;
        private bool cancelRequested;
        private bool finished;
        private bool failed;
        private string lastError;
        private DateTime started;
        private DateTime lastPoll = DateTime.MinValue;
        private int queryErrorRetries;

        internal AnnotationAutoLayoutWorkflow(
            DemoEntry entry,
            string username,
            string projectName,
            string extraParamsJson)
        {
            this.entry = entry;
            this.username = string.IsNullOrEmpty(username)
                ? "AVEVA"
                : username;
            this.projectName = string.IsNullOrEmpty(projectName)
                ? "Default"
                : projectName;
            this.extraParamsJson = string.IsNullOrEmpty(extraParamsJson)
                ? "{}"
                : extraParamsJson;
            started = DateTime.Now;
            runId = started.ToString(
                "yyyyMMdd_HHmmss_fff",
                CultureInfo.InvariantCulture);
            runDirectory = Path.Combine(
                Path.Combine("D:\\DXF\\AutoLayout", "current_drawing"),
                runId);
            logPath = Path.Combine(
                runDirectory,
                "auto-layout.log.txt");
            stage = "CHECK_DRAWING";
        }

        internal bool IsFinished { get { return finished; } }
        internal bool IsFailed { get { return failed; } }

        internal string Start()
        {
            try
            {
                Directory.CreateDirectory(runDirectory);
                WriteLog("RUN_ID=" + runId);
                WriteLog("BATCH_SIZE=" + BatchSize);
                WriteLog("WORK_DIRECTORY=" + runDirectory);
                WriteLog("STAGE=CHECK_DRAWING");
                return Status();
            }
            catch (Exception ex)
            {
                Fail("unable to create workflow directory: " + ex.Message);
                return Status();
            }
        }

        internal string Advance()
        {
            if (finished)
            {
                return Status();
            }
            if (cancelRequested)
            {
                Fail("cancelled by user");
                return Status();
            }

            try
            {
                if (stage == "CHECK_DRAWING")
                {
                    if (!HasCurrentDrawing())
                    {
                        return Fail("no current drawing");
                    }
                    drawingName = GetCurrentDrawingName();
                    WriteLog("DRAWING=" + drawingName);
                    dxfPath = Path.Combine(runDirectory, "source.dxf");
                    string exportResult = entry.ExportCurrentDrawingToDxf(dxfPath);
                    if (!IsSuccess(exportResult))
                    {
                        return Fail("DXF export failed | " + exportResult);
                    }
                    WriteLog("DXF_PATH=" + dxfPath);
                    stage = "UPLOAD";
                    return Status();
                }

                if (stage == "UPLOAD")
                {
                    string uploadResult = entry.UploadAlgorithmTask(
                        dxfPath,
                        username,
                        projectName,
                        extraParamsJson);
                    if (uploadResult.StartsWith("ERROR:", StringComparison.Ordinal))
                    {
                        return Fail("algorithm upload failed | " + uploadResult);
                    }
                    taskId = uploadResult;
                    resultJsonPath = Path.Combine(
                        runDirectory,
                        "algorithm_result.json");
                    WriteLog("TASK_ID=" + taskId);
                    WriteLog("RESULT_JSON_PATH=" + resultJsonPath);
                    stage = "POLL";
                    return Status();
                }

                if (stage == "POLL")
                {
                    if (lastPoll != DateTime.MinValue &&
                        (DateTime.Now - lastPoll).TotalSeconds < 2.0)
                    {
                        return Status();
                    }
                    lastPoll = DateTime.Now;
                    string queryResult = entry.QueryAlgorithmTask(
                        taskId,
                        resultJsonPath);
                    WriteLog("ALGORITHM_STATUS=" + queryResult);
                    if (queryResult.StartsWith("ERROR:",
                        StringComparison.Ordinal))
                    {
                        queryErrorRetries++;
                        if (queryErrorRetries <= 3)
                        {
                            WriteLog("ALGORITHM_QUERY_RETRY=" +
                                queryErrorRetries);
                            return Status();
                        }
                        return Fail("algorithm query failed after retries | " +
                            queryResult);
                    }
                    queryErrorRetries = 0;
                    if (string.Equals(queryResult, "QUEUED",
                        StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(queryResult, "PROCESSING",
                        StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(queryResult, "PENDING",
                        StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(queryResult, "RUNNING",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return Status();
                    }
                    if (!string.Equals(queryResult, "SUCCESS",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return Fail("algorithm task failed | " + queryResult);
                    }
                    stage = "PLAN";
                    return Status();
                }

                if (stage == "PLAN")
                {
                    int[] plan = DemoEntry.GetAlgorithmPlanCounts(
                        resultJsonPath);
                    moveCount = plan[0];
                    skippedCount = plan[1];
                    totalBatches = AnnotationAutoLayoutPlan.CreateBatchSizes(
                        moveCount).Count;
                    batchStart = 0;
                    batchNumber = 1;
                    WriteLog("TOTAL_RECORDS=" + plan[2]);
                    WriteLog("MOVE_COUNT=" + moveCount);
                    WriteLog("SKIPPED_COUNT=" + skippedCount);
                    WriteLog("TOTAL_BATCHES=" + totalBatches);
                    stage = totalBatches == 0 ? "COMPLETE" : "TEXT_MOVE";
                    return Status();
                }

                if (stage == "TEXT_MOVE")
                {
                    if (!HasCurrentDrawing())
                    {
                        return Fail("current drawing is no longer available");
                    }
                    int currentSize = Math.Min(
                        BatchSize,
                        moveCount - batchStart);
                    WriteLog("BATCH=" + batchNumber +
                        " | STAGE=TEXT_MOVE_START | COUNT=" + currentSize);
                    string result = entry.ApplyAlgorithmMovesBatch(
                        resultJsonPath,
                        batchStart,
                        BatchSize);
                    if (!IsSuccess(result))
                    {
                        return Fail("batch text move failed | batch=" +
                            batchNumber + " | " + result);
                    }
                    if (!RefreshDrawing("AFTER_TEXT_MOVE"))
                    {
                        WriteLog("REFRESH=FAILED | reason=AFTER_TEXT_MOVE");
                    }
                    WriteLog("BATCH=" + batchNumber +
                        " | STAGE=TEXT_MOVE_COMPLETE");
                    stage = "GEOMETRY_APPLY";
                    return Status();
                }

                if (stage == "GEOMETRY_APPLY")
                {
                    if (!HasCurrentDrawing())
                    {
                        return Fail("current drawing is no longer available");
                    }
                    WriteLog("BATCH=" + batchNumber +
                        " | STAGE=GEOMETRY_PRECHECK_AND_APPLY_START");
                    string result = entry.ApplyAlgorithmAnnotationGeometryBatch(
                        resultJsonPath,
                        dxfPath,
                        batchStart,
                        BatchSize);
                    if (!IsSuccess(result))
                    {
                        return Fail("batch geometry apply failed | batch=" +
                            batchNumber + " | " + result);
                    }
                    if (!RefreshDrawing("AFTER_GEOMETRY_APPLY"))
                    {
                        WriteLog("REFRESH=FAILED | reason=AFTER_GEOMETRY_APPLY");
                    }
                    WriteLog("BATCH=" + batchNumber +
                        " | STAGE=GEOMETRY_PRECHECK_AND_APPLY_COMPLETE");
                    stage = "PREVIEW";
                    return Status();
                }

                if (stage == "PREVIEW")
                {
                    if (!HasCurrentDrawing())
                    {
                        return Fail("current drawing is no longer available");
                    }
                    WriteLog("BATCH=" + batchNumber +
                        " | STAGE=PREVIEW_START");
                    string result = entry.PreviewAlgorithmAnnotationGeometryBatch(
                        resultJsonPath,
                        dxfPath,
                        batchStart,
                        BatchSize);
                    if (!IsSuccess(result))
                    {
                        return Fail("batch final preview failed | batch=" +
                            batchNumber + " | " + result);
                    }
                    if (!RefreshDrawing("AFTER_PREVIEW"))
                    {
                        WriteLog("REFRESH=FAILED | reason=AFTER_PREVIEW");
                    }
                    WriteLog("BATCH=" + batchNumber +
                        " | STAGE=PREVIEW_COMPLETE");
                    batchStart += BatchSize;
                    batchNumber++;
                    if (batchStart >= moveCount)
                    {
                        stage = "COMPLETE";
                    }
                    else
                    {
                        stage = "TEXT_MOVE";
                    }
                    return Status();
                }

                if (stage == "COMPLETE")
                {
                    finished = true;
                    WriteLog("FINAL_STATUS=SUCCESS");
                    return Status();
                }

                return Fail("unknown workflow stage: " + stage);
            }
            catch (Exception ex)
            {
                return Fail(stage + " failed | " + ex.GetType().FullName +
                    ": " + ex.Message);
            }
        }

        internal string Cancel()
        {
            if (!finished)
            {
                cancelRequested = true;
                WriteLog("CANCEL_REQUESTED=true");
            }
            return Status();
        }

        internal string Status()
        {
            string state = failed
                ? "FAILED"
                : finished
                    ? "COMPLETE"
                    : "RUNNING";
            return "STATE=" + state +
                " | STAGE=" + stage +
                " | BATCH=" + (batchNumber <= 0 ? 0 : batchNumber) +
                "/" + totalBatches +
                " | BATCH_SIZE=" + BatchSize +
                " | DRAWING=" + (drawingName ?? string.Empty) +
                " | MOVES=" + moveCount +
                " | TASK_ID=" + (taskId ?? string.Empty) +
                " | WORK_DIRECTORY=" + runDirectory +
                " | LOG=" + logPath +
                " | MESSAGE=" + (failed
                    ? lastError
                    : finished
                        ? "排版完成，可以保存图纸"
                        : StageMessage());
        }

        internal string DisplayField(string key)
        {
            string marker = key + "=";
            string status = Status();
            int start = status.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return string.Empty;
            }
            start += marker.Length;
            int end = status.IndexOf(" | ", start, StringComparison.Ordinal);
            return end < 0
                ? status.Substring(start)
                : status.Substring(start, end - start);
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
                return "ERROR: cannot open log | " + ex.Message;
            }
        }

        private string StageMessage()
        {
            if (stage == "POLL") return "算法处理中";
            if (stage == "TEXT_MOVE") return "正在移动文字";
            if (stage == "GEOMETRY_APPLY") return "正在应用几何";
            if (stage == "PREVIEW") return "正在验证当前批次";
            if (stage == "UPLOAD") return "正在上传算法任务";
            if (stage == "PLAN") return "正在生成批次计划";
            return "正在准备";
        }

        private string Fail(string error)
        {
            failed = true;
            finished = true;
            lastError = error;
            WriteLog("FINAL_STATUS=FAILED | ERROR=" + error);
            return Status();
        }

        private bool HasCurrentDrawing()
        {
            using (MarDrafting drafting = new MarDrafting())
            {
                if (!drafting.DwgCurrent())
                {
                    return false;
                }
                if (string.IsNullOrEmpty(drawingName))
                {
                    return true;
                }
                return string.Equals(
                    drawingName,
                    drafting.DwgNameGet(),
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private string GetCurrentDrawingName()
        {
            using (MarDrafting drafting = new MarDrafting())
            {
                return drafting.DwgNameGet();
            }
        }

        private bool RefreshDrawing(string reason)
        {
            WriteLog("REFRESH=START | reason=" + reason);
            try
            {
                using (MarDrafting drafting = new MarDrafting())
                {
                    drafting.DwgRepaint();
                }
                WriteLog("REFRESH=SUCCESS | reason=" + reason);
                return true;
            }
            catch (Exception ex)
            {
                WriteLog("REFRESH=UNAVAILABLE | reason=" + reason +
                    " | detail=" + ex.Message);
                return false;
            }
        }

        private void WriteLog(string line)
        {
            try
            {
                Directory.CreateDirectory(runDirectory);
                using (StreamWriter writer = new StreamWriter(
                    logPath,
                    true,
                    new System.Text.UTF8Encoding(false)))
                {
                    writer.WriteLine(
                        DateTime.Now.ToString("o",
                            CultureInfo.InvariantCulture) + " | " + line);
                }
            }
            catch
            {
            }
        }

        private static bool IsSuccess(string result)
        {
            return result != null &&
                result.StartsWith("SUCCESS", StringComparison.Ordinal);
        }

    }
}
