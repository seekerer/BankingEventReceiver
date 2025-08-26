namespace BankingApi.EventReceiver.Exceptions
{
    public class InvalidBalanceChangeEventException: ProcessingException
    {
        public InvalidBalanceChangeEventException()
            : base($"Cannot parse balance change event.")
        {
        }
    }
}
