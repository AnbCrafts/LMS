using System.Collections.Generic;
using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public interface ICourseRepository
    {
        IEnumerable<Course> GetAllCourses(string searchTerm = null, string level = null);
        IEnumerable<Course> GetCoursesFiltered(string searchTerm = null, IEnumerable<string> categories = null, string level = null, string priceFilter = null, string sortBy = null);
        IEnumerable<CategoryFilterItem> GetCourseCategories();
        IEnumerable<Course> GetCoursesByInstructor(int instructorId);
        Course GetCourseById(int courseId);
        Course CreateCourse(Course course);
        bool UpdateCourse(Course course);
        bool DeleteCourse(int courseId);
        bool SetCoursePublishStatus(int courseId, bool isPublished, out string errorMessage);
        bool CanCourseBePublished(int courseId, out int moduleCount, out string errorMessage);
    }
}
