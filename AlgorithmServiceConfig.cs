using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace AvevaIntegration
{
    internal class AlgorithmServiceConfig
    {
        public static string LoadBaseUrl()
        {
            try
            {
                return LoadBaseUrlCore();
            }
            catch (Exception ex)
            {
                return "ERROR: " +
                    ex.GetType().FullName + ": " +
                    ex.Message;
            }
        }

        private static string LoadBaseUrlCore()
        {
            string assemblyPath =
                Assembly.GetExecutingAssembly().Location;
            string assemblyDirectory =
                Path.GetDirectoryName(assemblyPath);
            string configPath = Path.Combine(
                assemblyDirectory,
                "AvevaIntegration.config");

            if (!File.Exists(configPath))
            {
                return "ERROR: configuration file not found: " +
                    configPath;
            }

            string baseUrl = null;
            string[] lines = File.ReadAllLines(
                configPath,
                new UTF8Encoding(false));

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.Length == 0 ||
                    line.StartsWith("#", StringComparison.Ordinal) ||
                    line.StartsWith(";", StringComparison.Ordinal))
                {
                    continue;
                }

                int separator = line.IndexOf('=');

                if (separator < 0)
                {
                    continue;
                }

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();

                if (string.Equals(
                    key,
                    "BaseUrl",
                    StringComparison.OrdinalIgnoreCase))
                {
                    baseUrl = value;
                    break;
                }
            }

            if (baseUrl == null)
            {
                return "ERROR: BaseUrl is missing in configuration file";
            }

            if (baseUrl.Length == 0)
            {
                return "ERROR: BaseUrl is empty";
            }

            Uri baseUri;

            if (!Uri.TryCreate(
                baseUrl,
                UriKind.Absolute,
                out baseUri) ||
                (!string.Equals(
                    baseUri.Scheme,
                    Uri.UriSchemeHttp,
                    StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(
                    baseUri.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return "ERROR: BaseUrl must be an absolute http or https URL";
            }

            while (baseUrl.EndsWith("/", StringComparison.Ordinal))
            {
                baseUrl = baseUrl.Substring(0, baseUrl.Length - 1);
            }

            if (baseUrl.Length == 0)
            {
                return "ERROR: BaseUrl is empty";
            }

            return baseUrl;
        }
    }
}
