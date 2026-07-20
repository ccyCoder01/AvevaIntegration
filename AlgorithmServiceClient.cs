using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace AvevaIntegration
{
    internal class AlgorithmServiceClient
    {
        private static readonly object logLock = new object();
        private readonly string baseUrl;

        public AlgorithmServiceClient(string configuredBaseUrl)
        {
            baseUrl = configuredBaseUrl;
        }

        public string UploadAlgorithmTask(
            string filePath,
            string username,
            string projectName,
            string extraParamsJson)
        {
            return UploadAlgorithmTask(filePath, username, projectName,
                extraParamsJson, null, null);
        }

        internal string UploadAlgorithmTask(
            string filePath,
            string username,
            string projectName,
            string extraParamsJson,
            string sharedLogPath,
            string runId)
        {
            string responseJson = string.Empty;
            string httpStatus = "N/A";

            try
            {
                ValidateUploadInput(
                    filePath,
                    username,
                    projectName);

                if (string.IsNullOrEmpty(extraParamsJson))
                {
                    extraParamsJson = "{}";
                }

                string url = baseUrl + "/api/v1/tasks/upload";
                string boundary =
                    "---------------------------" +
                    DateTime.Now.Ticks.ToString("x", CultureInfo.InvariantCulture);
                string fileName = Path.GetFileName(filePath);
                string fileHeader = BuildFileHeader(
                    boundary,
                    fileName);
                string[] fieldNames =
                    new string[] { "username", "project_name", "extra_params" };
                string[] fieldValues =
                    new string[] { username, projectName, extraParamsJson };
                long contentLength = GetUtf8Bytes(
                    BuildFieldHeader(boundary, fieldNames[0])).LongLength +
                    GetUtf8Bytes(username).LongLength + 2 +
                    GetUtf8Bytes(
                        BuildFieldHeader(boundary, fieldNames[1])).LongLength +
                    GetUtf8Bytes(projectName).LongLength + 2 +
                    GetUtf8Bytes(
                        BuildFieldHeader(boundary, fieldNames[2])).LongLength +
                    GetUtf8Bytes(extraParamsJson).LongLength + 2 +
                    GetUtf8Bytes(fileHeader).LongLength +
                    new FileInfo(filePath).Length +
                    2 +
                    GetUtf8Bytes("--" + boundary + "--\r\n").LongLength;

                HttpWebRequest request =
                    (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.Timeout = 120000;
                request.ReadWriteTimeout = 120000;
                request.KeepAlive = false;
                request.Proxy = null;
                request.AllowWriteStreamBuffering = true;
                request.Accept = "application/json";
                request.ContentType =
                    "multipart/form-data; boundary=" + boundary;
                request.ContentLength = contentLength;

                using (Stream requestStream = request.GetRequestStream())
                {
                    WriteUtf8(requestStream, BuildFieldHeader(
                        boundary,
                        fieldNames[0]));
                    WriteUtf8(requestStream, fieldValues[0]);
                    WriteUtf8(requestStream, "\r\n");
                    WriteUtf8(requestStream, BuildFieldHeader(
                        boundary,
                        fieldNames[1]));
                    WriteUtf8(requestStream, fieldValues[1]);
                    WriteUtf8(requestStream, "\r\n");
                    WriteUtf8(requestStream, BuildFieldHeader(
                        boundary,
                        fieldNames[2]));
                    WriteUtf8(requestStream, fieldValues[2]);
                    WriteUtf8(requestStream, "\r\n");
                    WriteUtf8(requestStream, fileHeader);
                    WriteFileBytes(requestStream, filePath);
                    WriteUtf8(requestStream, "\r\n");
                    WriteUtf8(requestStream, "--" + boundary + "--\r\n");
                }

                using (HttpWebResponse response =
                    (HttpWebResponse)request.GetResponse())
                {
                    httpStatus = ((int)response.StatusCode).ToString(
                        CultureInfo.InvariantCulture);
                    responseJson = ReadResponse(response);
                }

                WriteUploadLog(
                    filePath,
                    fileName,
                    username,
                    projectName,
                    url,
                    httpStatus,
                    responseJson,
                    sharedLogPath,
                    runId,
                    IsSuccessStatus(httpStatus)
                        ? "DXF_UPLOAD_COMPLETED"
                        : "DXF_UPLOAD_FAILED");

                if (!IsSuccessStatus(httpStatus))
                {
                    return "ERROR: HTTP=" + httpStatus +
                        " | " + responseJson;
                }

                string taskId;

                if (!TryGetJsonString(
                    responseJson,
                    "task_id",
                    out taskId) ||
                    string.IsNullOrEmpty(taskId))
                {
                    return "ERROR: HTTP=" + httpStatus +
                        " | task_id missing | " + responseJson;
                }

                return taskId;
            }
            catch (WebException ex)
            {
                responseJson = ReadWebExceptionResponse(
                    ex,
                    ref httpStatus);
                WriteUploadLog(
                    filePath,
                    SafeFileName(filePath),
                    username,
                    projectName,
                    baseUrl + "/api/v1/tasks/upload",
                    httpStatus,
                    responseJson,
                    sharedLogPath,
                    runId,
                    "DXF_UPLOAD_FAILED");
                return "ERROR: HTTP=" + httpStatus +
                    " | " + responseJson;
            }
            catch (Exception ex)
            {
                WriteUploadLog(
                    filePath,
                    SafeFileName(filePath),
                    username,
                    projectName,
                    baseUrl + "/api/v1/tasks/upload",
                    httpStatus,
                    responseJson,
                    sharedLogPath,
                    runId,
                    "DXF_UPLOAD_FAILED");
                return "ERROR: " +
                    ex.GetType().FullName + ": " +
                    ex.Message;
            }
        }

        public string QueryAlgorithmTask(
            string taskId,
            string outputJsonPath)
        {
            return QueryAlgorithmTask(taskId, outputJsonPath, null, null);
        }

        internal string QueryAlgorithmTask(
            string taskId,
            string outputJsonPath,
            string sharedLogPath,
            string runId)
        {
            string responseJson = string.Empty;
            string httpStatus = "N/A";
            string url = baseUrl +
                "/api/v1/tasks/" +
                Uri.EscapeDataString(taskId ?? string.Empty) +
                "/status";

            try
            {
                if (string.IsNullOrEmpty(taskId))
                {
                    throw new ArgumentException("taskId is empty");
                }

                if (string.IsNullOrEmpty(outputJsonPath))
                {
                    throw new ArgumentException("outputJsonPath is empty");
                }

                EnsureParentDirectory(outputJsonPath);

                HttpWebRequest request =
                    (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 60000;
                request.ReadWriteTimeout = 60000;
                request.KeepAlive = false;
                request.Proxy = null;
                request.Accept = "application/json";

                using (HttpWebResponse response =
                    (HttpWebResponse)request.GetResponse())
                {
                    httpStatus = ((int)response.StatusCode).ToString(
                        CultureInfo.InvariantCulture);
                    responseJson = ReadResponse(response);
                }

                WriteQueryLog(
                    outputJsonPath,
                    url,
                    taskId,
                    httpStatus,
                    string.Empty,
                    responseJson,
                    sharedLogPath,
                    runId);

                if (!IsSuccessStatus(httpStatus))
                {
                    return "ERROR: HTTP=" + httpStatus +
                        " | " + responseJson;
                }

                string status;

                if (!TryGetJsonString(
                    responseJson,
                    "status",
                    out status) ||
                    string.IsNullOrEmpty(status))
                {
                    return "ERROR: HTTP=" + httpStatus +
                        " | status missing | " + responseJson;
                }

                EnsureParentDirectory(outputJsonPath);
                File.WriteAllText(
                    outputJsonPath,
                    responseJson,
                    new UTF8Encoding(false));

                if (string.Equals(status, "QUEUED", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "PROCESSING", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "RUNNING", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateQueryLogStatus(
                        outputJsonPath,
                        url,
                        taskId,
                        httpStatus,
                        status,
                        responseJson,
                        sharedLogPath,
                        runId);
                    return status.ToUpperInvariant();
                }

                if (string.Equals(
                    status,
                    "FAILED",
                    StringComparison.OrdinalIgnoreCase))
                {
                    string errorCode;
                    string errorMessage;
                    TryGetJsonString(
                        responseJson,
                        "error_code",
                        out errorCode);
                    TryGetJsonString(
                        responseJson,
                        "error_message",
                        out errorMessage);
                    UpdateQueryLogStatus(
                        outputJsonPath,
                        url,
                        taskId,
                        httpStatus,
                        status,
                        responseJson,
                        sharedLogPath,
                        runId);
                    return "ERROR: FAILED | error_code=" +
                        (errorCode ?? string.Empty) +
                        " | error_message=" +
                        (errorMessage ?? string.Empty);
                }

                UpdateQueryLogStatus(
                    outputJsonPath,
                    url,
                    taskId,
                    httpStatus,
                    status,
                    responseJson,
                    sharedLogPath,
                    runId);
                return "ERROR: HTTP=" + httpStatus +
                    " | unknown status | " + responseJson;
            }
            catch (WebException ex)
            {
                responseJson = ReadWebExceptionResponse(
                    ex,
                    ref httpStatus);
                WriteQueryLog(
                    outputJsonPath,
                    url,
                    taskId,
                    httpStatus,
                    string.Empty,
                    responseJson,
                    sharedLogPath,
                    runId);
                return "ERROR: HTTP=" + httpStatus +
                    " | " + responseJson;
            }
            catch (Exception ex)
            {
                WriteQueryLog(
                    outputJsonPath,
                    url,
                    taskId,
                    httpStatus,
                    string.Empty,
                    responseJson,
                    sharedLogPath,
                    runId);
                return "ERROR: " +
                    ex.GetType().FullName + ": " +
                    ex.Message;
            }
        }

        private static void ValidateUploadInput(
            string filePath,
            string username,
            string projectName)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("filePath is empty");
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "DXF file was not found",
                    filePath);
            }

            if (!string.Equals(
                Path.GetExtension(filePath),
                ".dxf",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "filePath must have a .dxf extension");
            }

            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("username is empty");
            }

            if (string.IsNullOrEmpty(projectName))
            {
                throw new ArgumentException("projectName is empty");
            }
        }

        private static string BuildFieldHeader(
            string boundary,
            string fieldName)
        {
            return "--" + boundary + "\r\n" +
                "Content-Disposition: form-data; name=\"" +
                fieldName + "\"\r\n\r\n";
        }

        private static string BuildFileHeader(
            string boundary,
            string fileName)
        {
            string safeFileName = fileName.Replace("\"", "");
            return "--" + boundary + "\r\n" +
                "Content-Disposition: form-data; name=\"files\"; filename=\"" +
                safeFileName + "\"\r\n" +
                "Content-Type: application/dxf\r\n\r\n";
        }

        private static byte[] GetUtf8Bytes(string value)
        {
            return new UTF8Encoding(false).GetBytes(value ?? string.Empty);
        }

        private static void WriteUtf8(Stream stream, string value)
        {
            byte[] bytes = GetUtf8Bytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteFileBytes(
            Stream destination,
            string filePath)
        {
            byte[] buffer = new byte[8192];

            using (FileStream source = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                int read;

                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    destination.Write(buffer, 0, read);
                }
            }
        }

        private static string ReadResponse(HttpWebResponse response)
        {
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(
                stream,
                Encoding.UTF8,
                true))
            {
                return reader.ReadToEnd();
            }
        }

        private static string ReadWebExceptionResponse(
            WebException exception,
            ref string httpStatus)
        {
            HttpWebResponse response =
                exception.Response as HttpWebResponse;

            if (response == null)
            {
                httpStatus = "N/A";
                return exception.Message;
            }

            using (response)
            {
                httpStatus = ((int)response.StatusCode).ToString(
                    CultureInfo.InvariantCulture);
                return ReadResponse(response);
            }
        }

        private static bool IsSuccessStatus(string statusCode)
        {
            int status;

            if (!int.TryParse(
                statusCode,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out status))
            {
                return false;
            }

            return status >= 200 && status < 300;
        }

        private static bool TryGetJsonString(
            string json,
            string fieldName,
            out string value)
        {
            value = null;
            JavaScriptSerializer serializer =
                new JavaScriptSerializer();
            object parsed = serializer.DeserializeObject(json);
            IDictionary<string, object> dictionary =
                parsed as IDictionary<string, object>;

            if (dictionary == null ||
                !dictionary.ContainsKey(fieldName) ||
                dictionary[fieldName] == null)
            {
                return false;
            }

            value = Convert.ToString(
                dictionary[fieldName],
                CultureInfo.InvariantCulture);
            return true;
        }

        private static void EnsureParentDirectory(string filePath)
        {
            string directory = Path.GetDirectoryName(
                Path.GetFullPath(filePath));

            if (!string.IsNullOrEmpty(directory) &&
                !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static string SafeFileName(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            return Path.GetFileName(filePath);
        }

        private static void WriteUploadLog(
            string filePath,
            string fileName,
            string username,
            string projectName,
            string url,
            string httpStatus,
            string responseJson,
            string sharedLogPath,
            string runId,
            string eventName)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            string logPath = string.IsNullOrEmpty(sharedLogPath)
                ? filePath + ".upload.log.txt"
                : sharedLogPath;
            long fileSize = 0;

            if (File.Exists(filePath))
            {
                fileSize = new FileInfo(filePath).Length;
            }

            try
            {
                lock (logLock)
                {
                    using (StreamWriter writer = new StreamWriter(
                        logPath,
                        !string.IsNullOrEmpty(sharedLogPath),
                        new UTF8Encoding(false)))
                    {
                        if (!string.IsNullOrEmpty(sharedLogPath))
                        {
                            writer.WriteLine("event=" + eventName +
                                " | timestamp=" + DateTime.Now.ToString("o", CultureInfo.InvariantCulture) +
                                " | thread_id=" + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture) +
                                " | run_id=" + (runId ?? string.Empty) +
                                " | source_dxf=" + filePath +
                                " | file_size_bytes=" + fileSize.ToString(CultureInfo.InvariantCulture) +
                                " | request_uri=" + url +
                                " | http_status=" + httpStatus +
                                " | response_summary=" + Summarize(responseJson));
                        }
                        else
                        {
                            writer.WriteLine("Time: " + DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
                            writer.WriteLine("Request URL: " + url);
                            writer.WriteLine("File path: " + filePath);
                            writer.WriteLine("File name: " + fileName);
                            writer.WriteLine("File size: " + fileSize);
                            writer.WriteLine("username: " + username);
                            writer.WriteLine("project_name: " + projectName);
                            writer.WriteLine("HTTP status: " + httpStatus);
                            writer.WriteLine("Response JSON: " + responseJson);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void WriteQueryLog(
            string outputJsonPath,
            string url,
            string taskId,
            string httpStatus,
            string status,
            string responseJson,
            string sharedLogPath,
            string runId)
        {
            if (string.IsNullOrEmpty(outputJsonPath))
            {
                return;
            }

            try
            {
                string logPath = string.IsNullOrEmpty(sharedLogPath)
                    ? outputJsonPath + ".query.log.txt" : sharedLogPath;
                lock (logLock)
                {
                    using (StreamWriter writer = new StreamWriter(
                        logPath,
                        !string.IsNullOrEmpty(sharedLogPath),
                        new UTF8Encoding(false)))
                    {
                        if (!string.IsNullOrEmpty(sharedLogPath))
                        {
                            string eventName = string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase)
                                ? "ALGORITHM_SUCCEEDED"
                                : string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase)
                                    ? "ALGORITHM_FAILED" : "ALGORITHM_QUERY";
                            writer.WriteLine("event=" + eventName +
                                " | timestamp=" + DateTime.Now.ToString("o", CultureInfo.InvariantCulture) +
                                " | thread_id=" + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture) +
                                " | run_id=" + (runId ?? string.Empty) +
                                " | task_id=" + (taskId ?? string.Empty) +
                                " | http_status=" + httpStatus +
                                " | algorithm_status=" + (status ?? string.Empty) +
                                " | response_summary=" + Summarize(responseJson));
                        }
                        else
                        {
                            writer.WriteLine("Time: " + DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
                            writer.WriteLine("Request URL: " + url);
                            writer.WriteLine("task_id: " + taskId);
                            writer.WriteLine("HTTP status: " + httpStatus);
                            writer.WriteLine("status: " + status);
                            writer.WriteLine("Response JSON: " + responseJson);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void UpdateQueryLogStatus(
            string outputJsonPath,
            string url,
            string taskId,
            string httpStatus,
            string status,
            string responseJson,
            string sharedLogPath,
            string runId)
        {
            WriteQueryLog(
                outputJsonPath,
                url,
                taskId,
                httpStatus,
                status,
                responseJson,
                sharedLogPath,
                runId);
        }

        private static string Summarize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string text = value.Replace("\r", " ").Replace("\n", " ");
            return text.Length > 240 ? text.Substring(0, 240) : text;
        }
    }
}
