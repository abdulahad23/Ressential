using Ressential.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Services.Description;

namespace Ressential.Controllers
{
    public class CustomerController : BaseController
    {
        DB_RessentialEntities _db = new DB_RessentialEntities();
        public ActionResult Index()
        {
            
            return View();
        }

        public ActionResult Cart()
        {
            List<Cart> cartList = Session["Cart"] as List<Cart>;

            if (cartList == null)
            {
                cartList = new List<Cart>(); // Initialize an empty list if no cart exists
            }
            decimal totalAmount = cartList.Sum(item => item.TotalPrice)??0; 

            ViewBag.CartTotal = totalAmount;
            // Pass the cart items to the Cart view
            return View(cartList);
        }

        public ActionResult Checkout()
        {
            List<Cart> cartList = Session["Cart"] as List<Cart>;

            // Initialize the cart list if it's null (i.e., if no cart exists)
            if (cartList == null)
            {
                cartList = new List<Cart>();
            }

            // Calculate the total amount of the cart
            decimal totalAmount = cartList.Sum(item => item.TotalPrice) ?? 0;

            // Store the total amount in ViewBag so it can be accessed in the view
            ViewBag.CartTotal = totalAmount;

            // Pass the cart list to the Checkout view
            return View(cartList);
        }
        
        public ActionResult Shop(int page = 1, int? categoryId = null)
        {
            var categories = _db.ProductCategories.ToList(); // Fetch categories from the database
            ViewBag.Categories = categories;

            var products = _db.Products.AsQueryable();
            if (categoryId.HasValue)
            {
                products = products.Where(p => p.ProductCategoryId == categoryId.Value);
            }

            var pageSize = PaginationConstraints.CustomerShopPageSize;

            int totalItems = products.Count();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var paginatedProducts = products
                .OrderBy(p => p.ProductName) // Sorting
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.SelectedCategoryId = categoryId;

            return View(paginatedProducts);
        }


        [HttpPost]
        public ActionResult AddToCart(int productId)
        {
            if (Session["IsAuthenticated"] == null || !(bool)Session["IsAuthenticated"])
            {
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }
                return RedirectToAction("Login", "Account");
            }
            List<Cart> cartList = Session["Cart"] as List<Cart>;
            TempTblCart obj = new TempTblCart();
            if (cartList!=null)
            {
                obj.list = cartList;
            }
            
          

            var product = _db.Products.FirstOrDefault(p => p.ProductId == productId);
            decimal price = product.ProductPrice;
            decimal totalAmount = price * 1;
            var cartItem = new Cart
            {
                ProductID = productId,
                ProductName = product.ProductName,
                Quantity = 1,
                Price = price,
                TotalPrice = totalAmount,
                ProductImage = product.ProductImage
            };

            if (obj.list != null && obj.list.Count() > 0)
            {
               
                var row = obj.list.Where(m => m.ProductID == productId).FirstOrDefault();
                if (row!=null)
                {
                    row.Quantity += 1;
                    row.TotalPrice += price;
                    //update
                }
                else
                {
                    obj.list.Add(cartItem);
                }
            }
            else
            {
                obj.list.Add(cartItem);

            }

            Session["Cart"] = obj.list;

            if (Request.IsAjaxRequest())
            {
                // Return JSON result with updated cart data (you can return total cart count, etc.)
                var cartCount = obj.list.Count;
                var cartTotal = obj.list.Sum(item => item.TotalPrice);
                return Json(new { success = true, cartCount, cartTotal });
            }
            return RedirectToAction("Shop", "Customer");
        
        }

        [HttpGet]
        public ActionResult EmptyCart()
        {
            Session["Cart"] = null; // Clear the cart session
            return RedirectToAction("Cart"); // Redirect back to the Cart page
        }


        [HttpPost]
        public JsonResult RemoveFromCart(int productId)
        {
            
            var cart = Session["Cart"] as List<Cart>;
            var item = cart?.FirstOrDefault(x => x.ProductID == productId);

            if (item != null)
            {
                cart.Remove(item);
            }

            // Recalculate the cart total
            decimal cartTotal = cart?.Sum(x => x.TotalPrice) ?? 0;
            int cartCount = cart?.Count ?? 0;

            return Json(new { cartTotal = cartTotal, cartCount = cartCount });
        }


        [HttpPost]
        public JsonResult UpdateCart(int productId, int quantity)
        {
            // Retrieve the cart from session
            var cart = Session["Cart"] as List<Cart>;

            if (cart != null)
            {
                // Find the item to update
                var itemToUpdate = cart.FirstOrDefault(item => item.ProductID == productId);
                if (itemToUpdate != null)
                {
                    decimal price = itemToUpdate.Price ?? 0;
                    itemToUpdate.Quantity = quantity;
                    itemToUpdate.TotalPrice = price * quantity; // Update total price
                }

                // Recalculate the total and item count
                decimal cartTotal = cart.Sum(item => item.TotalPrice) ??0;
                int cartCount = cart.Count;

                // Return the updated cart count, total, and item totals in JSON format
                return Json(new
                {
                    cartCount,
                    cartTotal,
                    updatedItemTotal = cart.FirstOrDefault(item => item.ProductID == productId)?.TotalPrice
                });
            }

            return Json(new { cartCount = 0, cartTotal = 0, updatedItemTotal = 0 });
        }


        [HttpPost]
        public ActionResult PlaceOrder(string paymentMethod)
        {
            // Get cart items from session
            List<Cart> cartList = Session["Cart"] as List<Cart>;

            if (cartList == null || cartList.Count == 0)
            {
                // If cart is empty, redirect to Cart page
                return RedirectToAction("Cart");
            }


            decimal TotalAmount = cartList.Sum(item => item.TotalPrice) ?? 0;
            var lastOrder = _db.Orders
            .OrderByDescending(o => o.OrderId)
            .FirstOrDefault();

            int nextOrderNumber = 1;

            if (lastOrder != null)
            {
                // Increment the order ID based on the last one
                nextOrderNumber = lastOrder.OrderId + 1;
            }

            // Format the new OrderID with the prefix "RS" and zero-padded number
            string newOrderNo = $"OD-{nextOrderNumber:D8}";

            // Save order items (cart items) to the OrderItems table

            var orderItem = new Order
            {
                OrderNo = newOrderNo,
                PaymentMethod = paymentMethod,
                OrderDate = DateTime.Now,
                OrderType = "Online",
                BranchId = 1,
                CustomerId = 1,
                Status = "In-process",
                OrderDetails = new List<OrderDetail>()
            };


            foreach (var cartItem in cartList)
            {
                orderItem.OrderDetails.Add(
                    new OrderDetail {
                        OrderId = orderItem.OrderId,
                        ProductId = cartItem.ProductID,
                        ProductPrice = (decimal)cartItem.Price,
                        ProductQuantity = cartItem.Quantity,
                    }
                    );
            }
            _db.Orders.Add(orderItem);
            _db.SaveChanges();
            // Clear the session cart after order is placed
            Session["Cart"] = null;
            // Redirect to the Order Confirmation page or any other confirmation view
            return Json(new { success = true, message = "Your order has been placed successfully!" });
        }
        protected void Page_Load(object sender, EventArgs e)
        {
            // Disable caching to ensure the latest version of the page is served
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.Cache.SetExpires(DateTime.UtcNow.AddDays(-1));
            Response.Cache.SetNoStore();
        }
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(string email, string password)
        {
            var customer = _db.Customers.FirstOrDefault(c => c.Email == email && c.Password == password);

            if (customer != null)
            {
                // Set session variable
                Session["IsAuthenticated"] = true;
                Session["CustomerId"] = customer.CustomerId;
                Session["CustomerName"] = customer.CustomerName;

                // Redirect to the desired page
                return RedirectToAction("Index", "Customer");
            }
            else
            {
                TempData["ErrorMessage"] = "Invalid email or password!";
            }
            return View();
        }
        public ActionResult Logout()
        {
            Session.Clear(); // Clear all session variables
            return RedirectToAction("Login", "Customer");
        }
        [HttpGet]
        public JsonResult GetBranches()
        {
            var branches = _db.Branches.Select(b => new { b.BranchId, b.BranchName}).ToList();
            return Json(branches, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult SetBranch(int branchId, string branchName)
        {
            Session["BranchId"] = branchId;
            Session["BranchName"] = branchName;
            return new HttpStatusCodeResult(200); // Success
        }

        public ActionResult ContactUs()
        {
            return View();
        }
        public ActionResult About()
        {
            return View();
        }



    }
}