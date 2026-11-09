using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafyaClinic.Domain.Enums
{
    public enum PaymentMethodEnum
    {
        Cash,
        CreditCard,
        BankTransfer,
        Insurance,
        MobilePayment,
        /// <summary>P6b: no money changed hands; used only by write-offs (never accepted by Collect).</summary>
        WriteOff
    }
}
