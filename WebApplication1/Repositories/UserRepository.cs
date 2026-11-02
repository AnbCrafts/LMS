using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public class UserRepository : IUserRepository
    {
        public User Authenticate(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return null;

            DatabaseInitializer.Initialize();

            // Try stored procedure first
            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@Email", email.Trim()),
                    new SqlParameter("@Password", password)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_AuthenticateUser", parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapDataRowToUser(dt.Rows[0]);
                }
            }
            catch
            {
                // Fallback to direct SQL query if stored procedure fails or is missing
            }

            try
            {
                string sql = "SELECT TOP 1 UserId, FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt FROM LMS.Users WHERE LOWER(Email) = LOWER(@Email) AND Password = @Password";
                var p = new[]
                {
                    new SqlParameter("@Email", email.Trim()),
                    new SqlParameter("@Password", password)
                };
                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapDataRowToUser(dt.Rows[0]);
                }
            }
            catch { }

            return null;
        }

        public User Register(User user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            DatabaseInitializer.Initialize();

            bool isInstructor = string.Equals(user.Role, "Instructor", StringComparison.OrdinalIgnoreCase);
            bool isApproved = !isInstructor; // Students are auto-approved (1), Instructors are pending admin verification (0)
            string approvalStatus = isInstructor ? "Pending" : "Approved";
            string role = string.IsNullOrWhiteSpace(user.Role) ? "Student" : user.Role.Trim();

            // Try stored procedure first
            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@FullName", user.FullName.Trim()),
                    new SqlParameter("@Email", user.Email.Trim()),
                    new SqlParameter("@Password", user.Password),
                    new SqlParameter("@Role", role),
                    new SqlParameter("@IsApproved", isApproved),
                    new SqlParameter("@ApprovalStatus", approvalStatus)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_RegisterUser", parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapDataRowToUser(dt.Rows[0]);
                }
            }
            catch
            {
                // Fallback to direct SQL insert
            }

            try
            {
                string sql = @"
                    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
                    OUTPUT INSERTED.UserId, INSERTED.FullName, INSERTED.Email, INSERTED.Password, INSERTED.Role, INSERTED.IsApproved, INSERTED.ApprovalStatus, INSERTED.CreatedAt
                    VALUES (@FullName, @Email, @Password, @Role, @IsApproved, @ApprovalStatus, GETDATE());";

                var p = new[]
                {
                    new SqlParameter("@FullName", user.FullName.Trim()),
                    new SqlParameter("@Email", user.Email.Trim()),
                    new SqlParameter("@Password", user.Password),
                    new SqlParameter("@Role", role),
                    new SqlParameter("@IsApproved", isApproved),
                    new SqlParameter("@ApprovalStatus", approvalStatus)
                };

                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapDataRowToUser(dt.Rows[0]);
                }
            }
            catch { }

            return null;
        }

        public User GetUserById(int userId)
        {
            DatabaseInitializer.Initialize();

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@UserId", userId)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetUserById", parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapDataRowToUser(dt.Rows[0]);
                }
            }
            catch
            {
                // Fallback to direct SQL query
            }

            try
            {
                string sql = "SELECT TOP 1 UserId, FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt FROM LMS.Users WHERE UserId = @UserId";
                var p = new[] { new SqlParameter("@UserId", userId) };
                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapDataRowToUser(dt.Rows[0]);
                }
            }
            catch { }

            return null;
        }

        public OperationResult ChangePassword(int userId, string currentPassword, string newPassword)
        {
            DatabaseInitializer.Initialize();

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@CurrentPassword", currentPassword),
                    new SqlParameter("@NewPassword", newPassword)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_ChangePassword", parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    bool isSuccess = Convert.ToInt32(dt.Rows[0]["IsSuccess"]) == 1;
                    string message = dt.Rows[0]["Message"].ToString();
                    return new OperationResult { IsSuccess = isSuccess, Message = message };
                }
            }
            catch
            {
                // Fallback to direct SQL
            }

            try
            {
                string verifySql = "SELECT COUNT(1) FROM LMS.Users WHERE UserId = @UserId AND Password = @CurrentPassword";
                var p1 = new[]
                {
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@CurrentPassword", currentPassword)
                };
                int match = Convert.ToInt32(DbHelper.ExecuteScalar(verifySql, p1) ?? 0);
                if (match == 0)
                {
                    return OperationResult.Failure("Current password does not match.");
                }

                string updateSql = "UPDATE LMS.Users SET Password = @NewPassword WHERE UserId = @UserId";
                var p2 = new[]
                {
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@NewPassword", newPassword)
                };
                DbHelper.ExecuteNonQuery(updateSql, p2);
                return OperationResult.Success("Password updated successfully.");
            }
            catch (Exception ex)
            {
                return OperationResult.Failure("Failed to change password: " + ex.Message);
            }
        }

        public IEnumerable<User> GetPendingInstructors()
        {
            DatabaseInitializer.Initialize();
            var list = new List<User>();

            try
            {
                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetPendingInstructors");
                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(MapDataRowToUser(row));
                    }
                    return list;
                }
            }
            catch
            {
                // Fallback to direct SQL
            }

            try
            {
                string sql = @"
                    SELECT UserId, FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt 
                    FROM LMS.Users 
                    WHERE Role = 'Instructor' AND (IsApproved = 0 OR LOWER(ApprovalStatus) = 'pending')
                    ORDER BY CreatedAt DESC";

                DataTable dt = DbHelper.ExecuteDataTable(sql);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(MapDataRowToUser(row));
                    }
                }
            }
            catch { }

            return list;
        }

        public OperationResult ReviewInstructor(int userId, string action)
        {
            DatabaseInitializer.Initialize();
            string normalizedAction = string.Equals(action, "Approve", StringComparison.OrdinalIgnoreCase) ? "Approve" : "Reject";
            bool isApproved = normalizedAction == "Approve";
            string status = normalizedAction == "Approve" ? "Approved" : "Rejected";

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@Action", normalizedAction)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_ReviewInstructor", parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    bool isSuccess = Convert.ToInt32(dt.Rows[0]["IsSuccess"]) == 1;
                    string message = dt.Rows[0]["Message"].ToString();
                    return new OperationResult { IsSuccess = isSuccess, Message = message };
                }
            }
            catch
            {
                // Fallback to direct SQL
            }

            try
            {
                string sql = "UPDATE LMS.Users SET IsApproved = @IsApproved, ApprovalStatus = @ApprovalStatus WHERE UserId = @UserId";
                var p = new[]
                {
                    new SqlParameter("@IsApproved", isApproved),
                    new SqlParameter("@ApprovalStatus", status),
                    new SqlParameter("@UserId", userId)
                };
                int rows = DbHelper.ExecuteNonQuery(sql, p);
                if (rows > 0)
                {
                    return OperationResult.Success(normalizedAction == "Approve"
                        ? "Instructor account approved successfully! The instructor can now log in and publish courses."
                        : "Instructor application rejected.");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.Failure("Failed to review instructor status: " + ex.Message);
            }

            return OperationResult.Failure("Instructor record not found.");
        }

        public IEnumerable<User> GetAllInstructors()
        {
            DatabaseInitializer.Initialize();
            var list = new List<User>();

            try
            {
                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetAllInstructors");
                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(MapDataRowToUser(row));
                    }
                    return list;
                }
            }
            catch
            {
                // Fallback to direct SQL
            }

            try
            {
                string sql = @"
                    SELECT UserId, FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt 
                    FROM LMS.Users 
                    WHERE Role = 'Instructor'
                    ORDER BY FullName ASC";

                DataTable dt = DbHelper.ExecuteDataTable(sql);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(MapDataRowToUser(row));
                    }
                }
            }
            catch { }

            return list;
        }

        private User MapDataRowToUser(DataRow row)
        {
            return new User
            {
                UserId = Convert.ToInt32(row["UserId"]),
                FullName = row["FullName"].ToString(),
                Email = row["Email"].ToString(),
                Role = row.Table.Columns.Contains("Role") && row["Role"] != DBNull.Value ? row["Role"].ToString() : "Student",
                IsApproved = row.Table.Columns.Contains("IsApproved") && row["IsApproved"] != DBNull.Value ? Convert.ToBoolean(row["IsApproved"]) : true,
                ApprovalStatus = row.Table.Columns.Contains("ApprovalStatus") && row["ApprovalStatus"] != DBNull.Value ? row["ApprovalStatus"].ToString() : "Approved",
                CreatedAt = row.Table.Columns.Contains("CreatedAt") && row["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(row["CreatedAt"]) : DateTime.Now
            };
        }
    }
}
