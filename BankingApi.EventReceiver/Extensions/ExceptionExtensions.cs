using Microsoft.EntityFrameworkCore;

namespace BankingApi.EventReceiver.Extensions
{
    public static class ExceptionExtensions
    {
        public static bool IsTransient(this Exception ex)
        {
            return ex switch
            {
                DbUpdateConcurrencyException => true,
                DbUpdateException => true,
                TimeoutException => true,
                TaskCanceledException => true,
                OperationCanceledException => true,
                _ => false
            };
        }
    }
}
