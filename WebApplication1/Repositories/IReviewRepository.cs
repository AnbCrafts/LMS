using System.Collections.Generic;
using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public interface IReviewRepository
    {
        OperationResult AddReview(int courseId, int userId, int rating, string comment);
        IEnumerable<Review> GetCourseReviews(int courseId);
    }
}
