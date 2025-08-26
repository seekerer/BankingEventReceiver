using System.Text.Json.Serialization;
using BankingApi.EventReceiver.Exceptions;

namespace BankingApi.EventReceiver.Models;

public class BalanceChangeEvent
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    
    [JsonPropertyName("messageType")]
    public string MessageType { get; set; } = string.Empty;

    public TransactionType TransactionType
    {
        get
        {
            if (MessageType.Equals("Credit", StringComparison.OrdinalIgnoreCase))
            {
                return TransactionType.Credit;
            }
            else if (MessageType.Equals("Debit", StringComparison.OrdinalIgnoreCase))
            {
                return TransactionType.Debit;
            }
            else
            {
                throw new InvalidTransactionTypeException($"Unsupported message type: {MessageType}");
            }
        }
    }

    [JsonPropertyName("bankAccountId")]
    public Guid BankAccountId { get; set; }
    
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}
