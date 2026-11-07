using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;

namespace WebApplication1.Services
{
    /// <summary>
    /// Centralized Environment Configuration Provider.
    /// Loads key-value pairs from .env files or system environment variables,
    /// enabling seamless switching between Localhost and Production without recompiling.
    /// </summary>
    public static class EnvConfig
    {
        private static readonly ConcurrentDictionary<string, string> _envVars = 
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static bool _isLoaded = false;
        private static readonly object _lock = new object();

        static EnvConfig()
        {
            EnsureLoaded();
        }

        /// <summary>
        /// Explicit initialization hook (can also be called from Application_Start).
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_isLoaded) return;

            lock (_lock)
            {
                if (_isLoaded) return;

                LoadDotEnvFiles();
                _isLoaded = true;
            }
        }

        private static void LoadDotEnvFiles()
        {
            try
            {
                var candidatePaths = new List<string>();

                string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
                
                // 1. Current application base directory: WebApplication1/.env
                candidatePaths.Add(Path.Combine(baseDir, ".env"));
                
                // 2. Parent solution directory: F:\MCC_Practice\Projects\LMS\.env
                try
                {
                    var parent = Directory.GetParent(baseDir);
                    if (parent != null)
                    {
                        candidatePaths.Add(Path.Combine(parent.FullName, ".env"));
                        var grandParent = parent.Parent;
                        if (grandParent != null)
                        {
                            candidatePaths.Add(Path.Combine(grandParent.FullName, ".env"));
                        }
                    }
                }
                catch { }

                // 3. Rag_Service/.env
                candidatePaths.Add(Path.Combine(baseDir, "Rag_Service", ".env"));
                candidatePaths.Add(Path.Combine(baseDir, "..", "Rag_Service", ".env"));

                foreach (var path in candidatePaths)
                {
                    try
                    {
                        string fullPath = Path.GetFullPath(path);
                        if (File.Exists(fullPath))
                        {
                            ParseAndLoadEnvFile(fullPath);
                            Debug.WriteLine($"[EnvConfig] Loaded environment variables from: {fullPath}");
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EnvConfig] Error initializing .env reader: {ex.Message}");
            }
        }

        private static void ParseAndLoadEnvFile(string filePath)
        {
            foreach (var rawLine in File.ReadAllLines(filePath))
            {
                string line = rawLine?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith(";"))
                    continue;

                int equalIndex = line.IndexOf('=');
                if (equalIndex <= 0) continue;

                string key = line.Substring(0, equalIndex).Trim();
                string val = line.Substring(equalIndex + 1).Trim();

                // Unquote values if enclosed in quotes
                if ((val.StartsWith("\"") && val.EndsWith("\"")) ||
                    (val.StartsWith("'") && val.EndsWith("'")))
                {
                    if (val.Length >= 2)
                    {
                        val = val.Substring(1, val.Length - 2);
                    }
                }

                // Add or keep existing
                _envVars[key] = val;
            }
        }

        /// <summary>
        /// Reads an environment setting in order of priority:
        /// 1. OS System Environment Variable (Process / Machine / User)
        /// 2. Loaded .env file key
        /// 3. Web.config AppSettings
        /// 4. Default fallback value
        /// </summary>
        public static string Get(string key, string defaultValue = "")
        {
            EnsureLoaded();

            if (string.IsNullOrWhiteSpace(key)) return defaultValue;

            // 1. Check System Environment Variable (useful for Docker, Azure App Service, Render, MonsterASP)
            string sysVar = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(sysVar))
                return sysVar;

            // 2. Check loaded .env file
            if (_envVars.TryGetValue(key, out string envVal) && !string.IsNullOrWhiteSpace(envVal))
                return envVal;

            // 3. Check Web.config AppSettings
            try
            {
                string appSetting = ConfigurationManager.AppSettings[key];
                if (!string.IsNullOrWhiteSpace(appSetting))
                    return appSetting;
            }
            catch { }

            return defaultValue;
        }

        /// <summary>
        /// Redis connection string.
        /// Local default: "localhost:6379,abortConnect=false"
        /// Production (Upstash/Azure): "optimal-xxx.upstash.io:6379,password=...,ssl=true,abortConnect=false"
        /// </summary>
        public static string RedisConnectionString => 
            Get("REDIS_CONNECTION_STRING", Get("RedisConnectionString", "localhost:6379,abortConnect=false"));

        /// <summary>
        /// Redis endpoint host:port display string for health monitoring.
        /// </summary>
        public static string RedisEndpoint
        {
            get
            {
                string conn = RedisConnectionString;
                if (string.IsNullOrWhiteSpace(conn)) return "localhost:6379";
                string firstPart = conn.Split(',')[0].Trim();
                return string.IsNullOrWhiteSpace(firstPart) ? "localhost:6379" : firstPart;
            }
        }

        /// <summary>
        /// Python FastAPI AI Microservice URL.
        /// Local default: "http://127.0.0.1:8000/"
        /// Production (Render/HF): "https://edupulse-ai.onrender.com/"
        /// </summary>
        public static string AiMicroserviceUrl
        {
            get
            {
                string url = Get("AI_MICROSERVICE_URL", Get("AiMicroserviceUrl", "http://127.0.0.1:8000/"));
                if (!url.EndsWith("/"))
                    url += "/";
                return url;
            }
        }

        /// <summary>
        /// Relational Database Connection String (SQL Server).
        /// </summary>
        public static string SqlConnectionString
        {
            get
            {
                string envSql = Get("SQL_CONNECTION_STRING");
                if (!string.IsNullOrWhiteSpace(envSql))
                    return envSql;

                if (ConfigurationManager.ConnectionStrings["LmsConnection"] != null &&
                    !string.IsNullOrWhiteSpace(ConfigurationManager.ConnectionStrings["LmsConnection"].ConnectionString))
                {
                    return ConfigurationManager.ConnectionStrings["LmsConnection"].ConnectionString;
                }

                return "Data Source=.;Initial Catalog=Practice;Integrated Security=True;TrustServerCertificate=True;";
            }
        }

        /// <summary>
        /// Cloudinary API Key.
        /// </summary>
        public static string CloudinaryApiKey => 
            Get("CLOUDINARY_API_KEY", Get("Cloudinary:ApiKey", "534968466568819"));

        /// <summary>
        /// Cloudinary API Secret.
        /// </summary>
        public static string CloudinaryApiSecret => 
            Get("CLOUDINARY_API_SECRET", Get("Cloudinary:ApiSecret", "ywZbqE8MBDtLFW80a2au_R_rTf4"));

        /// <summary>
        /// Cloudinary Cloud Name.
        /// </summary>
        public static string CloudinaryCloudName => 
            Get("CLOUDINARY_CLOUD_NAME", Get("Cloudinary:CloudName", ""));
    }
}
