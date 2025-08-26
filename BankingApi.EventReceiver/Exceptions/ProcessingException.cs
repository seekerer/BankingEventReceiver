using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingApi.EventReceiver.Exceptions
{
    public class ProcessingException: Exception
    {
        public bool IsTransient { get; set; }

        public ProcessingException(string message, bool isTransient = false) : base(message)
        {
            IsTransient = isTransient;
        }
    }
}
