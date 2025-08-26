using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingApi.EventReceiver.Exceptions
{
    class TransactionAlreadyProcessedException : ProcessingException
    {
        public TransactionAlreadyProcessedException(string message) : base(message, false)
        {
        }
    }
}
