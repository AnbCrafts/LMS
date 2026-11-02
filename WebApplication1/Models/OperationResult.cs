namespace WebApplication1.Models
{
    public class OperationResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public int? TargetId { get; set; }

        public static OperationResult Success(string message, int? targetId = null)
        {
            return new OperationResult { IsSuccess = true, Message = message, TargetId = targetId };
        }

        public static OperationResult Failure(string message)
        {
            return new OperationResult { IsSuccess = false, Message = message };
        }
    }

    public class EnrollmentStatusResult
    {
        public bool IsEnrolled { get; set; }
        public int? EnrollmentId { get; set; }
        public int ProgressPercentage { get; set; }
        public string Status { get; set; }
        public System.DateTime? EnrolledAt { get; set; }
    }
}
