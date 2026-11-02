using System.Collections.Generic;
using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public interface IEnrollmentRepository
    {
        OperationResult EnrollCourse(int userId, int courseId);
        IEnumerable<StudentDashboardViewModel> GetStudentDashboard(int userId);
        OperationResult UpdateCourseProgress(int userId, int courseId, int progressPercentage);
        EnrollmentStatusResult CheckEnrollment(int userId, int courseId);
        OperationResult UnenrollCourse(int userId, int courseId);
    }
}
