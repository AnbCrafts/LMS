using System.Collections.Generic;
using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public interface IUserRepository
    {
        User Authenticate(string email, string password);
        User Register(User user);
        User GetUserById(int userId);
        OperationResult ChangePassword(int userId, string currentPassword, string newPassword);
        IEnumerable<User> GetPendingInstructors();
        OperationResult ReviewInstructor(int userId, string action); // "Approve" or "Reject"
        IEnumerable<User> GetAllInstructors();
    }
}
