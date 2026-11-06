using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace WebApplication1.Services
{
    /// <summary>
    /// Enterprise Redis Caching Provider for EduPulse LMS.
    /// Implements thread-safe singleton multiplexer, graceful fallback, and pattern-based invalidation.
    /// </summary>
    public static class RedisCacheService
    {
        private static readonly Lazy<ConnectionMultiplexer> _lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
        {
            try
            {
                var configurationOptions = new ConfigurationOptions
                {
                    EndPoints = { "localhost:6379" },
                    AbortOnConnectFail = false,
                    ConnectTimeout = 3000,
                    SyncTimeout = 3000,
                    ConnectRetry = 2,
                    KeepAlive = 60,
                    ClientName = "EduPulse_LMS_Web"
                };

                var multiplexer = ConnectionMultiplexer.Connect(configurationOptions);
                Debug.WriteLine("[RedisCacheService] Successfully connected to Redis on localhost:6379");
                return multiplexer;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[RedisCacheService] Failed to initialize Redis connection: " + ex.Message);
                return null;
            }
        });

        private static ConnectionMultiplexer Connection
        {
            get
            {
                try
                {
                    return _lazyConnection.Value;
                }
                catch
                {
                    return null;
                }
            }
        }

        public static bool IsConnected
        {
            get
            {
                try
                {
                    return Connection != null && Connection.IsConnected;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static IDatabase GetDb()
        {
            try
            {
                if (Connection != null && Connection.IsConnected)
                {
                    return Connection.GetDatabase();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[RedisCacheService] GetDatabase error: " + ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Retrieves and deserializes a cached object from Redis.
        /// Returns default(T) if key is missing or Redis is unavailable.
        /// </summary>
        public static T Get<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return default(T);

            try
            {
                var db = GetDb();
                if (db == null) return default(T);

                RedisValue val = db.StringGet(key);
                if (!val.HasValue) return default(T);

                return JsonConvert.DeserializeObject<T>(val);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RedisCacheService] Cache GET error for key '{key}': {ex.Message}");
                return default(T);
            }
        }

        /// <summary>
        /// Stores an object in Redis serialized as JSON with an optional TTL expiration.
        /// </summary>
        public static bool Set<T>(string key, T data, TimeSpan? expiry = null)
        {
            if (string.IsNullOrWhiteSpace(key) || data == null) return false;

            try
            {
                var db = GetDb();
                if (db == null) return false;

                string json = JsonConvert.SerializeObject(data, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

                return db.StringSet(key, json, expiry ?? TimeSpan.FromMinutes(30));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RedisCacheService] Cache SET error for key '{key}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes a specific key from Redis cache.
        /// </summary>
        public static bool Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;

            try
            {
                var db = GetDb();
                if (db == null) return false;

                return db.KeyDelete(key);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RedisCacheService] Cache REMOVE error for key '{key}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Evicts all keys matching a given prefix or wildcard pattern (e.g. 'catalog:*').
        /// </summary>
        public static int RemoveByPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return 0;

            int removedCount = 0;
            try
            {
                if (Connection == null || !Connection.IsConnected) return 0;

                var endpoint = Connection.GetEndPoints().FirstOrDefault();
                if (endpoint == null) return 0;

                var server = Connection.GetServer(endpoint);
                var db = Connection.GetDatabase();

                var keys = server.Keys(pattern: pattern).ToArray();
                foreach (var k in keys)
                {
                    if (db.KeyDelete(k))
                    {
                        removedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RedisCacheService] Cache pattern remove error for '{pattern}': {ex.Message}");
            }
            return removedCount;
        }

        /// <summary>
        /// Returns diagnostic metrics for the Admin Operations / Performance dashboard.
        /// </summary>
        public static RedisHealthStatus GetHealthStatus()
        {
            var status = new RedisHealthStatus
            {
                IsConnected = IsConnected,
                Endpoint = "localhost:6379"
            };

            if (IsConnected)
            {
                try
                {
                    var endpoint = Connection.GetEndPoints().FirstOrDefault();
                    if (endpoint != null)
                    {
                        var server = Connection.GetServer(endpoint);
                        status.ServerVersion = server.Version?.ToString() ?? "5.0";
                        status.TotalKeys = (int)server.DatabaseSize();
                    }
                }
                catch (Exception ex)
                {
                    status.ErrorMessage = ex.Message;
                }
            }
            return status;
        }
    }

    public class RedisHealthStatus
    {
        public bool IsConnected { get; set; }
        public string Endpoint { get; set; }
        public string ServerVersion { get; set; }
        public int TotalKeys { get; set; }
        public string ErrorMessage { get; set; }
    }
}
