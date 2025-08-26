namespace BankingApi.EventReceiver.Exceptions
{
    public class InvalidBalanceChangeEventException: ArgumentException
    {
        public InvalidBalanceChangeEventException()
            : base($"Cannot parse balance change event.")
        {
        }
    }
}
