using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(OnlineOrderDetailMetadata))]
    public partial class OnlineOrderDetail
    {
    }

    public class OnlineOrderDetailMetadata
    {
        [Key]
        public int OnlineOrderDetailId { get; set; }

        [Required(ErrorMessage = "Order ID is required")]
        public int OrderId { get; set; }

        [Required(ErrorMessage = "Customer ID is required")]
        public int CustomerId { get; set; }

        [StringLength(255)]
        public string Address { get; set; }

        [StringLength(255)]
        public string City { get; set; }

        [StringLength(255)]
        public string Country { get; set; }

        [Required(ErrorMessage = "Contact Number is required")]
        [StringLength(20, ErrorMessage = "Contact Number cannot exceed 20 characters.")]
        public string ContactNo { get; set; }

        [Required(ErrorMessage = "Delivery Charges are required")]
        [Range(0, int.MaxValue, ErrorMessage = "Delivery Charges must be a non-negative value.")]
        public int DeliveryCharges { get; set; }
    }
}
