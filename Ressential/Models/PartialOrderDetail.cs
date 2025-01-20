using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(OrderDetailMetadata))]
    public partial class OrderDetail
    {
    }

    public class OrderDetailMetadata
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Quantity > 0")]
        public decimal ProductQuantity { get; set; }

        [Required]
        //[Range(0.01, double.MaxValue, ErrorMessage = "Price > 0")]
        public decimal ProductPrice { get; set; }
    }
}
