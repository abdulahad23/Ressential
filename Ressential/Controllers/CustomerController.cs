using Ressential.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Runtime.CompilerServices;
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
        private static string otpCode;
        private static string emailAddress;
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
            decimal deliveryCharges = OnlineOrder.deliveryCharges;
            ViewBag.DeliveryCharges = deliveryCharges;
            if (totalAmount == 0)
            {
                deliveryCharges = 0;
                ViewBag.DeliveryCharges = deliveryCharges;
            }
            ViewBag.Subtotal = totalAmount;
            ViewBag.CartTotal = totalAmount + deliveryCharges;
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
            decimal deliveryCharges = OnlineOrder.deliveryCharges;

            // Store the total amount in ViewBag so it can be accessed in the view
            ViewBag.CartSubtotal = totalAmount;
            ViewBag.DeliveryCharges = deliveryCharges;
            ViewBag.CartTotal = totalAmount+deliveryCharges;

            ViewBag.Customer = _db.Customers.Find(Session["CustomerId"]);

            // Pass the cart list to the Checkout view
            return View(cartList);
        }
        
        public ActionResult Shop(int page = 1, int? categoryId = null)
        {
            var categories = _db.ProductCategories.ToList(); // Fetch categories from the database
            ViewBag.Categories = categories;

            if (Session["BranchId"] == null)
            {
                RedirectToAction("Index");
            }
            var branchId = Convert.ToInt32(Session["BranchId"]);

            var products = _db.Products.Where(p => p.BranchId == branchId);
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
                var cartTotal = obj.list.Sum(item => item.TotalPrice) + OnlineOrder.deliveryCharges;
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
            decimal deliveryCharges = OnlineOrder.deliveryCharges;
            decimal cartSubtotal = cart?.Sum(x => x.TotalPrice) ?? 0;
            decimal cartTotal = cart?.Sum(x => x.TotalPrice)+ deliveryCharges ?? 0;
            if (cartTotal == 0)
            {
                deliveryCharges = 0;
            }
            int cartCount = cart?.Count ?? 0;

            return Json(new { cartTotal = cartTotal, cartCount = cartCount, deliveryCharges = deliveryCharges, cartSubtotal = cartSubtotal });
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
                decimal cartSubtotal = cart.Sum(item => item.TotalPrice) ?? 0;
                decimal cartTotal = cart.Sum(item => item.TotalPrice)+OnlineOrder.deliveryCharges ??0;
                int cartCount = cart.Count;

                // Return the updated cart count, total, and item totals in JSON format
                return Json(new
                {
                    cartCount,
                    cartTotal,
                    cartSubtotal,
                    updatedItemTotal = cart.FirstOrDefault(item => item.ProductID == productId)?.TotalPrice
                });
            }

            return Json(new { cartCount = 0, cartTotal = 0, cartSubtotal = 0, updatedItemTotal = 0 });
        }

        [HttpPost]
        public ActionResult PlaceOrder(string paymentMethod, bool saveDetails, string streetAddress, string city, string phone)
        {
            try
            {
                // Get cart items from session
                List<Cart> cartList = Session["Cart"] as List<Cart>;

                if (cartList == null || cartList.Count == 0)
                {
                    // If cart is empty, redirect to Cart page
                    return RedirectToAction("Cart");
                }

                decimal totalAmount = cartList.Sum(item => item.TotalPrice) ?? 0;
                decimal deliveryCharges = OnlineOrder.deliveryCharges;
                totalAmount += deliveryCharges;

                var branchId = Convert.ToInt32(Session["BranchId"]);
                var customerId = Convert.ToInt32(Session["CustomerId"]);

                int nextOrderNumber = 1;

                // Check if there are any existing orders for the current branch
                if (_db.Orders.Any(o => o.BranchId == branchId))
                {
                    nextOrderNumber = _db.Orders
                        .Where(o => o.BranchId == branchId)
                        .AsEnumerable() // Force in-memory execution
                        .Select(o =>
                        {
                            int orderNo;
                            return int.TryParse(o.OrderNo, out orderNo) ? orderNo : 0;
                        })
                        .Max() + 1;
                }

                // Format OrderNo with a 5-digit sequence
                string newOrderNo = nextOrderNumber.ToString("D5");

                // Save order items (cart items) to the OrderItems table
                var orderItem = new Order
                {
                    OrderNo = newOrderNo,
                    PaymentMethod = "CashOnDelivery",
                    OrderDate = DateTime.Now,
                    OrderType = "Online",
                    BranchId = branchId,
                    CustomerId = customerId,
                    Status = "Pending",
                    OrderTotal = totalAmount,
                    OrderDetails = new List<OrderDetail>(),
                    CreatedAt = DateTime.Now,
                    OrderTotalCost = 0,
                };

                var customer = _db.Customers.Find(customerId);
                var onlineOrderDetail = new OnlineOrderDetail
                {
                    OrderId = orderItem.OrderId,
                    CustomerId = customerId,
                    Address = streetAddress,
                    City = city,
                    Country = customer.Country,
                    ContactNo = customer.ContactNo,
                    DeliveryCharges = (int) deliveryCharges

                };

                orderItem.OnlineOrderDetails.Add(onlineOrderDetail);

                foreach (var cartItem in cartList)
                {
                    orderItem.OrderDetails.Add(
                        new OrderDetail
                        {
                            OrderId = orderItem.OrderId,
                            ProductId = cartItem.ProductID,
                            ProductPrice = (decimal)cartItem.Price,
                            ProductQuantity = cartItem.Quantity,
                            ProductStatus = "Pending",
                        }
                    );
                }

                if (saveDetails)
                {
                    // Update the customer's details in the database
                    if (customer != null)
                    {
                        customer.Address = streetAddress;
                        customer.City = city;
                        customer.ContactNo = phone;
                    }
                }

                _db.Orders.Add(orderItem);
                _db.SaveChanges();

                // Clear the session cart after order is placed
                //Session["Cart"] = null;

                // Redirect to the Order Confirmation page or any other confirmation view
                return Json(new { success = true, message = "Your order has been placed successfully!" });
            }
            catch (DbEntityValidationException ex)
            {
                foreach (var validationError in ex.EntityValidationErrors)
                {
                    Console.WriteLine($"Entity of type {validationError.Entry.Entity.GetType().Name} in state {validationError.Entry.State} has validation errors:");
                    foreach (var error in validationError.ValidationErrors)
                    {
                        Console.WriteLine($"- Property: {error.PropertyName}, Error: {error.ErrorMessage}");
                    }
                }
                //throw; // Re-throw to preserve the original stack trace
                return Json(new { success = false, message = "An error occurred while placing the order." });
            }
        }


        public ActionResult BackToHome() {
            Session["Cart"] = null;
            return View("Index");
        }
        public ActionResult ThankYou()
        {
            // Retrieve order details from session or database
            var orderItems = Session["Cart"] as List<Cart>;
            decimal subtotal = orderItems?.Sum(i => i.TotalPrice) ?? 0;
            decimal deliveryCharges = 50; // Example value
            decimal totalAmount = subtotal + deliveryCharges;

            ViewBag.OrderItems = orderItems;
            ViewBag.Subtotal = subtotal;
            ViewBag.DeliveryCharges = deliveryCharges;
            ViewBag.TotalAmount = totalAmount;

            // Clear the cart session after order placement
            Session["Cart"] = null;

            return View();
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
        public ActionResult Register()
        {
            return View();
        }
        [HttpPost]
        public ActionResult Register(Customer customer)
        {
            try
            {
                // Check if a customer with the same email already exists
                var existingCustomer = _db.Customers.FirstOrDefault(c => c.Email == customer.Email);
                if (existingCustomer != null)
                {
                    TempData["ErrorMessage"] = "An account with this email already exists.";
                    return View(customer); // Return the view with the data to allow corrections
                }

                // Set the current time for the CreatedAt field
                customer.CreatedAt = DateTime.Now;

                // Add the customer to the database
                _db.Customers.Add(customer);
                _db.SaveChanges();

                // Set session variables and redirect to the customer dashboard
                Session["IsAuthenticated"] = true;
                Session["CustomerId"] = customer.CustomerId;
                Session["CustomerName"] = customer.CustomerName;

                return RedirectToAction("Index", "Customer");
            }
            catch (Exception ex)
            {
                // Log the exception (optional: integrate logging)
                TempData["ErrorMessage"] = "An error occurred while creating your account. Please try again.";
                return View(customer); // Return the view with data so the user doesn't lose their input
            }
        }
        public ActionResult EditAccount()
        {
            try
            {
                // Get the customer ID from the session (or other authentication context)
                int customerId = Convert.ToInt32(Session["CustomerId"]);
                var customer = _db.Customers.FirstOrDefault(c => c.CustomerId == customerId);

                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Customer not found.";
                    return RedirectToAction("Login", "Customer");
                }

                // Pass the customer data to the view
                return View(customer);
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while fetching your account details.";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        public ActionResult EditAccount(Customer model)
        {
            try
            {
                // Validate the model
                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessage"] = "Invalid data submitted.";
                    return View(model);
                }

                // Get the customer record from the database
                var existingCustomer = _db.Customers.FirstOrDefault(c => c.CustomerId == model.CustomerId);

                if (existingCustomer == null)
                {
                    return RedirectToAction("Login", "Customer");
                }

                // Update the customer's information
                existingCustomer.CustomerName = model.CustomerName;
                existingCustomer.ContactNo = model.ContactNo;
                existingCustomer.Address = model.Address;
                existingCustomer.City = model.City;
                existingCustomer.Country = model.Country;
                existingCustomer.Password = model.Password;


                // Save the changes to the database
                _db.SaveChanges();

                TempData["SuccessMessage"] = "Your account has been updated successfully!";
                return RedirectToAction("Index", "Customer");


            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while updating your account. Please try again.";
                return View(model);
            }
        }

        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                TempData["Message"] = "Email is required.";
                return View();
            }

            // Generate OTP
            Random random = new Random();
            otpCode = random.Next(100000, 999999).ToString();
            emailAddress = email;

            // Send OTP to the user's email
            try
            {
                SendOtpEmail(email, otpCode);
                TempData["Message"] = "OTP has been sent to your email.";
            }
            catch (Exception ex)
            {
                TempData["Message"] = "Error sending OTP: " + ex.Message;
            }

            return RedirectToAction("VerifyOtp");
        }

        public ActionResult VerifyOtp()
        {
            return View();
        }

        [HttpPost]
        public ActionResult VerifyOtp(string otp)
        {
            if (otp == otpCode)
            {
                TempData["Message"] = "OTP verified. You can now reset your password.";
                return RedirectToAction("ResetPassword");
            }

            TempData["Message"] = "Invalid OTP. Please try again.";
            return View();
        }

        public ActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ResetPassword(string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                TempData["Message"] = "Passwords do not match.";
                return View();
            }

            var customer = _db.Customers.Where(c => c.Email == emailAddress).FirstOrDefault();
            customer.Password = newPassword;
            _db.SaveChanges();
            TempData["Message"] = "Password has been reset successfully.";
            return RedirectToAction("Login");
        }

        private void SendOtpEmail(string email, string otp)
        {
            var fromAddress = new MailAddress("myressential@gmail.com", "Restaurant Portal");
            var toAddress = new MailAddress(email);
            const string fromPassword = "fgio azrf ibzt ccly"; // Gmail app password or generated token
            const string subject = "Your OTP for Password Reset";
            string body = $"Your OTP for password reset is: {otp}. This OTP is valid for 10 minutes.";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };

            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body
            })
            {
                smtp.Send(message);
            }
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
            return Json(new { success = true });
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