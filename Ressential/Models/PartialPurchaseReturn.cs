using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(PurchaseReturnMetadata))]
    public partial class PurchaseReturn
    {
    }

    public class PurchaseReturnMetadata
    {
        [Required(ErrorMessage = "Purchase Return Date is required")]
        public DateTime PurchaseReturnDate { get; set; }


        [StringLength(50)]
        public string ReferenceNo { get; set; }

        [Required]
        public int VendorID { get; set; }

        [StringLength(255)]
        public string Memo { get; set; }

     
    }
}
