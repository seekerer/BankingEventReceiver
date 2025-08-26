namespace BankingApi.EventReceiver.Models;

public enum TransactionType
{
    /// <summary>
    /// Funds transfered to the account
    /// </summary>
    Credit = 1,

    /// <summary>
    /// Funds transfered from the account
    /// </summary>
    Debit = 2
    
}
