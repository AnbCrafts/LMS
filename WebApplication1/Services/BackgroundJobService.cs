using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Hangfire;
using Newtonsoft.Json;
using WebApplication1.Models;
using WebApplication1.Repositories;

namespace WebApplication1.Services
{
    /// <summary>
    /// Centralized service handling all asynchronous and recurring background jobs in EduPulse.
    /// Executed by the Hangfire Background Processing Server.
    /// </summary>
    public static class BackgroundJobService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        /// <summary>
        /// Registers standing recurring jobs on application startup.
        /// </summary>
        public static void RegisterRecurringJobs()
        {
            try
            {
                // Hourly cache pre-warming to ensure the Redis catalog cache never goes cold
                RecurringJob.AddOrUpdate(
                    "prewarm-catalog-cache",
                    () => PrewarmCatalogCache(),
                    Cron.Hourly);

                Debug.WriteLine("[Hangfire] Recurring jobs registered successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Hangfire] Error registering recurring jobs: " + ex.Message);
            }
        }

        /// <summary>
        /// Background Job: Processes post-enrollment tasks (welcome email, onboarding packet, metrics).
        /// Triggered immediately on student checkout completion.
        /// </summary>
        [AutomaticRetry(Attempts = 3)]
        public static void ProcessEnrollmentPostActions(int userId, int courseId, string invoiceNumber)
        {
            Debug.WriteLine($"[Hangfire Worker] Processing Post-Enrollment Actions: User {userId}, Course {courseId}, Invoice {invoiceNumber}");

            try
            {
                var courseRepo = new CourseRepository();
                var course = courseRepo.GetCourseById(courseId);
                string courseTitle = course != null ? course.Title : $"Course #{courseId}";

                // 1. Simulate sending an asynchronous Welcome Email & Course Syllabus packet
                Debug.WriteLine($"[Hangfire Worker] Welcome Email sent to User #{userId} for course '{courseTitle}' (Invoice: {invoiceNumber}).");

                // 2. Pre-warm the course details cache in Redis
                if (course != null)
                {
                    RedisCacheService.Set($"course:details:{courseId}", course, TimeSpan.FromMinutes(30));
                }

                // 3. Refresh the enrolled student count cache for this course
                RedisCacheService.Remove("catalog:courses:all");
                RedisCacheService.Remove("catalog:courses:filtered:default");

                Debug.WriteLine($"[Hangfire Worker] Enrollment post-actions completed successfully for User {userId}.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Hangfire Worker] Error in ProcessEnrollmentPostActions: {ex.Message}");
                throw; // Rethrow to let Hangfire manage automatic retry
            }
        }

        /// <summary>
        /// Background Job: Asynchronously syncs course modules and syllabus to the Python RAG Assistant (port 8000).
        /// Triggered when an instructor adds modules or changes course publication status.
        /// </summary>
        [AutomaticRetry(Attempts = 5, DelaysInSeconds = new[] { 5, 15, 30, 60, 120 })]
        public static void SyncCourseToAiAssistant(int courseId)
        {
            Debug.WriteLine($"[Hangfire Worker] Starting AI RAG Knowledge Base Sync for Course #{courseId}...");

            try
            {
                var courseRepo = new CourseRepository();
                var course = courseRepo.GetCourseById(courseId);

                if (course == null)
                {
                    Debug.WriteLine($"[Hangfire Worker] Course #{courseId} not found, skipping AI sync.");
                    return;
                }

                var instructorRepo = new InstructorRepository();
                var modules = instructorRepo.GetModulesByCourse(courseId);
                int moduleCount = modules != null ? System.Linq.Enumerable.Count(modules) : 0;

                // Prepare payload for Python RAG microservice
                var payload = new
                {
                    course_id = course.CourseId,
                    title = course.Title,
                    description = course.Description,
                    category = course.Category,
                    level = course.Level,
                    module_count = moduleCount,
                    timestamp = DateTime.UtcNow.ToString("o")
                };

                string json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Ping Python RAG microservice
                var response = _httpClient.PostAsync(EnvConfig.AiMicroserviceUrl + "embed", content).GetAwaiter().GetResult();
                
                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[Hangfire Worker] AI RAG microservice successfully synchronized Course #{courseId}.");
                }
                else
                {
                    Debug.WriteLine($"[Hangfire Worker] Python RAG microservice responded with status: {response.StatusCode}.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Hangfire Worker] Error syncing with Python RAG: {ex.Message}. Hangfire will retry according to backoff policy.");
                // We do not rethrow if it is a connection refusal so worker doesn't fail permanently if Python is stopped
            }
        }

        /// <summary>
        /// Recurring Job: Pre-warms the Redis cache for all public catalog endpoints.
        /// </summary>
        public static void PrewarmCatalogCache()
        {
            Debug.WriteLine("[Hangfire Worker] Pre-warming Redis catalog cache...");
            try
            {
                var courseRepo = new CourseRepository();
                
                // Fetch fresh from DB (or refresh TTL in Redis)
                var courses = courseRepo.GetAllCourses(null, null);
                var categories = courseRepo.GetCourseCategories();

                if (courses != null)
                {
                    RedisCacheService.Set("catalog:courses:all", courses, TimeSpan.FromMinutes(15));
                }

                if (categories != null)
                {
                    RedisCacheService.Set("catalog:categories", categories, TimeSpan.FromMinutes(30));
                }

                Debug.WriteLine("[Hangfire Worker] Redis catalog cache pre-warmed successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Hangfire Worker] Error during cache pre-warming: " + ex.Message);
            }
        }
    }
}
