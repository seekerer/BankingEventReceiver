namespace BankingApi.EventReceiver.Exceptions
{
    public class InvalidTransactionTypeException: ArgumentException
    {
        public InvalidTransactionTypeException(string messageType)
            : base($"Unsupported transaction type: {messageType}")
        {
        }
    }
}
