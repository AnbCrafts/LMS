using System.Collections.Generic;
using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public interface IPaymentRepository
    {
        OperationResult ProcessPayment(int userId, int courseId, decimal amount, string paymentMethod, string transactionId);
        InvoiceViewModel GetInvoiceDetails(string invoiceNumber);
        IEnumerable<InvoiceViewModel> GetInvoicesByUser(int userId);
        IEnumerable<InvoiceViewModel> GetInvoicesByInstructor(int instructorId);
        IEnumerable<InvoiceViewModel> GetAllInvoices();
    }
}
