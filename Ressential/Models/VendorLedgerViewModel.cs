using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ressential.Models
{
    public class VendorLedgerViewModel
    {
        public string ReferenceNo { get; set; }
        public DateTime Date { get; set; }
        public string VendorName { get; set; }
        public string Memo { get; set; }
        public string Account { get; set; }
        public string InstrumentNo { get; set; }
        public DateTime? InstrumentDate { get; set; }
        public decimal Dr { get; set; }
        public decimal Cr { get; set; }
        public decimal Balance { get; set; }
    }
}
