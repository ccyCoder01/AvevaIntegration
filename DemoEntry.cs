using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Aveva.Marine.Drafting;
using Aveva.Marine.Geometry;
using Aveva.PDMS.PMLNet;

namespace AvevaIntegration
{
    [PMLNetCallable()]
    public class DemoEntry
    {
        private enum OldLeaderAbsenceResult
        {
            NotRun,
            Absent,
            Present,
            Inconclusive
        }

        private sealed class ApplyReceipt
        {
            public string AlgorithmHandle;
            public int OldLeaderHandleIdBeforeDelete = int.MinValue;
            public int SourceUnderlineHandleId = int.MinValue;
            public int MovedUnderlineHandleId = int.MinValue;
            public int CreatedNewLeaderHandleId = int.MinValue;
            public int OldLeaderLayerId = int.MinValue;
            public int CreatedNewLeaderLayerId = int.MinValue;
            public bool ApplyCompleted;
            public int BatchStart;
            public int BatchEndExclusive;
            public string Build;
            public string Timestamp;
        }

        private static string EscapeJsonString(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            StringBuilder result = new StringBuilder();

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                switch (c)
                {
                    case '"':
                        result.Append("\\\"");
                        break;

                    case '\\':
                        result.Append("\\\\");
                        break;

                    case '\b':
                        result.Append("\\b");
                        break;

                    case '\f':
                        result.Append("\\f");
                        break;

                    case '\n':
                        result.Append("\\n");
                        break;

                    case '\r':
                        result.Append("\\r");
                        break;

                    case '\t':
                        result.Append("\\t");
                        break;

                    default:
                        if (c < 32)
                        {
                            result.AppendFormat(
                                CultureInfo.InvariantCulture,
                                "\\u{0:X4}",
                                (int)c);
                        }
                        else
                        {
                            result.Append(c);
                        }

                        break;
                }
            }

            return result.ToString();
        }

        private static string SanitizePmlReturn(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            StringBuilder result = new StringBuilder();
            bool previousSpace = false;
            int index = 0;
            while (index < value.Length)
            {
                char current = value[index];
                bool isSpace = current == '\r' || current == '\n' ||
                    current == '\t' || current == ' ';
                if (isSpace)
                {
                    if (!previousSpace)
                    {
                        result.Append(' ');
                    }
                    previousSpace = true;
                }
                else
                {
                    result.Append(current);
                    previousSpace = false;
                }
                index++;
            }
            string sanitized = result.ToString().Trim();
            if (sanitized.Length > 800)
            {
                return sanitized.Substring(0, 800);
            }
            return sanitized;
        }

        [PMLNetCallable()]
        public DemoEntry()
        {
        }

        [PMLNetCallable()]
        public void Assign(DemoEntry that)
        {
            // 当前 Demo 没有需要复制的状态
        }

        [PMLNetCallable()]
        public void ShowMessage()
        {
            MessageBox.Show(
                "C# Demo 已成功被 AVEVA 调用",
                "AVEVA 集成验证",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        [PMLNetCallable()]
        public string GetMessage()
        {
            return "Hello from C#";
        }

        [PMLNetCallable()]
        public string Echo(string text)
        {
            return "C# received: " + text;
        }

        [PMLNetCallable()]
        public bool HasCurrentDrawing()
        {
            using (MarDrafting drafting = new MarDrafting())
            {
                return drafting.DwgCurrent();
            }
        }

        [PMLNetCallable()]
        public string ExportCurrentDrawingToDxf(string outputPath)
        {
            try
            {
                if (string.IsNullOrEmpty(outputPath))
                {
                    return "ERROR: outputPath is empty";
                }

                string directory = Path.GetDirectoryName(outputPath);

                if (!string.IsNullOrEmpty(directory) &&
                    !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (MarDrafting drafting = new MarDrafting())
                {
                    if (!drafting.DwgCurrent())
                    {
                        return "ERROR: no current drawing";
                    }

                    drafting.DwgDxfExport(outputPath);
                }

                if (!File.Exists(outputPath))
                {
                    return "ERROR: DXF file was not created";
                }

                return "SUCCESS: " + outputPath;
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        [PMLNetCallable()]
        public string CreateTestText(double x, double y)
        {
            try
            {
                using (MarDrafting drafting = new MarDrafting())
                {
                    if (!drafting.DwgCurrent())
                    {
                        return "ERROR: no current drawing";
                    }

                    MarPointPlanar position = new MarPointPlanar(x, y);

                    try
                    {
                        drafting.TextNew("C# TEST TEXT", position);
                    }
                    finally
                    {
                        position.Dispose();
                    }
                }

                return "SUCCESS";
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        [PMLNetCallable()]
        public string IdentifyText(double x, double y)
        {
            try
            {
                using (MarDrafting drafting = new MarDrafting())
                {
                    if (!drafting.DwgCurrent())
                    {
                        return "ERROR: no current drawing";
                    }

                    using (MarPointPlanar point = new MarPointPlanar(x, y))
                    {
                        using (MarElementHandle handle = drafting.TextIdentify(point))
                        {
                            if (handle == null)
                            {
                                return "ERROR: text not found";
                            }

                            using (MarText text = drafting.TextPropertiesGet(handle))
                            {
                                if (text == null)
                                {
                                    return "ERROR: text properties not found";
                                }

                                return "SUCCESS: " + text.String;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        [PMLNetCallable()]
        public string MoveTextByOffset(
            double identifyX,
            double identifyY,
            double deltaX,
            double deltaY)
        {
            try
            {
                using (MarDrafting drafting = new MarDrafting())
                {
                    if (!drafting.DwgCurrent())
                    {
                        return "ERROR: no current drawing";
                    }

                    using (MarPointPlanar identifyPoint =
                        new MarPointPlanar(identifyX, identifyY))
                    {
                        using (MarElementHandle handle =
                            drafting.TextIdentify(identifyPoint))
                        {
                            if (handle == null)
                            {
                                return "ERROR: text not found";
                            }

                            using (MarVectorPlanar vector =
                                new MarVectorPlanar(deltaX, deltaY))
                            {
                                MarTransformationPlanar transformation =
                                    new MarTransformationPlanar();

                                try
                                {
                                    transformation.Translate(vector);

                                    drafting.ElementTransform(
                                        handle,
                                        transformation);
                                }
                                finally
                                {
                                    IDisposable disposable =
                                        transformation as IDisposable;

                                    if (disposable != null)
                                    {
                                        disposable.Dispose();
                                    }
                                }
                            }
                        }
                    }
                }

                return string.Format(
                    "SUCCESS: moved by ({0}, {1})",
                    deltaX,
                    deltaY);
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        [PMLNetCallable()]
        public string ReadTextAt(double identifyX, double identifyY)
        {
            try
            {
                using (MarDrafting drafting = new MarDrafting())
                {
                    if (!drafting.DwgCurrent())
                    {
                        return "ERROR: no current drawing";
                    }

                    using (MarPointPlanar identifyPoint =
                        new MarPointPlanar(identifyX, identifyY))
                    {
                        using (MarElementHandle handle =
                            drafting.TextIdentify(identifyPoint))
                        {
                            if (handle == null)
                            {
                                return "ERROR: text not found";
                            }

                            using (MarText text =
                                drafting.TextPropertiesGet(handle))
                            {
                                if (text == null)
                                {
                                    return "ERROR: text properties are null";
                                }

                                using (MarPointPlanar position = text.Position)
                                {
                                    return string.Format(
                                        "FOUND: {0}, POSITION: ({1}, {2})",
                                        text.String,
                                        position.X,
                                        position.Y);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        [PMLNetCallable()]
        public string MoveTextTo(
            double identifyX,
            double identifyY,
            double targetX,
            double targetY)
        {
            try
            {
                using (MarDrafting drafting = new MarDrafting())
                {
                    if (!drafting.DwgCurrent())
                    {
                        return "ERROR: no current drawing";
                    }

                    using (MarPointPlanar identifyPoint =
                        new MarPointPlanar(identifyX, identifyY))
                    using (MarElementHandle handle =
                        drafting.TextIdentify(identifyPoint))
                    {
                        if (handle == null)
                        {
                            return "ERROR: text not found";
                        }

                        using (MarText text =
                            drafting.TextPropertiesGet(handle))
                        {
                            if (text == null)
                            {
                                return "ERROR: text properties are null";
                            }

                            using (MarPointPlanar currentPosition =
                                text.Position)
                            {
                                double deltaX = targetX - currentPosition.X;
                                double deltaY = targetY - currentPosition.Y;

                                using (MarVectorPlanar vector =
                                    new MarVectorPlanar(deltaX, deltaY))
                                {
                                    MarTransformationPlanar transformation =
                                        new MarTransformationPlanar();

                                    try
                                    {
                                        transformation.Translate(vector);

                                        drafting.ElementTransform(
                                            handle,
                                            transformation);
                                    }
                                    finally
                                    {
                                        IDisposable disposable =
                                            transformation as IDisposable;

                                        if (disposable != null)
                                        {
                                            disposable.Dispose();
                                        }
                                    }
                                }

                                return string.Format(
                                    "SUCCESS: moved from ({0}, {1}) to ({2}, {3})",
                                    currentPosition.X,
                                    currentPosition.Y,
                                    targetX,
                                    targetY);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        [PMLNetCallable()]
        public string CaptureTextsInRectangle(
            double x1,
            double y1,
            double x2,
            double y2)
        {
            try
            {
                using (MarDrafting drafting = new MarDrafting())
                {
                    if (!drafting.DwgCurrent())
                    {
                        return "ERROR: no current drawing";
                    }

                    double minX = Math.Min(x1, x2);
                    double minY = Math.Min(y1, y2);
                    double maxX = Math.Max(x1, x2);
                    double maxY = Math.Max(y1, y2);

                    using (MarRectanglePlanar rectangle =
                        new MarRectanglePlanar(
                            minX,
                            minY,
                            maxX,
                            maxY))
                    using (MarCaptureRegionPlanar region =
                        new MarCaptureRegionPlanar())
                    {
                        region.SetRectangle(rectangle);
                        region.SetInside();
                        region.SetNoCut();

                        MarElementHandle[] handles =
                            drafting.TextCapture(region);

                        if (handles == null || handles.Length == 0)
                        {
                            return "COUNT: 0";
                        }

                        StringBuilder result = new StringBuilder();

                        result.AppendFormat(
                            "COUNT: {0}",
                            handles.Length);

                        for (int i = 0; i < handles.Length; i++)
                        {
                            MarElementHandle handle = handles[i];

                            if (handle == null)
                            {
                                continue;
                            }

                            using (handle)
                            using (MarText text =
                                drafting.TextPropertiesGet(handle))
                            {
                                if (text == null)
                                {
                                    continue;
                                }

                                using (MarPointPlanar position =
                                    text.Position)
                                {
                                    result.AppendFormat(
                                        " | {0}: {1} @ ({2}, {3})",
                                        i + 1,
                                        text.String,
                                        position.X,
                                        position.Y);
                                }
                            }
                        }

                        return result.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        [PMLNetCallable()]
        public string MoveTextsInRectangleByOffset(
            double x1,
            double y1,
            double x2,
            double y2,
            double deltaX,
            double deltaY)
        {
            try
            {
                using (MarDrafting drafting = new MarDrafting())
                {
                    if (!drafting.DwgCurrent())
                    {
                        return "ERROR: no current drawing";
                    }

                    double minX = Math.Min(x1, x2);
                    double minY = Math.Min(y1, y2);
                    double maxX = Math.Max(x1, x2);
                    double maxY = Math.Max(y1, y2);

                    using (MarRectanglePlanar rectangle =
                        new MarRectanglePlanar(
                            minX,
                            minY,
                            maxX,
                            maxY))
                    using (MarCaptureRegionPlanar region =
                        new MarCaptureRegionPlanar())
                    {
                        region.SetRectangle(rectangle);
                        region.SetInside();
                        region.SetNoCut();

                        MarElementHandle[] handles =
                            drafting.TextCapture(region);

                        if (handles == null || handles.Length == 0)
                        {
                            return "COUNT: 0";
                        }

                        int movedCount = 0;

                        using (MarVectorPlanar vector =
                            new MarVectorPlanar(deltaX, deltaY))
                        {
                            MarTransformationPlanar transformation =
                                new MarTransformationPlanar();

                            try
                            {
                                transformation.Translate(vector);

                                for (int i = 0; i < handles.Length; i++)
                                {
                                    MarElementHandle handle = handles[i];

                                    if (handle == null)
                                    {
                                        continue;
                                    }

                                    using (handle)
                                    {
                                        drafting.ElementTransform(
                                            handle,
                                            transformation);

                                        movedCount++;
                                    }
                                }
                            }
                            finally
                            {
                                IDisposable disposable =
                                    transformation as IDisposable;

                                if (disposable != null)
                                {
                                    disposable.Dispose();
                                }
                            }
                        }

                        return string.Format(
                            "SUCCESS: moved {0} texts by ({1}, {2})",
                            movedCount,
                            deltaX,
                            deltaY);
                    }
                }
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        [PMLNetCallable()]
        public string CaptureTextsAsJson(
            double x1,
            double y1,
            double x2,
            double y2)
        {
            try
            {
                using (MarDrafting drafting = new MarDrafting())
                {
                    if (!drafting.DwgCurrent())
                    {
                        return "{\"error\":\"no current drawing\"}";
                    }

                    double minX = Math.Min(x1, x2);
                    double minY = Math.Min(y1, y2);
                    double maxX = Math.Max(x1, x2);
                    double maxY = Math.Max(y1, y2);

                    using (MarRectanglePlanar rectangle =
                        new MarRectanglePlanar(
                            minX,
                            minY,
                            maxX,
                            maxY))
                    using (MarCaptureRegionPlanar region =
                        new MarCaptureRegionPlanar())
                    {
                        region.SetRectangle(rectangle);
                        region.SetInside();
                        region.SetNoCut();

                        MarElementHandle[] handles =
                            drafting.TextCapture(region);

                        if (handles == null || handles.Length == 0)
                        {
                            return "{\"count\":0,\"texts\":[]}";
                        }

                        StringBuilder items = new StringBuilder();
                        int actualCount = 0;

                        for (int i = 0; i < handles.Length; i++)
                        {
                            MarElementHandle handle = handles[i];

                            if (handle == null)
                            {
                                continue;
                            }

                            using (handle)
                            using (MarText text =
                                drafting.TextPropertiesGet(handle))
                            {
                                if (text == null)
                                {
                                    continue;
                                }

                                using (MarPointPlanar position =
                                    text.Position)
                                {
                                    if (position == null)
                                    {
                                        continue;
                                    }

                                    if (actualCount > 0)
                                    {
                                        items.Append(",");
                                    }

                                    actualCount++;

                                    items.AppendFormat(
                                        CultureInfo.InvariantCulture,
                                        "{{\"index\":{0},\"text\":\"{1}\",\"x\":{2},\"y\":{3}}}",
                                        actualCount,
                                        EscapeJsonString(text.String),
                                        position.X,
                                        position.Y);
                                }
                            }
                        }

                        return string.Format(
                            CultureInfo.InvariantCulture,
                            "{{\"count\":{0},\"texts\":[{1}]}}",
                            actualCount,
                            items.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                return "{\"error\":\"" +
                    EscapeJsonString(ex.Message) +
                    "\"}";
            }
        }

        [PMLNetCallable()]
        public string PrepareAlgorithmInputDxf(
            string inputPath,
            string outputPath)
        {
            bool outputCreated = false;
            string outputFullPath = null;

            try
            {
                if (string.IsNullOrEmpty(inputPath))
                {
                    throw new ArgumentException("inputPath is empty");
                }

                if (string.IsNullOrEmpty(outputPath))
                {
                    throw new ArgumentException("outputPath is empty");
                }

                string inputFullPath = Path.GetFullPath(inputPath);
                outputFullPath = Path.GetFullPath(outputPath);

                if (StringComparer.OrdinalIgnoreCase.Equals(
                    inputFullPath,
                    outputFullPath))
                {
                    throw new ArgumentException(
                        "inputPath and outputPath must be different");
                }

                if (!File.Exists(inputFullPath))
                {
                    throw new FileNotFoundException(
                        "Input DXF file was not found",
                        inputFullPath);
                }

                string outputDirectory =
                    Path.GetDirectoryName(outputFullPath);

                if (!string.IsNullOrEmpty(outputDirectory) &&
                    !Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                byte[] inputBytes = File.ReadAllBytes(inputFullPath);
                Encoding dxfEncoding = Encoding.GetEncoding(936);
                DxfFullScanResult scan = ScanAllDxfEntities(
                    inputBytes,
                    dxfEncoding);
                List<DxfEntityInfo> entitiesToRemove =
                    new List<DxfEntityInfo>();
                Dictionary<string, int> deletedLayerCounts =
                    new Dictionary<string, int>(
                        StringComparer.OrdinalIgnoreCase);
                Dictionary<string, int> deletedTypeCounts =
                    new Dictionary<string, int>(
                        StringComparer.OrdinalIgnoreCase);
                int entityIndex = 0;

                while (entityIndex < scan.Entities.Count)
                {
                    int oldEntityIndex = entityIndex;
                    DxfEntityInfo entity = scan.Entities[entityIndex];

                    if (IsAutoBoxOrLeaderLinesLayer(
                        entity.LayerName))
                    {
                        entitiesToRemove.Add(entity);
                        AddCount(
                            deletedLayerCounts,
                            entity.LayerName);
                        AddCount(
                            deletedTypeCounts,
                            entity.EntityType);
                    }

                    entityIndex++;
                    EnsureParserIndexAdvanced(
                        oldEntityIndex,
                        entityIndex);
                }

                if (!scan.EntitiesSectionFound ||
                    !scan.EndSectionFound)
                {
                    throw new InvalidDataException(
                        "ENTITIES section or ENDSEC was not found");
                }

                int originalCount = scan.Entities.Count;
                int removedCount = entitiesToRemove.Count;
                int keptCount = originalCount - removedCount;

                outputCreated = true;
                WriteDxfWithoutEntities(
                    inputBytes,
                    entitiesToRemove,
                    outputFullPath);

                byte[] outputBytes = File.ReadAllBytes(outputFullPath);
                DxfFullScanResult outputScan = ScanAllDxfEntities(
                    outputBytes,
                    dxfEncoding);

                if (!outputScan.EntitiesSectionFound ||
                    !outputScan.EndSectionFound)
                {
                    throw new InvalidDataException(
                        "Output ENTITIES section validation failed");
                }

                int outputEntityIndex = 0;
                int outputMinus11Count = 0;

                while (outputEntityIndex < outputScan.Entities.Count)
                {
                    int oldOutputEntityIndex = outputEntityIndex;
                    DxfEntityInfo entity =
                        outputScan.Entities[outputEntityIndex];

                    if (IsAutoBoxOrLeaderLinesLayer(
                        entity.LayerName))
                    {
                        throw new InvalidDataException(
                            "Output contains a prohibited layer entity");
                    }

                    if (string.Equals(
                        entity.LayerName,
                        "-11",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        outputMinus11Count++;
                    }

                    outputEntityIndex++;
                    EnsureParserIndexAdvanced(
                        oldOutputEntityIndex,
                        outputEntityIndex);
                }

                WritePrepareDxfLog(
                    outputFullPath + ".prepare.log.txt",
                    inputFullPath,
                    outputFullPath,
                    originalCount,
                    removedCount,
                    keptCount,
                    deletedLayerCounts,
                    deletedTypeCounts,
                    outputMinus11Count,
                    "PASSED");

                return string.Format(
                    "SUCCESS | removed={0} | kept={1} | layer_-11={2} | output={3}",
                    removedCount,
                    keptCount,
                    outputMinus11Count,
                    outputFullPath);
            }
            catch (DxfParserIndexException ex)
            {
                DeleteGeneratedDxf(outputCreated, outputFullPath);
                return "ERROR: parser index did not advance at index=" +
                    ex.Index;
            }
            catch (Exception ex)
            {
                DeleteGeneratedDxf(outputCreated, outputFullPath);
                return "ERROR: " +
                    ex.GetType().FullName + ": " +
                    ex.Message;
            }
        }

        [PMLNetCallable()]
        public string GetAlgorithmServiceUrl()
        {
            return AlgorithmServiceConfig.LoadBaseUrl();
        }

        [PMLNetCallable()]
        public string UploadAlgorithmTask(
            string filePath,
            string username,
            string projectName,
            string extraParamsJson)
        {
            string baseUrl = AlgorithmServiceConfig.LoadBaseUrl();

            if (baseUrl.StartsWith(
                "ERROR:",
                StringComparison.Ordinal))
            {
                return baseUrl;
            }

            AlgorithmServiceClient client =
                new AlgorithmServiceClient(baseUrl);
            return client.UploadAlgorithmTask(
                filePath,
                username,
                projectName,
                extraParamsJson);
        }

        [PMLNetCallable()]
        public string QueryAlgorithmTask(
            string taskId,
            string outputJsonPath)
        {
            string baseUrl = AlgorithmServiceConfig.LoadBaseUrl();

            if (baseUrl.StartsWith(
                "ERROR:",
                StringComparison.Ordinal))
            {
                return baseUrl;
            }

            AlgorithmServiceClient client =
                new AlgorithmServiceClient(baseUrl);
            return client.QueryAlgorithmTask(
                taskId,
                outputJsonPath);
        }

        [PMLNetCallable()]
        public string PreviewFirstAlgorithmMove(string resultJsonPath)
        {
            try
            {
                AlgorithmMoveData move;
                string error;

                if (!TryLoadFirstAlgorithmMove(
                    resultJsonPath,
                    out move,
                    out error))
                {
                    return error;
                }

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "SUCCESS | handle={0} | text={1} | origin=({2},{3}) | new=({4},{5}) | offset=({6},{7})",
                    move.Handle,
                    move.Text,
                    move.OriginX,
                    move.OriginY,
                    move.NewX,
                    move.NewY,
                    move.DeltaX,
                    move.DeltaY);
            }
            catch (Exception ex)
            {
                return "ERROR: " +
                    ex.GetType().FullName + ": " +
                    ex.Message;
            }
        }

        [PMLNetCallable()]
        public string ApplyFirstAlgorithmMove(string resultJsonPath)
        {
            try
            {
                AlgorithmMoveData move;
                string error;

                if (!TryLoadFirstAlgorithmMove(
                    resultJsonPath,
                    out move,
                    out error))
                {
                    return error;
                }

                using (MarDrafting drafting = new MarDrafting())
                {
                    if (!drafting.DwgCurrent())
                    {
                        return "ERROR: no current drawing";
                    }

                    using (MarPointPlanar origin =
                        new MarPointPlanar(
                            move.OriginX,
                            move.OriginY))
                    using (MarElementHandle handle =
                        drafting.TextIdentify(origin))
                    {
                        if (handle == null)
                        {
                            return "ERROR: text not found";
                        }

                        using (MarText text =
                            drafting.TextPropertiesGet(handle))
                        {
                            if (text == null)
                            {
                                return "ERROR: text properties are null";
                            }

                            string actualText = text.String;

                            if (!string.Equals(
                                actualText,
                                move.Text,
                                StringComparison.Ordinal))
                            {
                                return "ERROR: text mismatch | expected=" +
                                    move.Text +
                                    " | actual=" +
                                    actualText;
                            }

                            using (MarVectorPlanar vector =
                                new MarVectorPlanar(
                                    move.DeltaX,
                                    move.DeltaY))
                            {
                                MarTransformationPlanar transformation =
                                    new MarTransformationPlanar();

                                try
                                {
                                    transformation.Translate(vector);
                                    drafting.ElementTransform(
                                        handle,
                                        transformation);
                                }
                                finally
                                {
                                    IDisposable disposable =
                                        transformation as IDisposable;

                                    if (disposable != null)
                                    {
                                        disposable.Dispose();
                                    }
                                }
                            }

                            return string.Format(
                                CultureInfo.InvariantCulture,
                                "SUCCESS | moved handle={0} | text={1} | dx={2} | dy={3}",
                                move.Handle,
                                move.Text,
                                move.DeltaX,
                                move.DeltaY);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return "ERROR: " +
                    ex.GetType().FullName + ": " +
                    ex.Message;
            }
        }

        [PMLNetCallable()]
        public string PreviewAllAlgorithmAnnotationGeometry(
            string resultJsonPath,
            string sourceDxfPath)
        {
            return SanitizePmlReturn(ProcessAllAlgorithmAnnotationGeometry(
                resultJsonPath,
                sourceDxfPath,
                false));
        }

        [PMLNetCallable()]
        public string ApplyAllAlgorithmAnnotationGeometry(
            string resultJsonPath,
            string sourceDxfPath)
        {
            return SanitizePmlReturn(ProcessAllAlgorithmAnnotationGeometry(
                resultJsonPath,
                sourceDxfPath,
                true));
        }

        [PMLNetCallable()]
        public string ApplyAlgorithmMovesBatch(
            string resultJsonPath,
            double startIndex,
            double batchSize)
        {
            return SanitizePmlReturn(ProcessAlgorithmMovesRange(
                resultJsonPath,
                startIndex,
                batchSize,
                false));
        }

        [PMLNetCallable()]
        public string PreviewAlgorithmAnnotationGeometryBatch(
            string resultJsonPath,
            string sourceDxfPath,
            double startIndex,
            double batchSize)
        {
            return SanitizePmlReturn(ProcessAllAlgorithmAnnotationGeometry(
                resultJsonPath,
                sourceDxfPath,
                false,
                startIndex,
                batchSize,
                false));
        }

        [PMLNetCallable()]
        public string ApplyAlgorithmAnnotationGeometryBatch(
            string resultJsonPath,
            string sourceDxfPath,
            double startIndex,
            double batchSize)
        {
            return SanitizePmlReturn(ProcessAllAlgorithmAnnotationGeometry(
                resultJsonPath,
                sourceDxfPath,
                true,
                startIndex,
                batchSize,
                false));
        }

        [PMLNetCallable()]
        public string DiagnoseAlgorithmGeometry(
            string resultJsonPath,
            string sourceDxfPath,
            string handle)
        {
            string logPath = (resultJsonPath ?? string.Empty) +
                ".geometry.diagnostic." + (handle ?? string.Empty) +
                ".log.txt";
            int completedStep = 0;
            string geometryName = string.Empty;
            int failedStep = 0;
            Exception failure = null;

            try
            {
                using (StreamWriter log = new StreamWriter(
                    logPath,
                    false,
                    new UTF8Encoding(false)))
                {
                    log.WriteLine("DiagnoseAlgorithmGeometry");
                    log.WriteLine("handle=" + handle);
                    log.WriteLine("resultJsonPath=" + resultJsonPath);
                    log.WriteLine("sourceDxfPath=" + sourceDxfPath);
                    log.WriteLine("API signatures confirmed from NETmarAPI.chm:");
                    log.WriteLine(
                        "Aveva.Marine.Drafting.MarDrafting.GeometryIdentify(" +
                        "Aveva.Marine.Geometry.MarPointPlanar point): " +
                        "Aveva.Marine.Drafting.MarElementHandle");
                    log.WriteLine(
                        "Aveva.Marine.Drafting.MarDrafting.ContourPropertiesGet(" +
                        "Aveva.Marine.Drafting.MarElementHandle handle): " +
                        "Aveva.Marine.Geometry.MarContourPlanar");
                    log.WriteLine(
                        "Aveva.Marine.Geometry.MarContourPlanar.Length(): double");
                    log.WriteLine(
                        "Aveva.Marine.Drafting.MarDrafting.ElementLayerGet(" +
                        "Aveva.Marine.Drafting.MarElementHandle Handle): " +
                        "Aveva.Marine.Drafting.MarLayer");
                    log.WriteLine(
                        "Element type numeric getter: not present in NETmarAPI.chm");
                    log.WriteLine(
                        "LINE Start/End direct getter from MarElementHandle: " +
                        "not present in NETmarAPI.chm");
                    log.WriteLine(
                        "Current selected element getter: not present in NETmarAPI.chm; " +
                        "DiagnoseCurrentSelectedElement not implemented");
                    log.Flush();

                    BatchMoveItem move =
                        LoadAlgorithmMoveByHandle(resultJsonPath, handle);
                    List<BatchMoveItem> moves =
                        new List<BatchMoveItem>();
                    moves.Add(move);
                    List<AlgorithmAnnotationGeometryItem> items =
                        new List<AlgorithmAnnotationGeometryItem>();
                    LoadSourceGeometryAssociations(
                        sourceDxfPath,
                        moves,
                        items);
                    if (items.Count != 1)
                    {
                        throw new InvalidDataException(
                            "source geometry association was not created");
                    }

                    AlgorithmAnnotationGeometryItem item = items[0];
                    List<DxfPoint> diagnosticUnderlinePoints =
                        NormalizeUnderlinePoints(
                            item.SourceUnderline == null
                                ? null
                                : item.SourceUnderline.Vertices);
                    if (diagnosticUnderlinePoints == null ||
                        diagnosticUnderlinePoints.Count < 2 ||
                        item.SourceOldLeader == null ||
                        item.SourceOldLeader.LineStart == null ||
                        item.SourceOldLeader.LineEnd == null)
                    {
                        throw new InvalidDataException(
                            "source geometry is incomplete");
                    }
                    DxfPoint underlineStart = diagnosticUnderlinePoints[0];
                    DxfPoint underlineEnd = diagnosticUnderlinePoints[1];
                    DxfPoint movedStart = new DxfPoint(
                        underlineStart.X + move.Data.DeltaX,
                        underlineStart.Y + move.Data.DeltaY);
                    DxfPoint movedEnd = new DxfPoint(
                        underlineEnd.X + move.Data.DeltaX,
                        underlineEnd.Y + move.Data.DeltaY);
                    DxfPoint oldLeaderStart = item.SourceOldLeader.LineStart;
                    DxfPoint oldLeaderEnd = item.SourceOldLeader.LineEnd;
                    DxfPoint newLeaderStart = new DxfPoint(
                        move.Data.LeaderStart[0],
                        move.Data.LeaderStart[1]);
                    DxfPoint newLeaderEnd = new DxfPoint(
                        move.Data.LeaderEnd[0],
                        move.Data.LeaderEnd[1]);

                    using (MarDrafting drafting = new MarDrafting())
                    {
                        if (!drafting.DwgCurrent())
                        {
                            throw new InvalidDataException(
                                "no current drawing");
                        }

                        geometryName = "source underline";
                        DiagnoseOneGeometry(
                            log,
                            drafting,
                            "source underline",
                            underlineStart,
                            underlineEnd,
                            ref completedStep,
                            ref failedStep,
                            ref failure);
                        if (failure == null)
                        {
                            geometryName = "moved underline";
                            DiagnoseOneGeometry(
                                log,
                                drafting,
                                "moved underline",
                                movedStart,
                                movedEnd,
                                ref completedStep,
                                ref failedStep,
                                ref failure);
                        }
                        if (failure == null)
                        {
                            geometryName = "old leader";
                            DiagnoseOneGeometry(
                                log,
                                drafting,
                                "old leader",
                                oldLeaderStart,
                                oldLeaderEnd,
                                ref completedStep,
                                ref failedStep,
                                ref failure);
                        }
                        if (failure == null)
                        {
                            geometryName = "new leader";
                            DiagnoseOneGeometry(
                                log,
                                drafting,
                                "new leader",
                                newLeaderStart,
                                newLeaderEnd,
                                ref completedStep,
                                ref failedStep,
                                ref failure);
                        }
                    }

                    DisposeAnnotationGeometryItems(items);
                    if (failure != null)
                    {
                        log.WriteLine(
                            "DIAGNOSTIC FAILED step=" + failedStep);
                        log.WriteLine(
                            "Exception type=" + failure.GetType().FullName);
                        log.WriteLine("Message=" + failure.Message);
                        log.WriteLine("StackTrace=" + failure.StackTrace);
                        log.WriteLine(
                            "InnerException=" +
                            (failure.InnerException == null
                                ? "(null)"
                                : failure.InnerException.ToString()));
                        log.WriteLine("Full exception=" + failure.ToString());
                        log.Flush();
                        return "ERROR: diagnostic failed | handle=" +
                            handle + " | geometry=" + geometryName +
                            " | step=" + failedStep +
                            " | exception_type=" +
                            failure.GetType().FullName +
                            " | message=" + failure.Message +
                            " | log=" + logPath;
                    }

                    log.WriteLine("DIAGNOSTIC SUCCESS");
                    log.Flush();
                    return "SUCCESS | handle=" + handle +
                        " | completed_step=10 | log=" + logPath;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    using (StreamWriter log = new StreamWriter(
                        logPath,
                        true,
                        new UTF8Encoding(false)))
                    {
                        log.WriteLine("DIAGNOSTIC EXCEPTION");
                        log.WriteLine("Exception type=" + ex.GetType().FullName);
                        log.WriteLine("Message=" + ex.Message);
                        log.WriteLine("StackTrace=" + ex.StackTrace);
                        log.WriteLine(
                            "InnerException=" +
                            (ex.InnerException == null
                                ? "(null)"
                                : ex.InnerException.ToString()));
                        log.WriteLine("Full exception=" + ex.ToString());
                        log.Flush();
                    }
                }
                catch
                {
                }
                return "ERROR: diagnostic failed | handle=" + handle +
                    " | geometry=" + geometryName +
                    " | step=" + failedStep +
                    " | exception_type=" + ex.GetType().FullName +
                    " | message=" + ex.Message +
                    " | log=" + logPath;
            }
        }

        private static BatchMoveItem LoadAlgorithmMoveByHandle(
            string resultJsonPath,
            string requestedHandle)
        {
            if (string.IsNullOrEmpty(resultJsonPath))
            {
                throw new InvalidDataException("resultJsonPath is empty");
            }
            if (string.IsNullOrEmpty(requestedHandle))
            {
                throw new InvalidDataException("handle is empty");
            }
            if (!File.Exists(resultJsonPath))
            {
                throw new FileNotFoundException(
                    "result JSON file not found",
                    resultJsonPath);
            }

            string json = File.ReadAllText(
                resultJsonPath,
                new UTF8Encoding(false));
            JavaScriptSerializer serializer =
                new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            object parsed = serializer.DeserializeObject(json);
            IDictionary<string, object> root =
                parsed as IDictionary<string, object>;
            if (root == null)
            {
                throw new InvalidDataException("JSON root is invalid");
            }

            IList<object> results = GetObjectList(root, "results");
            if (results == null)
            {
                throw new InvalidDataException("results is missing");
            }

            BatchMoveItem found = null;
            int resultIndex = 0;
            while (resultIndex < results.Count)
            {
                int oldResultIndex = resultIndex;
                IDictionary<string, object> resultObject =
                    results[resultIndex] as IDictionary<string, object>;
                if (resultObject == null)
                {
                    throw new InvalidDataException("result is invalid");
                }
                IList<object> layouts =
                    GetObjectList(resultObject, "layout");
                if (layouts == null)
                {
                    throw new InvalidDataException("layout is missing");
                }

                int layoutIndex = 0;
                while (layoutIndex < layouts.Count)
                {
                    int oldLayoutIndex = layoutIndex;
                    IDictionary<string, object> layoutObject =
                        layouts[layoutIndex] as IDictionary<string, object>;
                    if (layoutObject == null)
                    {
                        throw new InvalidDataException("layout is invalid");
                    }
                    IList<object> moves =
                        GetObjectList(layoutObject, "moves");
                    if (moves == null)
                    {
                        throw new InvalidDataException("moves is missing");
                    }

                    int moveIndex = 0;
                    while (moveIndex < moves.Count)
                    {
                        int oldMoveIndex = moveIndex;
                        IDictionary<string, object> moveObject =
                            moves[moveIndex] as IDictionary<string, object>;
                        if (moveObject == null)
                        {
                            throw new InvalidDataException("move is invalid");
                        }
                        string moveHandle = GetOptionalString(
                            moveObject,
                            "handle");
                        if (string.Equals(
                            moveHandle,
                            requestedHandle,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            bool isMove;
                            if (!TryGetBoolean(
                                moveObject,
                                "is_move",
                                out isMove) || !isMove)
                            {
                                throw new InvalidDataException(
                                    "requested handle is not is_move=true");
                            }
                            if (found != null)
                            {
                                throw new InvalidDataException(
                                    "requested handle is duplicated");
                            }
                            AlgorithmMoveData data =
                                ParseAlgorithmMove(moveObject);
                            data.Layer = GetRequiredString(
                                moveObject,
                                "layer");
                            data.NeedsPlan = GetRequiredBoolean(
                                moveObject,
                                "needs_plan");
                            found = new BatchMoveItem();
                            found.Data = data;
                            found.PrecheckStatus = "DIAGNOSTIC";
                            found.ApplyStatus = "NOT_APPLIED";
                        }
                        moveIndex++;
                        EnsureParserIndexAdvanced(
                            oldMoveIndex,
                            moveIndex);
                    }
                    layoutIndex++;
                    EnsureParserIndexAdvanced(
                        oldLayoutIndex,
                        layoutIndex);
                }
                resultIndex++;
                EnsureParserIndexAdvanced(
                    oldResultIndex,
                    resultIndex);
            }

            if (found == null)
            {
                throw new InvalidDataException(
                    "requested handle was not found");
            }
            return found;
        }

        private static void DiagnoseOneGeometry(
            StreamWriter log,
            MarDrafting drafting,
            string geometryName,
            DxfPoint start,
            DxfPoint end,
            ref int completedStep,
            ref int failedStep,
            ref Exception failure)
        {
            DxfPoint probe = new DxfPoint(
                start.X + (end.X - start.X) * 0.25,
                start.Y + (end.Y - start.Y) * 0.25);
            log.WriteLine();
            log.WriteLine("GEOMETRY=" + geometryName);
            log.WriteLine("target_start=" + FormatPoint(start));
            log.WriteLine("target_end=" + FormatPoint(end));
            log.WriteLine("probe_25_percent=" + FormatPoint(probe));

            MarPointPlanar inputPoint = null;
            MarElementHandle returnedHandle = null;
            MarContourPlanar contour = null;

            try
            {
                log.WriteLine("STEP 1: create input point START");
                inputPoint = new MarPointPlanar(probe.X, probe.Y);
                log.WriteLine("STEP 1: create input point SUCCESS");
                completedStep = 1;
            }
            catch (Exception ex)
            {
                LogDiagnosticException(log, ex);
                log.WriteLine("STEP 1: create input point ERROR");
                failedStep = 1;
                failure = ex;
                return;
            }

            try
            {
                log.WriteLine("STEP 2: call identify/select API START");
                returnedHandle = drafting.GeometryIdentify(inputPoint);
                log.WriteLine(
                    "STEP 2: call identify/select API SUCCESS returned=" +
                    (returnedHandle == null ? "null" : "non-null"));
                completedStep = 2;
            }
            catch (Exception ex)
            {
                LogDiagnosticException(log, ex);
                log.WriteLine("STEP 2: call identify/select API ERROR");
                failedStep = 2;
                failure = ex;
                DisposeDiagnosticObjects(log, inputPoint, returnedHandle, contour);
                return;
            }

            if (returnedHandle == null)
            {
                InvalidDataException ex = new InvalidDataException(
                    "GeometryIdentify returned null");
                log.WriteLine("STEP 3: obtain returned object SUCCESS returned=null");
                log.WriteLine("STEP 3: obtain returned object ERROR no object");
                failedStep = 3;
                failure = ex;
                DisposeDiagnosticObjects(log, inputPoint, returnedHandle, contour);
                return;
            }

            try
            {
                log.WriteLine("STEP 3: obtain returned object START");
                log.WriteLine("STEP 3: obtain returned object SUCCESS handle=" +
                    returnedHandle.handle);
                completedStep = 3;
            }
            catch (Exception ex)
            {
                LogDiagnosticException(log, ex);
                log.WriteLine("STEP 3: obtain returned object ERROR");
                failedStep = 3;
                failure = ex;
                DisposeDiagnosticObjects(log, inputPoint, returnedHandle, contour);
                return;
            }

            try
            {
                log.WriteLine("STEP 4: read object type START");
                log.WriteLine(
                    "STEP 4: read object type SUCCESS type=" +
                    returnedHandle.GetType().FullName);
                completedStep = 4;
            }
            catch (Exception ex)
            {
                LogDiagnosticException(log, ex);
                log.WriteLine("STEP 4: read object type ERROR");
                failedStep = 4;
                failure = ex;
                DisposeDiagnosticObjects(log, inputPoint, returnedHandle, contour);
                return;
            }

            try
            {
                log.WriteLine("STEP 5: read element type START");
                bool isGeometry = drafting.ElementIsGeometry(returnedHandle);
                log.WriteLine(
                    "STEP 5: read element type SUCCESS numeric element type " +
                    "getter is not exposed by NETmarAPI.chm; " +
                    "is_geometry=" + isGeometry);
            }
            catch (Exception ex)
            {
                LogDiagnosticException(log, ex);
                log.WriteLine("STEP 5: read element type ERROR");
                failedStep = 5;
                failure = ex;
                DisposeDiagnosticObjects(log, inputPoint, returnedHandle, contour);
                return;
            }

            try
            {
                log.WriteLine("STEP 5: read contour type START");
                bool isContour = drafting.ElementIsContour(returnedHandle);
                log.WriteLine(
                    "STEP 5: read contour type SUCCESS is_contour=" +
                    isContour);
                completedStep = 5;
            }
            catch (Exception ex)
            {
                LogDiagnosticException(log, ex);
                log.WriteLine("STEP 5: read contour type ERROR");
                failedStep = 5;
                failure = ex;
                DisposeDiagnosticObjects(log, inputPoint, returnedHandle, contour);
                return;
            }

            log.WriteLine(
                "STEP 6: read start point ERROR formal MarElementHandle/" +
                "MarContourPlanar Start property not present in NETmarAPI.chm");
            log.WriteLine(
                "STEP 7: read end point ERROR formal MarElementHandle/" +
                "MarContourPlanar End property not present in NETmarAPI.chm");
            completedStep = 5;

            try
            {
                log.WriteLine("STEP 8: obtain contour properties START");
                contour = drafting.ContourPropertiesGet(returnedHandle);
                if (contour == null)
                {
                    throw new InvalidDataException(
                        "ContourPropertiesGet returned null");
                }
                log.WriteLine("STEP 8: obtain contour properties SUCCESS type=" +
                    contour.GetType().FullName);
            }
            catch (Exception ex)
            {
                LogDiagnosticException(log, ex);
                log.WriteLine("STEP 8: obtain contour properties ERROR");
                failedStep = 8;
                failure = ex;
                DisposeDiagnosticObjects(log, inputPoint, returnedHandle, contour);
                return;
            }

            try
            {
                log.WriteLine("STEP 8: read length START");
                double length = contour.Length();
                log.WriteLine("STEP 8: read length SUCCESS length=" +
                    length.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                LogDiagnosticException(log, ex);
                log.WriteLine("STEP 8: read length ERROR");
                failedStep = 8;
                failure = ex;
                DisposeDiagnosticObjects(log, inputPoint, returnedHandle, contour);
                return;
            }
            completedStep = 8;

            try
            {
                log.WriteLine("STEP 9: read layer START");
                using (MarLayer layer = drafting.ElementLayerGet(returnedHandle))
                {
                    if (layer == null)
                    {
                        throw new InvalidDataException(
                            "ElementLayerGet returned null");
                    }
                    log.WriteLine(
                        "STEP 9: read layer SUCCESS layer_id=" +
                        layer.LayerId);
                    try
                    {
                        log.WriteLine(
                            "STEP 9: read layer description START");
                        string description = layer.GetDescription();
                        log.WriteLine(
                            "STEP 9: read layer description SUCCESS description=" +
                            description);
                    }
                    catch (Exception descriptionException)
                    {
                        LogDiagnosticException(log, descriptionException);
                        log.WriteLine(
                            "STEP 9: read layer description ERROR");
                        throw;
                    }
                }
                completedStep = 9;
            }
            catch (Exception ex)
            {
                LogDiagnosticException(log, ex);
                log.WriteLine("STEP 9: read layer ERROR");
                failedStep = 9;
                failure = ex;
                DisposeDiagnosticObjects(log, inputPoint, returnedHandle, contour);
                return;
            }

            DisposeDiagnosticObjects(log, inputPoint, returnedHandle, contour);
            completedStep = 10;
            log.WriteLine("STEP 10: release/dispose if required SUCCESS");
        }

        private static void LogDiagnosticException(
            StreamWriter log,
            Exception ex)
        {
            log.WriteLine("Exception type=" + ex.GetType().FullName);
            log.WriteLine("Message=" + ex.Message);
            log.WriteLine("StackTrace=" + ex.StackTrace);
            log.WriteLine(
                "InnerException=" +
                (ex.InnerException == null
                    ? "(null)"
                    : ex.InnerException.ToString()));
            log.WriteLine("Full exception=" + ex.ToString());
        }

        private static void DisposeDiagnosticObjects(
            StreamWriter log,
            MarPointPlanar inputPoint,
            MarElementHandle returnedHandle,
            MarContourPlanar contour)
        {
            if (contour != null)
            {
                try
                {
                    log.WriteLine("STEP 10: dispose contour START");
                    contour.Dispose();
                    log.WriteLine("STEP 10: dispose contour SUCCESS");
                }
                catch (Exception ex)
                {
                    LogDiagnosticException(log, ex);
                    log.WriteLine("STEP 10: dispose contour ERROR");
                }
            }
            if (returnedHandle != null)
            {
                try
                {
                    log.WriteLine("STEP 10: dispose returned object START");
                    returnedHandle.Dispose();
                    log.WriteLine("STEP 10: dispose returned object SUCCESS");
                }
                catch (Exception ex)
                {
                    LogDiagnosticException(log, ex);
                    log.WriteLine("STEP 10: dispose returned object ERROR");
                }
            }
            if (inputPoint != null)
            {
                try
                {
                    log.WriteLine("STEP 10: dispose input point START");
                    inputPoint.Dispose();
                    log.WriteLine("STEP 10: dispose input point SUCCESS");
                }
                catch (Exception ex)
                {
                    LogDiagnosticException(log, ex);
                    log.WriteLine("STEP 10: dispose input point ERROR");
                }
            }
        }

        private string ProcessAllAlgorithmAnnotationGeometry(
            string resultJsonPath,
            string sourceDxfPath,
            bool apply)
        {
            return ProcessAllAlgorithmAnnotationGeometry(
                resultJsonPath,
                sourceDxfPath,
                apply,
                0.0,
                1.0,
                true);
        }

        private string ProcessAllAlgorithmAnnotationGeometry(
            string resultJsonPath,
            string sourceDxfPath,
            bool apply,
            double startIndexValue,
            double batchSizeValue,
            bool fullMode)
        {
            DateTime startTime = DateTime.Now;
            string suffix = apply
                ? ".geometry.apply.log.txt"
                : ".geometry.preview.log.txt";
            string logPath = (resultJsonPath ?? string.Empty) + suffix;
            BatchAlgorithmData batch = new BatchAlgorithmData();
            List<AlgorithmAnnotationGeometryItem> items =
                new List<AlgorithmAnnotationGeometryItem>();
            int underlinesMoved = 0;
            int oldLeadersDeleted = 0;
            int newLeadersCreated = 0;
            int rollbackSuccess = 0;
            int rollbackFailed = 0;
            int completed = 0;
            string failedHandle = string.Empty;
            string failureMessage = null;
            int previewInconclusiveCount = 0;
            List<AlgorithmAnnotationGeometryItem> verificationItems = null;
            bool postVerificationRollbackRequired = false;

            try
            {
                LoadAllAlgorithmMoves(resultJsonPath, batch);
                ValidateDuplicateMoveHandles(batch.MoveItems);
                int batchStart;
                int batchEndExclusive;
                if (fullMode)
                {
                    batchStart = 0;
                    batchEndExclusive = batch.MoveItems.Count;
                }
                else
                {
                    ResolveBatchRange(
                        startIndexValue,
                        batchSizeValue,
                        batch.MoveItems.Count,
                        out batchStart,
                        out batchEndExclusive);
                }
                List<BatchMoveItem> selectedMoves = CreateMoveBatch(
                    batch.MoveItems,
                    batchStart,
                    batchEndExclusive);
                logPath = BuildGeometryBatchLogPath(
                    resultJsonPath,
                    apply,
                    batchStart,
                    batchEndExclusive,
                    fullMode);
                if (apply)
                {
                    AppendApplyStep(logPath, "STEP=METHOD_ENTER");
                    if (fullMode)
                    {
                        AppendApplyStep(logPath, "EXECUTION_MODE=FULL");
                    }
                }
                else
                {
                    WritePreviewBuildHeader(logPath, fullMode);
                }
                LoadSourceGeometryAssociations(
                    sourceDxfPath,
                    selectedMoves,
                    items);
                Dictionary<string, ApplyReceipt> previewReceipts =
                    apply
                        ? new Dictionary<string, ApplyReceipt>()
                        : LoadApplyReceipt(resultJsonPath);
                if (apply)
                {
                    AppendApplyStep(logPath, "STEP=JSON_DXF_LOADED");
                }

                using (MarDrafting drafting = new MarDrafting())
                {
                    if (!drafting.DwgCurrent())
                    {
                        throw new InvalidDataException(
                            "no current drawing");
                    }

                    if (apply)
                    {
                        AppendApplyStep(logPath, "STEP=PRECHECK_START");
                    }
                    PrecheckAnnotationGeometry(
                        drafting,
                        items,
                        true,
                        apply);
                    if (!apply)
                    {
                        previewInconclusiveCount = ApplyReceiptIdentityToPreview(
                            items,
                            previewReceipts,
                            logPath);
                    }

                    int failedCount = CountGeometryStatus(
                        items,
                        "FAILED_PRECHECK");
                    int readyCount = CountGeometryStatus(
                        items,
                        "READY");
                    int alreadyCount = CountGeometryStatus(
                        items,
                        "ALREADY_APPLIED");
                    int inconclusiveCount = previewInconclusiveCount;

                    if (apply)
                    {
                        AppendApplyStep(
                            logPath,
                            "STEP=PRECHECK_COMPLETE | ready=" +
                            readyCount +
                            " | already_applied=" + alreadyCount +
                            " | failed=" + failedCount);
                    }

                    if (failedCount > 0)
                    {
                        failedHandle = FindFirstGeometryFailureHandle(items);
                        WriteAnnotationGeometryLog(
                            logPath,
                            startTime,
                            DateTime.Now,
                            resultJsonPath,
                            sourceDxfPath,
                            batch,
                            items,
                            underlinesMoved,
                            oldLeadersDeleted,
                            newLeadersCreated,
                        rollbackSuccess,
                        rollbackFailed,
                        "precheck failed",
                        !apply);
                        return "ERROR: geometry " +
                            (apply ? "apply" : "preview") +
                            " precheck failed | stage=" +
                            (apply ? "GEOMETRY_APPLY" : "GEOMETRY_PREVIEW") +
                            " | start=" + batchStart +
                            " | end_exclusive=" + batchEndExclusive +
                            " | processed=" + items.Count +
                            " | total=" + batch.MoveItems.Count +
                            " | ready=" + readyCount +
                            " | already_applied=" + alreadyCount +
                            " | failed=" + failedCount +
                            " | first_failed_handle=" + failedHandle +
                            " | log=" + logPath;
                    }

                    if (!apply && inconclusiveCount > 0)
                    {
                        return "ERROR: geometry preview identity unresolved | " +
                            "stage=GEOMETRY_PREVIEW | start=" + batchStart +
                            " | end_exclusive=" + batchEndExclusive +
                            " | processed=" + items.Count +
                            " | ready=" + readyCount +
                            " | already_applied=" + alreadyCount +
                            " | failed=0 | inconclusive=" +
                            inconclusiveCount +
                            " | reason=missing apply identity receipt" +
                            " | log=" + logPath;
                    }

                    if (apply && readyCount == 0 &&
                        alreadyCount == items.Count && failedCount == 0)
                    {
                        WriteAnnotationGeometryLog(
                            logPath,
                            startTime,
                            DateTime.Now,
                            resultJsonPath,
                            sourceDxfPath,
                            batch,
                            items,
                            0,
                            0,
                            0,
                            0,
                            0,
                            string.Empty,
                            false);
                        return "SUCCESS | stage=GEOMETRY_APPLY | start=" +
                            batchStart + " | end_exclusive=" + batchEndExclusive +
                            " | processed=" + items.Count +
                            " | total=" + batch.MoveItems.Count +
                            " | underlines_moved=0" +
                            " | old_leaders_deleted=0" +
                            " | new_leaders_created=0" +
                            " | already_applied=" + alreadyCount +
                            " | failed=0 | log=" + logPath;
                    }

                    if (apply)
                    {
                        AppendApplyStep(
                            logPath,
                            "STEP=OLD_LEADER_LAYER_CAPTURE_START");
                        CaptureOldLeaderLayers(drafting, items, logPath);
                        AppendApplyStep(logPath, "STEP=APPLY_START");
                        List<AlgorithmAnnotationGeometryItem> completedItems =
                            new List<AlgorithmAnnotationGeometryItem>();
                        HashSet<int> activeExpectedHandleIds =
                            new HashSet<int>();
                        int index = 0;
                        while (index < items.Count)
                        {
                            int oldIndex = index;
                            AlgorithmAnnotationGeometryItem item =
                                items[index];

                            if (item.Status == "READY")
                            {
                                try
                                {
                                    ApplyAnnotationGeometryItem(
                                        drafting,
                                        item,
                                        activeExpectedHandleIds);
                                    item.ApplyStatus = "SUCCESS";
                                    completedItems.Add(item);
                                    completed++;
                                    underlinesMoved++;
                                    oldLeadersDeleted++;
                                    newLeadersCreated++;
                                }
                                catch (Exception ex)
                                {
                                    item.ApplyStatus = "FAILED: " + ex.Message;
                                    failureMessage = ex.Message;
                                    failedHandle = item.Move.Data.Handle;

                                    if (item.NewLeaderCreated ||
                                        item.UnderlineMoved ||
                                        item.OldLeaderDeleted)
                                    {
                                        try
                                        {
                                            RollbackAnnotationGeometryItem(
                                                drafting,
                                                item,
                                                activeExpectedHandleIds);
                                            item.RollbackStatus = "SUCCESS";
                                            rollbackSuccess++;
                                        }
                                        catch (Exception currentRollbackException)
                                        {
                                            item.RollbackStatus =
                                                "FAILED: " +
                                                currentRollbackException.Message;
                                            rollbackFailed++;
                                        }
                                    }

                                    int rollbackIndex =
                                        completedItems.Count - 1;
                                    while (rollbackIndex >= 0)
                                    {
                                        AlgorithmAnnotationGeometryItem rollbackItem =
                                            completedItems[rollbackIndex];
                                        try
                                        {
                                            RollbackAnnotationGeometryItem(
                                                drafting,
                                                rollbackItem,
                                                activeExpectedHandleIds);
                                            rollbackItem.RollbackStatus =
                                                "SUCCESS";
                                            rollbackSuccess++;
                                        }
                                        catch (Exception rollbackException)
                                        {
                                            rollbackItem.RollbackStatus =
                                                "FAILED: " +
                                                rollbackException.Message;
                                            rollbackFailed++;
                                        }
                                        rollbackIndex--;
                                    }
                                    break;
                                }
                            }

                            index++;
                            EnsureParserIndexAdvanced(oldIndex, index);
                        }

                        if (failureMessage == null)
                        {
                            string postVerificationFailureHandle;
                            string postVerificationFailureDetail;
                            if (!PostVerifyAppliedItems(
                                drafting,
                                items,
                                logPath,
                                activeExpectedHandleIds,
                                out postVerificationFailureHandle,
                                out postVerificationFailureDetail))
                            {
                                failureMessage = postVerificationFailureDetail;
                                failedHandle = postVerificationFailureHandle;
                                postVerificationRollbackRequired = true;
                            }
                        }

                        if (postVerificationRollbackRequired)
                        {
                            AppendApplyStep(
                                logPath,
                                "STEP=POST_VERIFICATION_ROLLBACK_START");
                            int rollbackIndex = completedItems.Count - 1;
                            while (rollbackIndex >= 0)
                            {
                                AlgorithmAnnotationGeometryItem rollbackItem =
                                    completedItems[rollbackIndex];
                                try
                                {
                                    RollbackAnnotationGeometryItem(
                                        drafting,
                                        rollbackItem,
                                        activeExpectedHandleIds);
                                    rollbackItem.RollbackStatus = "SUCCESS";
                                    rollbackSuccess++;
                                }
                                catch (Exception rollbackException)
                                {
                                    rollbackItem.RollbackStatus =
                                        "FAILED: " + rollbackException.Message;
                                    rollbackFailed++;
                                }
                                rollbackIndex--;
                            }
                            AppendApplyStep(
                                logPath,
                                "STEP=POST_VERIFICATION_ROLLBACK_COMPLETE | success=" +
                                rollbackSuccess +
                                " | failed=" + rollbackFailed);

                            AppendApplyStep(
                                logPath,
                                "STEP=POST_ROLLBACK_PREVIEW_START");
                            verificationItems =
                                new List<AlgorithmAnnotationGeometryItem>();
                            List<BatchMoveItem> restoredMoves =
                                CreateVerificationMoves(selectedMoves);
                            LoadSourceGeometryAssociations(
                                sourceDxfPath,
                                restoredMoves,
                                verificationItems);
                            int restoredLayerIndex = 0;
                            while (restoredLayerIndex < verificationItems.Count)
                            {
                                int oldRestoredLayerIndex = restoredLayerIndex;
                                verificationItems[restoredLayerIndex].OldLeaderLayerId =
                                    items[restoredLayerIndex].OldLeaderLayerId;
                                verificationItems[restoredLayerIndex].HasOldLeaderLayerId =
                                    items[restoredLayerIndex].HasOldLeaderLayerId;
                                restoredLayerIndex++;
                                EnsureParserIndexAdvanced(
                                    oldRestoredLayerIndex,
                                    restoredLayerIndex);
                            }
                            PrecheckAnnotationGeometry(
                                drafting,
                                verificationItems,
                                true,
                                true);
                            int restoredReadyCount = CountGeometryStatus(
                                verificationItems,
                                "READY");
                            int restoredFailedCount = CountGeometryStatus(
                                verificationItems,
                                "FAILED_PRECHECK");
                            AppendApplyStep(
                                logPath,
                                "STEP=POST_ROLLBACK_PREVIEW_COMPLETE | ready=" +
                                restoredReadyCount +
                                " | failed=" +
                                restoredFailedCount +
                                " | expected_ready=" + items.Count);
                            if (restoredReadyCount != items.Count ||
                                restoredFailedCount != 0)
                            {
                                failureMessage +=
                                    " | rollback restoration preview failed";
                            }
                            DisposeAnnotationGeometryItems(verificationItems);
                            verificationItems = null;
                        }
                    }

                    if (apply && failureMessage == null)
                    {
                        WriteApplyReceipt(
                            resultJsonPath,
                            items,
                            batchStart,
                            batchEndExclusive);
                    }

                    WriteAnnotationGeometryLog(
                        logPath,
                        startTime,
                        DateTime.Now,
                        resultJsonPath,
                        sourceDxfPath,
                        batch,
                        items,
                        underlinesMoved,
                        oldLeadersDeleted,
                        newLeadersCreated,
                        rollbackSuccess,
                        rollbackFailed,
                        failureMessage,
                        !apply);

                    if (failureMessage != null)
                    {
                        if (failureMessage.IndexOf(
                            "created leader layer verification failed",
                            StringComparison.Ordinal) == 0)
                        {
                            return "ERROR: created leader layer verification failed | " +
                                failureMessage.Substring(
                                    "created leader layer verification failed".Length).Trim() +
                                " | handle=" + failedHandle +
                                " | log=" + logPath;
                        }
                        return "ERROR: geometry apply failed | completed_before_error=" +
                            completed +
                            " | rollback_success=" + rollbackSuccess +
                            " | rollback_failed=" + rollbackFailed +
                            " | handle=" + failedHandle +
                            " | message=" + failureMessage +
                            " | log=" + logPath;
                    }

                    if (!apply)
                    {
                        if (!fullMode)
                        {
                            int nextPreview = batchEndExclusive >=
                                batch.MoveItems.Count
                                ? -1
                                : batchEndExclusive;
                            int remainingPreview = nextPreview < 0
                                ? 0
                                : batch.MoveItems.Count - nextPreview;
                            return "SUCCESS | stage=GEOMETRY_PREVIEW | start=" +
                                batchStart + " | end_exclusive=" + batchEndExclusive +
                                " | processed=" + items.Count +
                                " | ready=" + readyCount +
                                " | already_applied=" + alreadyCount +
                                " | failed=0 | next=" + nextPreview +
                                " | remaining=" + remainingPreview +
                                " | total=" + batch.MoveItems.Count +
                                " | inconclusive=0" +
                                " | elapsed_ms=0 | log=" + logPath;
                        }
                        return "SUCCESS | total=" + items.Count +
                            " | ready=" + readyCount +
                            " | already_applied=" + alreadyCount +
                            " | failed=0 | log=" + logPath;
                    }

                    if (!fullMode)
                    {
                        int nextApply = batchEndExclusive >=
                            batch.MoveItems.Count
                            ? -1
                            : batchEndExclusive;
                        int remainingApply = nextApply < 0
                            ? 0
                            : batch.MoveItems.Count - nextApply;
                        return "SUCCESS | stage=GEOMETRY_APPLY | start=" +
                            batchStart + " | end_exclusive=" + batchEndExclusive +
                            " | processed=" + items.Count +
                            " | ready_before_apply=" + readyCount +
                            " | underlines_moved=" + underlinesMoved +
                            " | old_leaders_deleted=" + oldLeadersDeleted +
                            " | old_leader_deletions_verified=" +
                            oldLeadersDeleted +
                            " | new_leaders_created=" + newLeadersCreated +
                            " | already_applied=" + alreadyCount +
                            " | failed=0 | rollback_success=" + rollbackSuccess +
                            " | rollback_failed=" + rollbackFailed +
                            " | next=" + nextApply +
                            " | remaining=" + remainingApply +
                            " | total=" + batch.MoveItems.Count +
                            " | elapsed_ms=0 | log=" + logPath;
                    }
                    return "SUCCESS | total=" + items.Count +
                        " | underlines_moved=" + underlinesMoved +
                        " | old_leaders_deleted=" + oldLeadersDeleted +
                        " | new_leaders_created=" + newLeadersCreated +
                        " | already_applied=" + alreadyCount +
                        " | failed=0 | log=" + logPath;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    WriteAnnotationGeometryLog(
                        logPath,
                        startTime,
                        DateTime.Now,
                        resultJsonPath,
                        sourceDxfPath,
                        batch,
                        items,
                        underlinesMoved,
                        oldLeadersDeleted,
                        newLeadersCreated,
                        rollbackSuccess,
                        rollbackFailed,
                        ex.Message,
                        !apply);
                }
                catch
                {
                }

                if (ex.Message.IndexOf(
                    "invalid batch parameters",
                    StringComparison.Ordinal) == 0)
                {
                    return "ERROR: " + ex.Message;
                }

                if (apply && ex.Message.IndexOf(
                    "old leader layer cannot be obtained",
                    StringComparison.Ordinal) == 0)
                {
                    return "ERROR: old leader layer cannot be obtained | " +
                        ex.Message.Substring(
                            "old leader layer cannot be obtained".Length).Trim() +
                        " | log=" + logPath;
                }
                if (apply && ex.Message.IndexOf(
                    "old leader layers are inconsistent",
                    StringComparison.Ordinal) == 0)
                {
                    return "ERROR: old leader layers are inconsistent | " +
                        ex.Message.Substring(
                            "old leader layers are inconsistent".Length).Trim() +
                        " | log=" + logPath;
                }

                return "ERROR: " + ex.GetType().FullName +
                    ": " + ex.Message;
            }
            finally
            {
                if (verificationItems != null)
                {
                    DisposeAnnotationGeometryItems(verificationItems);
                }
                DisposeAnnotationGeometryItems(items);
            }
        }

        private static void WritePreviewBuildHeader(
            string logPath,
            bool fullMode)
        {
            using (StreamWriter writer = new StreamWriter(
                logPath,
                false,
                new UTF8Encoding(false)))
            {
                writer.WriteLine("BUILD=PreviewApplyReceiptIdentityV7");
                writer.WriteLine("MATCHER=P25-P50-P75");
                writer.WriteLine("EXECUTION_MODE=" +
                    (fullMode ? "FULL" : "BATCH_SYNC"));
                writer.WriteLine("OLD_LEADER_IDENTITY_SOURCE=APPLY_RECEIPT");
                writer.WriteLine("FOREIGN_GEOMETRY_AWARE=true");
                writer.WriteLine("HANDLE_REUSE_AWARE=true");
                writer.Flush();
            }
        }

        private static void AppendApplyStep(
            string logPath,
            string step)
        {
            using (StreamWriter writer = new StreamWriter(
                logPath,
                !string.Equals(
                    step,
                    "STEP=METHOD_ENTER",
                    StringComparison.Ordinal),
                new UTF8Encoding(false)))
            {
                if (string.Equals(
                    step,
                    "STEP=METHOD_ENTER",
                    StringComparison.Ordinal))
                {
                    writer.WriteLine("BUILD=ApplyBatchSyncHandleReuseAwareV6");
                    writer.WriteLine("MATCHER=P25-P50-P75");
                    writer.WriteLine(
                        "LAYER_STRATEGY=INHERIT_OLD_LEADER");
                    writer.WriteLine(
                        "POST_VERIFY_STRATEGY=" +
                        "HANDLE_ID_GATED_TWO_PHASE_ABSENCE_CHECK");
                    writer.WriteLine("HANDLE_REUSE_AWARE=true");
                    writer.WriteLine("FOREIGN_GEOMETRY_AWARE=true");
                    writer.WriteLine("PML_RETURN_SINGLE_LINE=true");
                    writer.WriteLine(
                        "IMMEDIATE_VERIFY_MODE=TRISTATE_NON_THROWING");
                    writer.WriteLine("EXECUTION_MODE=BATCH_SYNC");
                }
                writer.WriteLine(step);
                writer.Flush();
            }
        }

        private static void ResolveBatchRange(
            double startIndexValue,
            double batchSizeValue,
            int total,
            out int start,
            out int endExclusive)
        {
            start = 0;
            endExclusive = 0;
            if (double.IsNaN(startIndexValue) ||
                double.IsInfinity(startIndexValue) ||
                startIndexValue != Math.Truncate(startIndexValue) ||
                startIndexValue < 0.0 ||
                startIndexValue > Int32.MaxValue)
            {
                throw new InvalidDataException(
                    "invalid batch parameters | field=startIndex | value=" +
                    startIndexValue + " | reason=must be a non-negative integer");
            }
            if (double.IsNaN(batchSizeValue) ||
                double.IsInfinity(batchSizeValue) ||
                batchSizeValue != Math.Truncate(batchSizeValue) ||
                batchSizeValue < 1.0 ||
                batchSizeValue > 50.0 ||
                batchSizeValue > Int32.MaxValue)
            {
                throw new InvalidDataException(
                    "invalid batch parameters | field=batchSize | value=" +
                    batchSizeValue + " | reason=must be an integer from 1 to 50");
            }
            start = (int)startIndexValue;
            int size = (int)batchSizeValue;
            if (start > Int32.MaxValue - size)
            {
                throw new InvalidDataException(
                    "invalid batch parameters | field=startIndex | value=" +
                    startIndexValue + " | reason=start plus batchSize overflows Int32");
            }
            endExclusive = Math.Min(start + size, total);
        }

        private static List<BatchMoveItem> CreateMoveBatch(
            List<BatchMoveItem> source,
            int start,
            int endExclusive)
        {
            List<BatchMoveItem> selected = new List<BatchMoveItem>();
            int index = start;
            while (index < endExclusive && index < source.Count)
            {
                int oldIndex = index;
                selected.Add(source[index]);
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            return selected;
        }

        private static void ValidateDuplicateMoveHandles(
            List<BatchMoveItem> moves)
        {
            Dictionary<string, BatchMoveItem> handles =
                new Dictionary<string, BatchMoveItem>(
                    StringComparer.OrdinalIgnoreCase);
            int index = 0;
            while (index < moves.Count)
            {
                int oldIndex = index;
                BatchMoveItem item = moves[index];
                if (handles.ContainsKey(item.Data.Handle))
                {
                    throw new InvalidDataException(
                        "duplicate move handle | handle=" + item.Data.Handle);
                }
                handles.Add(item.Data.Handle, item);
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
        }

        private static string BuildGeometryBatchLogPath(
            string resultJsonPath,
            bool apply,
            int start,
            int endExclusive,
            bool fullMode)
        {
            string suffix = apply
                ? ".geometry.apply"
                : ".geometry.preview";
            if (fullMode)
            {
                return (resultJsonPath ?? string.Empty) + suffix + ".log.txt";
            }
            return (resultJsonPath ?? string.Empty) + suffix + ".batch." +
                start.ToString("D4", CultureInfo.InvariantCulture) + "-" +
                endExclusive.ToString("D4", CultureInfo.InvariantCulture) +
                ".log.txt";
        }

        private static Dictionary<string, ApplyReceipt> LoadApplyReceipt(
            string resultJsonPath)
        {
            string receiptPath = resultJsonPath +
                ".geometry.apply.receipt.json";
            if (!File.Exists(receiptPath))
            {
                MigrateApplyReceiptFromLogs(resultJsonPath, receiptPath);
            }
            Dictionary<string, ApplyReceipt> result =
                new Dictionary<string, ApplyReceipt>(
                    StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(receiptPath))
            {
                return result;
            }
            string json = File.ReadAllText(receiptPath, new UTF8Encoding(false));
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            IDictionary<string, object> root =
                serializer.DeserializeObject(json) as IDictionary<string, object>;
            if (root == null)
            {
                return result;
            }
            IDictionary<string, object> records =
                root["records"] as IDictionary<string, object>;
            if (records == null)
            {
                return result;
            }
            foreach (KeyValuePair<string, object> pair in records)
            {
                IDictionary<string, object> value =
                    pair.Value as IDictionary<string, object>;
                if (value == null)
                {
                    continue;
                }
                ApplyReceipt receipt = new ApplyReceipt();
                receipt.AlgorithmHandle = pair.Key;
                receipt.OldLeaderHandleIdBeforeDelete =
                    GetReceiptInt(value, "old_leader_handle_id_before_delete");
                receipt.SourceUnderlineHandleId =
                    GetReceiptInt(value, "source_underline_handle_id");
                receipt.MovedUnderlineHandleId =
                    GetReceiptInt(value, "moved_underline_handle_id");
                receipt.CreatedNewLeaderHandleId =
                    GetReceiptInt(value, "created_new_leader_handle_id");
                receipt.OldLeaderLayerId =
                    GetReceiptInt(value, "old_leader_layer_id");
                receipt.CreatedNewLeaderLayerId =
                    GetReceiptInt(value, "created_new_leader_layer_id");
                receipt.ApplyCompleted =
                    GetReceiptBool(value, "apply_completed");
                if (receipt.ApplyCompleted)
                {
                    result.Add(receipt.AlgorithmHandle, receipt);
                }
            }
            return result;
        }

        private static int GetReceiptInt(
            IDictionary<string, object> value,
            string key)
        {
            object raw;
            if (!value.TryGetValue(key, out raw) || raw == null)
            {
                return int.MinValue;
            }
            int parsed;
            return int.TryParse(
                Convert.ToString(raw, CultureInfo.InvariantCulture),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed)
                ? parsed
                : int.MinValue;
        }

        private static bool GetReceiptBool(
            IDictionary<string, object> value,
            string key)
        {
            object raw;
            return value.TryGetValue(key, out raw) &&
                raw != null &&
                Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
        }

        private static void WriteApplyReceipt(
            string resultJsonPath,
            List<AlgorithmAnnotationGeometryItem> items,
            int batchStart,
            int batchEndExclusive)
        {
            string receiptPath = resultJsonPath +
                ".geometry.apply.receipt.json";
            Dictionary<string, object> records =
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ApplyReceipt> existing =
                LoadApplyReceipt(resultJsonPath);
            foreach (KeyValuePair<string, ApplyReceipt> pair in existing)
            {
                records[pair.Key] = ReceiptToDictionary(pair.Value);
            }
            int index = 0;
            while (index < items.Count)
            {
                int oldIndex = index;
                AlgorithmAnnotationGeometryItem item = items[index];
                if (item.RecordFullyApplied)
                {
                    ApplyReceipt receipt = new ApplyReceipt();
                    receipt.AlgorithmHandle = item.Move.Data.Handle;
                    receipt.OldLeaderHandleIdBeforeDelete =
                        item.OldLeaderHandleIdBeforeDelete;
                    receipt.SourceUnderlineHandleId =
                        item.OriginalUnderline == null
                            ? int.MinValue
                            : item.OriginalUnderline.handle;
                    receipt.MovedUnderlineHandleId =
                        receipt.SourceUnderlineHandleId;
                    receipt.CreatedNewLeaderHandleId =
                        item.CreatedNewLeaderHandleId;
                    receipt.OldLeaderLayerId = item.OldLeaderLayerId;
                    receipt.CreatedNewLeaderLayerId =
                        item.CreatedNewLeaderLayerId;
                    receipt.ApplyCompleted = true;
                    receipt.BatchStart = batchStart;
                    receipt.BatchEndExclusive = batchEndExclusive;
                    receipt.Build = "ApplyBatchSyncHandleReuseAwareV6";
                    receipt.Timestamp = DateTime.Now.ToString("o");
                    records[receipt.AlgorithmHandle] =
                        ReceiptToDictionary(receipt);
                }
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            Dictionary<string, object> root =
                new Dictionary<string, object>();
            root.Add("build", "ApplyBatchSyncHandleReuseAwareV6");
            root.Add("records", records);
            string tempPath = receiptPath + ".tmp";
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            File.WriteAllText(
                tempPath,
                serializer.Serialize(root),
                new UTF8Encoding(false));
            if (File.Exists(receiptPath))
            {
                File.Replace(tempPath, receiptPath, null);
            }
            else
            {
                File.Move(tempPath, receiptPath);
            }
        }

        private static Dictionary<string, object> ReceiptToDictionary(
            ApplyReceipt receipt)
        {
            Dictionary<string, object> value =
                new Dictionary<string, object>();
            value.Add("algorithm_handle", receipt.AlgorithmHandle);
            value.Add("old_leader_handle_id_before_delete",
                receipt.OldLeaderHandleIdBeforeDelete);
            value.Add("source_underline_handle_id",
                receipt.SourceUnderlineHandleId);
            value.Add("moved_underline_handle_id",
                receipt.MovedUnderlineHandleId);
            value.Add("created_new_leader_handle_id",
                receipt.CreatedNewLeaderHandleId);
            value.Add("old_leader_layer_id", receipt.OldLeaderLayerId);
            value.Add("created_new_leader_layer_id",
                receipt.CreatedNewLeaderLayerId);
            value.Add("apply_completed", receipt.ApplyCompleted);
            value.Add("batch_start", receipt.BatchStart);
            value.Add("batch_end_exclusive", receipt.BatchEndExclusive);
            value.Add("build", receipt.Build ?? string.Empty);
            value.Add("timestamp", receipt.Timestamp ?? string.Empty);
            return value;
        }

        private static void MigrateApplyReceiptFromLogs(
            string resultJsonPath,
            string receiptPath)
        {
            string directory = Path.GetDirectoryName(resultJsonPath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return;
            }
            string prefix = Path.GetFileName(resultJsonPath) +
                ".geometry.apply.batch.";
            string[] files = Directory.GetFiles(directory, prefix + "*.log.txt");
            Dictionary<string, object> records =
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            int fileIndex = 0;
            while (fileIndex < files.Length)
            {
                int oldFileIndex = fileIndex;
                string[] lines = File.ReadAllLines(files[fileIndex]);
                bool supported = false;
                bool success = false;
                bool clean = false;
                int lineIndex = 0;
                while (lineIndex < lines.Length)
                {
                    string line = lines[lineIndex];
                    if (line.IndexOf("BUILD=ApplyBatchSyncHandleReuseAwareV6", StringComparison.Ordinal) >= 0 ||
                        line.IndexOf("BUILD=ApplyBatchSyncHandleReuseAwareV5", StringComparison.Ordinal) >= 0 ||
                        line.IndexOf("BUILD=ApplyBatchSyncHandleReuseAwareV4", StringComparison.Ordinal) >= 0)
                    {
                        supported = true;
                    }
                    if (line.IndexOf("failed=0", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        success = true;
                    }
                    if (line.IndexOf("rollback_success=0", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        line.IndexOf("rollback_failed=0", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        clean = true;
                    }
                    lineIndex++;
                    EnsureParserIndexAdvanced(lineIndex - 1, lineIndex);
                }
                if (supported && success && clean)
                {
                    lineIndex = 0;
                    while (lineIndex < lines.Length)
                    {
                        string line = lines[lineIndex];
                        if (line.StartsWith("handle=", StringComparison.Ordinal))
                        {
                            string handle = ReadLogField(line, "handle");
                            int oldHandle = ReadLogInt(line, "old_leader_handle_id_before_delete");
                            int newHandle = ReadLogInt(line, "created_new_leader_handle_id");
                            if (!string.IsNullOrEmpty(handle) &&
                                oldHandle != int.MinValue &&
                                newHandle != int.MinValue)
                            {
                                ApplyReceipt receipt = new ApplyReceipt();
                                receipt.AlgorithmHandle = handle;
                                receipt.OldLeaderHandleIdBeforeDelete = oldHandle;
                                receipt.CreatedNewLeaderHandleId = newHandle;
                                receipt.ApplyCompleted = true;
                                receipt.Build = "ApplyBatchSyncHandleReuseAwareV6-MIGRATED";
                                records[handle] = ReceiptToDictionary(receipt);
                            }
                        }
                        lineIndex++;
                    }
                }
                fileIndex++;
                EnsureParserIndexAdvanced(oldFileIndex, fileIndex);
            }
            if (records.Count == 0)
            {
                return;
            }
            Dictionary<string, object> root = new Dictionary<string, object>();
            root.Add("build", "ApplyBatchSyncHandleReuseAwareV6-MIGRATED");
            root.Add("records", records);
            string tempPath = receiptPath + ".tmp";
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            File.WriteAllText(tempPath, serializer.Serialize(root), new UTF8Encoding(false));
            File.Move(tempPath, receiptPath);
        }

        private static string ReadLogField(string line, string field)
        {
            string prefix = field + "=";
            int start = line.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0)
            {
                return string.Empty;
            }
            start += prefix.Length;
            int end = line.IndexOf(" |", start, StringComparison.Ordinal);
            return (end < 0 ? line.Substring(start) : line.Substring(start, end - start)).Trim();
        }

        private static int ReadLogInt(string line, string field)
        {
            int value;
            return int.TryParse(
                ReadLogField(line, field),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value)
                ? value
                : int.MinValue;
        }

        private static int CaptureOldLeaderLayers(
            MarDrafting drafting,
            List<AlgorithmAnnotationGeometryItem> items,
            string logPath)
        {
            int readyCount = 0;
            int index = 0;
            int capturedLayerId = int.MinValue;
            bool hasLayerId = false;

            while (index < items.Count)
            {
                int oldIndex = index;
                AlgorithmAnnotationGeometryItem item = items[index];
                if (item.Status == "READY")
                {
                    readyCount++;
                    if (item.OldLeader == null)
                    {
                        item.ApplyStatus =
                            "FAILED: old leader layer cannot be obtained";
                        throw new InvalidDataException(
                            "old leader layer cannot be obtained | handle=" +
                            item.Move.Data.Handle +
                            " | old_leader_handle=");
                    }

                    AppendApplyStep(
                        logPath,
                        "STEP=OLD_LEADER_LAYER_GET START | handle=" +
                        item.Move.Data.Handle +
                        " | old_leader_handle=" +
                        item.OldLeader.handle);

                    MarLayer layer = null;
                    try
                    {
                        layer = drafting.ElementLayerGet(item.OldLeader);
                    }
                    catch (Exception ex)
                    {
                        item.ApplyStatus =
                            "FAILED: old leader layer cannot be obtained";
                        throw new InvalidDataException(
                            "old leader layer cannot be obtained | handle=" +
                            item.Move.Data.Handle +
                            " | old_leader_handle=" +
                            item.OldLeader.handle +
                            " | reason=" + ex.Message);
                    }
                    if (layer == null)
                    {
                        item.ApplyStatus =
                            "FAILED: old leader layer cannot be obtained";
                        throw new InvalidDataException(
                            "old leader layer cannot be obtained | handle=" +
                            item.Move.Data.Handle +
                            " | old_leader_handle=" +
                            item.OldLeader.handle);
                    }

                    item.OldLeaderLayer = layer;
                    try
                    {
                        item.OldLeaderLayerId = layer.LayerId;
                    }
                    catch (Exception ex)
                    {
                        item.ApplyStatus =
                            "FAILED: old leader layer cannot be obtained";
                        throw new InvalidDataException(
                            "old leader layer cannot be obtained | handle=" +
                            item.Move.Data.Handle +
                            " | old_leader_handle=" +
                            item.OldLeader.handle +
                            " | reason=" + ex.Message);
                    }
                    item.HasOldLeaderLayerId = true;
                    AppendApplyStep(
                        logPath,
                        "STEP=OLD_LEADER_LAYER_GET SUCCESS | handle=" +
                        item.Move.Data.Handle +
                        " | old_leader_handle=" +
                        item.OldLeader.handle +
                        " | old_leader_layer_id=" +
                        item.OldLeaderLayerId);

                    if (!hasLayerId)
                    {
                        capturedLayerId = item.OldLeaderLayerId;
                        hasLayerId = true;
                    }
                    else if (capturedLayerId != item.OldLeaderLayerId)
                    {
                        item.ApplyStatus =
                            "FAILED: old leader layers are inconsistent";
                        throw new InvalidDataException(
                            "old leader layers are inconsistent | expected_layer_id=" +
                            capturedLayerId +
                            " | actual_layer_id=" +
                            item.OldLeaderLayerId +
                            " | handle=" + item.Move.Data.Handle);
                    }
                }

                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }

            AppendApplyStep(
                logPath,
                "STEP=OLD_LEADER_LAYER_CAPTURE_COMPLETE | ready_count=" +
                readyCount +
                " | layer_id=" +
                (hasLayerId ? capturedLayerId.ToString() : ""));
            return readyCount;
        }

        [PMLNetCallable()]
        public string ApplyAllAlgorithmMoves(string resultJsonPath)
        {
            DateTime startTime = DateTime.Now;
            string logPath = (resultJsonPath ?? string.Empty) +
                ".apply.log.txt";
            BatchAlgorithmData batch = new BatchAlgorithmData();
            string firstFailedHandle = string.Empty;
            int movedCount = 0;
            int rollbackSuccess = 0;
            int rollbackFailed = 0;
            string executionError = null;

            try
            {
                LoadAllAlgorithmMoves(resultJsonPath, batch);

                Dictionary<string, BatchMoveItem> handles =
                    new Dictionary<string, BatchMoveItem>(
                        StringComparer.OrdinalIgnoreCase);
                int duplicateIndex = 0;

                while (duplicateIndex < batch.MoveItems.Count)
                {
                    int oldDuplicateIndex = duplicateIndex;
                    BatchMoveItem item = batch.MoveItems[duplicateIndex];

                    if (handles.ContainsKey(item.Data.Handle))
                    {
                        BatchMoveItem firstItem = handles[item.Data.Handle];
                        firstItem.PrecheckStatus = "FAILED_PRECHECK";
                        firstItem.Failure = "duplicate handle";
                        item.PrecheckStatus = "FAILED_PRECHECK";
                        item.Failure = "duplicate handle";
                    }
                    else
                    {
                        handles.Add(item.Data.Handle, item);
                    }

                    duplicateIndex++;
                    EnsureParserIndexAdvanced(
                        oldDuplicateIndex,
                        duplicateIndex);
                }

                using (MarDrafting drafting = new MarDrafting())
                {
                    if (!drafting.DwgCurrent())
                    {
                        throw new InvalidDataException(
                            "no current drawing");
                    }

                    int precheckIndex = 0;

                    while (precheckIndex < batch.MoveItems.Count)
                    {
                        int oldPrecheckIndex = precheckIndex;
                        BatchMoveItem item = batch.MoveItems[precheckIndex];

                        if (item.PrecheckStatus == "FAILED_PRECHECK")
                        {
                            precheckIndex++;
                            EnsureParserIndexAdvanced(
                                oldPrecheckIndex,
                                precheckIndex);
                            continue;
                        }

                        string originResult;
                        string expectedMovedResult = string.Empty;
                        string newCenterResult = string.Empty;
                        MarElementHandle handle;
                        MarText text;

                        if (TryFindMatchingAlgorithmText(
                            drafting,
                            item.Data.OriginX,
                            item.Data.OriginY,
                            item.Data.Text,
                            out handle,
                            out text,
                            out originResult))
                        {
                            item.Handle = handle;
                            item.TextObject = text;
                            item.PrecheckStatus = "READY";
                            item.OriginResult = originResult;
                        }
                        else
                        {
                            item.OriginResult = originResult;

                            double expectedMovedX =
                                item.Data.OriginX + item.Data.DeltaX;
                            double expectedMovedY =
                                item.Data.OriginY + item.Data.DeltaY;

                            if (TryFindMatchingAlgorithmText(
                                drafting,
                                expectedMovedX,
                                expectedMovedY,
                                item.Data.Text,
                                out handle,
                                out text,
                                out expectedMovedResult))
                            {
                                item.Handle = handle;
                                item.TextObject = text;
                                item.PrecheckStatus = "ALREADY_APPLIED";
                                item.ExpectedMovedResult = expectedMovedResult;
                            }
                            else if (TryFindMatchingAlgorithmText(
                                drafting,
                                item.Data.NewX,
                                item.Data.NewY,
                                item.Data.Text,
                                out handle,
                                out text,
                                out newCenterResult))
                            {
                                item.Handle = handle;
                                item.TextObject = text;
                                item.PrecheckStatus = "ALREADY_APPLIED";
                                item.NewCenterResult = newCenterResult;
                            }
                            else
                            {
                                item.PrecheckStatus = "FAILED_PRECHECK";
                                item.ExpectedMovedResult =
                                    expectedMovedResult;
                                item.NewCenterResult = newCenterResult;
                                item.Failure = "text not found at all candidate positions";
                            }
                        }

                        precheckIndex++;
                        EnsureParserIndexAdvanced(
                            oldPrecheckIndex,
                            precheckIndex);
                    }

                    int failedCount = CountBatchStatus(
                        batch.MoveItems,
                        "FAILED_PRECHECK");
                    int readyCount = CountBatchStatus(
                        batch.MoveItems,
                        "READY");
                    int alreadyAppliedCount = CountBatchStatus(
                        batch.MoveItems,
                        "ALREADY_APPLIED");

                    if (failedCount > 0)
                    {
                        firstFailedHandle = FindFirstFailedHandle(
                            batch.MoveItems);
                    }
                    else
                    {
                        List<BatchMoveItem> movedItems =
                            new List<BatchMoveItem>();
                        int executeIndex = 0;

                        while (executeIndex < batch.MoveItems.Count)
                        {
                            int oldExecuteIndex = executeIndex;
                            BatchMoveItem item =
                                batch.MoveItems[executeIndex];

                            if (item.PrecheckStatus == "READY")
                            {
                                try
                                {
                                    ApplySavedAlgorithmMove(
                                        drafting,
                                        item.Handle,
                                        item.Data.DeltaX,
                                        item.Data.DeltaY);
                                    item.ApplyStatus = "MOVED";
                                    movedItems.Add(item);
                                    movedCount++;
                                }
                                catch (Exception ex)
                                {
                                    item.ApplyStatus = "FAILED";
                                    item.Failure = ex.Message;
                                    executionError = ex.Message;

                                    int rollbackIndex =
                                        movedItems.Count - 1;

                                    while (rollbackIndex >= 0)
                                    {
                                        BatchMoveItem rollbackItem =
                                            movedItems[rollbackIndex];

                                        try
                                        {
                                            ApplySavedAlgorithmMove(
                                                drafting,
                                                rollbackItem.Handle,
                                                -rollbackItem.Data.DeltaX,
                                                -rollbackItem.Data.DeltaY);
                                            rollbackItem.RollbackStatus =
                                                "SUCCESS";
                                            rollbackSuccess++;
                                        }
                                        catch (Exception rollbackException)
                                        {
                                            rollbackItem.RollbackStatus =
                                                "FAILED: " +
                                                rollbackException.Message;
                                            rollbackFailed++;
                                        }

                                        rollbackIndex--;
                                    }

                                    break;
                                }
                            }

                            executeIndex++;
                            EnsureParserIndexAdvanced(
                                oldExecuteIndex,
                                executeIndex);
                        }
                    }

                    int finalFailedCount = CountBatchStatus(
                        batch.MoveItems,
                        "FAILED_PRECHECK");

                    WriteApplyAllMovesLog(
                        logPath,
                        startTime,
                        DateTime.Now,
                        resultJsonPath,
                        batch,
                        movedCount,
                        rollbackSuccess,
                        rollbackFailed,
                        executionError);

                    if (finalFailedCount > 0)
                    {
                        return "ERROR: precheck failed | total=" +
                            batch.MoveItems.Count +
                            " | ready=" +
                            CountBatchStatus(batch.MoveItems, "READY") +
                            " | already_applied=" +
                            CountBatchStatus(batch.MoveItems, "ALREADY_APPLIED") +
                            " | failed=" + finalFailedCount +
                            " | first_failed_handle=" +
                            firstFailedHandle +
                            " | log=" + logPath;
                    }

                    if (executionError != null)
                    {
                        string failedHandle =
                            FindFirstApplyFailedHandle(batch.MoveItems);
                        return "ERROR: batch move failed | moved_before_error=" +
                            movedCount +
                            " | rollback_success=" +
                            rollbackSuccess +
                            " | rollback_failed=" +
                            rollbackFailed +
                            " | handle=" + failedHandle +
                            " | message=" + executionError;
                    }

                    return "SUCCESS | total=" + batch.MoveItems.Count +
                        " | moved=" + movedCount +
                        " | already_applied=" + alreadyAppliedCount +
                        " | skipped=" + batch.SkippedCount +
                        " | failed=0 | log=" + logPath;
                }
            }
            catch (Exception ex)
            {
                WriteApplyAllMovesLog(
                    logPath,
                    startTime,
                    DateTime.Now,
                    resultJsonPath,
                    batch,
                    movedCount,
                    rollbackSuccess,
                    rollbackFailed,
                    ex.Message);
                return "ERROR: " +
                    ex.GetType().FullName + ": " +
                    ex.Message;
            }
            finally
            {
                DisposeBatchMoveItems(batch.AllItems);
            }
        }

        private static string ProcessAlgorithmMovesRange(
            string resultJsonPath,
            double startIndexValue,
            double batchSizeValue,
            bool fullMode)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            BatchAlgorithmData batch = new BatchAlgorithmData();
            int start = 0;
            int endExclusive = 0;
            int moved = 0;
            int alreadyApplied = 0;
            int rollbackSuccess = 0;
            int rollbackFailed = 0;
            string failedHandle = string.Empty;
            string error = null;
            try
            {
                LoadAllAlgorithmMoves(resultJsonPath, batch);
                ValidateDuplicateMoveHandles(batch.MoveItems);
                if (fullMode)
                {
                    endExclusive = batch.MoveItems.Count;
                }
                else
                {
                    ResolveBatchRange(
                        startIndexValue,
                        batchSizeValue,
                        batch.MoveItems.Count,
                        out start,
                        out endExclusive);
                }
                List<BatchMoveItem> selected = CreateMoveBatch(
                    batch.MoveItems,
                    start,
                    endExclusive);
                string logPath = (resultJsonPath ?? string.Empty) +
                    ".move.batch." +
                    start.ToString("D4", CultureInfo.InvariantCulture) +
                    "-" +
                    endExclusive.ToString("D4", CultureInfo.InvariantCulture) +
                    ".log.txt";
                WriteTextMoveBatchHeader(
                    logPath,
                    start,
                    endExclusive,
                    batch.MoveItems.Count,
                    fullMode);

                using (MarDrafting drafting = new MarDrafting())
                {
                    if (!drafting.DwgCurrent())
                    {
                        throw new InvalidDataException("no current drawing");
                    }
                    int index = 0;
                    while (index < selected.Count)
                    {
                        int oldIndex = index;
                        BatchMoveItem item = selected[index];
                        string originResult;
                        string movedResult;
                        string newCenterResult;
                        MarElementHandle handle;
                        MarText text;
                        if (TryFindMatchingAlgorithmText(
                            drafting,
                            item.Data.OriginX,
                            item.Data.OriginY,
                            item.Data.Text,
                            out handle,
                            out text,
                            out originResult))
                        {
                            item.Handle = handle;
                            item.TextObject = text;
                            item.PrecheckStatus = "READY";
                            item.OriginResult = originResult;
                        }
                        else if (TryFindMatchingAlgorithmText(
                            drafting,
                            item.Data.OriginX + item.Data.DeltaX,
                            item.Data.OriginY + item.Data.DeltaY,
                            item.Data.Text,
                            out handle,
                            out text,
                            out movedResult))
                        {
                            item.Handle = handle;
                            item.TextObject = text;
                            item.PrecheckStatus = "ALREADY_APPLIED";
                            item.ExpectedMovedResult = movedResult;
                        }
                        else if (TryFindMatchingAlgorithmText(
                            drafting,
                            item.Data.NewX,
                            item.Data.NewY,
                            item.Data.Text,
                            out handle,
                            out text,
                            out newCenterResult))
                        {
                            item.Handle = handle;
                            item.TextObject = text;
                            item.PrecheckStatus = "ALREADY_APPLIED";
                            item.NewCenterResult = newCenterResult;
                        }
                        else
                        {
                            item.PrecheckStatus = "FAILED_PRECHECK";
                            item.Failure =
                                "text not found at all candidate positions";
                            item.OriginResult = originResult;
                            item.ExpectedMovedResult = movedResult;
                            item.NewCenterResult = newCenterResult;
                        }
                        index++;
                        EnsureParserIndexAdvanced(oldIndex, index);
                    }

                    if (CountBatchStatus(selected, "FAILED_PRECHECK") > 0)
                    {
                        failedHandle = FindFirstFailedHandle(selected);
                        error = "text move precheck failed";
                    }
                    else
                    {
                        List<BatchMoveItem> movedItems =
                            new List<BatchMoveItem>();
                        index = 0;
                        while (index < selected.Count)
                        {
                            int oldIndex = index;
                            BatchMoveItem item = selected[index];
                            if (item.PrecheckStatus == "READY")
                            {
                                try
                                {
                                    ApplySavedAlgorithmMove(
                                        drafting,
                                        item.Handle,
                                        item.Data.DeltaX,
                                        item.Data.DeltaY);
                                    item.ApplyStatus = "MOVED";
                                    movedItems.Add(item);
                                    moved++;
                                }
                                catch (Exception ex)
                                {
                                    item.ApplyStatus = "FAILED";
                                    item.Failure = ex.Message;
                                    error = ex.Message;
                                    failedHandle = item.Data.Handle;
                                    int rollbackIndex = movedItems.Count - 1;
                                    while (rollbackIndex >= 0)
                                    {
                                        BatchMoveItem rollbackItem =
                                            movedItems[rollbackIndex];
                                        try
                                        {
                                            ApplySavedAlgorithmMove(
                                                drafting,
                                                rollbackItem.Handle,
                                                -rollbackItem.Data.DeltaX,
                                                -rollbackItem.Data.DeltaY);
                                            rollbackSuccess++;
                                        }
                                        catch
                                        {
                                            rollbackFailed++;
                                        }
                                        rollbackIndex--;
                                    }
                                    break;
                                }
                            }
                            index++;
                            EnsureParserIndexAdvanced(oldIndex, index);
                        }
                    }
                    alreadyApplied = CountBatchStatus(
                        selected,
                        "ALREADY_APPLIED");
                }

                WriteTextMoveBatchSummary(
                    logPath,
                    stopwatch.ElapsedMilliseconds,
                    selected,
                    moved,
                    alreadyApplied,
                    rollbackSuccess,
                    rollbackFailed,
                    error);
                if (error != null)
                {
                    return "ERROR: text move batch failed | stage=TEXT_MOVE" +
                        " | start=" + start +
                        " | end_exclusive=" + endExclusive +
                        " | processed=" + (endExclusive - start) +
                        " | first_failed_handle=" + failedHandle +
                        " | rollback_success=" + rollbackSuccess +
                        " | rollback_failed=" + rollbackFailed +
                        " | next=-1 | remaining=0 | total=" +
                        batch.MoveItems.Count + " | log=" + logPath;
                }
                int next = endExclusive >= batch.MoveItems.Count
                    ? -1
                    : endExclusive;
                int remaining = next < 0
                    ? 0
                    : batch.MoveItems.Count - next;
                return "SUCCESS | stage=TEXT_MOVE | start=" + start +
                    " | end_exclusive=" + endExclusive +
                    " | processed=" + (endExclusive - start) +
                    " | moved=" + moved +
                    " | already_applied=" + alreadyApplied +
                    " | failed=0 | next=" + next +
                    " | remaining=" + remaining +
                    " | total=" + batch.MoveItems.Count +
                    " | skipped_total=" + batch.SkippedCount +
                    " | elapsed_ms=" + stopwatch.ElapsedMilliseconds +
                    " | log=" + logPath;
            }
            catch (Exception ex)
            {
                if (ex.Message.IndexOf(
                    "invalid batch parameters",
                    StringComparison.Ordinal) == 0)
                {
                    return "ERROR: " + ex.Message;
                }
                return "ERROR: " + ex.GetType().FullName +
                    ": " + ex.Message;
            }
            finally
            {
                DisposeBatchMoveItems(batch.AllItems);
            }
        }

        private static void WriteTextMoveBatchHeader(
            string logPath,
            int start,
            int endExclusive,
            int total,
            bool fullMode)
        {
            using (StreamWriter writer = new StreamWriter(
                logPath,
                false,
                new UTF8Encoding(false)))
            {
                writer.WriteLine("BUILD=TextMoveBatchSyncV1");
                writer.WriteLine("EXECUTION_MODE=" +
                    (fullMode ? "FULL" : "BATCH_SYNC"));
                writer.WriteLine("STAGE=TEXT_MOVE");
                writer.WriteLine("BATCH_START=" + start);
                writer.WriteLine("BATCH_END_EXCLUSIVE=" + endExclusive);
                writer.WriteLine("TOTAL_MOVE_ITEMS=" + total);
                writer.WriteLine("NEXT_INDEX=" + endExclusive);
                writer.WriteLine("REMAINING=0");
                writer.Flush();
            }
        }

        private static void WriteTextMoveBatchSummary(
            string logPath,
            long elapsedMilliseconds,
            List<BatchMoveItem> selected,
            int moved,
            int alreadyApplied,
            int rollbackSuccess,
            int rollbackFailed,
            string error)
        {
            using (StreamWriter writer = new StreamWriter(
                logPath,
                true,
                new UTF8Encoding(false)))
            {
                writer.WriteLine("BATCH_PROCESSED=" + selected.Count);
                writer.WriteLine("NEXT_INDEX=runtime");
                writer.WriteLine("REMAINING=runtime");
                writer.WriteLine("MOVED=" + moved);
                writer.WriteLine("ALREADY_APPLIED=" + alreadyApplied);
                writer.WriteLine("ROLLBACK_SUCCESS=" + rollbackSuccess);
                writer.WriteLine("ROLLBACK_FAILED=" + rollbackFailed);
                writer.WriteLine("ELAPSED_MS=" + elapsedMilliseconds);
                writer.WriteLine("ERROR=" + (error ?? string.Empty));
                writer.WriteLine("--- SELECTED_MOVE_PRECHECKS ---");
                int index = 0;
                while (index < selected.Count)
                {
                    int oldIndex = index;
                    BatchMoveItem item = selected[index];
                    writer.WriteLine(
                        "handle=" + (item.Data.Handle ?? string.Empty) +
                        " | text=" + (item.Data.Text ?? string.Empty) +
                        " | origin=" + item.Data.OriginX + "," + item.Data.OriginY +
                        " | expected_moved=" +
                        (item.Data.OriginX + item.Data.DeltaX) + "," +
                        (item.Data.OriginY + item.Data.DeltaY) +
                        " | new_center=" + item.Data.NewX + "," + item.Data.NewY +
                        " | precheck=" + (item.PrecheckStatus ?? string.Empty) +
                        " | apply=" + (item.ApplyStatus ?? string.Empty) +
                        " | failure=" + (item.Failure ?? string.Empty));
                    writer.WriteLine("  origin_probe_results=" +
                        (item.OriginResult ?? string.Empty));
                    writer.WriteLine("  expected_moved_probe_results=" +
                        (item.ExpectedMovedResult ?? string.Empty));
                    writer.WriteLine("  new_center_probe_results=" +
                        (item.NewCenterResult ?? string.Empty));
                    writer.WriteLine("  rollback=" +
                        (item.RollbackStatus ?? string.Empty));
                    index++;
                    EnsureParserIndexAdvanced(oldIndex, index);
                }
                writer.Flush();
            }
        }

        private static void LoadAllAlgorithmMoves(
            string resultJsonPath,
            BatchAlgorithmData batch)
        {
            if (string.IsNullOrEmpty(resultJsonPath))
            {
                throw new InvalidDataException("resultJsonPath is empty");
            }

            if (!File.Exists(resultJsonPath))
            {
                throw new FileNotFoundException(
                    "result JSON file not found",
                    resultJsonPath);
            }

            string json = File.ReadAllText(
                resultJsonPath,
                new UTF8Encoding(false));
            JavaScriptSerializer serializer =
                new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;

            object parsed = serializer.DeserializeObject(json);
            IDictionary<string, object> root =
                parsed as IDictionary<string, object>;

            if (root == null)
            {
                throw new InvalidDataException("JSON root is invalid");
            }

            IList<object> results = GetObjectList(root, "results");

            if (results == null)
            {
                throw new InvalidDataException("results is missing");
            }

            int resultIndex = 0;
            while (resultIndex < results.Count)
            {
                int oldResultIndex = resultIndex;
                IDictionary<string, object> resultObject =
                    results[resultIndex] as IDictionary<string, object>;

                if (resultObject == null)
                {
                    throw new InvalidDataException("result is invalid");
                }

                IList<object> layouts =
                    GetObjectList(resultObject, "layout");

                if (layouts == null)
                {
                    throw new InvalidDataException("layout is missing");
                }

                int layoutIndex = 0;
                while (layoutIndex < layouts.Count)
                {
                    int oldLayoutIndex = layoutIndex;
                    IDictionary<string, object> layoutObject =
                        layouts[layoutIndex] as IDictionary<string, object>;

                    if (layoutObject == null)
                    {
                        throw new InvalidDataException("layout is invalid");
                    }

                    IList<object> moves =
                        GetObjectList(layoutObject, "moves");

                    if (moves == null)
                    {
                        throw new InvalidDataException("moves is missing");
                    }

                    int moveIndex = 0;
                    while (moveIndex < moves.Count)
                    {
                        int oldMoveIndex = moveIndex;
                        IDictionary<string, object> moveObject =
                            moves[moveIndex] as IDictionary<string, object>;

                        if (moveObject == null)
                        {
                            throw new InvalidDataException("move is invalid");
                        }

                        bool isMove;
                        if (!TryGetBoolean(
                            moveObject,
                            "is_move",
                            out isMove))
                        {
                            throw new InvalidDataException(
                                "is_move is missing or invalid");
                        }

                        batch.JsonRecordCount++;

                        if (isMove)
                        {
                            AlgorithmMoveData data =
                                ParseAlgorithmMove(moveObject);
                            data.Layer = GetRequiredString(
                                moveObject,
                                "layer");
                            data.NeedsPlan = GetRequiredBoolean(
                                moveObject,
                                "needs_plan");

                            BatchMoveItem item =
                                new BatchMoveItem();
                            item.Data = data;
                            item.PrecheckStatus = "PENDING";
                            item.ApplyStatus = "NOT_STARTED";
                            batch.MoveItems.Add(item);
                            batch.AllItems.Add(item);
                        }
                        else
                        {
                            BatchMoveItem skipped =
                                new BatchMoveItem();
                            skipped.Data =
                                CreateOptionalAlgorithmMove(moveObject);
                            skipped.PrecheckStatus = "SKIPPED";
                            skipped.ApplyStatus = "SKIPPED";
                            batch.SkippedCount++;
                            batch.AllItems.Add(skipped);
                        }

                        moveIndex++;
                        EnsureParserIndexAdvanced(
                            oldMoveIndex,
                            moveIndex);
                    }

                    layoutIndex++;
                    EnsureParserIndexAdvanced(
                        oldLayoutIndex,
                        layoutIndex);
                }

                resultIndex++;
                EnsureParserIndexAdvanced(
                    oldResultIndex,
                    resultIndex);
            }
        }

        private const double GeometryTolerance = 0.05;
        private const double EndpointTolerance = 0.10;
        private const double MidpointTolerance = 0.10;
        private const double LengthTolerance = 0.10;

        private static void LoadSourceGeometryAssociations(
            string sourceDxfPath,
            List<BatchMoveItem> moves,
            List<AlgorithmAnnotationGeometryItem> items)
        {
            if (string.IsNullOrEmpty(sourceDxfPath))
            {
                throw new InvalidDataException("sourceDxfPath is empty");
            }
            if (!File.Exists(sourceDxfPath))
            {
                throw new FileNotFoundException(
                    "source DXF file not found",
                    sourceDxfPath);
            }

            byte[] data = File.ReadAllBytes(sourceDxfPath);
            DxfFullScanResult scan = ScanAllDxfEntities(
                data,
                Encoding.Default);

            int moveIndex = 0;
            while (moveIndex < moves.Count)
            {
                int oldMoveIndex = moveIndex;
                BatchMoveItem move = moves[moveIndex];
                AlgorithmAnnotationGeometryItem item =
                    new AlgorithmAnnotationGeometryItem();
                item.Move = move;

                int textIndex = -1;
                int textMatches = 0;
                int entityIndex = 0;
                while (entityIndex < scan.Entities.Count)
                {
                    int oldEntityIndex = entityIndex;
                    DxfEntityInfo entity = scan.Entities[entityIndex];
                    if (string.Equals(
                            entity.EntityType,
                            "TEXT",
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            entity.Handle,
                            move.Data.Handle,
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            entity.TextValue,
                            move.Data.Text,
                            StringComparison.Ordinal))
                    {
                        textIndex = entityIndex;
                        textMatches++;
                    }
                    entityIndex++;
                    EnsureParserIndexAdvanced(
                        oldEntityIndex,
                        entityIndex);
                }

                if (textMatches != 1)
                {
                    item.Failure = "source TEXT match count=" + textMatches;
                    item.Status = "FAILED_PRECHECK";
                    items.Add(item);
                    moveIndex++;
                    EnsureParserIndexAdvanced(oldMoveIndex, moveIndex);
                    continue;
                }

                item.SourceText = scan.Entities[textIndex];
                int relatedIndex = textIndex + 1;
                while (relatedIndex < scan.Entities.Count)
                {
                    int oldRelatedIndex = relatedIndex;
                    DxfEntityInfo related = scan.Entities[relatedIndex];
                    if (string.Equals(
                        related.EntityType,
                        "TEXT",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (string.Equals(
                            related.LayerName,
                            "-11",
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            related.EntityType,
                            "LWPOLYLINE",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (item.SourceUnderline != null)
                        {
                            item.Failure =
                                "multiple source -11 LWPOLYLINE entities";
                        }
                        else
                        {
                            item.SourceUnderline = related;
                        }
                    }
                    else if (string.Equals(
                            related.LayerName,
                            "-11",
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            related.EntityType,
                            "LINE",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (item.SourceOldLeader != null)
                        {
                            item.Failure =
                                "multiple source -11 LINE entities";
                        }
                        else
                        {
                            item.SourceOldLeader = related;
                        }
                    }

                    relatedIndex++;
                    EnsureParserIndexAdvanced(
                        oldRelatedIndex,
                        relatedIndex);
                }

                if (item.SourceUnderline == null)
                {
                    item.Failure = "source -11 LWPOLYLINE not found";
                }
                else if (item.SourceUnderline.Vertices == null ||
                    item.SourceUnderline.Vertices.Count < 2)
                {
                    item.Failure =
                        "source -11 LWPOLYLINE has insufficient vertices";
                }
                else if (item.SourceOldLeader == null)
                {
                    item.Failure = "source -11 LINE not found";
                }
                else if (item.SourceOldLeader.LineStart == null ||
                    item.SourceOldLeader.LineEnd == null)
                {
                    item.Failure =
                        "source -11 LINE has incomplete endpoints";
                }

                items.Add(item);
                moveIndex++;
                EnsureParserIndexAdvanced(oldMoveIndex, moveIndex);
            }
        }

        private static void PrecheckAnnotationGeometry(
            MarDrafting drafting,
            List<AlgorithmAnnotationGeometryItem> items,
            bool useInteriorProbeMatching,
            bool usePerItemLeaderLayer)
        {
            int index = 0;
            while (index < items.Count)
            {
                int oldIndex = index;
                AlgorithmAnnotationGeometryItem item = items[index];
                if (item.Status == "FAILED_PRECHECK")
                {
                    index++;
                    EnsureParserIndexAdvanced(oldIndex, index);
                    continue;
                }

                MarElementHandle textHandle;
                MarText textObject;
                string textResult;
                if (TryFindMatchingAlgorithmText(
                    drafting,
                    item.Move.Data.NewX,
                    item.Move.Data.NewY,
                    item.Move.Data.Text,
                    out textHandle,
                    out textObject,
                    out textResult))
                {
                    item.Move.Handle = textHandle;
                    item.Move.TextObject = textObject;
                    item.TextStatus = "TEXT_AT_NEW_CENTER";
                    item.TextMatchDetail = textResult;
                }
                else
                {
                    item.TextStatus = textResult;
                    item.TextMatchDetail = textResult;
                    MarElementHandle originHandle;
                    MarText originText;
                    string originResult;
                    if (TryFindMatchingAlgorithmText(
                        drafting,
                        item.Move.Data.OriginX,
                        item.Move.Data.OriginY,
                        item.Move.Data.Text,
                        out originHandle,
                        out originText,
                        out originResult))
                    {
                        originText.Dispose();
                        originHandle.Dispose();
                        item.Status = "FAILED_PRECHECK";
                        item.Failure = "FAILED_TEXT_NOT_MOVED";
                        index++;
                        EnsureParserIndexAdvanced(oldIndex, index);
                        continue;
                    }

                    item.Status = "FAILED_PRECHECK";
                    item.Failure =
                        "text not found at new_center; origin=" +
                        originResult;
                    index++;
                    EnsureParserIndexAdvanced(oldIndex, index);
                    continue;
                }

                DxfEntityInfo underline = item.SourceUnderline;
                List<DxfPoint> originalPoints =
                    NormalizeUnderlinePoints(underline.Vertices);
                List<DxfPoint> movedPoints = TranslateDxfPoints(
                    originalPoints,
                    item.Move.Data.DeltaX,
                    item.Move.Data.DeltaY);
                if (useInteriorProbeMatching)
                {
                    item.OriginalUnderline =
                        TryMatchGeometryByInteriorProbes(
                            drafting,
                            underline.Vertices,
                            -11,
                            false,
                            null,
                            null,
                            out item.OriginalUnderlineDetail);
                    item.MovedUnderline =
                        TryMatchGeometryByInteriorProbes(
                            drafting,
                            TranslateDxfPoints(
                                underline.Vertices,
                                item.Move.Data.DeltaX,
                                item.Move.Data.DeltaY),
                            -11,
                            false,
                            null,
                            null,
                            out item.MovedUnderlineDetail);
                }
                else
                {
                    item.OriginalUnderline = TryMatchGeometry(
                        drafting,
                        originalPoints,
                        string.Empty,
                        out item.OriginalUnderlineDetail);
                    item.MovedUnderline = TryMatchGeometry(
                        drafting,
                        movedPoints,
                        string.Empty,
                        out item.MovedUnderlineDetail);
                }
                item.UnderlineStatus =
                    item.OriginalUnderline != null
                        ? "UNDERLINE_ORIGINAL"
                        : (item.MovedUnderline != null
                            ? "UNDERLINE_MOVED"
                            : "UNDERLINE_NOT_FOUND");

                DxfEntityInfo oldLeader = item.SourceOldLeader;
                List<DxfPoint> oldLeaderPoints = CreateLinePoints(
                    oldLeader.LineStart,
                    oldLeader.LineEnd);
                List<DxfPoint> newLeaderPoints = CreateLinePoints(
                    new DxfPoint(
                        item.Move.Data.LeaderStart[0],
                        item.Move.Data.LeaderStart[1]),
                    new DxfPoint(
                        item.Move.Data.LeaderEnd[0],
                        item.Move.Data.LeaderEnd[1]));
                if (useInteriorProbeMatching)
                {
                    item.OldLeader =
                        TryMatchGeometryByInteriorProbes(
                            drafting,
                            oldLeaderPoints,
                            -11,
                            false,
                            null,
                            null,
                            out item.OldLeaderDetail);
                    if (item.OldLeader != null)
                    {
                        item.OldLeaderLayerId =
                            GetElementLayerId(drafting, item.OldLeader);
                        item.HasOldLeaderLayerId = true;
                    }
                    int expectedNewLeaderLayerId = -1;
                    bool allowUnresolvedNewLeaderLayer = true;
                    if (usePerItemLeaderLayer &&
                        item.HasOldLeaderLayerId)
                    {
                        expectedNewLeaderLayerId = item.OldLeaderLayerId;
                        allowUnresolvedNewLeaderLayer = false;
                    }
                    item.NewLeader =
                        TryMatchGeometryByInteriorProbes(
                            drafting,
                            newLeaderPoints,
                            expectedNewLeaderLayerId,
                            allowUnresolvedNewLeaderLayer,
                            item.OriginalUnderline,
                            item.OldLeader,
                            out item.NewLeaderDetail);
                }
                else
                {
                    item.OldLeader = TryMatchGeometry(
                        drafting,
                        oldLeaderPoints,
                        string.Empty,
                        out item.OldLeaderDetail);
                    item.NewLeader = TryMatchGeometry(
                        drafting,
                        newLeaderPoints,
                        string.Empty,
                        out item.NewLeaderDetail);
                }
                item.OldLeaderStatus = item.OldLeader != null
                    ? "OLD_LEADER_PRESENT"
                    : "OLD_LEADER_ABSENT";
                item.NewLeaderStatus = item.NewLeader != null
                    ? "NEW_LEADER_PRESENT"
                    : "NEW_LEADER_ABSENT";
                if (item.OldLeader != null)
                {
                    if (!usePerItemLeaderLayer ||
                        !item.HasOldLeaderLayerId)
                    {
                        item.OldLeaderLayerId =
                            GetElementLayerId(drafting, item.OldLeader);
                        item.HasOldLeaderLayerId = true;
                    }
                    item.OldLeaderColour =
                        drafting.ElementColourGet(item.OldLeader);
                    item.OldLeaderLinetype =
                        drafting.ElementLinetypeGet(item.OldLeader);
                }
                if (item.NewLeader != null)
                {
                    item.CreatedNewLeaderLayerId =
                        GetElementLayerId(drafting, item.NewLeader);
                }
                if (item.OriginalUnderline != null &&
                    item.OldLeader != null &&
                    item.NewLeader == null)
                {
                    item.Status = "READY";
                }
                else if (item.MovedUnderline != null &&
                    item.OldLeader == null &&
                    item.NewLeader != null)
                {
                    item.Status = "ALREADY_APPLIED";
                }
                else
                {
                    item.Status = "FAILED_PRECHECK";
                    item.Failure = "geometry state combination is invalid";
                }

                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
        }

        private static int ApplyReceiptIdentityToPreview(
            List<AlgorithmAnnotationGeometryItem> items,
            Dictionary<string, ApplyReceipt> receipts,
            string logPath)
        {
            int inconclusive = 0;
            int index = 0;
            while (index < items.Count)
            {
                int oldIndex = index;
                AlgorithmAnnotationGeometryItem item = items[index];
                ApplyReceipt receipt;
                bool found = receipts.TryGetValue(
                    item.Move.Data.Handle,
                    out receipt);
                item.ApplyReceiptFound = found;
                if (found)
                {
                    item.ExpectedDeletedOldLeaderHandleId =
                        receipt.OldLeaderHandleIdBeforeDelete;
                    item.ReceiptCreatedNewLeaderHandleId =
                        receipt.CreatedNewLeaderHandleId;
                    item.ReceiptMovedUnderlineHandleId =
                        receipt.MovedUnderlineHandleId;
                    item.PreviewIdentityStatus = "RECEIPT";
                    if (item.Status == "FAILED_PRECHECK" &&
                        item.TextStatus == "TEXT_AT_NEW_CENTER" &&
                        item.MovedUnderline != null &&
                        item.NewLeader != null)
                    {
                        item.OldLeader = null;
                        item.OldLeaderStatus =
                            "OLD_LEADER_ABSENT_RECEIPT";
                        item.OldLeaderDetail =
                            "identity_gate=FOREIGN_GEOMETRY_OR_ABSENT";
                        item.Status = "ALREADY_APPLIED";
                        item.Failure = null;
                    }
                }
                else if (item.Status == "FAILED_PRECHECK" &&
                    item.TextStatus == "TEXT_AT_NEW_CENTER" &&
                    item.MovedUnderline != null &&
                    item.NewLeader != null)
                {
                    item.Status = "INCONCLUSIVE_PREVIEW";
                    item.PreviewIdentityStatus =
                        "missing apply identity receipt";
                    item.OldLeaderStatus =
                        "OLD_LEADER_IDENTITY_UNRESOLVED";
                    item.Failure = "missing apply identity receipt";
                    inconclusive++;
                }
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            AppendApplyStep(
                logPath,
                "PREVIEW_APPLY_RECEIPT_INCONCLUSIVE=" + inconclusive);
            return inconclusive;
        }

        private static List<BatchMoveItem> CreateVerificationMoves(
            List<BatchMoveItem> sourceMoves)
        {
            List<BatchMoveItem> verificationMoves =
                new List<BatchMoveItem>();
            int index = 0;
            while (index < sourceMoves.Count)
            {
                int oldIndex = index;
                BatchMoveItem source = sourceMoves[index];
                BatchMoveItem verification = new BatchMoveItem();
                verification.Data = source.Data;
                verification.PrecheckStatus = "PENDING";
                verification.ApplyStatus = "NOT_STARTED";
                verificationMoves.Add(verification);
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            return verificationMoves;
        }

        private static List<DxfPoint> CreateLinePoints(
            DxfPoint start,
            DxfPoint end)
        {
            List<DxfPoint> points = new List<DxfPoint>();
            points.Add(start);
            points.Add(end);
            return points;
        }

        private static List<DxfPoint> TranslateDxfPoints(
            List<DxfPoint> points,
            double deltaX,
            double deltaY)
        {
            List<DxfPoint> translated = new List<DxfPoint>();
            int index = 0;
            while (index < points.Count)
            {
                int oldIndex = index;
                translated.Add(
                    new DxfPoint(
                        points[index].X + deltaX,
                        points[index].Y + deltaY));
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            return translated;
        }

        private static MarElementHandle TryMatchGeometry(
            MarDrafting drafting,
            List<DxfPoint> points,
            string expectedLayer,
            out string detail)
        {
            detail = string.Empty;
            if (points == null || points.Count < 2)
            {
                detail = "insufficient points";
                return null;
            }

            MarElementHandle matched = null;
            double expectedLength = CalculatePathLength(points);
            DxfPoint expectedStart = points[0];
            DxfPoint expectedEnd = points[points.Count - 1];
            DxfPoint expectedMidpoint = new DxfPoint(
                (expectedStart.X + expectedEnd.X) / 2.0,
                (expectedStart.Y + expectedEnd.Y) / 2.0);
            int index = 0;
            try
            {
                while (index < points.Count)
                {
                    int oldIndex = index;
                    MarElementHandle candidate =
                        IdentifyGeometryAt(
                            drafting,
                            points[index]);
                    if (candidate == null)
                    {
                        DisposeHandle(matched);
                        detail = "point " + index + " not matched";
                        return null;
                    }
                    if (matched == null)
                    {
                        matched = candidate;
                    }
                    else if (matched.handle != candidate.handle)
                    {
                        candidate.Dispose();
                        matched.Dispose();
                        detail = "different handles at geometry points";
                        return null;
                    }
                    else
                    {
                        candidate.Dispose();
                    }

                    index++;
                    EnsureParserIndexAdvanced(oldIndex, index);
                }

                MarContourPlanar contour =
                    drafting.ContourPropertiesGet(matched);
                if (contour == null)
                {
                    DisposeHandle(matched);
                    detail = "contour properties are null";
                    return null;
                }
                try
                {
                    double actualLength = contour.Length();
                    bool isGeometry = drafting.ElementIsGeometry(matched);
                    bool isContour = drafting.ElementIsContour(matched);
                    if (!isGeometry || !isContour)
                    {
                        DisposeHandle(matched);
                        detail =
                            "candidate is not a contour geometry" +
                            " | is_geometry=" + isGeometry +
                            " | is_contour=" + isContour;
                        return null;
                    }
                    using (MarPointPlanar startPoint =
                        new MarPointPlanar(expectedStart.X, expectedStart.Y))
                    using (MarPointPlanar endPoint =
                        new MarPointPlanar(expectedEnd.X, expectedEnd.Y))
                    using (MarPointPlanar midpoint =
                        new MarPointPlanar(
                            expectedMidpoint.X,
                            expectedMidpoint.Y))
                    {
                        double startDeviation = contour.Distance(startPoint);
                        double endDeviation = contour.Distance(endPoint);
                        double midpointDeviation = contour.Distance(midpoint);
                        double lengthDeviation =
                            Math.Abs(actualLength - expectedLength);
                        if (startDeviation > EndpointTolerance ||
                            endDeviation > EndpointTolerance ||
                            midpointDeviation > MidpointTolerance ||
                            lengthDeviation > LengthTolerance)
                        {
                            DisposeHandle(matched);
                            detail =
                                "endpoint deviation=" +
                                Math.Max(startDeviation, endDeviation).ToString(
                                    CultureInfo.InvariantCulture) +
                                " | midpoint deviation=" +
                                midpointDeviation.ToString(
                                    CultureInfo.InvariantCulture) +
                                " | length deviation=" +
                                lengthDeviation.ToString(
                                    CultureInfo.InvariantCulture);
                            return null;
                        }
                        detail =
                            "element_type=not exposed by marAPI" +
                            " | is_geometry=" + isGeometry +
                            " | is_contour=" + isContour +
                            " | layer=" +
                            GetLayerDescription(drafting, matched) +
                            " | start=" + FormatPoint(expectedStart) +
                            " | end=" + FormatPoint(expectedEnd) +
                            " | midpoint=" + FormatPoint(expectedMidpoint) +
                            " | endpoint deviation=" +
                            Math.Max(startDeviation, endDeviation).ToString(
                                CultureInfo.InvariantCulture) +
                            " | midpoint deviation=" +
                            midpointDeviation.ToString(
                                CultureInfo.InvariantCulture) +
                            " | length deviation=" +
                            lengthDeviation.ToString(
                                CultureInfo.InvariantCulture);
                    }
                }
                finally
                {
                    contour.Dispose();
                }

                return matched;
            }
            catch (Exception ex)
            {
                DisposeHandle(matched);
                detail = "ERROR: " + ex.Message;
                return null;
            }
        }

        private static MarElementHandle IdentifyGeometryAt(
            MarDrafting drafting,
            DxfPoint point)
        {
            using (MarPointPlanar position =
                new MarPointPlanar(point.X, point.Y))
            {
                MarElementHandle handle =
                    drafting.GeometryIdentify(position);
                if (handle == null)
                {
                    return null;
                }
                return handle;
            }
        }

        private static List<DxfPoint> NormalizeUnderlinePoints(
            List<DxfPoint> points)
        {
            if (points == null || points.Count < 2)
            {
                return points;
            }
            if (points.Count >= 3 &&
                AreDxfPointsEqual(
                    points[0],
                    points[points.Count - 1],
                    GeometryTolerance))
            {
                List<DxfPoint> normalized = new List<DxfPoint>();
                normalized.Add(points[0]);
                normalized.Add(points[1]);
                return normalized;
            }
            return points;
        }

        private static bool AreDxfPointsEqual(
            DxfPoint first,
            DxfPoint second,
            double tolerance)
        {
            if (first == null || second == null)
            {
                return false;
            }
            double dx = first.X - second.X;
            double dy = first.Y - second.Y;
            return Math.Sqrt(dx * dx + dy * dy) <= tolerance;
        }

        private static int GetElementLayerId(
            MarDrafting drafting,
            MarElementHandle handle)
        {
            using (MarLayer layer = drafting.ElementLayerGet(handle))
            {
                if (layer == null)
                {
                    throw new InvalidDataException(
                        "element layer is null");
                }
                return layer.LayerId;
            }
        }

        private static string GetLayerDescription(
            MarDrafting drafting,
            MarElementHandle handle)
        {
            using (MarLayer layer = drafting.ElementLayerGet(handle))
            {
                if (layer == null)
                {
                    return "<null>";
                }
                return layer.GetDescription();
            }
        }

        private static double CalculatePathLength(
            List<DxfPoint> points)
        {
            double length = 0.0;
            int index = 1;
            while (index < points.Count)
            {
                int oldIndex = index;
                double dx = points[index].X - points[index - 1].X;
                double dy = points[index].Y - points[index - 1].Y;
                length += Math.Sqrt(dx * dx + dy * dy);
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            return length;
        }

        private static MarElementHandle TryMatchGeometryByInteriorProbes(
            MarDrafting drafting,
            List<DxfPoint> points,
            int expectedLayerId,
            bool allowUnresolvedLayer,
            MarElementHandle excludedFirst,
            MarElementHandle excludedSecond,
            out string detail)
        {
            detail = string.Empty;
            if (points == null || points.Count < 2)
            {
                detail = "NOT_FOUND: insufficient target points";
                return null;
            }

            DxfPoint p0 = points[0];
            DxfPoint p1 = points[points.Count - 1];
            if (points.Count >= 3 &&
                AreDxfPointsEqual(
                    points[0],
                    points[points.Count - 1],
                    GeometryTolerance))
            {
                p1 = points[1];
            }
            double dx = p1.X - p0.X;
            double dy = p1.Y - p0.Y;
            DxfPoint[] probes = new DxfPoint[]
            {
                new DxfPoint(p0.X + dx * 0.25, p0.Y + dy * 0.25),
                new DxfPoint(p0.X + dx * 0.50, p0.Y + dy * 0.50),
                new DxfPoint(p0.X + dx * 0.75, p0.Y + dy * 0.75)
            };
            List<GeometryProbeCandidate> candidates =
                new List<GeometryProbeCandidate>();
            StringBuilder diagnostics = new StringBuilder();
            int probeIndex = 0;

            try
            {
                while (probeIndex < probes.Length)
                {
                    int oldProbeIndex = probeIndex;
                    MarElementHandle candidate =
                        IdentifyGeometryAt(drafting, probes[probeIndex]);
                    diagnostics.Append(
                        "probe=" + probeIndex +
                        " point=" + FormatPoint(probes[probeIndex]));

                    if (candidate == null)
                    {
                        diagnostics.AppendLine(" | no object");
                    }
                    else
                    {
                        bool duplicate = false;
                        int candidateIndex = 0;
                        while (candidateIndex < candidates.Count)
                        {
                            int oldCandidateIndex = candidateIndex;
                            if (candidates[candidateIndex].Handle.handle ==
                                candidate.handle)
                            {
                                duplicate = true;
                                candidates[candidateIndex].HitCount++;
                            }
                            candidateIndex++;
                            EnsureParserIndexAdvanced(
                                oldCandidateIndex,
                                candidateIndex);
                        }

                        diagnostics.Append(
                            " | candidate_handle=" + candidate.handle);
                        try
                        {
                            diagnostics.Append(
                                " | dbref=" +
                                drafting.ElementDbrefGet(candidate));
                        }
                        catch (Exception ex)
                        {
                            diagnostics.Append(
                                " | dbref_error=" + ex.GetType().FullName +
                                ":" + ex.Message);
                        }
                        try
                        {
                            diagnostics.Append(
                                " | layer=" +
                                GetLayerDescription(drafting, candidate));
                        }
                        catch (Exception ex)
                        {
                            diagnostics.Append(
                                " | layer_error=" + ex.GetType().FullName +
                                ":" + ex.Message);
                        }

                        if (duplicate)
                        {
                            candidate.Dispose();
                            diagnostics.AppendLine(" | duplicate candidate");
                        }
                        else
                        {
                            GeometryProbeCandidate probeCandidate =
                                new GeometryProbeCandidate();
                            probeCandidate.Handle = candidate;
                            probeCandidate.HitCount = 1;
                            candidates.Add(probeCandidate);
                            diagnostics.AppendLine(" | candidate collected");
                        }
                    }

                    probeIndex++;
                    EnsureParserIndexAdvanced(
                        oldProbeIndex,
                        probeIndex);
                }

                double expectedLength = CalculatePathLength(points);
                diagnostics.AppendLine(
                    "expected_length=" + expectedLength.ToString(
                        CultureInfo.InvariantCulture));
                if (allowUnresolvedLayer)
                {
                    diagnostics.AppendLine("target layer not resolved");
                }
                DxfPoint midpoint = new DxfPoint(
                    (p0.X + p1.X) / 2.0,
                    (p0.Y + p1.Y) / 2.0);
                List<MarElementHandle> matches =
                    new List<MarElementHandle>();
                int index = 0;
                while (index < candidates.Count)
                {
                    int oldIndex = index;
                    GeometryProbeCandidate probeCandidate = candidates[index];
                    MarElementHandle candidate = probeCandidate.Handle;
                    MarContourPlanar contour = null;
                    try
                    {
                        contour = drafting.ContourPropertiesGet(candidate);
                        if (contour == null)
                        {
                            diagnostics.AppendLine(
                                "candidate=" + candidate.handle +
                                " rejected=contour properties are null");
                        }
                        else
                        {
                            using (MarPointPlanar middle =
                                new MarPointPlanar(midpoint.X, midpoint.Y))
                            {
                                double midpointDeviation = contour.Distance(middle);
                                double actualLength = contour.Length();
                                double lengthDeviation = Math.Abs(
                                    actualLength - expectedLength);
                                bool geometry = drafting.ElementIsGeometry(candidate);
                                bool contourElement = drafting.ElementIsContour(candidate);
                                int actualLayerId = GetElementLayerId(
                                    drafting,
                                    candidate);
                                bool layerMatch = allowUnresolvedLayer ||
                                    actualLayerId == expectedLayerId;
                                bool excluded =
                                    (excludedFirst != null &&
                                        candidate.handle == excludedFirst.handle) ||
                                    (excludedSecond != null &&
                                        candidate.handle == excludedSecond.handle);
                                bool accepted = geometry && contourElement &&
                                    layerMatch && !excluded &&
                                    probeCandidate.HitCount >= 2 &&
                                    lengthDeviation <= LengthTolerance;

                                diagnostics.AppendLine(
                                    "candidate_handle=" + candidate.handle +
                                    " | hit_count=" +
                                    probeCandidate.HitCount +
                                    " | element_type=not exposed by marAPI" +
                                    " | is_geometry=" + geometry +
                                    " | is_contour=" + contourElement +
                                    " | start=" + FormatPoint(p0) +
                                    " | end=" + FormatPoint(p1) +
                                    " | contour_length=" + actualLength.ToString(CultureInfo.InvariantCulture) +
                                    " | expected_length=" + expectedLength.ToString(CultureInfo.InvariantCulture) +
                                    " | layer_id=" + actualLayerId +
                                    " | expected_layer_id=" +
                                    (allowUnresolvedLayer
                                        ? "not resolved"
                                        : expectedLayerId.ToString()) +
                                    " | endpoint_distance=" +
                                    "not used; Start/End getter absent" +
                                    " | midpoint_deviation=" + midpointDeviation.ToString(CultureInfo.InvariantCulture) +
                                    " | length_deviation=" + lengthDeviation.ToString(CultureInfo.InvariantCulture) +
                                    " | direction=not exposed by marAPI" +
                                    " | result=" + (accepted ? "accepted" : "rejected"));

                                if (accepted)
                                {
                                    matches.Add(candidate);
                                }
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        diagnostics.AppendLine(
                            "candidate=" + candidate.handle +
                            " rejected=" + ex.Message);
                    }
                    finally
                    {
                        if (contour != null)
                        {
                            contour.Dispose();
                        }
                    }

                    index++;
                    EnsureParserIndexAdvanced(oldIndex, index);
                }

                if (matches.Count == 1)
                {
                    MarElementHandle matched = matches[0];
                    int indexToDispose = 0;
                    while (indexToDispose < candidates.Count)
                    {
                        int oldIndex = indexToDispose;
                        if (candidates[indexToDispose].Handle.handle != matched.handle)
                        {
                            candidates[indexToDispose].Handle.Dispose();
                        }
                        indexToDispose++;
                        EnsureParserIndexAdvanced(oldIndex, indexToDispose);
                    }
                    detail = "MATCHED | " + diagnostics.ToString();
                    return matched;
                }

                int disposeIndex = 0;
                while (disposeIndex < candidates.Count)
                {
                    int oldIndex = disposeIndex;
                    candidates[disposeIndex].Handle.Dispose();
                    disposeIndex++;
                    EnsureParserIndexAdvanced(oldIndex, disposeIndex);
                }
                detail = (matches.Count == 0 ? "NOT_FOUND" : "AMBIGUOUS") +
                    " | matches=" + matches.Count +
                    " | " + diagnostics.ToString();
                return null;
            }
            catch (Exception ex)
            {
                int disposeIndex = 0;
                while (disposeIndex < candidates.Count)
                {
                    int oldIndex = disposeIndex;
                    candidates[disposeIndex].Handle.Dispose();
                    disposeIndex++;
                    EnsureParserIndexAdvanced(oldIndex, disposeIndex);
                }
                detail = "NOT_FOUND | ERROR: " + ex.Message +
                    " | " + diagnostics.ToString();
                return null;
            }
        }

        private static bool VerifyLiveGeometryAtTarget(
            MarDrafting drafting,
            MarElementHandle expectedHandle,
            List<DxfPoint> targetPoints,
            int expectedLayerId,
            out string detail)
        {
            detail = string.Empty;
            if (expectedHandle == null || targetPoints == null ||
                targetPoints.Count < 2)
            {
                detail = "expected handle or target points are missing";
                return false;
            }

            DxfPoint first = targetPoints[0];
            DxfPoint last = targetPoints[targetPoints.Count - 1];
            double dx = last.X - first.X;
            double dy = last.Y - first.Y;
            DxfPoint[] probes = new DxfPoint[]
            {
                new DxfPoint(first.X + dx * 0.25, first.Y + dy * 0.25),
                new DxfPoint(first.X + dx * 0.50, first.Y + dy * 0.50),
                new DxfPoint(first.X + dx * 0.75, first.Y + dy * 0.75)
            };
            int hits = 0;
            int probeIndex = 0;
            while (probeIndex < probes.Length)
            {
                int oldProbeIndex = probeIndex;
                MarElementHandle candidate =
                    IdentifyGeometryAt(drafting, probes[probeIndex]);
                if (candidate != null)
                {
                    if (candidate.handle == expectedHandle.handle)
                    {
                        hits++;
                    }
                    candidate.Dispose();
                }
                probeIndex++;
                EnsureParserIndexAdvanced(oldProbeIndex, probeIndex);
            }

            bool contourValid = false;
            bool layerValid = false;
            using (MarContourPlanar contour =
                drafting.ContourPropertiesGet(expectedHandle))
            {
                if (contour != null)
                {
                    double expectedLength = CalculatePathLength(targetPoints);
                    contourValid = Math.Abs(
                        contour.Length() - expectedLength) <= LengthTolerance &&
                        drafting.ElementIsGeometry(expectedHandle) &&
                        drafting.ElementIsContour(expectedHandle);
                }
            }
            using (MarLayer layer = drafting.ElementLayerGet(expectedHandle))
            {
                layerValid = layer != null && layer.LayerId == expectedLayerId;
            }
            detail = "hits=" + hits + " | contour=" + contourValid +
                " | layer=" + layerValid + " | expected_layer_id=" +
                expectedLayerId;
            return hits >= 2 && contourValid && layerValid;
        }

        private static OldLeaderAbsenceResult VerifyOldLeaderAbsentImmediately(
            MarDrafting drafting,
            AlgorithmAnnotationGeometryItem item,
            HashSet<int> activeExpectedHandleIds,
            out string detail)
        {
            detail = string.Empty;
            List<DxfPoint> points = CreateLinePoints(
                item.SourceOldLeader.LineStart,
                item.SourceOldLeader.LineEnd);
            DxfPoint first = points[0];
            DxfPoint last = points[1];
            double dx = last.X - first.X;
            double dy = last.Y - first.Y;
            DxfPoint[] probes = new DxfPoint[]
            {
                new DxfPoint(first.X + dx * 0.25, first.Y + dy * 0.25),
                new DxfPoint(first.X + dx * 0.50, first.Y + dy * 0.50),
                new DxfPoint(first.X + dx * 0.75, first.Y + dy * 0.75)
            };
            List<GeometryProbeCandidate> candidates =
                new List<GeometryProbeCandidate>();
            int validProbeCount = 0;
            int inconclusiveProbeCount = 0;
            int probeIndex = 0;
            while (probeIndex < probes.Length)
            {
                int oldProbeIndex = probeIndex;
                try
                {
                    MarElementHandle candidate =
                        IdentifyGeometryAt(drafting, probes[probeIndex]);
                    if (candidate == null)
                    {
                        detail += "probe_index=" + probeIndex +
                            " | probe_x=" + probes[probeIndex].X +
                            " | probe_y=" + probes[probeIndex].Y +
                            " | geometry_identify_status=NULL" +
                            " | candidate_matches_deleted_handle=false" +
                            " | probe_result=ABSENT;\n";
                        validProbeCount++;
                    }
                    else if (candidate.handle !=
                        item.OldLeaderHandleIdBeforeDelete)
                    {
                        int foreignHandleId = candidate.handle;
                        candidate.Dispose();
                        detail += "probe_index=" + probeIndex +
                            " | probe_x=" + probes[probeIndex].X +
                            " | probe_y=" + probes[probeIndex].Y +
                            " | candidate_handle=" + foreignHandleId +
                            " | candidate_matches_deleted_handle=false" +
                            " | candidate_active_expected=false" +
                            " | probe_result=FOREIGN_GEOMETRY" +
                            " | identity_gate_result=REJECTED;\n";
                        validProbeCount++;
                    }
                    else if (activeExpectedHandleIds.Contains(candidate.handle))
                    {
                        int activeHandleId = candidate.handle;
                        if (candidate.handle == item.OldLeaderHandleIdBeforeDelete)
                        {
                            item.ReusedHandleDetected = true;
                            item.ReusedHandleId = candidate.handle;
                        }
                        candidate.Dispose();
                        detail += "probe_index=" + probeIndex +
                            " | probe_x=" + probes[probeIndex].X +
                            " | probe_y=" + probes[probeIndex].Y +
                            " | candidate_handle=" +
                            activeHandleId +
                            " | candidate_active_expected=true" +
                            " | candidate_matches_deleted_handle=true" +
                            " | probe_result=REUSED_ACTIVE_HANDLE" +
                            " | identity_gate_result=REUSED;\n";
                        validProbeCount++;
                    }
                    else
                    {
                        int candidateId = candidate.handle;
                        bool duplicate = false;
                        int candidateIndex = 0;
                        while (candidateIndex < candidates.Count)
                        {
                            int oldCandidateIndex = candidateIndex;
                            if (candidates[candidateIndex].Handle.handle ==
                                candidate.handle)
                            {
                                candidates[candidateIndex].HitCount++;
                                duplicate = true;
                            }
                            candidateIndex++;
                            EnsureParserIndexAdvanced(
                                oldCandidateIndex,
                                candidateIndex);
                        }
                        if (!duplicate)
                        {
                            GeometryProbeCandidate probeCandidate =
                                new GeometryProbeCandidate();
                            probeCandidate.Handle = candidate;
                            probeCandidate.HitCount = 1;
                            candidates.Add(probeCandidate);
                        }
                        else
                        {
                            candidate.Dispose();
                        }
                        validProbeCount++;
                        detail += "probe_index=" + probeIndex +
                            " | probe_x=" + probes[probeIndex].X +
                            " | probe_y=" + probes[probeIndex].Y +
                            " | candidate_handle=" + candidateId +
                            " | candidate_matches_deleted_handle=true" +
                            " | candidate_active_expected=false" +
                            " | probe_result=CANDIDATE_COLLECTED" +
                            " | identity_gate_result=ACCEPTED;\n";
                    }
                }
                catch (Exception ex)
                {
                    inconclusiveProbeCount++;
                    detail += "probe_index=" + probeIndex +
                        " | probe_x=" + probes[probeIndex].X +
                        " | probe_y=" + probes[probeIndex].Y +
                        " | geometry_identify_status=API_ERROR" +
                        " | probe_result=API_ERROR" +
                        " | exception_type=" + ex.GetType().FullName +
                        " | exception_message=" + ex.Message + ";\n";
                }
                probeIndex++;
                EnsureParserIndexAdvanced(oldProbeIndex, probeIndex);
            }

            bool oldLeaderPresent = false;
            int candidateIndexToCheck = 0;
            while (candidateIndexToCheck < candidates.Count)
            {
                int oldCandidateIndexToCheck = candidateIndexToCheck;
                GeometryProbeCandidate candidate =
                    candidates[candidateIndexToCheck];
                if (candidate.HitCount >= 2)
                {
                    try
                    {
                        using (MarContourPlanar contour =
                            drafting.ContourPropertiesGet(candidate.Handle))
                        using (MarLayer layer =
                            drafting.ElementLayerGet(candidate.Handle))
                        {
                            if (contour != null && layer != null &&
                                Math.Abs(
                                    contour.Length() -
                                    CalculatePathLength(points)) <= LengthTolerance &&
                                layer.LayerId == item.OldLeaderLayerId)
                            {
                                oldLeaderPresent = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        inconclusiveProbeCount++;
                        detail += "candidate_handle=" + candidate.Handle.handle +
                            " | probe_result=CANDIDATE_UNREADABLE" +
                            " | exception_type=" + ex.GetType().FullName +
                            " | exception_message=" + ex.Message + ";\n";
                    }
                    if (oldLeaderPresent)
                    {
                        candidate.Handle.Dispose();
                        return OldLeaderAbsenceResult.Present;
                    }
                }
                else
                {
                    detail += "candidate_handle=" + candidate.Handle.handle +
                        " | candidate_geometry_status=INSUFFICIENT_HITS" +
                        " | probe_result=INCONCLUSIVE;\n";
                }
                candidate.Handle.Dispose();
                candidateIndexToCheck++;
                EnsureParserIndexAdvanced(
                    oldCandidateIndexToCheck,
                    candidateIndexToCheck);
            }
            if (inconclusiveProbeCount > 0 && validProbeCount < 2)
            {
                return OldLeaderAbsenceResult.Inconclusive;
            }
            if (oldLeaderPresent)
            {
                return OldLeaderAbsenceResult.Present;
            }
            if (validProbeCount < 2)
            {
                return OldLeaderAbsenceResult.Inconclusive;
            }
            return OldLeaderAbsenceResult.Absent;
        }

        private static bool PostVerifyAppliedItems(
            MarDrafting drafting,
            List<AlgorithmAnnotationGeometryItem> items,
            string logPath,
            HashSet<int> activeExpectedHandleIds,
            out string failedHandle,
            out string failureDetail)
        {
            AppendApplyStep(
                logPath,
                "POST_VERIFY_STRATEGY=" +
                "HANDLE_ID_GATED_TWO_PHASE_ABSENCE_CHECK");
            failedHandle = string.Empty;
            failureDetail = string.Empty;
            int index = 0;
            while (index < items.Count)
            {
                int oldIndex = index;
                AlgorithmAnnotationGeometryItem item = items[index];
                if (item.Status == "READY")
                {
                    string underlineDetail;
                    bool underlineValid = VerifyLiveGeometryAtTarget(
                        drafting,
                        item.OriginalUnderline,
                        TranslateDxfPoints(
                            item.SourceUnderline.Vertices,
                            item.Move.Data.DeltaX,
                            item.Move.Data.DeltaY),
                        GetElementLayerId(drafting, item.OriginalUnderline),
                        out underlineDetail);
                    string leaderDetail;
                    bool leaderValid = VerifyLiveGeometryAtTarget(
                        drafting,
                        item.CreatedNewLeader,
                        CreateLinePoints(
                            new DxfPoint(
                                item.Move.Data.LeaderStart[0],
                                item.Move.Data.LeaderStart[1]),
                            new DxfPoint(
                                item.Move.Data.LeaderEnd[0],
                                item.Move.Data.LeaderEnd[1])),
                        item.OldLeaderLayerId,
                        out leaderDetail);
                    string finalAbsenceDetail;
                    OldLeaderAbsenceResult finalAbsenceResult =
                        VerifyOldLeaderAbsentImmediately(
                            drafting,
                            item,
                            activeExpectedHandleIds,
                            out finalAbsenceDetail);
                    item.FinalOldLeaderAbsenceResult = finalAbsenceResult;
                    item.FinalOldLeaderAbsenceDetail = finalAbsenceDetail;
                    item.PostVerificationStatus =
                        finalAbsenceResult == OldLeaderAbsenceResult.Absent &&
                        underlineValid && leaderValid
                            ? "ALREADY_APPLIED"
                            : "FAILED_PRECHECK";
                    item.PostVerificationDetail =
                        "underline=" + underlineDetail +
                        " | new_leader=" + leaderDetail +
                        " | old_leader_absence=" +
                        finalAbsenceResult +
                        " | reused_handle_detected=" +
                        item.ReusedHandleDetected;
                    if (item.PostVerificationStatus != "ALREADY_APPLIED" &&
                        failedHandle.Length == 0)
                    {
                        failedHandle = item.Move.Data.Handle;
                        failureDetail = finalAbsenceResult ==
                            OldLeaderAbsenceResult.Inconclusive
                            ? "old leader absence verification inconclusive | handle=" +
                                failedHandle +
                                " | immediate_detail=" +
                                item.ImmediateOldLeaderAbsenceDetail +
                                " | final_detail=" + finalAbsenceDetail
                            : "old leader absence verification failed | handle=" +
                                failedHandle +
                                " | final_detail=" + finalAbsenceDetail;
                    }
                }
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            return failedHandle.Length == 0;
        }

        private static void ApplyAnnotationGeometryItem(
            MarDrafting drafting,
            AlgorithmAnnotationGeometryItem item,
            HashSet<int> activeExpectedHandleIds)
        {
            MarElementHandle created =
                null;
            if (item.OldLeaderLayer == null)
            {
                throw new InvalidDataException(
                    "old leader layer cannot be obtained | handle=" +
                    item.Move.Data.Handle +
                    " | old_leader_handle=" +
                    (item.OldLeader == null
                        ? string.Empty
                        : item.OldLeader.handle.ToString()));
            }

            item.ApplyTrace = "create new leader START";
            item.NewLeaderCreationAttempted = true;
            try
            {
                created =
                    CreateLineOnLayer(
                        drafting,
                        item.Move.Data.LeaderStart[0],
                        item.Move.Data.LeaderStart[1],
                        item.Move.Data.LeaderEnd[0],
                        item.Move.Data.LeaderEnd[1],
                        item.OldLeaderLayer,
                        item.OldLeaderColour,
                        item.OldLeaderLinetype);
                item.CreatedNewLeader = created;
                item.NewLeaderCreated = true;
                item.CreatedNewLeaderHandleId = created.handle;
                item.OldLeaderHandleBeforeDelete = item.OldLeader.handle;
                item.OldLeaderHandleIdBeforeDelete = item.OldLeader.handle;
                item.ApplyTrace += " | create new leader SUCCESS handle=" +
                    created.handle;

                item.ApplyTrace += " | set inherited layer START";
                MarElementHandle handle = created;
                try
                {
                    drafting.ElementLayerSet(handle, item.OldLeaderLayer);
                }
                catch
                {
                    item.LayerInheritanceStatus = "ERROR";
                    throw;
                }
                item.ApplyTrace += " | set inherited layer SUCCESS";

                item.ApplyTrace += " | verify inherited layer START";
                try
                {
                    using (MarLayer actualLayer =
                        drafting.ElementLayerGet(created))
                    {
                        if (actualLayer == null)
                        {
                            throw new InvalidDataException(
                                "created leader layer is null");
                        }

                        item.CreatedNewLeaderLayerId = actualLayer.LayerId;
                        if (actualLayer.LayerId != item.OldLeaderLayerId)
                        {
                            throw new InvalidDataException(
                                "created leader layer verification failed | expected=" +
                                item.OldLeaderLayerId +
                                " | actual=" + actualLayer.LayerId);
                        }
                    }
                }
                catch
                {
                    item.LayerInheritanceStatus = "ERROR";
                    throw;
                }
                item.ApplyTrace += " | verify inherited layer SUCCESS";
                item.LayerInheritanceStatus = "SUCCESS";

                if (item.OldLeaderColour != null)
                {
                    drafting.ElementColourSet(
                        item.CreatedNewLeader,
                        item.OldLeaderColour);
                }
                if (item.OldLeaderLinetype != null)
                {
                    drafting.ElementLinetypeSet(
                        item.CreatedNewLeader,
                        item.OldLeaderLinetype);
                }
                activeExpectedHandleIds.Add(item.OriginalUnderline.handle);
                activeExpectedHandleIds.Add(item.CreatedNewLeader.handle);

                item.ApplyTrace += " | move underline START";
                item.UnderlineMoveAttempted = true;
                ApplySavedAlgorithmMove(
                    drafting,
                    item.OriginalUnderline,
                    item.Move.Data.DeltaX,
                    item.Move.Data.DeltaY);
                item.UnderlineMoved = true;
                item.UnderlineMoveStatus = "SUCCESS";
                item.ApplyTrace += " | move underline SUCCESS";

                item.ApplyTrace += " | delete old leader START";
                item.OldLeaderDeleteAttempted = true;
                drafting.ElementDelete(item.OldLeader);
                item.OldLeaderDeleted = true;
                item.OldLeaderDeleteStatus = "SUCCESS";
                item.ApplyTrace += " | delete old leader SUCCESS";

                item.ApplyTrace += " | verify old leader absence START";
                string absenceDetail;
                OldLeaderAbsenceResult absenceResult =
                    VerifyOldLeaderAbsentImmediately(
                    drafting,
                    item,
                    activeExpectedHandleIds,
                    out absenceDetail);
                item.ImmediateOldLeaderAbsenceResult = absenceResult;
                item.ImmediateOldLeaderAbsenceDetail = absenceDetail;
                item.OldLeaderAbsenceVerificationDetail = absenceDetail;
                item.ImmediateAbsenceVerificationStatus =
                    absenceResult.ToString();
                if (absenceResult == OldLeaderAbsenceResult.Present)
                {
                    throw new InvalidDataException(
                        "old leader absence verification failed | " +
                        absenceDetail);
                }
                item.OldLeaderAbsenceVerified =
                    absenceResult == OldLeaderAbsenceResult.Absent;
                item.OldLeader = null;
                item.ApplyTrace +=
                    " | verify old leader absence SUCCESS";
                item.RecordFullyApplied = true;
            }
            catch
            {
                if (created != null && !item.OldLeaderDeleted)
                {
                    try
                    {
                        activeExpectedHandleIds.Remove(created.handle);
                        drafting.ElementDelete(created);
                    }
                    catch
                    {
                    }
                    item.CreatedNewLeader = null;
                    item.NewLeaderCreated = false;
                }
                throw;
            }
        }
        private static void RollbackAnnotationGeometryItem(
            MarDrafting drafting,
            AlgorithmAnnotationGeometryItem item,
            HashSet<int> activeExpectedHandleIds)
        {
            if (item.OldLeaderDeleted)
            {
                item.RollbackTrace += "recreate old leader START";
                MarElementHandle recreated =
                    CreateLineOnLayer(
                        drafting,
                        item.SourceOldLeader.LineStart.X,
                        item.SourceOldLeader.LineStart.Y,
                        item.SourceOldLeader.LineEnd.X,
                        item.SourceOldLeader.LineEnd.Y,
                        item.OldLeaderLayer,
                        item.OldLeaderColour,
                        item.OldLeaderLinetype);
                item.OldLeader = recreated;
                item.OldLeaderDeleted = false;
                item.RollbackTrace += " | recreate old leader SUCCESS";
            }
            if (item.UnderlineMoved && item.OriginalUnderline != null)
            {
                item.RollbackTrace += " | move underline back START";
                ApplySavedAlgorithmMove(
                    drafting,
                    item.OriginalUnderline,
                    -item.Move.Data.DeltaX,
                    -item.Move.Data.DeltaY);
                item.UnderlineMoved = false;
                item.RollbackTrace += " | move underline back SUCCESS";
            }
            if (item.CreatedNewLeader != null)
            {
                item.RollbackTrace += " | delete new leader START";
                activeExpectedHandleIds.Remove(item.CreatedNewLeader.handle);
                drafting.ElementDelete(item.CreatedNewLeader);
                item.CreatedNewLeader = null;
                item.NewLeaderCreated = false;
                item.RollbackTrace += " | delete new leader SUCCESS";
            }
            item.OldLeaderAbsenceVerified = false;
        }

        private static MarElementHandle CreateLineOnLayer(
            MarDrafting drafting,
            double startX,
            double startY,
            double endX,
            double endY,
            MarLayer layer,
            MarColour colour,
            MarLinetype linetype)
        {
            if (layer == null)
            {
                throw new InvalidDataException(
                    "target layer is null");
            }

            using (MarPointPlanar start =
                new MarPointPlanar(startX, startY))
            using (MarPointPlanar end =
                new MarPointPlanar(endX, endY))
            using (MarRlinePlanar line =
                new MarRlinePlanar(start, end))
            {
                MarElementHandle handle = drafting.LineNew(line);
                if (handle == null)
                {
                    throw new InvalidDataException(
                        "LineNew returned null");
                }

                try
                {
                    drafting.ElementLayerSet(handle, layer);
                    if (colour != null)
                    {
                        drafting.ElementColourSet(handle, colour);
                    }
                    if (linetype != null)
                    {
                        drafting.ElementLinetypeSet(handle, linetype);
                    }
                    return handle;
                }
                catch
                {
                    try
                    {
                        drafting.ElementDelete(handle);
                    }
                    catch
                    {
                    }
                    throw;
                }
            }
        }
        private static int CountGeometryStatus(
            List<AlgorithmAnnotationGeometryItem> items,
            string status)
        {
            int count = 0;
            int index = 0;
            while (index < items.Count)
            {
                int oldIndex = index;
                if (items[index].Status == status)
                {
                    count++;
                }
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            return count;
        }

        private static string FindFirstGeometryFailureHandle(
            List<AlgorithmAnnotationGeometryItem> items)
        {
            int index = 0;
            while (index < items.Count)
            {
                int oldIndex = index;
                if (items[index].Status == "FAILED_PRECHECK")
                {
                    return items[index].Move.Data.Handle;
                }
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            return string.Empty;
        }

        private static void DisposeHandle(MarElementHandle handle)
        {
            if (handle != null)
            {
                try
                {
                    handle.Dispose();
                }
                catch
                {
                }
            }
        }

        private static void DisposeAnnotationGeometryItems(
            List<AlgorithmAnnotationGeometryItem> items)
        {
            int index = 0;
            while (index < items.Count)
            {
                int oldIndex = index;
                AlgorithmAnnotationGeometryItem item = items[index];
                DisposeHandle(item.Move.Handle);
                item.Move.Handle = null;
                if (item.Move.TextObject != null)
                {
                    try
                    {
                        item.Move.TextObject.Dispose();
                    }
                    catch
                    {
                    }
                    item.Move.TextObject = null;
                }
                DisposeHandle(item.OriginalUnderline);
                DisposeHandle(item.MovedUnderline);
                DisposeHandle(item.OldLeader);
                DisposeHandle(item.NewLeader);
                DisposeHandle(item.CreatedNewLeader);
                if (item.OldLeaderLayer != null)
                {
                    try
                    {
                        item.OldLeaderLayer.Dispose();
                    }
                    catch
                    {
                    }
                    item.OldLeaderLayer = null;
                }
                if (item.OldLeaderColour != null)
                {
                    try
                    {
                        item.OldLeaderColour.Dispose();
                    }
                    catch
                    {
                    }
                    item.OldLeaderColour = null;
                }
                if (item.OldLeaderLinetype != null)
                {
                    try
                    {
                        item.OldLeaderLinetype.Dispose();
                    }
                    catch
                    {
                    }
                    item.OldLeaderLinetype = null;
                }
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
        }

        private static int CountGeometryFlag(
            List<AlgorithmAnnotationGeometryItem> items,
            int flag)
        {
            int count = 0;
            int index = 0;
            while (index < items.Count)
            {
                int oldIndex = index;
                AlgorithmAnnotationGeometryItem item = items[index];
                bool value = flag == 1
                    ? item.NewLeaderCreationAttempted
                    : flag == 2
                        ? item.UnderlineMoveAttempted
                        : flag == 3
                            ? item.OldLeaderDeleteAttempted
                            : flag == 4
                                ? item.RecordFullyApplied
                                : item.ImmediateOldLeaderAbsenceResult ==
                                    OldLeaderAbsenceResult.Absent;
                if (value)
                {
                    count++;
                }
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            return count;
        }

        private static int CountImmediateAbsenceInconclusive(
            List<AlgorithmAnnotationGeometryItem> items)
        {
            int count = 0;
            int index = 0;
            while (index < items.Count)
            {
                int oldIndex = index;
                if (items[index].ImmediateOldLeaderAbsenceResult ==
                    OldLeaderAbsenceResult.Inconclusive)
                {
                    count++;
                }
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            return count;
        }

        private static int CountGeometryAbsenceResult(
            List<AlgorithmAnnotationGeometryItem> items,
            OldLeaderAbsenceResult result)
        {
            int count = 0;
            int index = 0;
            while (index < items.Count)
            {
                int oldIndex = index;
                if (items[index].ImmediateOldLeaderAbsenceResult == result)
                {
                    count++;
                }
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            return count;
        }

        private static void WriteAnnotationGeometryLog(
            string logPath,
            DateTime startTime,
            DateTime endTime,
            string resultJsonPath,
            string sourceDxfPath,
            BatchAlgorithmData batch,
            List<AlgorithmAnnotationGeometryItem> items,
            int underlinesMoved,
            int oldLeadersDeleted,
            int newLeadersCreated,
            int rollbackSuccess,
            int rollbackFailed,
            string error,
            bool previewLog)
        {
            using (StreamWriter writer = new StreamWriter(
                logPath,
                true,
                new UTF8Encoding(false)))
            {
                writer.WriteLine("Start time: " + startTime.ToString("o"));
                writer.WriteLine("End time: " + endTime.ToString("o"));
                writer.WriteLine("JSON file: " + resultJsonPath);
                writer.WriteLine("Source DXF: " + sourceDxfPath);
                writer.WriteLine("Total is_move=true: " + items.Count);
                writer.WriteLine("Underlines moved: " + underlinesMoved);
                writer.WriteLine("Old leaders deleted: " + oldLeadersDeleted);
                writer.WriteLine("New leaders created: " + newLeadersCreated);
                writer.WriteLine("new_leaders_created_attempted=" +
                    CountGeometryFlag(items, 1));
                writer.WriteLine("underlines_moved_attempted=" +
                    CountGeometryFlag(items, 2));
                writer.WriteLine("old_leaders_deleted_attempted=" +
                    CountGeometryFlag(items, 3));
                writer.WriteLine("records_fully_applied=" +
                    CountGeometryFlag(items, 4));
                writer.WriteLine("immediate_absence_verified=" +
                    CountGeometryFlag(items, 5));
                writer.WriteLine("immediate_absence_present=" +
                    CountGeometryAbsenceResult(
                        items,
                        OldLeaderAbsenceResult.Present));
                writer.WriteLine("immediate_absence_inconclusive=" +
                    CountImmediateAbsenceInconclusive(items));
                writer.WriteLine("Already applied: " +
                    CountGeometryStatus(items, "ALREADY_APPLIED"));
                writer.WriteLine("READY: " + CountGeometryStatus(items, "READY"));
                writer.WriteLine("FAILED_PRECHECK: " +
                    CountGeometryStatus(items, "FAILED_PRECHECK"));
                writer.WriteLine("Rollback success: " + rollbackSuccess);
                writer.WriteLine("Rollback failed: " + rollbackFailed);
                writer.WriteLine("Error: " + (error ?? string.Empty));
                writer.WriteLine();

                int index = 0;
                while (index < items.Count)
                {
                    int oldIndex = index;
                    AlgorithmAnnotationGeometryItem item = items[index];
                    AlgorithmMoveData data = item.Move.Data;
                    writer.WriteLine(
                        "handle=" + data.Handle +
                        " | source_underline_handle=" +
                        (item.OriginalUnderline == null
                            ? string.Empty
                            : item.OriginalUnderline.handle.ToString()) +
                        " | old_leader_handle_before_delete=" +
                        (item.OldLeaderHandleBeforeDelete == int.MinValue
                            ? string.Empty
                            : item.OldLeaderHandleBeforeDelete.ToString()) +
                        " | old_leader_handle=" +
                        (item.OldLeaderHandleBeforeDelete == int.MinValue
                            ? string.Empty
                            : item.OldLeaderHandleBeforeDelete.ToString()) +
                        " | old_leader_handle_id_before_delete=" +
                        (item.OldLeaderHandleIdBeforeDelete == int.MinValue
                            ? string.Empty
                            : item.OldLeaderHandleIdBeforeDelete.ToString()) +
                        " | created_new_leader_handle=" +
                        (item.CreatedNewLeader == null
                            ? (item.CreatedNewLeaderHandleId == int.MinValue
                                ? string.Empty
                                : item.CreatedNewLeaderHandleId.ToString())
                            : item.CreatedNewLeader.handle.ToString()) +
                        " | created_new_leader_handle_id=" +
                        (item.CreatedNewLeaderHandleId == int.MinValue
                            ? string.Empty
                            : item.CreatedNewLeaderHandleId.ToString()) +
                        " | old_leader_layer_id=" +
                        (item.HasOldLeaderLayerId
                            ? item.OldLeaderLayerId.ToString()
                            : string.Empty) +
                        " | created_new_leader_layer_id=" +
                        (item.CreatedNewLeaderLayerId == int.MinValue
                            ? string.Empty
                            : item.CreatedNewLeaderLayerId.ToString()) +
                        " | moved_underline_direct_verification=" +
                        (item.PostVerificationDetail ?? string.Empty) +
                        " | new_leader_direct_verification=" +
                        (item.PostVerificationStatus ?? string.Empty) +
                        " | immediate_old_leader_absence_result=" +
                        item.ImmediateOldLeaderAbsenceResult +
                        " | immediate_old_leader_absence_detail=" +
                        (item.ImmediateOldLeaderAbsenceDetail ?? string.Empty) +
                        " | final_old_leader_absence_result=" +
                        item.FinalOldLeaderAbsenceResult +
                        " | final_old_leader_absence_detail=" +
                        (item.FinalOldLeaderAbsenceDetail ?? string.Empty) +
                        " | reused_handle_detected=" +
                        item.ReusedHandleDetected +
                        " | reused_handle_id=" +
                        (item.ReusedHandleId == int.MinValue
                            ? string.Empty
                            : item.ReusedHandleId.ToString()) +
                        " | layer_inheritance_status=" +
                        (item.LayerInheritanceStatus ?? string.Empty) +
                        " | underline_move_status=" +
                        (item.UnderlineMoveStatus ?? string.Empty) +
                        " | old_leader_delete_status=" +
                        (item.OldLeaderDeleteStatus ?? string.Empty) +
                        " | immediate_absence_verification_status=" +
                        (item.ImmediateAbsenceVerificationStatus ?? string.Empty) +
                        " | text_str=" + data.Text +
                        " | dx=" + data.DeltaX.ToString(CultureInfo.InvariantCulture) +
                        " | dy=" + data.DeltaY.ToString(CultureInfo.InvariantCulture) +
                        " | text=" + item.TextStatus +
                        " | text_probe_results=" +
                        (item.TextMatchDetail ?? string.Empty) +
                        " | apply_receipt_found=" + item.ApplyReceiptFound +
                        " | expected_deleted_old_leader_handle_id=" +
                        (item.ExpectedDeletedOldLeaderHandleId == int.MinValue
                            ? string.Empty
                            : item.ExpectedDeletedOldLeaderHandleId.ToString()) +
                        " | receipt_created_new_leader_handle_id=" +
                        (item.ReceiptCreatedNewLeaderHandleId == int.MinValue
                            ? string.Empty
                            : item.ReceiptCreatedNewLeaderHandleId.ToString()) +
                        " | receipt_moved_underline_handle_id=" +
                        (item.ReceiptMovedUnderlineHandleId == int.MinValue
                            ? string.Empty
                            : item.ReceiptMovedUnderlineHandleId.ToString()) +
                        " | preview_identity_status=" +
                        (item.PreviewIdentityStatus ?? string.Empty) +
                        " | underline=" + item.UnderlineStatus +
                        " | old_leader=" + item.OldLeaderStatus +
                        " | new_leader=" + item.NewLeaderStatus +
                        " | status=" + item.Status +
                        " | apply_status=" + (item.ApplyStatus ?? string.Empty) +
                        " | post_verification=" +
                        (item.PostVerificationStatus ?? string.Empty) +
                        " | post_verification_detail=" +
                        (item.PostVerificationDetail ?? string.Empty) +
                        " | apply_trace=" +
                        (item.ApplyTrace ?? string.Empty) +
                        " | rollback_trace=" +
                        (item.RollbackTrace ?? string.Empty) +
                        " | rollback_status=" + (item.RollbackStatus ?? string.Empty) +
                        " | elapsed_ms=0" +
                        " | failure=" + (item.Failure ?? string.Empty));
                    writer.WriteLine(
                        "  source underline=" + FormatPoints(
                            item.SourceUnderline == null
                                ? null
                                : item.SourceUnderline.Vertices));
                    writer.WriteLine(
                        "  moved underline=" + FormatPoints(
                            item.SourceUnderline == null
                                ? null
                                : TranslateDxfPoints(
                                    item.SourceUnderline.Vertices,
                                    data.DeltaX,
                                    data.DeltaY)));
                    writer.WriteLine(
                        "  old leader=" + FormatPointPair(
                            item.SourceOldLeader == null
                                ? null
                                : item.SourceOldLeader.LineStart,
                            item.SourceOldLeader == null
                                ? null
                                : item.SourceOldLeader.LineEnd));
                    writer.WriteLine(
                        "  new leader=" + FormatPointPair(
                            new DxfPoint(
                                data.LeaderStart[0],
                                data.LeaderStart[1]),
                            new DxfPoint(
                                data.LeaderEnd[0],
                                data.LeaderEnd[1])));
                    writer.WriteLine("  matches=" +
                        (item.OriginalUnderlineDetail ?? string.Empty) +
                        " | " + (item.MovedUnderlineDetail ?? string.Empty) +
                        " | " + (item.OldLeaderDetail ?? string.Empty) +
                        " | " + (item.NewLeaderDetail ?? string.Empty));
                    index++;
                    EnsureParserIndexAdvanced(oldIndex, index);
                }
            }
        }

        private static string FormatPoints(List<DxfPoint> points)
        {
            if (points == null)
            {
                return "<none>";
            }
            StringBuilder result = new StringBuilder();
            result.Append("[");
            int index = 0;
            while (index < points.Count)
            {
                int oldIndex = index;
                if (index > 0)
                {
                    result.Append(";");
                }
                result.Append(FormatPoint(points[index]));
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            result.Append("]");
            return result.ToString();
        }

        private static string FormatPointPair(
            DxfPoint start,
            DxfPoint end)
        {
            return FormatPoint(start) + " -> " + FormatPoint(end);
        }

        private static string FormatPoint(DxfPoint point)
        {
            if (point == null)
            {
                return "<none>";
            }
            return "(" + point.X.ToString(CultureInfo.InvariantCulture) +
                "," + point.Y.ToString(CultureInfo.InvariantCulture) + ")";
        }

        private static AlgorithmMoveData CreateOptionalAlgorithmMove(
            IDictionary<string, object> moveObject)
        {
            AlgorithmMoveData data = new AlgorithmMoveData();
            data.Handle = GetOptionalString(moveObject, "handle");
            data.Layer = GetOptionalString(moveObject, "layer");
            data.Text = GetOptionalString(moveObject, "text_str");
            return data;
        }

        private static string GetOptionalString(
            IDictionary<string, object> dictionary,
            string fieldName)
        {
            if (dictionary == null ||
                !dictionary.ContainsKey(fieldName) ||
                dictionary[fieldName] == null)
            {
                return string.Empty;
            }

            return Convert.ToString(
                dictionary[fieldName],
                CultureInfo.InvariantCulture);
        }

        private static bool GetRequiredBoolean(
            IDictionary<string, object> dictionary,
            string fieldName)
        {
            bool value;
            if (!TryGetBoolean(dictionary, fieldName, out value))
            {
                throw new InvalidDataException(
                    fieldName + " is missing or invalid");
            }

            return value;
        }

        private static bool TryFindMatchingAlgorithmText(
            MarDrafting drafting,
            double x,
            double y,
            string expectedText,
            out MarElementHandle handle,
            out MarText text,
            out string detail)
        {
            handle = null;
            text = null;
            detail = string.Empty;

            try
            {
                DxfPoint[] probes = new DxfPoint[]
                {
                    new DxfPoint(x, y),
                    new DxfPoint(x + 0.25, y),
                    new DxfPoint(x - 0.25, y),
                    new DxfPoint(x, y + 0.25),
                    new DxfPoint(x, y - 0.25),
                    new DxfPoint(x + 0.50, y),
                    new DxfPoint(x - 0.50, y),
                    new DxfPoint(x, y + 0.50),
                    new DxfPoint(x, y - 0.50)
                };
                StringBuilder diagnostics = new StringBuilder();
                int probeIndex = 0;
                while (probeIndex < probes.Length)
                {
                    int oldProbeIndex = probeIndex;
                    MarElementHandle candidate = null;
                    MarText candidateText = null;
                    try
                    {
                        using (MarPointPlanar point = new MarPointPlanar(
                            probes[probeIndex].X,
                            probes[probeIndex].Y))
                        {
                            candidate = drafting.TextIdentify(point);
                        }
                        if (candidate == null)
                        {
                            diagnostics.Append(
                                "probe=" + probeIndex +
                                "|x=" + probes[probeIndex].X +
                                "|y=" + probes[probeIndex].Y +
                                "|handle=<none>|actual=<none>|match=false;");
                        }
                        else
                        {
                            candidateText = drafting.TextPropertiesGet(candidate);
                            string actualText = candidateText == null
                                ? string.Empty
                                : candidateText.String;
                            bool exact = string.Equals(
                                actualText,
                                expectedText,
                                StringComparison.Ordinal);
                            diagnostics.Append(
                                "probe=" + probeIndex +
                                "|x=" + probes[probeIndex].X +
                                "|y=" + probes[probeIndex].Y +
                                "|handle=" + candidate.handle +
                                "|actual=" + actualText +
                                "|match=" + exact + ";");
                            if (exact && handle == null)
                            {
                                handle = candidate;
                                text = candidateText;
                                candidate = null;
                                candidateText = null;
                            }
                        }
                    }
                    finally
                    {
                        if (candidateText != null)
                        {
                            candidateText.Dispose();
                        }
                        if (candidate != null)
                        {
                            candidate.Dispose();
                        }
                    }
                    if (handle != null)
                    {
                        detail = diagnostics.ToString();
                        return true;
                    }
                    probeIndex++;
                    EnsureParserIndexAdvanced(oldProbeIndex, probeIndex);
                }
                detail = diagnostics.ToString();
                return false;
            }
            catch (Exception ex)
            {
                if (text != null)
                {
                    try
                    {
                        text.Dispose();
                    }
                    catch
                    {
                    }
                    text = null;
                }

                if (handle != null)
                {
                    try
                    {
                        handle.Dispose();
                    }
                    catch
                    {
                    }
                    handle = null;
                }

                detail += "ERROR: " + ex.Message;
                return false;
            }
        }

        private static void ApplySavedAlgorithmMove(
            MarDrafting drafting,
            MarElementHandle handle,
            double deltaX,
            double deltaY)
        {
            if (handle == null)
            {
                throw new InvalidDataException("saved text handle is null");
            }

            using (MarVectorPlanar vector =
                new MarVectorPlanar(deltaX, deltaY))
            {
                MarTransformationPlanar transformation =
                    new MarTransformationPlanar();

                try
                {
                    transformation.Translate(vector);
                    drafting.ElementTransform(handle, transformation);
                }
                finally
                {
                    IDisposable disposable =
                        transformation as IDisposable;
                    if (disposable != null)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        private static int CountBatchStatus(
            List<BatchMoveItem> items,
            string status)
        {
            int count = 0;
            int index = 0;
            while (index < items.Count)
            {
                int oldIndex = index;
                if (string.Equals(
                    items[index].PrecheckStatus,
                    status,
                    StringComparison.Ordinal))
                {
                    count++;
                }
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            return count;
        }

        private static string FindFirstFailedHandle(
            List<BatchMoveItem> items)
        {
            int index = 0;
            while (index < items.Count)
            {
                int oldIndex = index;
                if (items[index].PrecheckStatus == "FAILED_PRECHECK")
                {
                    return items[index].Data.Handle;
                }
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            return string.Empty;
        }

        private static string FindFirstApplyFailedHandle(
            List<BatchMoveItem> items)
        {
            int index = 0;
            while (index < items.Count)
            {
                int oldIndex = index;
                if (items[index].ApplyStatus == "FAILED")
                {
                    return items[index].Data.Handle;
                }
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
            return string.Empty;
        }

        private static void DisposeBatchMoveItems(
            List<BatchMoveItem> items)
        {
            int index = 0;
            while (index < items.Count)
            {
                int oldIndex = index;
                BatchMoveItem item = items[index];
                if (item.TextObject != null)
                {
                    try
                    {
                        item.TextObject.Dispose();
                    }
                    catch
                    {
                    }
                    item.TextObject = null;
                }
                if (item.Handle != null)
                {
                    try
                    {
                        item.Handle.Dispose();
                    }
                    catch
                    {
                    }
                    item.Handle = null;
                }
                index++;
                EnsureParserIndexAdvanced(oldIndex, index);
            }
        }

        private static void WriteApplyAllMovesLog(
            string logPath,
            DateTime startTime,
            DateTime endTime,
            string resultJsonPath,
            BatchAlgorithmData batch,
            int movedCount,
            int rollbackSuccess,
            int rollbackFailed,
            string executionError)
        {
            using (StreamWriter writer = new StreamWriter(
                logPath,
                false,
                new UTF8Encoding(false)))
            {
                writer.WriteLine("Start time: " + startTime.ToString("o"));
                writer.WriteLine("End time: " + endTime.ToString("o"));
                writer.WriteLine("JSON file: " + resultJsonPath);
                writer.WriteLine("JSON total records: " + batch.JsonRecordCount);
                writer.WriteLine("is_move=true: " + batch.MoveItems.Count);
                writer.WriteLine("is_move=false: " + batch.SkippedCount);
                writer.WriteLine("READY: " + CountBatchStatus(batch.MoveItems, "READY"));
                writer.WriteLine("ALREADY_APPLIED: " + CountBatchStatus(batch.MoveItems, "ALREADY_APPLIED"));
                writer.WriteLine("FAILED_PRECHECK: " + CountBatchStatus(batch.MoveItems, "FAILED_PRECHECK"));
                writer.WriteLine("Actual moved: " + movedCount);
                writer.WriteLine("Rollback success: " + rollbackSuccess);
                writer.WriteLine("Rollback failed: " + rollbackFailed);
                writer.WriteLine("Execution error: " + (executionError ?? string.Empty));
                writer.WriteLine();

                int index = 0;
                while (index < batch.AllItems.Count)
                {
                    int oldIndex = index;
                    BatchMoveItem item = batch.AllItems[index];
                    AlgorithmMoveData data = item.Data;
                    writer.WriteLine(
                        "handle=" + (data.Handle ?? string.Empty) +
                        " | text_str=" + (data.Text ?? string.Empty) +
                        " | origin=(" + data.OriginX.ToString(CultureInfo.InvariantCulture) +
                        "," + data.OriginY.ToString(CultureInfo.InvariantCulture) + ")" +
                        " | expectedMoved=(" +
                        (data.OriginX + data.DeltaX).ToString(CultureInfo.InvariantCulture) +
                        "," + (data.OriginY + data.DeltaY).ToString(CultureInfo.InvariantCulture) + ")" +
                        " | new_center=(" + data.NewX.ToString(CultureInfo.InvariantCulture) +
                        "," + data.NewY.ToString(CultureInfo.InvariantCulture) + ")" +
                        " | dx=" + data.DeltaX.ToString(CultureInfo.InvariantCulture) +
                        " | dy=" + data.DeltaY.ToString(CultureInfo.InvariantCulture) +
                        " | precheck=" + (item.PrecheckStatus ?? string.Empty) +
                        " | apply=" + (item.ApplyStatus ?? string.Empty) +
                        " | failure=" + (item.Failure ?? string.Empty));
                    if (!string.IsNullOrEmpty(item.OriginResult))
                    {
                        writer.WriteLine("  origin_result=" + item.OriginResult);
                    }
                    if (!string.IsNullOrEmpty(item.ExpectedMovedResult))
                    {
                        writer.WriteLine("  expectedMoved_result=" + item.ExpectedMovedResult);
                    }
                    if (!string.IsNullOrEmpty(item.NewCenterResult))
                    {
                        writer.WriteLine("  new_center_result=" + item.NewCenterResult);
                    }
                    if (!string.IsNullOrEmpty(item.RollbackStatus))
                    {
                        writer.WriteLine("  rollback=" + item.RollbackStatus);
                    }
                    index++;
                    EnsureParserIndexAdvanced(oldIndex, index);
                }
            }
        }

        private static bool TryLoadFirstAlgorithmMove(
            string resultJsonPath,
            out AlgorithmMoveData move,
            out string error)
        {
            move = null;
            error = null;

            try
            {
                if (string.IsNullOrEmpty(resultJsonPath))
                {
                    error = "ERROR: resultJsonPath is empty";
                    return false;
                }

                if (!File.Exists(resultJsonPath))
                {
                    error = "ERROR: result JSON file not found";
                    return false;
                }

                JavaScriptSerializer serializer =
                    new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                string json = File.ReadAllText(
                    resultJsonPath,
                    new UTF8Encoding(false));
                object parsed = serializer.DeserializeObject(json);
                IDictionary<string, object> root =
                    parsed as IDictionary<string, object>;
                IList<object> results = GetObjectList(
                    root,
                    "results");

                if (results == null)
                {
                    error = "ERROR: results is missing or invalid";
                    return false;
                }

                int resultIndex = 0;

                while (resultIndex < results.Count)
                {
                    int oldResultIndex = resultIndex;
                    IDictionary<string, object> result =
                        results[resultIndex] as IDictionary<string, object>;
                    IList<object> layouts = GetObjectList(
                        result,
                        "layout");

                    if (layouts != null)
                    {
                        int layoutIndex = 0;

                        while (layoutIndex < layouts.Count)
                        {
                            int oldLayoutIndex = layoutIndex;
                            IDictionary<string, object> layout =
                                layouts[layoutIndex] as IDictionary<string, object>;
                            IList<object> moves = GetObjectList(
                                layout,
                                "moves");

                            if (moves != null)
                            {
                                int moveIndex = 0;

                                while (moveIndex < moves.Count)
                                {
                                    int oldMoveIndex = moveIndex;
                                    IDictionary<string, object> moveObject =
                                        moves[moveIndex] as IDictionary<string, object>;
                                    bool isMove;

                                    if (TryGetBoolean(
                                        moveObject,
                                        "is_move",
                                        out isMove) &&
                                        isMove)
                                    {
                                        move = ParseAlgorithmMove(moveObject);
                                        return true;
                                    }

                                    moveIndex++;

                                    if (moveIndex <= oldMoveIndex)
                                    {
                                        error =
                                            "ERROR: parser index did not advance at index=" +
                                            oldMoveIndex;
                                        return false;
                                    }
                                }
                            }

                            layoutIndex++;

                            if (layoutIndex <= oldLayoutIndex)
                            {
                                error =
                                    "ERROR: parser index did not advance at index=" +
                                    oldLayoutIndex;
                                return false;
                            }
                        }
                    }

                    resultIndex++;

                    if (resultIndex <= oldResultIndex)
                    {
                        error =
                            "ERROR: parser index did not advance at index=" +
                            oldResultIndex;
                        return false;
                    }
                }

                error = "ERROR: no movable move found";
                return false;
            }
            catch (Exception ex)
            {
                error = "ERROR: " +
                    ex.GetType().FullName + ": " +
                    ex.Message;
                return false;
            }
        }

        private static AlgorithmMoveData ParseAlgorithmMove(
            IDictionary<string, object> moveObject)
        {
            if (moveObject == null)
            {
                throw new InvalidDataException("move is invalid");
            }

            AlgorithmMoveData move = new AlgorithmMoveData();
            move.Handle = GetRequiredString(moveObject, "handle");
            move.Text = GetRequiredString(moveObject, "text_str");
            double[] origin = GetRequiredPair(
                moveObject,
                "origin_center");
            double[] newCenter = GetRequiredPair(
                moveObject,
                "new_center");
            move.OriginX = origin[0];
            move.OriginY = origin[1];
            move.NewX = newCenter[0];
            move.NewY = newCenter[1];
            move.DeltaX = GetRequiredDouble(moveObject, "dx");
            move.DeltaY = GetRequiredDouble(moveObject, "dy");
            move.LeaderStart = GetRequiredPair(
                moveObject,
                "leader_start");
            move.LeaderEnd = GetRequiredPair(
                moveObject,
                "leader_end");
            return move;
        }

        private static IList<object> GetObjectList(
            IDictionary<string, object> dictionary,
            string fieldName)
        {
            if (dictionary == null ||
                !dictionary.ContainsKey(fieldName) ||
                dictionary[fieldName] == null)
            {
                return null;
            }

            return dictionary[fieldName] as IList<object>;
        }

        private static bool TryGetBoolean(
            IDictionary<string, object> dictionary,
            string fieldName,
            out bool value)
        {
            value = false;

            if (dictionary == null ||
                !dictionary.ContainsKey(fieldName) ||
                dictionary[fieldName] == null)
            {
                return false;
            }

            object raw = dictionary[fieldName];

            if (raw is bool)
            {
                value = (bool)raw;
                return true;
            }

            return bool.TryParse(
                Convert.ToString(raw, CultureInfo.InvariantCulture),
                out value);
        }

        private static string GetRequiredString(
            IDictionary<string, object> dictionary,
            string fieldName)
        {
            if (dictionary == null ||
                !dictionary.ContainsKey(fieldName) ||
                dictionary[fieldName] == null)
            {
                throw new InvalidDataException(
                    fieldName + " is missing");
            }

            string value = Convert.ToString(
                dictionary[fieldName],
                CultureInfo.InvariantCulture);

            if (value == null)
            {
                throw new InvalidDataException(
                    fieldName + " is invalid");
            }

            return value;
        }

        private static double GetRequiredDouble(
            IDictionary<string, object> dictionary,
            string fieldName)
        {
            if (dictionary == null ||
                !dictionary.ContainsKey(fieldName) ||
                dictionary[fieldName] == null)
            {
                throw new InvalidDataException(
                    fieldName + " is missing");
            }

            try
            {
                return Convert.ToDouble(
                    dictionary[fieldName],
                    CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    fieldName + " is invalid: " + ex.Message);
            }
        }

        private static double[] GetRequiredPair(
            IDictionary<string, object> dictionary,
            string fieldName)
        {
            IList<object> values = GetObjectList(dictionary, fieldName);

            if (values == null || values.Count < 2)
            {
                throw new InvalidDataException(
                    fieldName + " must contain two numbers");
            }

            double[] pair = new double[2];
            pair[0] = Convert.ToDouble(
                values[0],
                CultureInfo.InvariantCulture);
            pair[1] = Convert.ToDouble(
                values[1],
                CultureInfo.InvariantCulture);
            return pair;
        }

        private sealed class AlgorithmMoveData
        {
            public string Handle;
            public string Layer;
            public string Text;
            public double OriginX;
            public double OriginY;
            public double NewX;
            public double NewY;
            public double DeltaX;
            public double DeltaY;
            public double[] LeaderStart;
            public double[] LeaderEnd;
            public bool NeedsPlan;
        }

        private sealed class BatchAlgorithmData
        {
            public readonly List<BatchMoveItem> AllItems;
            public readonly List<BatchMoveItem> MoveItems;
            public int SkippedCount;
            public int JsonRecordCount;

            public BatchAlgorithmData()
            {
                AllItems = new List<BatchMoveItem>();
                MoveItems = new List<BatchMoveItem>();
            }
        }

        private sealed class BatchMoveItem
        {
            public AlgorithmMoveData Data;
            public MarElementHandle Handle;
            public MarText TextObject;
            public string PrecheckStatus;
            public string ApplyStatus;
            public string Failure;
            public string OriginResult;
            public string ExpectedMovedResult;
            public string NewCenterResult;
            public string RollbackStatus;
        }

        private sealed class AlgorithmAnnotationGeometryItem
        {
            public BatchMoveItem Move;
            public DxfEntityInfo SourceText;
            public DxfEntityInfo SourceUnderline;
            public DxfEntityInfo SourceOldLeader;
            public MarElementHandle OriginalUnderline;
            public MarElementHandle MovedUnderline;
            public MarElementHandle OldLeader;
            public MarElementHandle NewLeader;
            public MarElementHandle CreatedNewLeader;
            public MarLayer OldLeaderLayer;
            public int OldLeaderLayerId = -1;
            public int CreatedNewLeaderLayerId = int.MinValue;
            public bool HasOldLeaderLayerId;
            public MarColour OldLeaderColour;
            public MarLinetype OldLeaderLinetype;
            public bool UnderlineMoved;
            public bool OldLeaderDeleted;
            public bool OldLeaderAbsenceVerified;
            public string OldLeaderAbsenceVerificationDetail;
            public OldLeaderAbsenceResult ImmediateOldLeaderAbsenceResult =
                OldLeaderAbsenceResult.NotRun;
            public string ImmediateOldLeaderAbsenceDetail;
            public OldLeaderAbsenceResult FinalOldLeaderAbsenceResult =
                OldLeaderAbsenceResult.NotRun;
            public string FinalOldLeaderAbsenceDetail;
            public bool ReusedHandleDetected;
            public int ReusedHandleId = int.MinValue;
            public int OldLeaderHandleBeforeDelete = int.MinValue;
            public int CreatedNewLeaderHandleId = int.MinValue;
            public int OldLeaderHandleIdBeforeDelete = int.MinValue;
            public bool NewLeaderCreated;
            public bool NewLeaderCreationAttempted;
            public bool UnderlineMoveAttempted;
            public bool OldLeaderDeleteAttempted;
            public bool RecordFullyApplied;
            public string LayerInheritanceStatus;
            public string UnderlineMoveStatus;
            public string OldLeaderDeleteStatus;
            public string ImmediateAbsenceVerificationStatus;
            public string TextStatus;
            public string TextMatchDetail;
            public bool ApplyReceiptFound;
            public int ExpectedDeletedOldLeaderHandleId = int.MinValue;
            public int ReceiptCreatedNewLeaderHandleId = int.MinValue;
            public int ReceiptMovedUnderlineHandleId = int.MinValue;
            public string PreviewIdentityStatus;
            public string UnderlineStatus;
            public string OldLeaderStatus;
            public string NewLeaderStatus;
            public string OriginalUnderlineDetail;
            public string MovedUnderlineDetail;
            public string OldLeaderDetail;
            public string NewLeaderDetail;
            public string Status;
            public string ApplyStatus;
            public string RollbackStatus;
            public string PostVerificationStatus = "NotRun";
            public string PostVerificationDetail;
            public string ApplyTrace;
            public string RollbackTrace;
            public string Failure;
        }

        private sealed class GeometryProbeCandidate
        {
            public MarElementHandle Handle;
            public int HitCount;
        }

        private static DxfFullScanResult ScanAllDxfEntities(
            byte[] data,
            Encoding encoding)
        {
            DxfFullScanResult result = new DxfFullScanResult();
            DxfOpenEntity openEntity = null;
            bool inEntities = false;
            bool sectionPending = false;
            int offset = 0;

            while (offset < data.Length)
            {
                int oldIndex = offset;
                int firstStart = offset;
                int firstEnd = FindLineEnd(data, firstStart);

                if (firstEnd <= offset)
                {
                    throw new DxfParserIndexException(oldIndex);
                }

                if (firstEnd >= data.Length)
                {
                    offset = firstEnd;
                    EnsureParserIndexAdvanced(oldIndex, offset);
                    break;
                }

                int secondStart = firstEnd;
                int secondEnd = FindLineEnd(data, secondStart);

                if (secondEnd <= secondStart)
                {
                    offset = data.Length;
                    EnsureParserIndexAdvanced(oldIndex, offset);
                    break;
                }

                string codeText = GetLineText(
                    data,
                    firstStart,
                    firstEnd,
                    encoding);
                int code;

                if (!int.TryParse(
                    codeText.Trim(),
                    System.Globalization.NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out code))
                {
                    offset = secondEnd;
                    EnsureParserIndexAdvanced(oldIndex, offset);
                    continue;
                }

                string value = GetLineText(
                    data,
                    secondStart,
                    secondEnd,
                    encoding).Trim();

                if (inEntities && code == 0)
                {
                    if (string.Equals(
                        value,
                        "ENDSEC",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        FinalizeAllOpenEntity(
                            openEntity,
                            firstStart,
                            result.Entities);
                        openEntity = null;
                        inEntities = false;
                        result.EndSectionFound = true;
                    }
                    else
                    {
                        FinalizeAllOpenEntity(
                            openEntity,
                            firstStart,
                            result.Entities);
                        openEntity =
                            new DxfOpenEntity(firstStart, value);
                    }
                }
                else if (!inEntities &&
                    code == 0 &&
                    string.Equals(
                        value,
                        "SECTION",
                        StringComparison.OrdinalIgnoreCase))
                {
                    sectionPending = true;
                }
                else if (sectionPending && code == 2)
                {
                    if (string.Equals(
                        value,
                        "ENTITIES",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        inEntities = true;
                        sectionPending = false;
                        result.EntitiesSectionFound = true;
                    }
                    else
                    {
                        sectionPending = false;
                    }
                }
                else if (sectionPending && code != 2)
                {
                    sectionPending = false;
                }

                if (inEntities &&
                    openEntity != null &&
                    code == 5 &&
                    openEntity.Handle == null)
                {
                    openEntity.Handle = value;
                }
                else if (inEntities &&
                    openEntity != null &&
                    code == 8 &&
                    openEntity.LayerName == null)
                {
                    openEntity.LayerName = value;
                }

                if (inEntities && openEntity != null)
                {
                    double coordinate;

                    if (code == 1)
                    {
                        openEntity.TextValue = value;
                    }
                    else if (code == 10 &&
                        double.TryParse(
                            value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out coordinate))
                    {
                        if (string.Equals(
                            openEntity.EntityType,
                            "LINE",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            openEntity.PendingVertexX = coordinate;
                            openEntity.HasPendingVertexX = true;
                        }
                        else if (string.Equals(
                            openEntity.EntityType,
                            "LWPOLYLINE",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            openEntity.PendingVertexX = coordinate;
                            openEntity.HasPendingVertexX = true;
                        }
                    }
                    else if (code == 20 &&
                        openEntity.HasPendingVertexX &&
                        double.TryParse(
                            value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out coordinate))
                    {
                        if (string.Equals(
                            openEntity.EntityType,
                            "LINE",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            openEntity.LineStart = new DxfPoint(
                                openEntity.PendingVertexX,
                                coordinate);
                        }
                        else if (string.Equals(
                            openEntity.EntityType,
                            "LWPOLYLINE",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            openEntity.Vertices.Add(
                                new DxfPoint(
                                    openEntity.PendingVertexX,
                                    coordinate));
                        }
                        openEntity.HasPendingVertexX = false;
                    }
                    else if (code == 11 &&
                        double.TryParse(
                            value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out coordinate))
                    {
                        openEntity.PendingLineEndX = coordinate;
                        openEntity.HasPendingLineEndX = true;
                    }
                    else if (code == 21 &&
                        openEntity.HasPendingLineEndX &&
                        double.TryParse(
                            value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out coordinate))
                    {
                        openEntity.LineEnd = new DxfPoint(
                            openEntity.PendingLineEndX,
                            coordinate);
                        openEntity.HasPendingLineEndX = false;
                    }
                }

                offset = secondEnd;
                EnsureParserIndexAdvanced(oldIndex, offset);
            }

            FinalizeAllOpenEntity(
                openEntity,
                data.Length,
                result.Entities);
            return result;
        }

        private static void FinalizeAllOpenEntity(
            DxfOpenEntity openEntity,
            int endOffset,
            List<DxfEntityInfo> entities)
        {
            if (openEntity == null)
            {
                return;
            }

            entities.Add(
                new DxfEntityInfo(
                    openEntity.StartOffset,
                    endOffset,
                    openEntity.EntityType,
                    openEntity.LayerName,
                    openEntity.Handle,
                    openEntity.TextValue,
                    new List<DxfPoint>(openEntity.Vertices),
                    openEntity.LineStart,
                    openEntity.LineEnd));
        }

        private static bool IsAutoBoxOrLeaderLinesLayer(
            string layerName)
        {
            return string.Equals(
                    layerName,
                    "AUTO_BOX",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    layerName,
                    "LEADER_LINES",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureParserIndexAdvanced(
            int oldIndex,
            int newIndex)
        {
            if (newIndex <= oldIndex)
            {
                throw new DxfParserIndexException(oldIndex);
            }
        }

        private static int FindLineEnd(byte[] data, int startOffset)
        {
            int offset = startOffset;

            while (offset < data.Length &&
                data[offset] != 10 &&
                data[offset] != 13)
            {
                int oldIndex = offset;
                offset++;
                EnsureParserIndexAdvanced(oldIndex, offset);
            }

            if (offset >= data.Length)
            {
                return offset;
            }

            if (data[offset] == 13 &&
                offset + 1 < data.Length &&
                data[offset + 1] == 10)
            {
                return offset + 2;
            }

            return offset + 1;
        }

        private static string GetLineText(
            byte[] data,
            int startOffset,
            int endOffset,
            Encoding encoding)
        {
            int contentEnd = endOffset;

            if (contentEnd > startOffset &&
                data[contentEnd - 1] == 10)
            {
                contentEnd--;
            }

            if (contentEnd > startOffset &&
                data[contentEnd - 1] == 13)
            {
                contentEnd--;
            }

            return encoding.GetString(
                data,
                startOffset,
                contentEnd - startOffset);
        }

        private static void WriteDxfWithoutEntities(
            byte[] data,
            List<DxfEntityInfo> entities,
            string outputPath)
        {
            using (FileStream stream = new FileStream(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                int currentOffset = 0;

                int entityIndex = 0;

                while (entityIndex < entities.Count)
                {
                    int oldEntityIndex = entityIndex;
                    DxfEntityInfo entity = entities[entityIndex];

                    if (entity.StartOffset < currentOffset ||
                        entity.EndOffset < entity.StartOffset ||
                        entity.EndOffset > data.Length)
                    {
                        throw new InvalidDataException(
                            "Invalid entity byte range");
                    }

                    WriteBytes(
                        stream,
                        data,
                        currentOffset,
                        entity.StartOffset - currentOffset);
                    currentOffset = entity.EndOffset;
                    entityIndex++;
                    EnsureParserIndexAdvanced(
                        oldEntityIndex,
                        entityIndex);
                }

                WriteBytes(
                    stream,
                    data,
                    currentOffset,
                    data.Length - currentOffset);
            }
        }

        private static void WriteBytes(
            FileStream stream,
            byte[] data,
            int offset,
            int count)
        {
            if (count > 0)
            {
                stream.Write(data, offset, count);
            }
        }

        private static void AddCount(
            Dictionary<string, int> counts,
            string key)
        {
            string actualKey = key;

            if (string.IsNullOrEmpty(actualKey))
            {
                actualKey = "<missing>";
            }

            int count;

            if (counts.TryGetValue(actualKey, out count))
            {
                counts[actualKey] = count + 1;
            }
            else
            {
                counts.Add(actualKey, 1);
            }
        }

        private static void DeleteGeneratedDxf(
            bool outputCreated,
            string outputPath)
        {
            if (!outputCreated ||
                string.IsNullOrEmpty(outputPath) ||
                !File.Exists(outputPath))
            {
                return;
            }

            try
            {
                File.Delete(outputPath);
            }
            catch
            {
            }
        }

        private static void WritePrepareDxfLog(
            string logPath,
            string inputPath,
            string outputPath,
            int originalCount,
            int removedCount,
            int keptCount,
            Dictionary<string, int> deletedLayerCounts,
            Dictionary<string, int> deletedTypeCounts,
            int outputMinus11Count,
            string validationResult)
        {
            using (StreamWriter writer = new StreamWriter(
                logPath,
                false,
                new UTF8Encoding(false)))
            {
                writer.WriteLine("Input file: " + inputPath);
                writer.WriteLine("Output file: " + outputPath);
                writer.WriteLine(
                    "Original entity total: " + originalCount);
                writer.WriteLine("Deleted entity total: " + removedCount);
                writer.WriteLine("Kept entity total: " + keptCount);
                writer.WriteLine(
                    "Output -11 layer entity total: " +
                    outputMinus11Count);
                writer.WriteLine("Validation result: " + validationResult);
                writer.WriteLine();
                writer.WriteLine("Deleted entities by layer:");

                foreach (KeyValuePair<string, int> item in
                    deletedLayerCounts)
                {
                    writer.WriteLine(
                        "  " + item.Key + ": " + item.Value);
                }

                writer.WriteLine();
                writer.WriteLine("Deleted entities by type:");

                foreach (KeyValuePair<string, int> item in
                    deletedTypeCounts)
                {
                    writer.WriteLine(
                        "  " + item.Key + ": " + item.Value);
                }
            }
        }

        private sealed class DxfFullScanResult
        {
            public readonly List<DxfEntityInfo> Entities;
            public bool EntitiesSectionFound;
            public bool EndSectionFound;

            public DxfFullScanResult()
            {
                Entities = new List<DxfEntityInfo>();
            }
        }

        private sealed class DxfOpenEntity
        {
            public readonly int StartOffset;
            public readonly string EntityType;
            public readonly List<DxfPoint> Vertices;
            public string LayerName;
            public string Handle;
            public string TextValue;
            public DxfPoint LineStart;
            public DxfPoint LineEnd;
            public double PendingVertexX;
            public bool HasPendingVertexX;
            public double PendingLineEndX;
            public bool HasPendingLineEndX;

            public DxfOpenEntity(
                int startOffset,
                string entityType)
            {
                StartOffset = startOffset;
                EntityType = entityType;
                Vertices = new List<DxfPoint>();
            }
        }

        private sealed class DxfParserIndexException : Exception
        {
            public readonly int Index;

            public DxfParserIndexException(int index)
                : base("parser index did not advance")
            {
                Index = index;
            }
        }

        private sealed class DxfEntityInfo
        {
            public readonly int StartOffset;
            public readonly int EndOffset;
            public readonly string EntityType;
            public readonly string LayerName;
            public readonly string Handle;
            public readonly string TextValue;
            public readonly List<DxfPoint> Vertices;
            public readonly DxfPoint LineStart;
            public readonly DxfPoint LineEnd;

            public DxfEntityInfo(
                int startOffset,
                int endOffset,
                string entityType,
                string layerName,
                string handle,
                string textValue,
                List<DxfPoint> vertices,
                DxfPoint lineStart,
                DxfPoint lineEnd)
            {
                StartOffset = startOffset;
                EndOffset = endOffset;
                EntityType = entityType;
                LayerName = layerName;
                Handle = handle;
                TextValue = textValue;
                Vertices = vertices;
                LineStart = lineStart;
                LineEnd = lineEnd;
            }
        }

        private sealed class DxfPoint
        {
            public readonly double X;
            public readonly double Y;

            public DxfPoint(double x, double y)
            {
                X = x;
                Y = y;
            }
        }
    }
}
