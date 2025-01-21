using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(ReturnStockMetadata))]
    public partial class ReturnStock
    {
    }

    public class ReturnStockMetadata
    {
        [Required(ErrorMessage = "Return Date is required")]
        public DateTime ReturnDate { get; set; }


        [StringLength(50)]
        public string ReferenceNo { get; set; }

    
        [StringLength(255)]
        public string Description { get; set; }

       
    }
}
