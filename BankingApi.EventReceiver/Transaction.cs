namespace BankingApi.EventReceiver
{

    /// <summary>
    /// Simple entity to store information about already processed txn. Could be extended afterwards.
    /// </summary>
    public class Transaction
    {
        public Guid Id { get; set; }
    }

}