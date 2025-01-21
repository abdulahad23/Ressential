using System;
using System.ComponentModel.DataAnnotations;
namespace Ressential.Models
{
    [MetadataType(typeof(PaymentVoucherMetadata))]
    public partial class PaymentVoucher
    {
        // Additional methods or properties can be added here
    }

    public class PaymentVoucherMetadata
    {
    
        [Required(ErrorMessage = "Payment Voucher Date is required")]
        public DateTime PaymentVoucherDate { get; set; }

        [Required(ErrorMessage = "Vendor ID is required")]
        public int VendorId { get; set; }

        [Required(ErrorMessage = "Account ID is required")]
        public int AccountId { get; set; }

        [StringLength(50, ErrorMessage = "Instrument Number cannot exceed 50 characters")]
        public string InstrumentNo { get; set; }

        public DateTime? InstrumentDate { get; set; }

        [Required(ErrorMessage = "Amount is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Amount must be a positive value")]
        public decimal Amount { get; set; }

      
       
    }
}
