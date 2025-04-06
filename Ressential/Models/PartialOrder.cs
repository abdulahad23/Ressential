using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(OrderMetadata))]
    public partial class Order
    {

    }

    public class OrderMetadata
    {
        [Required(ErrorMessage = "Order Date is required")]
        public DateTime OrderDate { get; set; }

        [Required]
        [StringLength(20)]
        [RegularExpression(@"DineIn|TakeAway|Online", ErrorMessage = "Invalid Order Type")]
        public string OrderType { get; set; }

        [StringLength(10)]
        public string TableNo { get; set; }

        [Required]
        [StringLength(20)]
        [RegularExpression(@"CashOnDelivery|Cash|Card", ErrorMessage = "Invalid Payment Method.")]
        public string PaymentMethod { get; set; }

        [Required(ErrorMessage = "Order Total is required")]
        [Range(0, double.MaxValue, ErrorMessage = "OrderTotal must be a positive value.")]
        public decimal OrderTotal { get; set; }

    }
}

