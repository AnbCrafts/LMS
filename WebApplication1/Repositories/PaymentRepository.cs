using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        public OperationResult ProcessPayment(int userId, int courseId, decimal amount, string paymentMethod, string transactionId)
        {
            DatabaseInitializer.Initialize();
            string txId = string.IsNullOrWhiteSpace(transactionId) ? ("TXN_SIM_" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper()) : transactionId;
            string invNum = "INV-" + DateTime.Now.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

            try
            {
                string sqlPayment = @"
                    IF NOT EXISTS (SELECT 1 FROM LMS.Enrollments WHERE UserId = @UserId AND CourseId = @CourseId)
                    BEGIN
                        INSERT INTO LMS.Enrollments (UserId, CourseId, EnrolledAt, ProgressPercentage, IsCompleted)
                        VALUES (@UserId, @CourseId, GETDATE(), 0, 0);
                    END;

                    INSERT INTO LMS.Payments (InvoiceNumber, UserId, CourseId, Amount, PaymentMethod, TransactionId, Status, PaymentDate)
                    VALUES (@InvoiceNumber, @UserId, @CourseId, @Amount, @PaymentMethod, @TransactionId, 'Completed', GETDATE());";

                var p = new[]
                {
                    new SqlParameter("@InvoiceNumber", invNum),
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@CourseId", courseId),
                    new SqlParameter("@Amount", amount),
                    new SqlParameter("@PaymentMethod", (object)paymentMethod ?? "Card (Simulated)"),
                    new SqlParameter("@TransactionId", txId)
                };

                DbHelper.ExecuteNonQuery(sqlPayment, p);

                return new OperationResult
                {
                    IsSuccess = true,
                    Message = invNum
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ProcessPayment error: " + ex.Message);
                return OperationResult.Failure("Payment processing failed: " + ex.Message);
            }
        }

        public InvoiceViewModel GetInvoiceDetails(string invoiceNumber)
        {
            DatabaseInitializer.Initialize();

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@InvoiceNumber", invoiceNumber.Trim())
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetInvoiceDetails", parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapRowToInvoice(dt.Rows[0]);
                }
            }
            catch
            {
                // Fallback to direct SQL
            }

            try
            {
                string sql = @"
                    SELECT p.PaymentId, p.InvoiceNumber, p.Amount, p.PaymentMethod, p.TransactionId, p.Status, p.PaymentDate,
                           s.UserId AS StudentId, s.FullName AS StudentName, s.Email AS StudentEmail,
                           c.CourseId, c.Title AS CourseTitle, c.Category, c.Level, c.DurationWeeks,
                           inst.UserId AS InstructorId, 
                           ISNULL(inst.FullName, c.InstructorName) AS InstructorName, 
                           ISNULL(inst.Email, '') AS InstructorEmail
                    FROM LMS.Payments p
                    JOIN LMS.Courses c ON p.CourseId = c.CourseId
                    JOIN LMS.Users s ON p.UserId = s.UserId
                    LEFT JOIN LMS.Users inst ON c.InstructorId = inst.UserId
                    WHERE p.InvoiceNumber = @InvoiceNumber";

                var p = new[] { new SqlParameter("@InvoiceNumber", invoiceNumber.Trim()) };
                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapRowToInvoice(dt.Rows[0]);
                }
            }
            catch { }

            return null;
        }

        public IEnumerable<InvoiceViewModel> GetInvoicesByUser(int userId)
        {
            DatabaseInitializer.Initialize();
            var list = new List<InvoiceViewModel>();

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@UserId", userId)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetInvoicesByUser", parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(new InvoiceViewModel
                        {
                            PaymentId = Convert.ToInt32(row["PaymentId"]),
                            InvoiceNumber = row["InvoiceNumber"].ToString(),
                            Amount = Convert.ToDecimal(row["Amount"]),
                            PaymentMethod = row["PaymentMethod"].ToString(),
                            TransactionId = row["TransactionId"].ToString(),
                            Status = row["Status"].ToString(),
                            PaymentDate = Convert.ToDateTime(row["PaymentDate"]),
                            CourseId = Convert.ToInt32(row["CourseId"]),
                            CourseTitle = row["CourseTitle"].ToString(),
                            InstructorName = row["InstructorName"] != DBNull.Value ? row["InstructorName"].ToString() : "EduPulse Instructor"
                        });
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
                    SELECT p.PaymentId, p.InvoiceNumber, p.Amount, p.PaymentMethod, p.TransactionId, p.Status, p.PaymentDate,
                           c.CourseId, c.Title AS CourseTitle, 
                           ISNULL(u.FullName, c.InstructorName) AS InstructorName
                    FROM LMS.Payments p
                    JOIN LMS.Courses c ON p.CourseId = c.CourseId
                    LEFT JOIN LMS.Users u ON c.InstructorId = u.UserId
                    WHERE p.UserId = @UserId
                    ORDER BY p.PaymentDate DESC";

                var p = new[] { new SqlParameter("@UserId", userId) };
                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(new InvoiceViewModel
                        {
                            PaymentId = Convert.ToInt32(row["PaymentId"]),
                            InvoiceNumber = row["InvoiceNumber"].ToString(),
                            Amount = Convert.ToDecimal(row["Amount"]),
                            PaymentMethod = row["PaymentMethod"].ToString(),
                            TransactionId = row["TransactionId"].ToString(),
                            Status = row["Status"].ToString(),
                            PaymentDate = Convert.ToDateTime(row["PaymentDate"]),
                            CourseId = Convert.ToInt32(row["CourseId"]),
                            CourseTitle = row["CourseTitle"].ToString(),
                            InstructorName = row["InstructorName"] != DBNull.Value ? row["InstructorName"].ToString() : "EduPulse Instructor"
                        });
                    }
                }
            }
            catch { }

            return list;
        }

        public IEnumerable<InvoiceViewModel> GetInvoicesByInstructor(int instructorId)
        {
            DatabaseInitializer.Initialize();
            var list = new List<InvoiceViewModel>();

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@InstructorId", instructorId)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetInvoicesByInstructor", parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(new InvoiceViewModel
                        {
                            PaymentId = Convert.ToInt32(row["PaymentId"]),
                            InvoiceNumber = row["InvoiceNumber"].ToString(),
                            Amount = Convert.ToDecimal(row["Amount"]),
                            PaymentMethod = row["PaymentMethod"].ToString(),
                            TransactionId = row["TransactionId"].ToString(),
                            Status = row["Status"].ToString(),
                            PaymentDate = Convert.ToDateTime(row["PaymentDate"]),
                            StudentName = row["StudentName"].ToString(),
                            StudentEmail = row["StudentEmail"].ToString(),
                            CourseId = Convert.ToInt32(row["CourseId"]),
                            CourseTitle = row["CourseTitle"].ToString()
                        });
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
                    SELECT p.PaymentId, p.InvoiceNumber, p.Amount, p.PaymentMethod, p.TransactionId, p.Status, p.PaymentDate,
                           u.FullName AS StudentName, u.Email AS StudentEmail,
                           c.CourseId, c.Title AS CourseTitle
                    FROM LMS.Payments p
                    JOIN LMS.Courses c ON p.CourseId = c.CourseId
                    JOIN LMS.Users u ON p.UserId = u.UserId
                    WHERE c.InstructorId = @InstructorId
                    ORDER BY p.PaymentDate DESC";

                var p = new[] { new SqlParameter("@InstructorId", instructorId) };
                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(new InvoiceViewModel
                        {
                            PaymentId = Convert.ToInt32(row["PaymentId"]),
                            InvoiceNumber = row["InvoiceNumber"].ToString(),
                            Amount = Convert.ToDecimal(row["Amount"]),
                            PaymentMethod = row["PaymentMethod"].ToString(),
                            TransactionId = row["TransactionId"].ToString(),
                            Status = row["Status"].ToString(),
                            PaymentDate = Convert.ToDateTime(row["PaymentDate"]),
                            StudentName = row["StudentName"].ToString(),
                            StudentEmail = row["StudentEmail"].ToString(),
                            CourseId = Convert.ToInt32(row["CourseId"]),
                            CourseTitle = row["CourseTitle"].ToString()
                        });
                    }
                }
            }
            catch { }

            return list;
        }

        public IEnumerable<InvoiceViewModel> GetAllInvoices()
        {
            DatabaseInitializer.Initialize();
            var list = new List<InvoiceViewModel>();

            try
            {
                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetAllInvoices");
                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(new InvoiceViewModel
                        {
                            PaymentId = Convert.ToInt32(row["PaymentId"]),
                            InvoiceNumber = row["InvoiceNumber"].ToString(),
                            Amount = Convert.ToDecimal(row["Amount"]),
                            PaymentMethod = row["PaymentMethod"].ToString(),
                            TransactionId = row["TransactionId"].ToString(),
                            Status = row["Status"].ToString(),
                            PaymentDate = Convert.ToDateTime(row["PaymentDate"]),
                            StudentName = row["StudentName"].ToString(),
                            InstructorName = row["InstructorName"] != DBNull.Value ? row["InstructorName"].ToString() : "N/A",
                            CourseTitle = row["CourseTitle"].ToString()
                        });
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
                    SELECT p.PaymentId, p.InvoiceNumber, p.Amount, p.PaymentMethod, p.TransactionId, p.Status, p.PaymentDate,
                           s.FullName AS StudentName,
                           ISNULL(inst.FullName, c.InstructorName) AS InstructorName,
                           c.CourseId, c.Title AS CourseTitle
                    FROM LMS.Payments p
                    JOIN LMS.Courses c ON p.CourseId = c.CourseId
                    JOIN LMS.Users s ON p.UserId = s.UserId
                    LEFT JOIN LMS.Users inst ON c.InstructorId = inst.UserId
                    ORDER BY p.PaymentDate DESC";

                DataTable dt = DbHelper.ExecuteDataTable(sql);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(new InvoiceViewModel
                        {
                            PaymentId = Convert.ToInt32(row["PaymentId"]),
                            InvoiceNumber = row["InvoiceNumber"].ToString(),
                            Amount = Convert.ToDecimal(row["Amount"]),
                            PaymentMethod = row["PaymentMethod"].ToString(),
                            TransactionId = row["TransactionId"].ToString(),
                            Status = row["Status"].ToString(),
                            PaymentDate = Convert.ToDateTime(row["PaymentDate"]),
                            StudentName = row["StudentName"].ToString(),
                            InstructorName = row["InstructorName"] != DBNull.Value ? row["InstructorName"].ToString() : "N/A",
                            CourseTitle = row["CourseTitle"].ToString()
                        });
                    }
                }
            }
            catch { }

            return list;
        }

        private InvoiceViewModel MapRowToInvoice(DataRow row)
        {
            return new InvoiceViewModel
            {
                PaymentId = Convert.ToInt32(row["PaymentId"]),
                InvoiceNumber = row["InvoiceNumber"].ToString(),
                Amount = Convert.ToDecimal(row["Amount"]),
                PaymentMethod = row["PaymentMethod"].ToString(),
                TransactionId = row["TransactionId"].ToString(),
                Status = row["Status"].ToString(),
                PaymentDate = Convert.ToDateTime(row["PaymentDate"]),
                StudentId = row.Table.Columns.Contains("StudentId") && row["StudentId"] != DBNull.Value ? Convert.ToInt32(row["StudentId"]) : 0,
                StudentName = row.Table.Columns.Contains("StudentName") && row["StudentName"] != DBNull.Value ? row["StudentName"].ToString() : "Student",
                StudentEmail = row.Table.Columns.Contains("StudentEmail") && row["StudentEmail"] != DBNull.Value ? row["StudentEmail"].ToString() : "",
                CourseId = Convert.ToInt32(row["CourseId"]),
                CourseTitle = row["CourseTitle"].ToString(),
                Category = row.Table.Columns.Contains("Category") && row["Category"] != DBNull.Value ? row["Category"].ToString() : "Frontend",
                Level = row.Table.Columns.Contains("Level") && row["Level"] != DBNull.Value ? row["Level"].ToString() : "Beginner",
                DurationWeeks = row.Table.Columns.Contains("DurationWeeks") && row["DurationWeeks"] != DBNull.Value ? Convert.ToInt32(row["DurationWeeks"]) : 4,
                InstructorId = row.Table.Columns.Contains("InstructorId") && row["InstructorId"] != DBNull.Value ? (int?)Convert.ToInt32(row["InstructorId"]) : null,
                InstructorName = row.Table.Columns.Contains("InstructorName") && row["InstructorName"] != DBNull.Value ? row["InstructorName"].ToString() : "EduPulse Instructor",
                InstructorEmail = row.Table.Columns.Contains("InstructorEmail") && row["InstructorEmail"] != DBNull.Value ? row["InstructorEmail"].ToString() : ""
            };
        }
    }
}
