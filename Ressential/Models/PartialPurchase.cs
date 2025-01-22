using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(PurchaseMetadata))]
    public partial class Purchase
    {
    }

    public class PurchaseMetadata
    {
        [Required(ErrorMessage = "Purchase Date is required")]
        public DateTime PurchaseDate { get; set; }

      

        [StringLength(50)]
        public string ReferenceNo { get; set; }

        [Required(ErrorMessage = "Vendor is required")]
        public int VendorId { get; set; }

        [StringLength(255)]
        public string Memo { get; set; }


    }
}
