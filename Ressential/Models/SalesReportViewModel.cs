using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ressential.Models
{
    public class SalesReportViewModel
    {
        public int OrderId { get; set; }
        public string OrderNo { get; set; }
        public DateTime OrderDate { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string OrderType { get; set; }
        public string Status { get; set; }
        public string PaymentMethod { get; set; }
        public decimal OrderTotal { get; set; }
        public List<OrderItemDetail> Items { get; set; }
    }

    public class OrderItemDetail
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductCode { get; set; }
        public decimal ProductQuantity { get; set; }
        public decimal ProductPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string ProductStatus { get; set; }
    }
} 