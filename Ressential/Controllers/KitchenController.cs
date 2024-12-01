using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using Ressential.Models;
using Ressential.Utilities;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using Microsoft.Owin.Security;
using System.Security.Claims;
using System.Data.Entity.Migrations;
using System.Data.Entity.Validation;

namespace Ressential.Controllers
{
    [Authorize]
    public class KitchenController : Controller
    {
        DB_RessentialEntities _db = new DB_RessentialEntities();
        public ActionResult Index() { 
            return View();
        }
        public ActionResult ItemList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var branchItems = _db.BranchItems.AsQueryable().Where(c => c.BranchId == selectedBranchId);
            
            if (!string.IsNullOrEmpty(search))
            {
                branchItems = branchItems.Where(c => c.Item.ItemName.Contains(search) && c.BranchId == selectedBranchId);
            }
            return View(branchItems.ToList());
        }
        public ActionResult CreateItem()
        {
            ViewBag.items = _db.Items.ToList();

            return View();
        }
        [HttpPost]
        public ActionResult CreateItem(BranchItem branchItems)
        {
            try
            {
                branchItems.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                branchItems.CreatedAt = DateTime.Now;
                branchItems.BranchId = Convert.ToInt32(Helper.GetUserInfo("branchId")); ;
                _db.BranchItems.Add(branchItems);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Item created successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while creating the Item.";
                return RedirectToAction("Index", "Error");

            }
            ViewBag.items = _db.Items.ToList();

            return RedirectToAction("ItemList");
        }
        [HttpPost]
        public ActionResult DeleteItem(int BranchItemId)
        {
            try
            {
                var BranchItem = _db.BranchItems.Find(BranchItemId);
                if (BranchItem == null)
                {
                    TempData["ErrorMessage"] = "Item not found.";
                    return RedirectToAction("ItemList");
                }
                _db.BranchItems.Remove(BranchItem);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Item deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This Item is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Item.";
                }
            }
            return RedirectToAction("ItemList");
        }
        [HttpPost]

        public ActionResult DeleteSelectedItems(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var itemsToDelete = _db.BranchItems.Where(c => selectedItems.Contains(c.BranchItemId)).ToList();

                    _db.BranchItems.RemoveRange(itemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Items deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "An Item is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Item.";
                    }
                }
            }
            return RedirectToAction("ItemList");

        }
        public ActionResult EditItem(int BranchItemId)
        {
            var branchItems = _db.BranchItems.Find(BranchItemId);
            if (branchItems == null)
            {
                return HttpNotFound();
            }
            ViewBag.items = _db.Items.ToList();

            return View(branchItems);
        }

        [HttpPost]
        public ActionResult EditItem(BranchItem branchItem)
        {
            try
            {
                var existingItem = _db.BranchItems.Find(branchItem.BranchItemId);
                if (existingItem == null)
                {
                    return HttpNotFound();
                }
                branchItem.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                branchItem.ModifiedAt = DateTime.Now;
                _db.Entry(existingItem).CurrentValues.SetValues(branchItem);
                _db.Entry(existingItem).State = EntityState.Modified;
                _db.Entry(existingItem).Property(x => x.CreatedBy).IsModified = false;
                _db.Entry(existingItem).Property(x => x.CreatedAt).IsModified = false;
                _db.Entry(existingItem).Property(x => x.BranchId).IsModified = false;
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Item updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the Item.";
            }
            return RedirectToAction("ItemList");
        }
        public ActionResult RequisitionList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var requisition = _db.Requisitions.Where(c => c.BranchId == selectedBranchId);
            if (!string.IsNullOrEmpty(search))
            {
                requisition = requisition.Where(c => (c.RequisitionDetails.Where(i => i.Item.ItemName.Contains(search)).Count()>0 || c.RequisitionNo.Contains(search) || c.Description.Contains(search)) && c.BranchId == selectedBranchId);
            }
            return View(requisition.ToList());

        }
        public ActionResult CreateRequisition()
        {
            var requisition = new Requisition
            {
                RequisitionDetails = new List<RequisitionDetail>
                {
                    new RequisitionDetail()
                }

            };
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();

            return View(requisition);
        }
        [HttpPost]
        public ActionResult CreateRequisition(Requisition requisition)
        {
            try
            {
                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                if (ModelState.IsValid)
                {
                    string datePart = DateTime.Now.ToString("yyyyMMdd");
                    int nextRequisitionNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.Requisitions.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextRequisitionNumber = _db.Requisitions
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.RequisitionNo.Substring(13)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    requisition.RequisitionNo = $"REQ-{datePart}{nextRequisitionNumber:D4}";
                    requisition.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    requisition.CreatedAt = DateTime.Now;
                    requisition.BranchId = selectedBranchId;
                    requisition.Status = "Pending";
                    _db.Requisitions.Add(requisition);
                    _db.SaveChanges();

                    //foreach (var requisitionDetails in requisition.RequisitionDetails)
                    //{
                    //    var currentItemStock = _db.WarehouseItemStocks.Where(i => i.ItemId == purchaseDetails.ItemId).FirstOrDefault();
                    //    decimal currentQuantity = currentItemStock.Quantity;
                    //    currentItemStock.Quantity = currentQuantity + purchaseDetails.Quantity;
                    //    currentItemStock.CostPerUnit = ((currentQuantity * currentItemStock.CostPerUnit) + (purchaseDetails.Quantity * purchaseDetails.UnitPrice)) / (currentItemStock.Quantity);

                    //    var warehouseItemTransaction = new WarehouseItemTransaction
                    //    {
                    //        TransactionDate = purchase.PurchaseDate,
                    //        ItemId = purchaseDetails.ItemId,
                    //        TransactionType = "Purchase",
                    //        TransactionTypeId = purchase.PurchaseId,
                    //        Quantity = purchaseDetails.Quantity,
                    //        CostPerUnit = purchaseDetails.UnitPrice
                    //    };
                    //    _db.WarehouseItemStocks.AddOrUpdate(currentItemStock);
                    //    _db.WarehouseItemTransactions.Add(warehouseItemTransaction);
                    //}
                    //_db.SaveChanges();

                    return Json("0", JsonRequestBehavior.AllowGet);
                }
                
                ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
                return View(requisition);
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                Console.WriteLine($"Error creating purchase: {ex.Message}");
                return RedirectToAction("Index", "Error");
            }
        }
        public ActionResult EditRequisition(int requisitionId)
        {
            Requisition requisition = _db.Requisitions.Find(requisitionId);
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
            return View(requisition);
        }
        [HttpPost]
        public ActionResult EditRequisition(Requisition requisition)
        {
            if (requisition == null)
            {
                foreach (var state in ModelState)
                {
                    var key = state.Key; // Property name
                    var errors = state.Value.Errors; // List of errors for this property

                    foreach (var error in errors)
                    {
                        System.Diagnostics.Debug.WriteLine($"Key: {key}, Error: {error.ErrorMessage}");
                    }
                }

                return Json(new { status = "error", message = "Invalid data provided" }, JsonRequestBehavior.AllowGet);
            }

            using (var transaction = _db.Database.BeginTransaction())
            {
                try
                {
                    var existingRequisition = _db.Requisitions.Include(p => p.RequisitionDetails).FirstOrDefault(p => p.RequisitionId == requisition.RequisitionId);
                    if (existingRequisition == null)
                    {
                        return Json(new { status = "error", message = "Purchase not found" }, JsonRequestBehavior.AllowGet);
                    }

                    // Update purchase metadata
                    requisition.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    requisition.ModifiedAt = DateTime.Now;

                    // Update existing purchase values
                    _db.Entry(existingRequisition).CurrentValues.SetValues(requisition);
                    _db.Entry(existingRequisition).Property(x => x.CreatedBy).IsModified = false;
                    _db.Entry(existingRequisition).Property(x => x.CreatedAt).IsModified = false;
                    _db.Entry(existingRequisition).Property(x => x.BranchId).IsModified = false;
                    _db.Entry(existingRequisition).Property(x => x.Status).IsModified = false;

                    // Update PurchaseDetails
                    var existingDetails = existingRequisition.RequisitionDetails.ToList();
                    var newDetails = requisition.RequisitionDetails;

                    // Update or Add new details
                    foreach (var newDetail in newDetails)
                    {
                        var existingDetail = existingDetails.FirstOrDefault(d => d.RequisitionDetailId == newDetail.RequisitionDetailId);
                        //var warehouseItemStock = _db.WarehouseItemStocks.Find(newDetail.ItemId);
                        if (existingDetail != null)
                        {
                            //decimal RevertedQuantity = warehouseItemStock.Quantity - existingDetail.Quantity;
                            
                            //warehouseItemStock.Quantity = RevertedQuantity + newDetail.Quantity; //Updated Quantity
                            //if (warehouseItemStock.Quantity == 0)
                            //{
                            //    warehouseItemStock.CostPerUnit = 0;
                            //}
                            //else
                            //{
                            //    warehouseItemStock.CostPerUnit = ((warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit) - (existingDetail.Quantity * existingDetail.UnitPrice) + (newDetail.Quantity * newDetail.UnitPrice)) / warehouseItemStock.Quantity; //Updated Per Unit Cost
                            //}

                            _db.Entry(existingDetail).CurrentValues.SetValues(newDetail);
                            existingDetails.Remove(existingDetail); // Remove from existing list once matched
                        }
                        else
                        {
                            //decimal oldQuantity = warehouseItemStock.Quantity;
                            //warehouseItemStock.Quantity = warehouseItemStock.Quantity + newDetail.Quantity; //Updated Quantity
                            //if (warehouseItemStock.Quantity == 0)
                            //{
                            //    warehouseItemStock.CostPerUnit = 0;
                            //}
                            //else
                            //{
                            //    warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) + (newDetail.Quantity * newDetail.UnitPrice)) / warehouseItemStock.Quantity; //Updated Per Unit Cost
                            //}
                            existingRequisition.RequisitionDetails.Add(newDetail); // Add new detail
                        }
                    }

                    // Delete unmatched details
                    if (existingDetails != null)
                    {
                        //foreach (var item in existingDetails)
                        //{
                            //var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);
                            //decimal oldQuantity = warehouseItemStock.Quantity;
                            //warehouseItemStock.Quantity = warehouseItemStock.Quantity - item.Quantity;
                            //if (warehouseItemStock.Quantity == 0)
                            //{
                            //    warehouseItemStock.CostPerUnit = 0;
                            //}
                            //else
                            //{
                            //    warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) - (item.Quantity * item.UnitPrice)) / (warehouseItemStock.Quantity == 0 ? 1 : warehouseItemStock.Quantity);
                            //}
                        //}
                        _db.RequisitionDetails.RemoveRange(existingDetails);
                    }


                    // Save changes
                    _db.SaveChanges();
                    transaction.Commit();

                    TempData["SuccessMessage"] = "Item updated successfully.";
                    return Json(new { status = "success" }, JsonRequestBehavior.AllowGet);
                }
                catch (DbEntityValidationException ex)
                {
                    transaction.Rollback();

                    // Log the validation errors for debugging
                    foreach (var validationError in ex.EntityValidationErrors)
                    {
                        foreach (var error in validationError.ValidationErrors)
                        {
                            // Log property name and error message
                            System.Diagnostics.Debug.WriteLine($"Property: {error.PropertyName}, Error: {error.ErrorMessage}");
                        }
                    }

                    TempData["ErrorMessage"] = "An error occurred while updating the Item.";
                    return Json(new { status = "error", message = "Validation error occurred. Check logs for details." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        [HttpPost]
        public ActionResult DeleteRequisition(int requisitionId)
        {
            try
            {
                var requisition = _db.Requisitions.Find(requisitionId);
                if (requisition == null)
                {
                    TempData["ErrorMessage"] = "Requisition not found.";
                    return RedirectToAction("RequisitionList");
                }
                var requisitionDetails = _db.RequisitionDetails.Select(p => p).Where(p => p.RequisitionId == requisitionId);

                //if (requisitionDetails != null)
                //{
                //    foreach (var item in requisitionDetails)
                //    {
                //        var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);
                //        decimal oldQuantity = warehouseItemStock.Quantity;
                //        warehouseItemStock.Quantity = warehouseItemStock.Quantity - item.Quantity;
                //        if (warehouseItemStock.Quantity == 0)
                //        {
                //            warehouseItemStock.CostPerUnit = 0;
                //        }
                //        else
                //        {
                //            warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) - (item.Quantity * item.UnitPrice)) / warehouseItemStock.Quantity;
                //        }
                //    }
                //}
                _db.RequisitionDetails.RemoveRange(requisitionDetails);
                _db.Requisitions.Remove(requisition);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Requisition deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This requisition is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the requisition.";
                }
            }
            return RedirectToAction("RequisitionList");
        }
        [HttpPost]
        public ActionResult DeleteSelectedRequisitions(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var requisitionsToDelete = _db.Requisitions.Where(c => selectedItems.Contains(c.RequisitionId)).ToList();
                    var requisitionDetails = _db.RequisitionDetails.Where(c => selectedItems.Contains(c.RequisitionId)).ToList();

                    //foreach (var item in requisitionsToDelete)
                    //{
                    //    var warehouseItemTransaction = _db.WarehouseItemTransactions.Where(w => w.TransactionType == "Requisition" && w.TransactionTypeId == item.RequisitionId);
                    //    _db.WarehouseItemTransactions.RemoveRange(warehouseItemTransaction);
                    //}

                    //if (requisitionDetails != null)
                    //{
                    //    foreach (var item in requisitionDetails)
                    //    {
                    //        var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);
                    //        decimal oldQuantity = warehouseItemStock.Quantity;
                    //        warehouseItemStock.Quantity = warehouseItemStock.Quantity - item.Quantity;
                    //        if (warehouseItemStock.Quantity == 0)
                    //        {
                    //            warehouseItemStock.CostPerUnit = 0;
                    //        }
                    //        else
                    //        {
                    //            warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) - (item.Quantity * item.UnitPrice)) / warehouseItemStock.Quantity;
                    //        }
                    //    }
                    //}

                    _db.RequisitionDetails.RemoveRange(requisitionDetails);
                    _db.Requisitions.RemoveRange(requisitionsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Requisitions deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A requisition is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the requisition.";
                    }
                }
            }
            return RedirectToAction("RequisitionList");
        }
        public ActionResult ReceiveStockList()
        {
            return View();
        }

        public ActionResult StockReturnList()
        {
            return View();
        }
        public ActionResult CreateStockReturn()
        {
            return View();
        }
        public ActionResult ConsumeList()
        {
            return View();
        }
        public ActionResult CreateConsume()
        {
            return View();
        }
        public ActionResult WastageList()
        {
            return View();
        }
        public ActionResult CreateWastage()
        {
            return View();
        }
        public ActionResult CreateCategory()
        {
            return View();
        }
        [HttpPost]
        public ActionResult CreateCategory(ProductCategory productCategory)
        {
            try
            {

                productCategory.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                productCategory.CreatedAt = DateTime.Now;
                _db.ProductCategories.Add(productCategory);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Product Category created successfully.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while creating the Product Category.";
            }
            return RedirectToAction("CategoryList");
        }

        public ActionResult CategoryList(string search)
        {
            var productCategory = _db.ProductCategories.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                productCategory = productCategory.Where(c => c.ProductCategoryName.Contains(search));
            }
            return View(productCategory.ToList());
        }

        public ActionResult EditCategory(int ProductCategoryId)
        {
            var ProductCategory = _db.ProductCategories.Find(ProductCategoryId);
            if (ProductCategory == null)
            {
                return HttpNotFound();
            }
            return View(ProductCategory);
        }
        [HttpPost]
        public ActionResult EditCategory(ProductCategory productCategory)
        {
            try
            {
                var existingItem = _db.ProductCategories.Find(productCategory.ProductCategoryId);
                if (existingItem == null)
                {
                    return HttpNotFound();
                }
                productCategory.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                productCategory.ModifiedAt = DateTime.Now;
                _db.Entry(existingItem).CurrentValues.SetValues(productCategory);
                _db.Entry(existingItem).State = EntityState.Modified;
                _db.Entry(existingItem).Property(x => x.CreatedBy).IsModified = false;
                _db.Entry(existingItem).Property(x => x.CreatedAt).IsModified = false;
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Product Category updated successfully.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the Product Category.";
            }

            return RedirectToAction("CategoryList");
        }



        [HttpPost]
        public ActionResult DeleteCategory(int ProductCategoryId)
        {
            try
            {
                var ProductCategory = _db.ProductCategories.Find(ProductCategoryId);
                if (ProductCategory == null)
                {
                    TempData["ErrorMessage"] = "Product Category not found.";
                    return RedirectToAction("CategoryList");
                }
                _db.ProductCategories.Remove(ProductCategory);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Product Category  deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This Product Category  is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Product Category .";
                }
            }
            return RedirectToAction("CategoryList");
        }
        [HttpPost]

        public ActionResult DeleteSelectedCategory(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var itemsToDelete = _db.ProductCategories.Where(c => selectedItems.Contains(c.ProductCategoryId)).ToList();

                    _db.ProductCategories.RemoveRange(itemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Product Category  deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "An Product Category  is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Product Category .";
                    }
                }
            }
            return RedirectToAction("CategoryList");

        }
        public ActionResult ProductList()
        {
            return View();
        }
        public ActionResult CreateProduct()
        {
            return View();
        }
        public ActionResult OrderList()
        {
            return View();
        }
        public ActionResult CreateOrder()
        {
            return View();
        }
        public ActionResult OrderReturnList()
        {
            return View();
        }
        public ActionResult CreateOrderReturn()
        {
            return View();
        }
        public ActionResult OrderView()
        {
            return View();
        }
        public ActionResult ChefView()
        {
            return View();
        }

        public ActionResult UserList()
        {
            var users = _db.Users.Include(u => u.Roles).ToList();
            return View(users);
        }
        public ActionResult CreateUser()
        {
            ViewBag.Roles = new SelectList(_db.Roles.ToList(), "RoleID", "RoleName");
            return View();
        }

        [HttpPost]
        public ActionResult CreateUser(User user, String ConfirmPassword)
        {
            if (user.Password != ConfirmPassword)
            {
                ModelState.AddModelError("", "Password and Confirm Password do not match.");
                ViewBag.Roles = new SelectList(_db.Roles.ToList(), "RoleID", "RoleName");
                return View(user);
            }

            _db.Users.Add(user);
            _db.SaveChanges();
            return RedirectToAction("Index");

            //if (ModelState.IsValid)
            //{

            //}

            //ViewBag.Roles = new SelectList(_db.Roles.ToList(), "RoleID", "RoleName");
            //return View(user);
        }

        [ChildActionOnly]
        public ActionResult BranchesDropdown()
        {
            var branches = _db.Branches.Where(b => b.IsActive).ToList();

            // Get the selected branchId from claims
            var branchIdClaim = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.SelectedBranchId = branchIdClaim;
            return PartialView("_BranchesDropdown", branches);
        }

        [HttpPost]
        public ActionResult SetBranchClaim(int branchId)
        {
            var claimsIdentity = User.Identity as ClaimsIdentity;

            // Remove existing branch claim if exists
            var existingClaim = claimsIdentity?.FindFirst("BranchId");
            if (existingClaim != null)
            {
                claimsIdentity.RemoveClaim(existingClaim);
            }

            // Add the new branch claim
            claimsIdentity?.AddClaim(new Claim("BranchId", branchId.ToString()));

            // Update the authentication cookie
            var ctx = Request.GetOwinContext();
            var authenticationManager = ctx.Authentication;
            authenticationManager.AuthenticationResponseGrant = new AuthenticationResponseGrant(
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties() { IsPersistent = true }
            );

            return Json(new { success = true });
        }
    }
}