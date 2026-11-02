using System.Collections.Generic;
using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public interface IInstructorRepository
    {
        // Profile
        InstructorProfile GetProfile(int userId);
        InstructorProfile SaveProfile(InstructorProfile profile);
        bool CheckProfileCompletionGate(int userId);

        // Course Modules
        IEnumerable<CourseModule> GetModulesByCourse(int courseId);
        CourseModule GetModuleById(int moduleId, int courseId);
        OperationResult SaveModule(CourseModule module);
        OperationResult DeleteModule(int moduleId, int courseId);

        // Student Submissions & Grading
        IEnumerable<StudentSubmission> GetSubmissionsForInstructor(int instructorId);
        OperationResult GradeSubmission(int submissionId, decimal marks, string feedback, string status, int reviewedBy);
        IEnumerable<StudentSubmission> GetStudentSubmissions(int userId, int? courseId = null);
        OperationResult SubmitAssignment(StudentSubmission submission);

        // Student Quiz Attempts & Grade Summaries
        OperationResult SaveQuizAttempt(StudentQuizAttempt attempt);
        IEnumerable<StudentQuizAttempt> GetStudentQuizAttempts(int userId, int? courseId = null);
        CourseGradeSummary GetCourseGradeSummary(int userId, int courseId);

        // Admin Verification & Application Status
        InstructorProfile GetInstructorVerificationDetails(int userId);
        IEnumerable<InstructorProfile> GetAllInstructorsWithProfiles(string statusFilter = null);
        OperationResult ReviewInstructorWithFeedback(int userId, string action, string feedback, int? reviewedBy);
        InstructorProfile GetInstructorApplicationStatusByEmail(string email);
    }
}
