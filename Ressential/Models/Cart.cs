namespace Ressential.Models
{
    using System;
    using System.Collections.Generic;

    public class Cart
    {
        public int ProductID { get; set; }
        public int Quantity { get; set; }
        public string ProductName { get; set; }
        public string ProductImage { get; set; }
        public Nullable<decimal> Price { get; set; }
        public Nullable<decimal> TotalPrice { get; set; }
    }
    public class TempTblCart
    {
        public List<Cart> list { get; set; }
        public TempTblCart()
        {
            list = new List<Cart>();
        }
    }


}
