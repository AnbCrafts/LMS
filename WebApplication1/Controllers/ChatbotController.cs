using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json;
using WebApplication1.Repositories;

namespace WebApplication1.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly IEnrollmentRepository _enrollmentRepo;

        private static readonly HttpClient _httpClient = new HttpClient
        {
            BaseAddress = new Uri(WebApplication1.Services.EnvConfig.AiMicroserviceUrl),
            Timeout = TimeSpan.FromSeconds(45)
        };

        public ChatbotController()
        {
            _enrollmentRepo = new EnrollmentRepository();
        }

        public ChatbotController(IEnrollmentRepository enrollmentRepo)
        {
            _enrollmentRepo = enrollmentRepo;
        }

        public class ChatRequestDto
        {
            public string Query { get; set; }
            public int CourseId { get; set; }
        }

        // POST: /Chatbot/Ask
        [HttpPost]
        public async Task<ActionResult> Ask(ChatRequestDto dto)
        {
            // 1. Enforce Authentication: Only logged-in users can query the assistant
            if (Session["UserId"] == null)
            {
                return Json(new { success = false, message = "Please sign in to access the Course AI Assistant." });
            }

            if (dto == null || string.IsNullOrWhiteSpace(dto.Query))
            {
                return Json(new { success = false, message = "Please enter a valid question." });
            }

            int userId = Convert.ToInt32(Session["UserId"]);
            string userRole = Session["UserRole"] as string ?? "Student";
            int courseId = dto.CourseId > 0 ? dto.CourseId : 0;
            var enrolledCourseIds = new System.Collections.Generic.List<int>();

            // 2. Enforce Course Purchase / Enrollment Isolation for Students
            if (string.Equals(userRole, "Student", StringComparison.OrdinalIgnoreCase))
            {
                var dashboardEnrollments = _enrollmentRepo.GetStudentDashboard(userId);
                if (dashboardEnrollments != null)
                {
                    foreach (var item in dashboardEnrollments)
                    {
                        if (!enrolledCourseIds.Contains(item.CourseId))
                        {
                            enrolledCourseIds.Add(item.CourseId);
                        }
                    }
                }

                // If querying a specific course, student must be enrolled in it
                if (courseId > 0)
                {
                    var enrollmentStatus = _enrollmentRepo.CheckEnrollment(userId, courseId);
                    if (enrollmentStatus == null || !enrollmentStatus.IsEnrolled)
                    {
                        return Json(new
                        {
                            success = false,
                            message = "You are not currently enrolled in this course. Please enroll to unlock the AI Teaching Assistant for this curriculum."
                        });
                    }
                }
                else
                {
                    // Global multi-course mode: must have at least one active enrollment
                    if (enrolledCourseIds.Count == 0)
                    {
                        return Json(new
                        {
                            success = false,
                            message = "You are not enrolled in any courses yet. Please explore the course catalog and enroll to start asking questions!"
                        });
                    }
                }
            }

            var payload = new
            {
                query = dto.Query.Trim(),
                course_id = courseId,
                enrolled_course_ids = enrolledCourseIds.Count > 0 ? enrolledCourseIds : null,
                user_id = userId.ToString(),
                user_role = userRole.ToLower(),
                top_k = 3
            };

            // Fire-and-forget logging of student query for Student Friction Heatmap Analytics
            if (string.Equals(userRole, "Student", StringComparison.OrdinalIgnoreCase))
            {
                int capturedStudentId = userId;
                int capturedCourseId = courseId;
                string capturedQuery = dto.Query.Trim();
                Task.Run(() =>
                {
                    try
                    {
                        string connStr = System.Configuration.ConfigurationManager.ConnectionStrings["LmsConnection"]?.ConnectionString;
                        if (!string.IsNullOrEmpty(connStr))
                        {
                            using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                            {
                                conn.Open();
                                string insertSql = @"
                                    INSERT INTO LMS.StudentQueryLogs (CourseId, StudentId, QueryText, Topic, FrictionLevel, CreatedAt)
                                    VALUES (@CourseId, @StudentId, @QueryText, @Topic, @FrictionLevel, GETDATE());";
                                using (var cmd = new System.Data.SqlClient.SqlCommand(insertSql, conn))
                                {
                                    cmd.Parameters.AddWithValue("@CourseId", capturedCourseId > 0 ? (object)capturedCourseId : DBNull.Value);
                                    cmd.Parameters.AddWithValue("@StudentId", capturedStudentId);
                                    cmd.Parameters.AddWithValue("@QueryText", capturedQuery);
                                    cmd.Parameters.AddWithValue("@Topic", capturedCourseId == 2 ? "Enterprise Architecture" : "Core Concepts");
                                    cmd.Parameters.AddWithValue("@FrictionLevel", "Moderate");
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("[StudentQueryLog Error] " + ex.Message);
                    }
                });
            }

            try
            {
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/chat", content);

                if (!response.IsSuccessStatusCode)
                {
                    string errBody = await response.Content.ReadAsStringAsync();
                    string friendlyMsg = "The RAG Assistant service reported an error: " + (string.IsNullOrWhiteSpace(errBody) ? response.StatusCode.ToString() : errBody);

                    // Parse clean detail message from FastAPI (e.g. 429 rate limit cooldown)
                    try
                    {
                        var errObj = JsonConvert.DeserializeAnonymousType(errBody, new { detail = "" });
                        if (errObj != null && !string.IsNullOrWhiteSpace(errObj.detail))
                        {
                            friendlyMsg = errObj.detail;
                        }
                    }
                    catch { }

                    return Json(new { 
                        success = false, 
                        message = friendlyMsg
                    });
                }

                string responseString = await response.Content.ReadAsStringAsync();
                return Content("{\"success\":true,\"data\":" + responseString + "}", "application/json");
            }
            catch (HttpRequestException)
            {
                return Json(new
                {
                    success = false,
                    message = $"Could not connect to the Python RAG Assistant microservice ({WebApplication1.Services.EnvConfig.AiMicroserviceUrl}). Please ensure the service is running."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }
    }
}
