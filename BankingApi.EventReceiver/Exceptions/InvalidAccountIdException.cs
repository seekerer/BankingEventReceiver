using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingApi.EventReceiver.Exceptions
{
    public class InvalidAccountIdException : ArgumentException
    {
        public InvalidAccountIdException(string accountId)
            : base($"Account ID {accountId} is not valid")
        {
        }
    }
}
