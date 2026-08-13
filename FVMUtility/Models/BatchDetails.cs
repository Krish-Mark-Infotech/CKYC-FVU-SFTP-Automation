using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FVMUtility.Models
{
    public class BatchDetails
    {
        public int ID { get; set; }

        public int MWBatchID { get; set; }

        public int CSRBatchID { get; set; }

        public string TransactionType { get; set; }

        public string Status { get; set; }
    }
}
