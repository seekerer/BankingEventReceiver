namespace BankingApi.EventReceiver.Exceptions
{
    public class InvalidTransactionTypeException: ProcessingException
    {
        public InvalidTransactionTypeException(string messageType)
            : base($"Unsupported transaction type: {messageType}")
        {
        }
    }
}
