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
using System.Reflection;
using System.Web.WebPages;
using System.IO;
using System.Data.Entity.Migrations;
using System.Data.Entity.Validation;
using Microsoft.Owin.Security;
using System.Security.Claims;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Text;
using OfficeOpenXml;
using System.ComponentModel;
using Microsoft.AspNet.SignalR;
using AuthorizeAttribute = System.Web.Mvc.AuthorizeAttribute;
using Ressential.ViewModels;

namespace Ressential.Controllers
{
    [Authorize]
    [HasWarehouseAccess]
    public class WarehouseController : PermissionsController
    {
        DB_RessentialEntities _db = new DB_RessentialEntities();

        public ActionResult Index()
        {
            var dashboard = new DashboardViewModel();

            // Get total counts
            dashboard.TotalItems = _db.Items.Count();
            dashboard.TotalVendors = _db.Vendors.Count();
            dashboard.TotalBranches = _db.Branches.Count();
            dashboard.TotalUsers = _db.Users.Count();

            // Get analytics
            dashboard.UnfulfilledRequisitions = _db.Requisitions.Count(r => r.Status == "Pending" || r.Status == "Partially Settled");
            dashboard.LowStockItems = _db.WarehouseItemStocks.Count(i => i.Quantity <= i.Item.MinimumStockLevel);
            dashboard.CompletedIssues = _db.WarehouseIssues.Count(i => i.Status == "Settled");
            dashboard.ReturnedStocks = _db.ReturnStocks.Count(r => r.Status == "Pending");

            // Calculate total stock value
            dashboard.TotalStockValue = _db.WarehouseItemStocks
    .Select(i => (decimal?)(i.Quantity * i.CostPerUnit))
    .Sum() ?? 0;


            // Get top 5 items by value
            dashboard.TopItems = _db.WarehouseItemStocks
                .OrderByDescending(i => i.Quantity * i.CostPerUnit)
                .Take(5)
                .Select(i => new TopItemViewModel
                {
                    ItemName = i.Item.ItemName,
                    Quantity = i.Quantity,
                    Value = (i.Quantity) * (i.CostPerUnit)
                })
                .ToList();

            // Get recent activities
            var recentActivities = new List<RecentActivityViewModel>();

            // Add recent purchases

            var recentPurchases = _db.Purchases
                .OrderByDescending(p => p.PurchaseDate)
                .Take(3)
                .AsEnumerable() // Forces in-memory evaluation  
                .Select(p => new RecentActivityViewModel
                {
                    ActivityType = "Purchase",
                    Description = $"Purchase from {p.Vendor.Name} - {p.PurchaseNo}",
                    Timestamp = p.PurchaseDate,
                    Status = p.Status
                });

            recentActivities.AddRange(recentPurchases);

            // Add recent requisitions
            var recentRequisitions = _db.Requisitions
                .OrderByDescending(r => r.RequisitionDate)
                .Take(3)
                .AsEnumerable() // Forces in-memory evaluation  
                .Select(r => new RecentActivityViewModel
                {
                    ActivityType = "Requisition",
                    Description = $"Requisition from {r.Branch.BranchName} - {r.RequisitionNo}",
                    Timestamp = r.RequisitionDate,
                    Status = r.Status
                });

            recentActivities.AddRange(recentRequisitions);

            // Add recent issues
            var recentIssues = _db.WarehouseIssues
                .OrderByDescending(i => i.IssueDate)
                .Take(3)
                .AsEnumerable()
                .Select(i => new RecentActivityViewModel
                {
                    ActivityType = "Issue",
                    Description = $"Issue to {i.Branch.BranchName} - {i.IssueNo}",
                    Timestamp = i.IssueDate,
                    Status = i.Status
                });
            recentActivities.AddRange(recentIssues);

            dashboard.RecentActivities = recentActivities
                .OrderByDescending(a => a.Timestamp)
                .Take(7)
                .AsEnumerable()
                .ToList();

            return View(dashboard);
        }
        public ActionResult GetUnreadNotifications()
        {
            int currentUserId = Convert.ToInt32(Helper.GetUserInfo("userId"));
            var notifications = _db.Notifications
                .Where(n => n.UserId == currentUserId && !n.IsRead)
                .ToList();

            return PartialView("_NotificationsPartial", notifications);
        }
        [HttpPost]
        public JsonResult MarkAsRead(int notificationId)
        {
            var notification = _db.Notifications.SingleOrDefault(n => n.NotificationId == notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                _db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        public JsonResult GetUnreadNotificationCount()
        {
            int currentUserId = Convert.ToInt32(Helper.GetUserInfo("userId"));
            var count = _db.Notifications
                .Count(n => n.UserId == currentUserId && !n.IsRead);

            return Json(new { count }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult MarkAllAsRead()
        {
            int currentUserId = Convert.ToInt32(Helper.GetUserInfo("userId"));
            var notifications = _db.Notifications
                .Where(n => n.UserId == currentUserId && !n.IsRead)
                .ToList();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            _db.SaveChanges();
            return Json(new { success = true });
        }

        #region ItemCategory

        [HasPermission("Item Category Create")]
        public ActionResult CreateItemCategory()
        {
            return View();
        }

        [HttpPost]
        [HasPermission("Item Category Create")]
        public ActionResult CreateItemCategory(ItemCategory itemCategory)
        {
            try
            {
                _db.ItemCategories.Add(itemCategory);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Item Category created successfully.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while creating the Item Category.";
            }
            return RedirectToAction("ItemCategoryList");
        }
        [HasPermission("Item Category List")]
        public ActionResult ItemCategoryList(string search)
        {
            var itemCategories = _db.ItemCategories.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                itemCategories = itemCategories.Where(c => c.ItemCategoryName.Contains(search) || c.Description.Contains(search));
            }
            return View(itemCategories.ToList());
        }
        [HasPermission("Item Category Edit")]
        public ActionResult EditItemCategory(int itemCategoryId)
        {
            var ItemCategory = _db.ItemCategories.Find(itemCategoryId);
            if (ItemCategory == null)
            {
                return HttpNotFound();
            }
            return View(ItemCategory);
        }
        [HttpPost]
        [HasPermission("Item Category Edit")]
        public ActionResult EditItemEditItemCategory(ItemCategory itemCategory)
        {
            try
            {
                _db.Entry(itemCategory).State = EntityState.Modified;
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Item Category updated successfully.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the Item Category.";
            }

            return RedirectToAction("ItemCategoryList");
        }
        [HttpPost]
        [HasPermission("Item Category Delete")]
        public ActionResult DeleteItemCategory(int itemCategoryId)
        {
            try
            {
                var itemCategory = _db.ItemCategories.Find(itemCategoryId);
                if (itemCategory == null)
                {
                    TempData["ErrorMessage"] = "Item Category not found.";
                    return RedirectToAction("ItemCategoryList");
                }
                _db.ItemCategories.Remove(itemCategory);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Item Category deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This Item Category is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Item Category.";
                }
            }
            return RedirectToAction("ItemCategoryList");
        }
        [HttpPost]
        [HasPermission("Item Category Delete")]
        public ActionResult DeleteSelectedItemCategories(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var itemsToDelete = _db.ItemCategories.Where(c => selectedItems.Contains(c.ItemCategoryId)).ToList();
                    _db.ItemCategories.RemoveRange(itemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Item Category deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "This Item Category is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Item Category.";
                    }
                }
            }
            return RedirectToAction("ItemCategoryList");
        }

        #endregion


        [HasPermission("Item Create")]
        public ActionResult CreateItem()
        {
            ViewBag.Units = _db.UnitOfMeasures.ToList();
            ViewBag.Categories = _db.ItemCategories.ToList();
            Item item = new Item
            {
                OpeningStockDate = DateTime.Today,
                IsActive = true,
            };
            return View(item);
        }
        [HttpPost]
        [HasPermission("Item Create")]
        public ActionResult CreateItem(Item item)
        {
            try {
                if (ModelState.IsValid)
                {
                    item.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    item.CreatedAt = DateTime.Now;
                    _db.Items.Add(item);
                    _db.SaveChanges();

                    var warehouseItemStock = new WarehouseItemStock
                    {
                        ItemId = item.ItemId,
                        Quantity = item.OpeningStockQuantity,
                        CostPerUnit = item.OpeningStockValue/item.OpeningStockQuantity
                    };
                    //var warehouseItemTransaction = new WarehouseItemTransaction
                    //{
                    //    TransactionDate = openingDate,
                    //    ItemId = item.ItemId,
                    //    TransactionType = "Opening",
                    //    TransactionTypeId = item.ItemId,
                    //    Quantity = quantity,
                    //    CostPerUnit = cost/quantity
                    //};
                    _db.WarehouseItemStocks.Add(warehouseItemStock);
                    //_db.WarehouseItemTransactions.Add(warehouseItemTransaction);
                    _db.SaveChanges();

                    return RedirectToAction("ItemList");
                }
                ViewBag.Units = _db.UnitOfMeasures.ToList();
                ViewBag.Categories = _db.ItemCategories.ToList();
                return View(item);
            }
            catch {
                return RedirectToAction("Index","Error");
            }
            
        }
        [HasPermission("Item List")]
        public ActionResult ItemList(string search)
        {
            var items = _db.Items.AsQueryable();
            
            if (!string.IsNullOrEmpty(search))
            {
                items = items.Where(c => c.ItemName.Contains(search) || c.Sku.Contains(search));
            }
            return View(items.ToList());
        }
        [HasPermission("Item Edit")]
        public ActionResult EditItem(int itemId)
        {
            var item = _db.Items.Find(itemId);
            if (item == null)
            {
                return HttpNotFound();
            }
            //var warehouseItemTransaction = _db.WarehouseItemTransactions.Where(w => w.TransactionType == "Opening" && w.TransactionTypeId == itemId).Single();
            //if (warehouseItemTransaction == null)
            //{
            //    return HttpNotFound();
            //}
            ViewBag.Units = _db.UnitOfMeasures.ToList();
            ViewBag.Categories = _db.ItemCategories.ToList();
            //ViewBag.Quantity = warehouseItemTransaction.Quantity;
            //ViewBag.Cost = warehouseItemTransaction.CostPerUnit * warehouseItemTransaction.Quantity;
            //ViewBag.Date = warehouseItemTransaction.TransactionDate;

            return View(item);
        }
        [HttpPost]
        [HasPermission("Item Edit")]
        public ActionResult EditItem(Item item)
        {
            decimal perUnitCost = 0;
            if (item.OpeningStockQuantity == 0)
            {
                item.OpeningStockValue = 0;
            }
            else
            {
                perUnitCost = item.OpeningStockValue / item.OpeningStockQuantity;
            }
            try
            {
                var existingItem = _db.Items.Find(item.ItemId);
                if (existingItem == null)
                {
                    return HttpNotFound();
                }
                var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);
                
                decimal oldTotalCost = warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit;
                decimal oldTransactionCost = item.OpeningStockQuantity * perUnitCost;

                decimal newTotalCost = oldTotalCost - oldTransactionCost;
                decimal newQuantity = warehouseItemStock.Quantity - item.OpeningStockQuantity;

                decimal previousAverageCost;
                if (newQuantity == 0)
                {
                    previousAverageCost = 0;
                }
                else
                {
                    previousAverageCost = newTotalCost /  newQuantity;
                }
                decimal updatedQuantity = newQuantity + item.OpeningStockQuantity;

                warehouseItemStock.Quantity = updatedQuantity;
                if (updatedQuantity == 0)
                {
                    warehouseItemStock.CostPerUnit = 0;
                }
                else
                {
                    warehouseItemStock.CostPerUnit = ((newQuantity * previousAverageCost) + (item.OpeningStockQuantity * item.OpeningStockValue)) / updatedQuantity;
                }

                item.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                item.ModifiedAt = DateTime.Now;
                _db.Entry(existingItem).CurrentValues.SetValues(item);
                _db.Entry(existingItem).State = EntityState.Modified;
                _db.Entry(existingItem).Property(x => x.CreatedBy).IsModified = false;
                _db.Entry(existingItem).Property(x => x.CreatedAt).IsModified = false;
                
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Item updated successfully.";
            }
            catch (DbEntityValidationException ex)
            {
                // Log validation errors
                foreach (var validationError in ex.EntityValidationErrors)
                {
                    foreach (var error in validationError.ValidationErrors)
                    {
                        // Log property name and error message
                        System.Diagnostics.Debug.WriteLine($"Entity: {validationError.Entry.Entity.GetType().Name}, Property: {error.PropertyName}, Error: {error.ErrorMessage}");
                    }
                }

                TempData["ErrorMessage"] = "An error occurred while updating the Item. Please check the logs for details.";
            }
            return RedirectToAction("ItemList");
        }
        [HttpPost]
        [HasPermission("Item Delete")]
        public ActionResult DeleteItem(int itemId)
        {
            try
            {
                var item = _db.Items.Find(itemId);
                if (item == null)
                {
                    TempData["ErrorMessage"] = "Item not found.";
                    return RedirectToAction("ItemList");
                }
                var warehouseItemStock = _db.WarehouseItemStocks.Find(itemId);

                _db.WarehouseItemStocks.Remove(warehouseItemStock);
                _db.Items.Remove(item);
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
        [HasPermission("Item Delete")]
        public ActionResult DeleteSelectedItems(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var itemsToDelete = _db.Items.Where(c => selectedItems.Contains(c.ItemId)).ToList();

                    foreach (var item in itemsToDelete)
                    {
                        var warehouseItemStock = _db.WarehouseItemStocks.Where(w => w.ItemId == item.ItemId);
                        _db.WarehouseItemStocks.RemoveRange(warehouseItemStock);
                        _db.Items.Remove(item);
                    }
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
        [HasPermission("Vendor Create")]
        public ActionResult CreateVendor()
        {
            return View();
        }
        [HttpPost]
        [HasPermission("Vendor Create")]
        public ActionResult CreateVendor(Vendor vendor)
        {
            if (!ModelState.IsValid)
            {
                // If validation fails, return the same view with the current model to show errors
                return View(vendor);
            }
            try
            {
                vendor.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                vendor.CreatedAt = DateTime.Now;

                _db.Vendors.Add(vendor);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Vendor created successfully.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while creating the Vendor.";
                return RedirectToAction("Index", "Error");

            }
            return RedirectToAction("VendorList");
        }
        [HasPermission("Vendor List")]
        public ActionResult VendorList(string search)
        {

            var Vendors = _db.Vendors.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                Vendors = Vendors.Where(c => c.Name.Contains(search) || c.CompanyName.Contains(search) || c.Email.Contains(search));
            }
            return View(Vendors.ToList());
        }
        [HasPermission("Vendor Edit")]
        public ActionResult EditVendor(int VendorId)
        {
            var Vendors = _db.Vendors.Find(VendorId);
            if (Vendors == null)
            {
                return HttpNotFound();
            }
            return View(Vendors);
        }
        [HttpPost]
        [HasPermission("Vendor Edit")]
        public ActionResult EditVendor(Vendor vendor)
        {
            try
            {
                var existingVendor = _db.Vendors.Find(vendor.VendorId);
                if (existingVendor == null)
                {
                    return HttpNotFound();
                }
                vendor.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                vendor.ModifiedAt = DateTime.Now;
                _db.Entry(existingVendor).CurrentValues.SetValues(vendor);
                _db.Entry(existingVendor).State = EntityState.Modified;
                _db.Entry(existingVendor).Property(x => x.CreatedBy).IsModified = false;
                _db.Entry(existingVendor).Property(x => x.CreatedAt).IsModified = false;
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Vendor updated successfully.";
            }
            catch {
                TempData["ErrorMessage"] = "An error occurred while updating the Vendor.";
            }
            return RedirectToAction("VendorList");
        }
        [HttpPost]
        [HasPermission("Vendor Delete")]
        public ActionResult DeleteVendor(int VendorId)
        {
            try
            {
                var Vendor = _db.Vendors.Find(VendorId);
                if (Vendor == null)
                {
                    TempData["ErrorMessage"] = "Vendor not found.";
                    return RedirectToAction("VendorList");
                }

                _db.Vendors.Remove(Vendor);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Vendor deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This Vendor is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Vendor.";
                }
            }
            return RedirectToAction("VendorList");
        }
        [HttpPost]
        [HasPermission("Vendor Delete")]
        public ActionResult DeleteSelectedVendors(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var itemsToDelete = _db.Vendors.Where(c => selectedItems.Contains(c.VendorId)).ToList();
                    _db.Vendors.RemoveRange(itemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Vendor deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "This Vendor is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Vendor.";
                    }
                }
            }
            return RedirectToAction("VendorList");
        }
        [HasPermission("Branch Create")]
        public ActionResult CreateBranch()
        {
            return View();
        }

        [HttpPost]
        [HasPermission("Branch Create")]
        public ActionResult CreateBranch(Branch branch)
        {
            try
            {
                branch.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                branch.CreatedAt = DateTime.Now;

                _db.Branches.Add(branch);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Branch created successfully.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while creating the Branch.";
                return RedirectToAction("Index", "Error");

            }
            return RedirectToAction("BranchList");
        }
        [HasPermission("Branch List")]
        public ActionResult BranchList(string search)
        {
            var Branch = _db.Branches.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                Branch = Branch.Where(c => c.BranchName.Contains(search) || c.OwnerName.Contains(search));
            }
            return View(Branch.ToList());
        }
        [HasPermission("Branch Edit")]
        public ActionResult EditBranch(int BranchId)
        {
            var Branch = _db.Branches.Find(BranchId);
            if (Branch == null)
            {
                return HttpNotFound();
            }
            return View(Branch);
        }
        [HttpPost]
        [HasPermission("Branch Edit")]
        public ActionResult EditBranch(Branch branch)
        {
            try
            {
                var existingBranch = _db.Branches.Find(branch.BranchId);
                if (existingBranch == null)
                {
                    return HttpNotFound();
                }
                branch.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                branch.ModifiedAt = DateTime.Now;
                _db.Entry(existingBranch).CurrentValues.SetValues(branch);
                _db.Entry(existingBranch).State = EntityState.Modified;
                _db.Entry(existingBranch).Property(x => x.CreatedBy).IsModified = false;
                _db.Entry(existingBranch).Property(x => x.CreatedAt).IsModified = false;
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Branch updated successfully.";
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while updating the Branch.";
            }
            return RedirectToAction("BranchList");
        }
        [HttpPost]
        [HasPermission("Branch Delete")]
        public ActionResult DeleteBranch(int BranchId)
        {
            try
            {
                var Branch = _db.Branches.Find(BranchId);
                if (Branch == null)
                {
                    TempData["ErrorMessage"] = "Branch not found.";
                    return RedirectToAction("BranchList");
                }
                _db.Branches.Remove(Branch);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Branch deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This Branch is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Branch.";
                }
            }
            return RedirectToAction("BranchList");
        }
        [HttpPost]
        [HasPermission("Branch Delete")]
        public ActionResult DeleteSelectedBranches(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var itemsToDelete = _db.Branches.Where(c => selectedItems.Contains(c.BranchId)).ToList();
                    _db.Branches.RemoveRange(itemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Branch deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "This Branch is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Branch.";
                    }
                }
            }
            return RedirectToAction("BranchList");
        }

        [HasPermission("Bank And Cash Create")]
        public ActionResult CreateBankAndCash()
        {
            return View();
        }
        [HttpPost]
        [HasPermission("Bank And Cash Create")]
        public ActionResult CreateBankAndCash(Account account)
        {
            try
            {
                account.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                account.CreatedAt = DateTime.Now;

                _db.Accounts.Add(account);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Account created successfully.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while creating the Account.";
                return RedirectToAction("Index", "Error");

            }
            return RedirectToAction("BankAndCashList");
        }
        [HasPermission("Bank And Cash List")]
        public ActionResult BankAndCashList(string search)
        {
            var Account = _db.Accounts.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                Account = Account.Where(c => c.AccountTitle.Contains(search) || c.AccountType.Contains(search) || c.AccountNumber.Contains(search) || c.BankName.Contains(search));
            }
            return View(Account.ToList());
        }
        [HasPermission("Bank And Cash Edit")]
        public ActionResult EditBankAndCash(int AccountId)
        {
            var Account = _db.Accounts.Find(AccountId);
            if (Account == null)
            {
                return HttpNotFound();
            }
            return View(Account);
        }
        [HttpPost]
        [HasPermission("Bank And Cash Edit")]
        public ActionResult EditBankAndCash(Account account)
        {
            try
            {
                var existingAccount = _db.Accounts.Find(account.AccountId);
                if (existingAccount == null)
                {
                    return HttpNotFound();
                }
                if (account.AccountType == "Cash")
                {
                    account.AccountNumber = null;
                    account.BankName = null;
                }
                account.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                account.ModifiedAt = DateTime.Now;
                _db.Entry(existingAccount).CurrentValues.SetValues(account);
                _db.Entry(existingAccount).State = EntityState.Modified;
                _db.Entry(existingAccount).Property(x => x.CreatedBy).IsModified = false;
                _db.Entry(existingAccount).Property(x => x.CreatedAt).IsModified = false;
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Account updated successfully.";
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while updating the Account.";
            }
            return RedirectToAction("BankAndCashList");
        }
        [HttpPost]
        [HasPermission("Bank And Cash Delete")]
        public ActionResult DeleteBankAndCash(int AccountId)
        {
            try
            {
                var Account = _db.Accounts.Find(AccountId);
                if (Account == null)
                {
                    TempData["ErrorMessage"] = "Account not found.";
                    return RedirectToAction("BankAndCashList");
                }
                _db.Accounts.Remove(Account);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Account deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This Account is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Account.";
                }
            }
            return RedirectToAction("BankAndCashList");
        }
        [HttpPost]
        [HasPermission("Bank And Cash Delete")]
        public ActionResult DeleteSelectedBankAndCashs(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var itemsToDelete = _db.Accounts.Where(c => selectedItems.Contains(c.AccountId)).ToList();
                    _db.Accounts.RemoveRange(itemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Accounts deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "This Account is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Account.";
                    }
                }
            }
            return RedirectToAction("BankAndCashList");
        }

        [HasPermission("Payment Voucher Create")]
        public ActionResult CreatePaymentVoucher()
        {
            ViewBag.Vendors = _db.Vendors.ToList();
            ViewBag.Accounts = _db.Accounts.ToList();

            var paymentVoucher = new PaymentVoucher
            {
                PaymentVoucherDate = DateTime.Now,
                InstrumentDate = DateTime.Now,
            };
            return View(paymentVoucher);
        }
        [HttpPost]
        [HasPermission("Payment Voucher Create")]
        public ActionResult CreatePaymentVoucher(PaymentVoucher model, IEnumerable<HttpPostedFileBase> files)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    string datePart = DateTime.Now.ToString("yyyyMM");
                    int nextPaymentVoucherNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.PaymentVouchers.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextPaymentVoucherNumber = _db.PaymentVouchers
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.PaymentVoucherNo.Substring(11)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    model.PaymentVoucherNo = $"PV-{datePart}{nextPaymentVoucherNumber:D4}";
                    model.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    model.CreatedAt = DateTime.Now;
                    model.Status = "Pending";
                    _db.PaymentVouchers.Add(model);
                    _db.SaveChanges();
                    string uploadFolder = Server.MapPath("~/Uploads");
                    if (!Directory.Exists(uploadFolder))
                    {
                        Directory.CreateDirectory(uploadFolder);
                    }

                    if (files != null)
                    {
                        foreach (var file in files)
                        {
                            if (file != null && file.ContentLength > 0)
                            {
                                try
                                {
                                    string fileName = DateTime.Now.ToString("yyyymmddMMss") + "_" + Path.GetFileName(file.FileName);
                                    string filePath = Path.Combine(uploadFolder, fileName);
                                    file.SaveAs(filePath);
                                    var attachment = new PaymentVoucherAttachment
                                    {
                                        PaymentVoucherId = model.PaymentVoucherId,
                                        AttachmentPath = fileName // Store the file name instead of full path
                                    };
                                    _db.PaymentVoucherAttachments.Add(attachment);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error saving file: {ex.Message}");
                                    ModelState.AddModelError("", "An error occurred while saving file attachments. Please try again.");
                                }
                            }
                        }
                        _db.SaveChanges();
                        TempData["SuccessMessage"] = "Payment Voucher created successfully.";
                    }
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "An error occurred while updating the Payment Voucher.";
                    throw;
                }
                return RedirectToAction("PaymentVoucherList");
            }

            ViewBag.Vendors = _db.Vendors.ToList();
            ViewBag.Accounts = _db.Accounts.ToList();
            return View(model);
        }

        [HasPermission("Payment Voucher List")]
        public ActionResult PaymentVoucherList(string search)
        {

            var PaymentVoucher = _db.PaymentVouchers.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                PaymentVoucher = PaymentVoucher.Where(c => c.PaymentVoucherNo.Contains(search) || c.InstrumentNo.Contains(search));
            }
            return View(PaymentVoucher.ToList());
        }
        [HasPermission("Payment Voucher Edit")]
        public ActionResult EditPaymentVoucher(int PaymentVoucherId)
        {
            var PaymentVoucher = _db.PaymentVouchers.Find(PaymentVoucherId);
            if (PaymentVoucher == null)
            {
                return HttpNotFound();
            }
            ViewBag.Vendors = _db.Vendors.ToList();
            ViewBag.Accounts = _db.Accounts.ToList();
            return View(PaymentVoucher);
        }
        [HttpPost]
        [HasPermission("Payment Voucher Edit")]
        public ActionResult EditPaymentVoucher(PaymentVoucher paymentVoucher, IEnumerable<HttpPostedFileBase> files)
        {
            try
            {
                var existingPaymentVoucher = _db.PaymentVouchers.Find(paymentVoucher.PaymentVoucherId);
                var paymentMethod = _db.Accounts.Find(paymentVoucher.AccountId);

                if (existingPaymentVoucher == null)
                {
                    return HttpNotFound(); // Return 404 if the voucher is not found
                }
                if (paymentMethod.AccountType == "Cash")
                {
                    paymentVoucher.InstrumentNo = null;
                    paymentVoucher.InstrumentDate = null;
                }

                _db.Entry(existingPaymentVoucher).CurrentValues.SetValues(paymentVoucher);
                _db.Entry(existingPaymentVoucher).State = EntityState.Modified;
                existingPaymentVoucher.Status = "Pending";
                existingPaymentVoucher.PaymentVoucherNo = "PV-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                existingPaymentVoucher.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                existingPaymentVoucher.ModifiedAt = DateTime.Now;
                _db.Entry(existingPaymentVoucher).Property(x => x.CreatedBy).IsModified = false;
                _db.Entry(existingPaymentVoucher).Property(x => x.CreatedAt).IsModified = false;

                if (!ModelState.IsValid)
                {
                    return View(paymentVoucher);
                }
                if (files != null && files.Any())
                {
                    string uploadFolder = Server.MapPath("~/Uploads");
                    if (!Directory.Exists(uploadFolder))
                    {
                        Directory.CreateDirectory(uploadFolder);
                    }

                    foreach (var file in files)
                    {
                        if (file != null && file.ContentLength > 0)
                        {
                            try
                            {
                                string fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Path.GetFileName(file.FileName)}";
                                string filePath = Path.Combine(uploadFolder, fileName);
                                file.SaveAs(filePath);

                                var deleteAttachments = existingPaymentVoucher.PaymentVoucherAttachments.ToList();
                                foreach (var item in deleteAttachments)
                                {
                                    var path = Path.Combine(Server.MapPath("~/Uploads"), item.AttachmentPath);
                                    if (System.IO.File.Exists(path))
                                    {
                                        System.IO.File.Delete(path);
                                    }
                                }
                                _db.PaymentVoucherAttachments.RemoveRange(deleteAttachments);
                                var attachment = new PaymentVoucherAttachment
                                {
                                    PaymentVoucherId = existingPaymentVoucher.PaymentVoucherId,
                                    AttachmentPath = fileName
                                };
                                _db.PaymentVoucherAttachments.Add(attachment);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error saving file: {ex.Message}");
                                ModelState.AddModelError("", "An error occurred while saving file attachments. Please try again.");
                            }
                        }
                    }
                }
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Payment Voucher updated successfully.";
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"DB Update Error: {dbEx.InnerException?.Message}");
                ModelState.AddModelError("", "An error occurred while updating the Payment Voucher in the database.");
            }
            catch (Exception ex)
            {

                Console.WriteLine($"Error: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while updating the Payment Voucher.";
            }

            return RedirectToAction("PaymentVoucherList");
        }

        [HttpPost]
        [HasPermission("Payment Voucher Delete")]
        public ActionResult DeletePaymentVoucher(int PaymentVoucherId)
        {
            try
            {
                var PaymentVoucher = _db.PaymentVouchers.Find(PaymentVoucherId);
                if (PaymentVoucher == null)
                {
                    TempData["ErrorMessage"] = "Payment Voucher not found.";
                    return RedirectToAction("PaymentVoucherList");
                }

                var attachmentList = PaymentVoucher.PaymentVoucherAttachments.ToList();

                foreach (var attachment in attachmentList)
                {
                    var filePath = Path.Combine(Server.MapPath("~/Uploads"), attachment.AttachmentPath);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }


                _db.PaymentVouchers.Remove(PaymentVoucher);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Payment Voucher deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This payment voucher is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Payment Voucher.";
                }
            }
            return RedirectToAction("PaymentVoucherList");
        }
        [HttpPost]
        [HasPermission("Payment Voucher Delete")]
        public ActionResult DeleteSelectedPaymentVouchers(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var itemsToDelete = _db.PaymentVouchers.Where(c => selectedItems.Contains(c.PaymentVoucherId)).ToList();

                    foreach (var item in itemsToDelete)
                    {
                        var attachmentList = item.PaymentVoucherAttachments.ToList();

                        foreach (var attachment in attachmentList)
                        {
                            var filePath = Path.Combine(Server.MapPath("~/Uploads"), attachment.AttachmentPath);
                            if (System.IO.File.Exists(filePath))
                            {
                                System.IO.File.Delete(filePath);
                            }
                        }
                    }

                    _db.PaymentVouchers.RemoveRange(itemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Payment Voucher deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "Payment Voucher is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Payment Voucher.";
                    }
                }
            }
            return RedirectToAction("PaymentVoucherList");
        }


        [HasPermission("Receipt Voucher Create")]
        public ActionResult CreateReceiptVoucher()
        {
            ViewBag.Vendors = _db.Vendors.ToList();
            ViewBag.Accounts = _db.Accounts.ToList();
            var receiptVoucher = new ReceiptVoucher
            {
                ReceiptVoucherDate = DateTime.Now,
                InstrumentDate = DateTime.Now,
            };
            return View(receiptVoucher);

        }
        [HttpPost]
        [HasPermission("Receipt Voucher Create")]
        public ActionResult CreateReceiptVoucher(ReceiptVoucher model, IEnumerable<HttpPostedFileBase> files)
        {

            if (ModelState.IsValid)
            {
                try
                {
                    string datePart = DateTime.Now.ToString("yyyyMM");
                    int nextReceiptVoucherNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.ReceiptVouchers.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextReceiptVoucherNumber = _db.ReceiptVouchers
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.ReceiptVoucherNo.Substring(11)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    model.ReceiptVoucherNo = $"RV-{datePart}{nextReceiptVoucherNumber:D4}";
                    model.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    model.CreatedAt = DateTime.Now;
                    model.Status = "Pending";
                    _db.ReceiptVouchers.Add(model);
                    _db.SaveChanges();
                    string uploadFolder = Server.MapPath("~/Uploads");
                    if (!Directory.Exists(uploadFolder))
                    {
                        Directory.CreateDirectory(uploadFolder);
                    }

                    if (files != null)
                    {
                        foreach (var file in files)
                        {
                            if (file != null && file.ContentLength > 0)
                            {
                                try
                                {
                                    string fileName = DateTime.Now.ToString("yyyymmddMMss") + "_" + Path.GetFileName(file.FileName);
                                    string filePath = Path.Combine(uploadFolder, fileName);
                                    file.SaveAs(filePath);
                                    var attachment = new ReceiptVoucherAttachment
                                    {
                                        ReceiptVoucherId = model.ReceiptVoucherId,
                                        AttachmentPath = fileName
                                    };
                                    _db.ReceiptVoucherAttachments.Add(attachment);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error saving file: {ex.Message}");
                                    ModelState.AddModelError("", "An error occurred while saving file attachments. Please try again.");
                                }
                            }
                        }
                        _db.SaveChanges();
                        TempData["SuccessMessage"] = "Receipt Voucher created successfully.";
                    }
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Receipt Voucher.";
                    throw;
                }
                return RedirectToAction("ReceiptVoucherList");
            }

            ViewBag.Vendors = _db.Vendors.ToList();
            ViewBag.Accounts = _db.Accounts.ToList();
            return View(model);
        }

        private string GenerateReceiptVoucherNumber()
        {
            return "RV-" + DateTime.Now.Ticks;
        }
        [HasPermission("Receipt Voucher List")]
        public ActionResult ReceiptVoucherList(string search)
        {

            var ReceiptVoucher = _db.ReceiptVouchers.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                ReceiptVoucher = ReceiptVoucher.Where(c => c.ReceiptVoucherNo.Contains(search) || c.InstrumentNo.Contains(search));
            }
            return View(ReceiptVoucher.ToList());
        }
        [HttpPost]
        [HasPermission("Receipt Voucher Delete")]
        public ActionResult DeleteReceiptVoucher(int ReceiptVoucherId)
        {
            try
            {
                var ReceiptVoucher = _db.ReceiptVouchers.Find(ReceiptVoucherId);
                if (ReceiptVoucher == null)
                {
                    TempData["ErrorMessage"] = "Receipt Voucher not found.";
                    return RedirectToAction("ReceiptVoucherList");
                }

                var attachmentList = ReceiptVoucher.ReceiptVoucherAttachments.ToList();

                foreach (var attachment in attachmentList)
                {
                    var filePath = Path.Combine(Server.MapPath("~/Uploads"), attachment.AttachmentPath);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }


                _db.ReceiptVouchers.Remove(ReceiptVoucher);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Receipt Voucher deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This Receipt Voucher is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Receipt Voucher.";
                }
            }
            return RedirectToAction("ReceiptVoucherList");
        }
        [HttpPost]
        [HasPermission("Receipt Voucher Delete")]
        public ActionResult DeleteSelectedReceiptVouchers(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var itemsToDelete = _db.ReceiptVouchers.Where(c => selectedItems.Contains(c.ReceiptVoucherId)).ToList();

                    foreach (var item in itemsToDelete)
                    {
                        var attachmentList = item.ReceiptVoucherAttachments.ToList();

                        foreach (var attachment in attachmentList)
                        {
                            var filePath = Path.Combine(Server.MapPath("~/Uploads"), attachment.AttachmentPath);
                            if (System.IO.File.Exists(filePath))
                            {
                                System.IO.File.Delete(filePath);
                            }
                        }
                    }

                    _db.ReceiptVouchers.RemoveRange(itemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Receipt Voucher deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "Receipt Voucher is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Receipt Voucher.";
                    }
                }
            }
            return RedirectToAction("ReceiptVoucherList");
        }

        [HasPermission("Receipt Voucher Edit")]
        public ActionResult EditReceiptVoucher(int ReceiptVoucherId)
        {
            var ReceiptVoucher = _db.ReceiptVouchers.Find(ReceiptVoucherId);
            if (ReceiptVoucher == null)
            {
                return HttpNotFound();
            }
            ViewBag.Vendors = _db.Vendors.ToList();
            ViewBag.Accounts = _db.Accounts.ToList();
            return View(ReceiptVoucher);
        }
        [HttpPost]
        [HasPermission("Receipt Voucher Edit")]
        public ActionResult EditReceiptVoucher(ReceiptVoucher ReceiptVoucher, IEnumerable<HttpPostedFileBase> files)
        {
            try
            {
                var existingReceiptVoucher = _db.ReceiptVouchers.Find(ReceiptVoucher.ReceiptVoucherId);
                var paymentMethod = _db.Accounts.Find(ReceiptVoucher.AccountId);

                if (existingReceiptVoucher == null)
                {
                    return HttpNotFound(); // Return 404 if the voucher is not found
                }
                if (paymentMethod.AccountType == "Cash")
                {
                    ReceiptVoucher.InstrumentNo = null;
                    ReceiptVoucher.InstrumentDate = null;
                }

                _db.Entry(existingReceiptVoucher).CurrentValues.SetValues(ReceiptVoucher);
                _db.Entry(existingReceiptVoucher).State = EntityState.Modified;
                existingReceiptVoucher.Status = "Pending";
                existingReceiptVoucher.ReceiptVoucherNo = "RV-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                existingReceiptVoucher.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                existingReceiptVoucher.ModifiedAt = DateTime.Now;
                _db.Entry(existingReceiptVoucher).Property(x => x.CreatedBy).IsModified = false;
                _db.Entry(existingReceiptVoucher).Property(x => x.CreatedAt).IsModified = false;

                if (!ModelState.IsValid)
                {
                    return View(ReceiptVoucher);
                }
                if (files != null && files.Any())
                {
                    string uploadFolder = Server.MapPath("~/Uploads");
                    if (!Directory.Exists(uploadFolder))
                    {
                        Directory.CreateDirectory(uploadFolder);
                    }

                    foreach (var file in files)
                    {
                        if (file != null && file.ContentLength > 0)
                        {
                            try
                            {
                                string fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Path.GetFileName(file.FileName)}";
                                string filePath = Path.Combine(uploadFolder, fileName);
                                file.SaveAs(filePath);

                                var deleteAttachments = existingReceiptVoucher.ReceiptVoucherAttachments.ToList();
                                foreach (var item in deleteAttachments)
                                {
                                    var path = Path.Combine(Server.MapPath("~/Uploads"), item.AttachmentPath);
                                    if (System.IO.File.Exists(path))
                                    {
                                        System.IO.File.Delete(path);
                                    }
                                }
                                _db.ReceiptVoucherAttachments.RemoveRange(deleteAttachments);
                                var attachment = new ReceiptVoucherAttachment
                                {
                                    ReceiptVoucherId = existingReceiptVoucher.ReceiptVoucherId,
                                    AttachmentPath = fileName
                                };
                                _db.ReceiptVoucherAttachments.Add(attachment);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error saving file: {ex.Message}");
                                ModelState.AddModelError("", "An error occurred while saving file attachments. Please try again.");
                            }
                        }
                    }
                }
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Receipt Voucher updated successfully.";
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"DB Update Error: {dbEx.InnerException?.Message}");
                ModelState.AddModelError("", "An error occurred while updating the Receipt Voucher in the database.");
            }
            catch (Exception ex)
            {

                Console.WriteLine($"Error: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while updating the Receipt Voucher.";
            }

            return RedirectToAction("ReceiptVoucherList");
        }
        [HasPermission("Purchase Create")]
        public ActionResult CreatePurchase()
        {

            var purchase = new Purchase
            {
                PurchaseDate = DateTime.Today,
                PurchaseDetails = new List<PurchaseDetail>
                {
                    new PurchaseDetail()
                }

            };
            ViewBag.Items = _db.Items.Where(i => i.IsActive == true).ToList();
            ViewBag.Vendors = _db.Vendors.ToList();

            return View(purchase);
        }
        [HttpPost]
        [HasPermission("Purchase Create")]
        public ActionResult CreatePurchase(Purchase purchase)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check if reference number already exists
                    if (!string.IsNullOrEmpty(purchase.ReferenceNo) && !purchase.ConfirmDuplicate)
                    {
                        var existingPurchase = _db.Purchases.FirstOrDefault(p => p.ReferenceNo.Equals(purchase.ReferenceNo, StringComparison.OrdinalIgnoreCase));
                        if (existingPurchase != null)
                        {
                            return Json(new { status = "warning", message = "This reference number already exists. Do you want to continue?" }, JsonRequestBehavior.AllowGet);
                        }
                    }

                    string datePart = DateTime.Now.ToString("yyyyMM");
                    int nextPurchaseNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.Purchases.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextPurchaseNumber = _db.Purchases
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.PurchaseNo.Substring(11)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    purchase.PurchaseNo = $"PUR-{datePart}{nextPurchaseNumber:D4}";
                    purchase.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    purchase.CreatedAt = DateTime.Now;
                    purchase.Status = "Not Paid";
                    _db.Purchases.Add(purchase);
                    _db.SaveChanges();

                    foreach (var purchaseDetails in purchase.PurchaseDetails)
                    {
                        var currentItemStock = _db.WarehouseItemStocks.Where(i => i.ItemId == purchaseDetails.ItemId).FirstOrDefault();
                        decimal currentQuantity = currentItemStock.Quantity;
                        currentItemStock.Quantity = currentQuantity + purchaseDetails.Quantity;
                        currentItemStock.CostPerUnit = ((currentQuantity * currentItemStock.CostPerUnit) + (purchaseDetails.Quantity * purchaseDetails.UnitPrice))/(currentItemStock.Quantity);

                        //var warehouseItemTransaction = new WarehouseItemTransaction
                        //{
                        //    TransactionDate = purchase.PurchaseDate,
                        //    ItemId = purchaseDetails.ItemId,
                        //    TransactionType = "Purchase",
                        //    TransactionTypeId = purchase.PurchaseId,
                        //    Quantity = purchaseDetails.Quantity,
                        //    CostPerUnit = purchaseDetails.UnitPrice
                        //};
                        _db.WarehouseItemStocks.AddOrUpdate(currentItemStock);
                        //_db.WarehouseItemTransactions.Add(warehouseItemTransaction);
                    }
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Purchase created successfully.";
                    return Json("0",JsonRequestBehavior.AllowGet);
                }
                ViewBag.Vendors = _db.Vendors.Select(v => new { v.VendorId, v.Name }).ToList();
                ViewBag.Items = _db.Items.Select(i => new { i.ItemId, i.ItemName }).ToList();
                return View(purchase);
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                Console.WriteLine($"Error creating purchase: {ex.Message}");
                return RedirectToAction("Index", "Error");
            }
        }
        [HasPermission("Purchase List")]
        public ActionResult PurchaseList(string search)
        {
            if (!string.IsNullOrEmpty(search))
            {
                var purchaseList = _db.Purchases
             .Select(p => new PurchaseListViewModel
             {
                 PurchaseId = p.PurchaseId,
                 PurchaseNo = p.PurchaseNo,
                 PurchaseDate = p.PurchaseDate,
                 ReferenceNo = p.ReferenceNo,
                 VendorName = p.Vendor.Name,
                 Status = p.Status,
                 TotalAmount = p.PurchaseDetails.Sum(pd => (decimal?)(pd.Quantity * pd.UnitPrice)) ?? 0
             }).Where(p => p.PurchaseNo.Contains(search) || p.ReferenceNo.Contains(search))
                .ToList();
                return View(purchaseList);
            }
            var purchaseList2 = _db.Purchases
             .Select(p => new PurchaseListViewModel
             {
                PurchaseId = p.PurchaseId,
                PurchaseNo = p.PurchaseNo,
                PurchaseDate = p.PurchaseDate,
                ReferenceNo = p.ReferenceNo,
                VendorName = p.Vendor.Name,
                Status = p.Status,
                 TotalAmount = p.PurchaseDetails.Sum(pd => (decimal?)(pd.Quantity * pd.UnitPrice)) ?? 0
                    }).ToList();
            return View(purchaseList2);
        }
        [HasPermission("Purchase Edit")]
        public ActionResult EditPurchase(int purchaseId)
        {
            Purchase purchase = _db.Purchases.Find(purchaseId);
            ViewBag.Items = _db.Items.Where(i => i.IsActive == true).ToList();
            ViewBag.Vendors = _db.Vendors.ToList();
            return View(purchase);
        }
        [HttpPost]
        [HasPermission("Purchase Edit")]
        public ActionResult EditPurchase(Purchase purchase)
        {
            if (purchase == null)
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
                    var existingPurchase = _db.Purchases.Include(p => p.PurchaseDetails).FirstOrDefault(p => p.PurchaseId == purchase.PurchaseId);
                    if (existingPurchase == null)
                    {
                        TempData["ErrorMessage"] = "Purchase not found";
                        return Json(new { status = "error", message = "Purchase not found" }, JsonRequestBehavior.AllowGet);
                    }

                    // Update purchase metadata
                    purchase.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    purchase.ModifiedAt = DateTime.Now;

                    // Update existing purchase values
                    _db.Entry(existingPurchase).CurrentValues.SetValues(purchase);
                    _db.Entry(existingPurchase).Property(x => x.CreatedBy).IsModified = false;
                    _db.Entry(existingPurchase).Property(x => x.CreatedAt).IsModified = false;
                    _db.Entry(existingPurchase).Property(x => x.Status).IsModified = false;

                    // Update PurchaseDetails
                    var existingDetails = existingPurchase.PurchaseDetails.ToList();
                    var newDetails = purchase.PurchaseDetails;

                    // Update or Add new details
                    foreach (var newDetail in newDetails)
                    {
                        var existingDetail = existingDetails.FirstOrDefault(d => d.PurchaseDetailId == newDetail.PurchaseDetailId);
                        var warehouseItemStock = _db.WarehouseItemStocks.Find(newDetail.ItemId);
                        if (existingDetail != null)
                        {
                            //decimal RevertedTotalCost = (warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit) - (existingDetail.Quantity * existingDetail.UnitPrice);
                            decimal RevertedQuantity = warehouseItemStock.Quantity - existingDetail.Quantity;
                            decimal currentQuantity = warehouseItemStock.Quantity;
                            
                            //decimal RevertedAverageCost = RevertedTotalCost / (RevertedQuantity == 0 ? 1 : RevertedQuantity);
                            //decimal RevertedAverageCost = (warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit) - (existingDetail.Quantity * existingDetail.UnitPrice) / (RevertedQuantity == 0 ? 1 : RevertedQuantity);

                            warehouseItemStock.Quantity = RevertedQuantity + newDetail.Quantity; //Updated Quantity
                            if (warehouseItemStock.Quantity == 0)
                            {
                                warehouseItemStock.CostPerUnit = 0;
                            }
                            else if (warehouseItemStock.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to update the purchase. Quantity of " + warehouseItemStock.Item.ItemName + " cannot be < 0.";
                                return Json(new { status = "error", message = "Quantity Error" }, JsonRequestBehavior.AllowGet);
                            }
                            else
                            {
                                warehouseItemStock.CostPerUnit = ((currentQuantity * warehouseItemStock.CostPerUnit) - (existingDetail.Quantity * existingDetail.UnitPrice) + (newDetail.Quantity * newDetail.UnitPrice)) / warehouseItemStock.Quantity; //Updated Per Unit Cost
                            }
                            var Notifications = CheckStockLevel(warehouseItemStock.ItemId);
                            SendStockLevelNotifications(Notifications);
                            _db.Entry(existingDetail).CurrentValues.SetValues(newDetail);
                            existingDetails.Remove(existingDetail); // Remove from existing list once matched
                        }
                        else
                        {
                            decimal currentQuantity = warehouseItemStock.Quantity;
                            warehouseItemStock.Quantity = warehouseItemStock.Quantity + newDetail.Quantity; //Updated Quantity
                            if (warehouseItemStock.Quantity == 0)
                            {
                                warehouseItemStock.CostPerUnit = 0;
                            }
                            else if (warehouseItemStock.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to update the purchase. Quantity of " + warehouseItemStock.Item.ItemName + " cannot be < 0.";
                                return Json(new { status = "error", message = "Quantity Error" }, JsonRequestBehavior.AllowGet);
                            }
                            else
                            {
                                warehouseItemStock.CostPerUnit = ((currentQuantity * warehouseItemStock.CostPerUnit) + (newDetail.Quantity * newDetail.UnitPrice)) / warehouseItemStock.Quantity; //Updated Per Unit Cost
                            }
                            existingPurchase.PurchaseDetails.Add(newDetail); // Add new detail
                        }
                    }

                    // Delete unmatched details
                    if (existingDetails != null)
                    {
                        foreach (var item in existingDetails)
                        {
                            var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);
                            decimal currentQuantity = warehouseItemStock.Quantity;
                            warehouseItemStock.Quantity = warehouseItemStock.Quantity - item.Quantity;
                            if (warehouseItemStock.Quantity == 0)
                            {
                                warehouseItemStock.CostPerUnit = 0;
                            }
                            else if (warehouseItemStock.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to update the purchase. Quantity of " + warehouseItemStock.Item.ItemName + " cannot be < 0.";
                                return Json(new { status = "error", message = "Quantity Error" }, JsonRequestBehavior.AllowGet);
                            }
                            else
                            {
                                warehouseItemStock.CostPerUnit = ((currentQuantity * warehouseItemStock.CostPerUnit) - (item.Quantity * item.UnitPrice)) / (warehouseItemStock.Quantity == 0 ? 1 : warehouseItemStock.Quantity);
                            }
                            var Notifications = CheckStockLevel(item.ItemId);
                            SendStockLevelNotifications(Notifications);
                        }
                        _db.PurchaseDetails.RemoveRange(existingDetails);
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
        [HasPermission("Purchase Delete")]
        public ActionResult DeletePurchase(int purchaseId)
        {
            try
            {
                var purchase = _db.Purchases.Find(purchaseId);
                if (purchase == null)
                {
                    TempData["ErrorMessage"] = "Purchase not found.";
                    return RedirectToAction("PurchaseList");
                }
                var purchaseDetails = _db.PurchaseDetails.Select(p => p).Where(p => p.PurchaseId == purchaseId);

                if (purchaseDetails != null)
                {
                    foreach (var item in purchaseDetails)
                    {
                        var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);
                        decimal currentQuantity = warehouseItemStock.Quantity;
                        warehouseItemStock.Quantity = warehouseItemStock.Quantity - item.Quantity;
                        if (warehouseItemStock.Quantity == 0)
                        {
                            warehouseItemStock.CostPerUnit = 0;
                        }
                        else if (warehouseItemStock.Quantity < 0)
                        {
                            TempData["ErrorMessage"] = "Unable to delete the purchase. Quantity of " + warehouseItemStock.Item.ItemName + " cannot be < 0.";
                            return RedirectToAction("PurchaseList");
                        }
                        else
                        {
                            warehouseItemStock.CostPerUnit = ((currentQuantity * warehouseItemStock.CostPerUnit) - (item.Quantity * item.UnitPrice)) / warehouseItemStock.Quantity;
                        }
                        var Notifications = CheckStockLevel(item.ItemId);
                        SendStockLevelNotifications(Notifications);
                    }
                }
                _db.PurchaseDetails.RemoveRange(purchaseDetails);
                _db.Purchases.Remove(purchase);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Item deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This purchase is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the purchase.";
                }
            }
            return RedirectToAction("PurchaseList");
        }
        [HttpPost]
        [HasPermission("Purchase Delete")]
        public ActionResult DeleteSelectedPurchases(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var purchasesToDelete = _db.Purchases.Where(c => selectedItems.Contains(c.PurchaseId)).ToList();
                    var purchaseDetails = _db.PurchaseDetails.Where(c => selectedItems.Contains(c.PurchaseId)).ToList();

                    if (purchaseDetails != null)
                    {
                        foreach (var item in purchaseDetails)
                        {
                            var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);
                            decimal oldQuantity = warehouseItemStock.Quantity;
                            warehouseItemStock.Quantity = warehouseItemStock.Quantity - item.Quantity;
                            if (warehouseItemStock.Quantity == 0)
                            {
                                warehouseItemStock.CostPerUnit = 0;
                            }
                            else if (warehouseItemStock.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to delete the purchase "+ item.Purchase.PurchaseNo +". Quantity of " + warehouseItemStock.Item.ItemName + " cannot be < 0.";
                                return RedirectToAction("PurchaseList");
                            }
                            else
                            {
                                warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) - (item.Quantity * item.UnitPrice)) / warehouseItemStock.Quantity;
                            }
                            var Notifications = CheckStockLevel(item.ItemId);
                            SendStockLevelNotifications(Notifications);
                        }
                    }

                    _db.PurchaseDetails.RemoveRange(purchaseDetails);
                    _db.Purchases.RemoveRange(purchasesToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Purchases deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A purchase is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the purchase.";
                    }
                }
            }
            return RedirectToAction("PurchaseList");
        }
        [HasPermission("Purchase Return Create")]

        public ActionResult CreatePurchaseReturn()
        {

            var purchaseReturn = new PurchaseReturn
            {
                PurchaseReturnDetails = new List<PurchaseReturnDetail>
                {
                    new PurchaseReturnDetail()
                }

            };
            ViewBag.Items = _db.Items.Where(i => i.IsActive == true).ToList();
            ViewBag.Vendors = _db.Vendors.ToList();

            return View(purchaseReturn);
        }
        [HttpPost]
        [HasPermission("Purchase Return Create")]
        public ActionResult CreatePurchaseReturn(PurchaseReturn purchaseReturn)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check if reference number already exists
                    if (!string.IsNullOrEmpty(purchaseReturn.ReferenceNo) && !purchaseReturn.ConfirmDuplicate)
                    {
                        var existingPurchaseReturn = _db.PurchaseReturns.FirstOrDefault(p => p.ReferenceNo.Equals(purchaseReturn.ReferenceNo, StringComparison.OrdinalIgnoreCase));
                        if (existingPurchaseReturn != null)
                        {
                            return Json(new { status = "warning", message = "This reference number already exists. Do you want to continue?" }, JsonRequestBehavior.AllowGet);
                        }
                    }

                    string datePart = DateTime.Now.ToString("yyyyMM");
                    int nextPurchaseNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.PurchaseReturns.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextPurchaseNumber = _db.PurchaseReturns
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.PurchaseReturnNo.Substring(11)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    purchaseReturn.PurchaseReturnNo = $"PRE-{datePart}{nextPurchaseNumber:D4}";
                    purchaseReturn.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    purchaseReturn.CreatedAt = DateTime.Now;
                    purchaseReturn.Status = "Not Received";
                    _db.PurchaseReturns.Add(purchaseReturn);
                    _db.SaveChanges();

                    foreach (var purchaseReturnDetails in purchaseReturn.PurchaseReturnDetails)
                    {
                        var currentItemStock = _db.WarehouseItemStocks.Where(i => i.ItemId == purchaseReturnDetails.ItemId).FirstOrDefault();
                        decimal currentQuantity = currentItemStock.Quantity;
                        currentItemStock.Quantity = currentQuantity - purchaseReturnDetails.Quantity;
                        if (currentItemStock.Quantity <= 0)
                        {
                            currentItemStock.CostPerUnit = 0;
                        }
                        else
                        {
                            currentItemStock.CostPerUnit = ((currentQuantity * currentItemStock.CostPerUnit) - (purchaseReturnDetails.Quantity * purchaseReturnDetails.UnitPrice)) / (currentItemStock.Quantity);
                        }

                        _db.WarehouseItemStocks.AddOrUpdate(currentItemStock);
                        var Notifications = CheckStockLevel(purchaseReturnDetails.ItemId);
                        SendStockLevelNotifications(Notifications);
                    }
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Purchase return created successfully.";
                    return Json("0", JsonRequestBehavior.AllowGet);
                }
                ViewBag.Vendors = _db.Vendors.Select(v => new { v.VendorId, v.Name }).ToList();
                ViewBag.Items = _db.Items.Select(i => new { i.ItemId, i.ItemName }).ToList();
                return View(purchaseReturn);
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                Console.WriteLine($"Error creating purchase return: {ex.Message}");
                return RedirectToAction("Index", "Error");
            }
        }
        [HasPermission("Purchase Return List")]
        public ActionResult PurchaseReturnList(string search)
        {
            if (!string.IsNullOrEmpty(search))
            {
                var purchaseReturnList = _db.PurchaseReturns
             .Select(p => new PurchaseReturnListViewModel
             {
                 PurchaseReturnId = p.PurchaseReturnId,
                 PurchaseReturnNo = p.PurchaseReturnNo,
                 PurchaseReturnDate = p.PurchaseReturnDate,
                 ReferenceNo = p.ReferenceNo,
                 VendorName = p.Vendor.Name,
                 Status = p.Status,
                 TotalAmount = p.PurchaseReturnDetails.Sum(pd => pd.Quantity * pd.UnitPrice)
             }).Where(p => p.PurchaseReturnNo.Contains(search) || p.ReferenceNo.Contains(search))
                .ToList();
                return View(purchaseReturnList);
            }
            var purchaseReturnList2 = _db.PurchaseReturns
             .Select(p => new PurchaseReturnListViewModel
             {
                 PurchaseReturnId = p.PurchaseReturnId,
                 PurchaseReturnNo = p.PurchaseReturnNo,
                 PurchaseReturnDate = p.PurchaseReturnDate,
                 ReferenceNo = p.ReferenceNo,
                 VendorName = p.Vendor.Name,
                 Status = p.Status,
                 TotalAmount = p.PurchaseReturnDetails.Sum(pd => pd.Quantity * pd.UnitPrice)
             }).ToList();
            return View(purchaseReturnList2);
        }
        [HasPermission("Purchase Return Edit")]
        public ActionResult EditPurchaseReturn(int purchaseReturnId)
        {
            PurchaseReturn purchaseReturn = _db.PurchaseReturns.Find(purchaseReturnId);
            ViewBag.Items = _db.Items.Where(i => i.IsActive == true).ToList();
            ViewBag.Vendors = _db.Vendors.ToList();
            return View(purchaseReturn);
        }
        [HttpPost]
        [HasPermission("Purchase Return Edit")]
        public ActionResult EditPurchaseReturn(PurchaseReturn purchaseReturn)
        {
            if (purchaseReturn == null)
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
                    var existingPurchaseReturn = _db.PurchaseReturns.Include(p => p.PurchaseReturnDetails).FirstOrDefault(p => p.PurchaseReturnId == purchaseReturn.PurchaseReturnId);
                    if (existingPurchaseReturn == null)
                    {
                        return Json(new { status = "error", message = "Purchase return not found" }, JsonRequestBehavior.AllowGet);
                    }

                    // Update purchase metadata
                    purchaseReturn.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    purchaseReturn.ModifiedAt = DateTime.Now;

                    // Update existing purchase values
                    _db.Entry(existingPurchaseReturn).CurrentValues.SetValues(purchaseReturn);
                    _db.Entry(existingPurchaseReturn).Property(x => x.CreatedBy).IsModified = false;
                    _db.Entry(existingPurchaseReturn).Property(x => x.CreatedAt).IsModified = false;
                    _db.Entry(existingPurchaseReturn).Property(x => x.Status).IsModified = false;

                    // Update PurchaseDetails
                    var existingDetails = existingPurchaseReturn.PurchaseReturnDetails.ToList();
                    var newDetails = purchaseReturn.PurchaseReturnDetails;

                    // Update or Add new details
                    foreach (var newDetail in newDetails)
                    {
                        var existingDetail = existingDetails.FirstOrDefault(d => d.PurchaseReturnDetailId == newDetail.PurchaseReturnDetailId);
                        var warehouseItemStock = _db.WarehouseItemStocks.Find(newDetail.ItemId);
                        if (existingDetail != null)
                        {
                            // Revert stock to previous state before applying new changes
                            var revertedQuantity = warehouseItemStock.Quantity + existingDetail.Quantity;
                            var revertedTotalCost = (warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit) + (existingDetail.Quantity * existingDetail.UnitPrice);
                            var revertedPerUnitCost = revertedTotalCost / revertedQuantity;

                            warehouseItemStock.Quantity = revertedQuantity;
                            warehouseItemStock.CostPerUnit = revertedPerUnitCost;

                            warehouseItemStock.Quantity -= newDetail.Quantity;
                            var newTotalCost = newDetail.Quantity * newDetail.UnitPrice;
                            if (warehouseItemStock.Quantity == 0)
                            {
                                warehouseItemStock.CostPerUnit = 0;
                            }
                            else if (warehouseItemStock.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to update the purchase return. Quantity of " + warehouseItemStock.Item.ItemName + " cannot be < 0.";
                                return RedirectToAction("PurchaseList");
                            }
                            else
                            {
                                warehouseItemStock.CostPerUnit = ((revertedTotalCost - newTotalCost) / warehouseItemStock.Quantity);
                            }
                            var Notifications = CheckStockLevel(newDetail.ItemId);
                            SendStockLevelNotifications(Notifications);
                            _db.Entry(existingDetail).CurrentValues.SetValues(newDetail);
                            existingDetails.Remove(existingDetail); // Remove from existing list once matched
                        }
                        else
                        {
                            // For new details, update stock directly
                            var totalCostBeforeChange = (warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit);

                            warehouseItemStock.Quantity -= newDetail.Quantity; // Subtract new quantity

                            if (warehouseItemStock.Quantity == 0)
                            {
                                warehouseItemStock.CostPerUnit = 0;
                            }
                            else if (warehouseItemStock.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to update the purchase return. Quantity of " + warehouseItemStock.Item.ItemName + " cannot be < 0.";
                                return RedirectToAction("PurchaseList");
                            }
                            else
                            {
                                var totalCostAfterChange = totalCostBeforeChange + (newDetail.Quantity * newDetail.UnitPrice);
                                warehouseItemStock.CostPerUnit = totalCostAfterChange / warehouseItemStock.Quantity;
                            }
                            existingPurchaseReturn.PurchaseReturnDetails.Add(newDetail); // Add new detail
                        }
                    }

                    // Delete unmatched details
                    if (existingDetails != null)
                    {
                        foreach (var item in existingDetails)
                        {
                            var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);
                            decimal currentQuantity = warehouseItemStock.Quantity;
                            warehouseItemStock.Quantity = warehouseItemStock.Quantity + item.Quantity;
                            if (warehouseItemStock.Quantity == 0)
                            {
                                warehouseItemStock.CostPerUnit = 0;
                            }
                            else if (warehouseItemStock.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to update the purchase return. Quantity of " + warehouseItemStock.Item.ItemName + " cannot be < 0.";
                                return RedirectToAction("PurchaseList");
                            }
                            else
                            {
                                warehouseItemStock.CostPerUnit = ((currentQuantity * warehouseItemStock.CostPerUnit) + (item.Quantity * item.UnitPrice)) / (warehouseItemStock.Quantity == 0 ? 1 : warehouseItemStock.Quantity);
                            }
                            var Notifications = CheckStockLevel(item.ItemId);
                            SendStockLevelNotifications(Notifications);
                        }
                        _db.PurchaseReturnDetails.RemoveRange(existingDetails);
                    }


                    // Save changes
                    _db.SaveChanges();
                    transaction.Commit();

                    TempData["SuccessMessage"] = "Purchase return updated successfully.";
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

                    TempData["ErrorMessage"] = "An error occurred while updating the purchase return.";
                    return Json(new { status = "error", message = "Validation error occurred. Check logs for details." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        [HttpPost]
        [HasPermission("Purchase Return Delete")]
        public ActionResult DeletePurchaseReturn(int purchaseReturnId)
        {
            try
            {
                var purchaseReturn = _db.PurchaseReturns.Find(purchaseReturnId);
                if (purchaseReturn == null)
                {
                    TempData["ErrorMessage"] = "Purchase return not found.";
                    return RedirectToAction("PurchaseReturnList");
                }
                var purchaseReturnDetails = _db.PurchaseReturnDetails.Select(p => p).Where(p => p.PurchaseReturnId == purchaseReturnId);

                if (purchaseReturnDetails != null)
                {
                    foreach (var item in purchaseReturnDetails)
                    {
                        var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);
                        decimal currentQuantity = warehouseItemStock.Quantity;
                        warehouseItemStock.Quantity = warehouseItemStock.Quantity + item.Quantity;
                        if (warehouseItemStock.Quantity == 0)
                        {
                            warehouseItemStock.CostPerUnit = 0;
                        }
                        else if (warehouseItemStock.Quantity < 0)
                        {
                            TempData["ErrorMessage"] = "Unable to delete the purchase return. Quantity of " + warehouseItemStock.Item.ItemName + " cannot be < 0.";
                            return RedirectToAction("PurchaseList");
                        }
                        else
                        {
                            warehouseItemStock.CostPerUnit = ((currentQuantity * warehouseItemStock.CostPerUnit) + (item.Quantity * item.UnitPrice)) / warehouseItemStock.Quantity;
                        }
                        var Notifications = CheckStockLevel(item.ItemId);
                        SendStockLevelNotifications(Notifications);
                    }
                }
                _db.PurchaseReturnDetails.RemoveRange(purchaseReturnDetails);
                _db.PurchaseReturns.Remove(purchaseReturn);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Purchase return deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This purchase return is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the purchase return.";
                }
            }
            return RedirectToAction("PurchaseReturnList");
        }
        [HttpPost]
        [HasPermission("Purchase Return Delete")]
        public ActionResult DeleteSelectedPurchaseReturn(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var purchaseReturnToDelete = _db.PurchaseReturns.Where(c => selectedItems.Contains(c.PurchaseReturnId)).ToList();
                    var purchaseReturnDetails = _db.PurchaseReturnDetails.Where(c => selectedItems.Contains(c.PurchaseReturnId)).ToList();

                    //foreach (var item in purchaseReturnToDelete)
                    //{
                    //    var warehouseItemTransaction = _db.WarehouseItemTransactions.Where(w => w.TransactionType == "PurchaseReturn" && w.TransactionTypeId == item.PurchaseReturnId);
                    //    _db.WarehouseItemTransactions.RemoveRange(warehouseItemTransaction);
                    //}

                    if (purchaseReturnDetails != null)
                    {
                        foreach (var item in purchaseReturnDetails)
                        {
                            var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);
                            decimal oldQuantity = warehouseItemStock.Quantity;
                            warehouseItemStock.Quantity = warehouseItemStock.Quantity + item.Quantity;
                            if (warehouseItemStock.Quantity == 0)
                            {
                                warehouseItemStock.CostPerUnit = 0;
                            }
                            else if (warehouseItemStock.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to delete the purchase return "+item.PurchaseReturn.PurchaseReturnNo+". Quantity of " + warehouseItemStock.Item.ItemName + " cannot be < 0.";
                                return RedirectToAction("PurchaseList");
                            }
                            else
                            {
                                warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) + (item.Quantity * item.UnitPrice)) / warehouseItemStock.Quantity;
                            }
                            var Notifications = CheckStockLevel(item.ItemId);
                            SendStockLevelNotifications(Notifications);
                        }
                    }

                    _db.PurchaseReturnDetails.RemoveRange(purchaseReturnDetails);
                    _db.PurchaseReturns.RemoveRange(purchaseReturnToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Purchase returns deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A purchase return is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the purchase return.";
                    }
                }
            }
            return RedirectToAction("PurchaseReturnList");
        }
        [HasPermission("Issue Create")]
        public ActionResult CreateRequisitionIssue()
        {
            var branches = _db.Branches.Where(b => b.IsActive).Select(b => new { b.BranchId, b.BranchName }).ToList();
            ViewBag.Branches = new SelectList(branches, "BranchId", "BranchName");
            return View();
        }

        public JsonResult GetRequisitions(int BranchId)
        {
            var requisitions = _db.Requisitions
                .Where(r => r.BranchId == BranchId && r.Status != "Settled" && r.Status != "Rejected")
                .Select(r => new
                {
                    r.RequisitionId,
                    r.RequisitionNo
                }).ToList();

            return Json(requisitions, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetRequisitionDetails(int RequisitionId)
        {
            var requisitionDetails = _db.RequisitionDetails
                .Where(rd => rd.RequisitionId == RequisitionId)
                .Select(rd => new
                {
                    rd.ItemId,
                    rd.Item.ItemName,
                    rd.Description,
                    rd.Quantity,
                    IssuedQuantity = rd.Quantity - _db.WarehouseIssues
                    .Where(wi => wi.RequisitionId == RequisitionId)
                    .SelectMany(wi => wi.WarehouseIssueDetails)
                    .Where(wid => wid.ItemId == rd.ItemId)
                    .Sum(wid => (decimal?)wid.Quantity) ?? 0,
                }).ToList();

            return Json(requisitionDetails, JsonRequestBehavior.AllowGet);
        }
        [HasPermission("Issue Create")]
        public ActionResult CreateIssue(int requisitionId)
        {

            var requisition = _db.Requisitions.Find(requisitionId);
            if (requisition.Status == "Rejected" || requisition.Status == "Settled")
            {
                TempData["ErrorMessage"] = "Cannot create issue for 'Rejected' or 'Settled' requisition";
                return RedirectToAction("RequisitionList");
            }
            List<WarehouseIssueDetailsHelper> warehouseIssueDetailsHelper = new List<WarehouseIssueDetailsHelper>(); 
            foreach (var item in requisition.RequisitionDetails)
            {
                var previousIssuedQuantity = _db.WarehouseIssues
            .Where(wi => wi.RequisitionId == requisitionId)
            .SelectMany(wi => wi.WarehouseIssueDetails)
            .Where(wid => wid.ItemId == item.ItemId)
            .Sum(wid => (decimal?)wid.Quantity) ?? 0;

                var warehouseIssueDetails = new WarehouseIssueDetailsHelper
                {
                    ItemId = item.ItemId,
                    ItemName = item.Item.ItemName,
                    Description = item.Description,
                    RequestedQuantity = item.Quantity,
                    IssuedQuantity = 0,
                    PreviousIssuedQuantity = previousIssuedQuantity
                };
                warehouseIssueDetailsHelper.Add(warehouseIssueDetails);
            }

            var warehouseIssue = new WarehouseIssueHelper
            {
                BranchID = requisition.BranchId,
                BranchName = requisition.Branch.BranchName,
                RequisitionId = requisition.RequisitionId,
                RequisitionStatus = requisition.Status,
                RequisitionNo = requisition.RequisitionNo,
                WarehouseIssueDetails = warehouseIssueDetailsHelper

            };
            ViewBag.Items = _db.Items.Where(i => i.IsActive == true).ToList();
            ViewBag.Requisition = _db.Requisitions.Find(requisitionId);

            return View(warehouseIssue);
        }
        [HttpPost]
        [HasPermission("Issue Create")]
        public ActionResult CreateIssue(WarehouseIssueHelper warehouseIssueHelper)
        {
            try
            {
                // Remove items with zero or null IssuedQuantity
                warehouseIssueHelper.WarehouseIssueDetails = warehouseIssueHelper.WarehouseIssueDetails
                    .Where(item => item.IssuedQuantity > 0)
                    .ToList();

                // Check if all issued quantities are 0 or null
                if (warehouseIssueHelper.WarehouseIssueDetails.All(item => item.IssuedQuantity == 0))
                {
                    return Json(new { success = false, errorMessage = "Items issue quantity cannot be 0" });
                }

                if (warehouseIssueHelper.RequisitionStatus == "Rejected" || warehouseIssueHelper.RequisitionStatus == "Settled")
                {
                    TempData["ErrorMessage"] = "Cannot create issue for 'Rejected' or 'Settled' requisition";
                    return Json(new { success = false, redirect = Url.Action("RequisitionList", "Warehouse") });
                }
                else
                {
                    string datePart = DateTime.Now.ToString("yyyyMM");
                    int nextIssueNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.WarehouseIssues.Any())
                    {
                        // Bring the IssueNo values into memory, then extract the numeric part and calculate the max
                        nextIssueNumber = _db.WarehouseIssues
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.IssueNo.Substring(11)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    var requisition = _db.Requisitions.Find(warehouseIssueHelper.RequisitionId);

                    WarehouseIssue warehouseIssue = new WarehouseIssue
                    {
                        IssueNo = $"ISU-{datePart}{nextIssueNumber:D4}",
                        BranchID = warehouseIssueHelper.BranchID,
                        IssueDate = warehouseIssueHelper.IssueDate,
                        ReferenceNo = warehouseIssueHelper.ReferenceNo,
                        RequisitionId = warehouseIssueHelper.RequisitionId,
                        Memo = warehouseIssueHelper.Memo,
                        Status = "Pending",
                        CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId")),
                        CreatedAt = DateTime.Now
                    };

                    List<WarehouseIssueDetail> warehouseIssueDetails = new List<WarehouseIssueDetail>();
                    var isSettled = true;
                    foreach (var item in warehouseIssueHelper.WarehouseIssueDetails)
                    {
                        var warehouseItemStock = _db.WarehouseItemStocks.Where(m => m.ItemId == item.ItemId).FirstOrDefault();
                        if (item.IssuedQuantity > warehouseItemStock.Quantity)
                        {
                            return Json(new { success = false, errorMessage = $"Insufficient stock for {item.ItemName}. The available quantity is {warehouseItemStock.Quantity}" });
                        }
                        if ((item.PreviousIssuedQuantity + item.IssuedQuantity) > item.RequestedQuantity)
                        {
                            return Json(new { success = false, errorMessage = "'" + item.ItemName + "' issue quantity cannot be more than " + (item.RequestedQuantity-item.PreviousIssuedQuantity) });
                        }
                        else if ((item.PreviousIssuedQuantity + item.IssuedQuantity) < item.RequestedQuantity)
                        {
                            requisition.Status = "Partially Settled";
                            isSettled = false;
                        }
                        var selectedItem = _db.WarehouseItemStocks.Where(i => i.ItemId == item.ItemId).FirstOrDefault();
                        var warehouseIssueDetail = new WarehouseIssueDetail
                        {
                            IssueId = warehouseIssue.IssueId,
                            ItemId = item.ItemId,
                            Description = item.Description,
                            Quantity = item.IssuedQuantity,
                            CostApplied = selectedItem.CostPerUnit,
                        };
                        warehouseIssueDetails.Add(warehouseIssueDetail);
                        warehouseItemStock.Quantity = warehouseItemStock.Quantity - item.IssuedQuantity;
                        var Notifications = CheckStockLevel(item.ItemId);
                        SendStockLevelNotifications(Notifications);
                    }
                    if (isSettled)
                    {
                        requisition.Status = "Settled";
                    }
                    warehouseIssue.WarehouseIssueDetails = warehouseIssueDetails;

                    _db.WarehouseIssues.Add(warehouseIssue);
                    _db.SaveChanges();

                    // Send notifications to users with branch permissions
                    var branchId = requisition.BranchId;
                    var usersWithPermission = _db.UserBranchPermissions
                        .Where(ubp => ubp.BranchId == branchId)
                        .Select(ubp => ubp.UserId)
                        .ToList();

                    var issueNotifications = new List<Notification>();
                    foreach (var userId in usersWithPermission)
                    {
                        var user = _db.Users.Find(userId);
                        if (user != null)
                        {
                            var notification = new Notification
                            {
                                DateTime = DateTime.Now,
                                Title = "New Issue Created",
                                Message = $"A new issue has been created for requisition #{requisition.RequisitionNo} from {requisition.Branch.BranchName}",
                                RedirectUrl = $"/Kitchen/ViewIssue?IssueId={warehouseIssue.IssueId}",
                                Type = "Issue Alert",
                                IsRead = false,
                                BranchId = branchId,
                                UserId = userId,
                                ReferenceId = warehouseIssue.IssueId
                            };
                            issueNotifications.Add(notification);
                        }
                    }

                    if (issueNotifications.Any())
                    {
                        _db.Notifications.AddRange(issueNotifications);
                        _db.SaveChanges();

                        // Send real-time notifications via SignalR
                        var hubContext = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<Hub.RessentialHub>();
                        foreach (var notification in issueNotifications)
                        {
                            hubContext.Clients.Client(notification.User?.ConnectionId)
                                .receiveStockAlert(notification.Title, notification.Message, notification.NotificationId, notification.RedirectUrl);
                        }
                    }

                    TempData["SuccessMessage"] = "Warehouse Issue Created Successfully!";
                    return Json(new { success = true });
                }
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while creating the Issue.";
                return Json(new { success = false, redirect = Url.Action("RequisitionList", "Warehouse") });
            }
        }

        [HasPermission("Issue List")]
        public ActionResult IssueList(string search)
        {
            var warehouseIssue = _db.WarehouseIssues.AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                warehouseIssue = warehouseIssue.Where(c => c.WarehouseIssueDetails.Where(i => i.Item.ItemName.Contains(search)).Count() > 0 || c.Requisition.RequisitionNo.Contains(search) || c.IssueNo.Contains(search) || c.ReferenceNo.Contains(search) || c.Branch.BranchName.Contains(search) || c.Status.Contains(search));
            }
            return View(warehouseIssue.ToList());
        }
        [HasPermission("Issue Edit")]
        public ActionResult EditIssue(int issueId)
        {
            var warehouseIssue = _db.WarehouseIssues
                .Include(wi => wi.WarehouseIssueDetails)
                .FirstOrDefault(wi => wi.IssueId == issueId);

            if (warehouseIssue == null)
            {
                TempData["ErrorMessage"] = "Issue not found.";
                return RedirectToAction("IssueList");
            }

            var warehouseIssueHelper = new WarehouseIssueHelper
            {
                IssueId = warehouseIssue.IssueId,
                IssueNo = warehouseIssue.IssueNo,
                IssueDate = warehouseIssue.IssueDate,
                ReferenceNo = warehouseIssue.ReferenceNo,
                RequisitionId = warehouseIssue.RequisitionId,
                BranchID = warehouseIssue.BranchID,
                BranchName = warehouseIssue.Branch.BranchName,
                Memo = warehouseIssue.Memo,
                WarehouseIssueDetails = warehouseIssue.WarehouseIssueDetails.Select(detail => new WarehouseIssueDetailsHelper
                {
                    ItemId = detail.ItemId,
                    ItemName = detail.Item.ItemName,
                    Description = detail.Description,
                    RequestedQuantity = _db.RequisitionDetails
                        .Where(rd => rd.RequisitionId == warehouseIssue.RequisitionId && rd.ItemId == detail.ItemId)
                        .Select(rd => rd.Quantity).FirstOrDefault(),
                    PreviousIssuedQuantity = _db.WarehouseIssues
                        .Where(wi => wi.RequisitionId == warehouseIssue.RequisitionId)
                        .SelectMany(wi => wi.WarehouseIssueDetails)
                        .Where(wid => wid.ItemId == detail.ItemId)
                        .Sum(wid => (decimal?)wid.Quantity) - detail.Quantity ?? 0,
                    IssuedQuantity = detail.Quantity,
                    CostApplied = detail.CostApplied
                }).ToList()
            };

            return View(warehouseIssueHelper);
        }

        [HttpPost]
        [HasPermission("Issue Edit")]
        public ActionResult EditIssue(WarehouseIssueHelper warehouseIssueHelper)
        {
            try
            {
                if (warehouseIssueHelper.Status == "Settled")
                {
                    TempData["ErrorMessage"] = "Cannot edit settled issue.";
                    return Json(new { success = false, redirect = Url.Action("IssueList", "Warehouse") });
                }
                var requisition = _db.Requisitions.Find(warehouseIssueHelper.RequisitionId);
                var existingIssue = _db.WarehouseIssues
                    .Include(wi => wi.WarehouseIssueDetails)
                    .FirstOrDefault(wi => wi.IssueId == warehouseIssueHelper.IssueId);

                if (existingIssue == null)
                {
                    TempData["ErrorMessage"] = "Issue not found.";
                    return Json(new { success = false,redirect = Url.Action("IssueList", "Warehouse") });
                }
                var isSettled = true;
                foreach (var detail in warehouseIssueHelper.WarehouseIssueDetails)
                {
                    var existingDetail = existingIssue.WarehouseIssueDetails
                        .FirstOrDefault(d => d.ItemId == detail.ItemId);

                    if (existingDetail != null)
                    {
                        var warehouseItemStock = _db.WarehouseItemStocks.FirstOrDefault(ws => ws.ItemId == detail.ItemId);
                        if ((detail.PreviousIssuedQuantity + detail.IssuedQuantity) > detail.RequestedQuantity)
                        {
                            return Json(new { success = false, errorMessage = "'" + detail.ItemName + "' issue quantity cannot be more than " + (detail.RequestedQuantity - detail.PreviousIssuedQuantity) });
                        }
                        if (warehouseItemStock == null || detail.IssuedQuantity > (warehouseItemStock.Quantity + existingDetail.Quantity))
                        {
                            return Json(new { success = false, errorMessage = $"Insufficient stock for {detail.ItemName}. The available quantity is {warehouseItemStock.Quantity}" });
                        }
                        else if ((detail.PreviousIssuedQuantity + detail.IssuedQuantity) < detail.RequestedQuantity)
                        {
                            requisition.Status = "Partially Settled";
                            isSettled = false;
                        }

                        // Revert stock to previous state before applying new changes
                        var revertedQuantity = warehouseItemStock.Quantity + existingDetail.Quantity;
                        var revertedTotalCost = (warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit) + (existingDetail.Quantity * existingDetail.CostApplied);
                        var revertedPerUnitCost = revertedTotalCost / revertedQuantity;

                        warehouseItemStock.Quantity = revertedQuantity;
                        warehouseItemStock.CostPerUnit = revertedPerUnitCost;

                        warehouseItemStock.Quantity -= detail.IssuedQuantity;
                        var newTotalCost = detail.IssuedQuantity * detail.CostApplied;
                        if (warehouseItemStock.Quantity <= 0)
                        {
                            warehouseItemStock.CostPerUnit = 0;
                        }
                        else
                        {
                            warehouseItemStock.CostPerUnit = ((revertedTotalCost - newTotalCost) / warehouseItemStock.Quantity);
                        }

                        existingDetail.Description = detail.Description;
                        existingDetail.Quantity = detail.IssuedQuantity;
                        existingDetail.CostApplied = warehouseItemStock.CostPerUnit;
                    }
                    var Notifications = CheckStockLevel(detail.ItemId);
                    SendStockLevelNotifications(Notifications);
                }
                if (isSettled)
                {
                    requisition.Status = "Settled";
                }
                existingIssue.IssueDate = warehouseIssueHelper.IssueDate;
                existingIssue.ReferenceNo = warehouseIssueHelper.ReferenceNo;
                existingIssue.Memo = warehouseIssueHelper.Memo;
                existingIssue.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                existingIssue.ModifiedAt = DateTime.Now;

                _db.SaveChanges();
                TempData["SuccessMessage"] = "Issue updated successfully.";
                return Json(new { success = true });
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the issue.";
                return Json(new { success = false, redirect = Url.Action("IssueList", "Warehouse") });
            }
        }
        [HasPermission("Issue Delete")]
        public ActionResult DeleteIssue(int issueId)
        {
            try
            {
                var warehouseIssue = _db.WarehouseIssues
                    .Include(wi => wi.WarehouseIssueDetails)
                    .FirstOrDefault(wi => wi.IssueId == issueId);

                if (warehouseIssue == null)
                {
                    TempData["ErrorMessage"] = "Issue not found.";
                    return RedirectToAction("IssueList");
                }
                if (warehouseIssue.Status == "Settled")
                {
                    TempData["ErrorMessage"] = "Cannot delete settled issue.";
                    return Json(new { success = false, redirect = Url.Action("IssueList", "Warehouse") });
                }

                var requisition = _db.Requisitions.FirstOrDefault(r => r.RequisitionId == warehouseIssue.RequisitionId);
                if (requisition == null)
                {
                    TempData["ErrorMessage"] = "Associated requisition not found.";
                    return RedirectToAction("IssueList");
                }

                foreach (var detail in warehouseIssue.WarehouseIssueDetails)
                {
                    var warehouseItemStock = _db.WarehouseItemStocks.FirstOrDefault(ws => ws.ItemId == detail.ItemId);

                    if (warehouseItemStock != null)
                    {
                        // Revert stock by adding the issued quantity back
                        var revertedQuantity = warehouseItemStock.Quantity + detail.Quantity;
                        var revertedTotalCost = (warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit) + (detail.Quantity * detail.CostApplied);

                        warehouseItemStock.Quantity = revertedQuantity;
                        warehouseItemStock.CostPerUnit = revertedQuantity > 0 ? (revertedTotalCost / revertedQuantity) : 0;
                    }
                }

                // Remove the issue and its details
                _db.WarehouseIssueDetails.RemoveRange(warehouseIssue.WarehouseIssueDetails);
                _db.WarehouseIssues.Remove(warehouseIssue);

                // Update requisition status based on remaining issued quantities
                var totalIssuedQuantity = _db.WarehouseIssues
                    .Where(wi => wi.RequisitionId == warehouseIssue.RequisitionId && !(issueId == wi.IssueId))
                    .SelectMany(wi => wi.WarehouseIssueDetails)
                    .Sum(wid => (decimal?)wid.Quantity) ?? 0;

                var totalRequestedQuantity = _db.RequisitionDetails
                    .Where(rd => rd.RequisitionId == warehouseIssue.RequisitionId)
                    .Sum(rd => rd.Quantity);

                if (totalIssuedQuantity == 0)
                {
                    requisition.Status = "Pending";
                }
                else if (totalIssuedQuantity < totalRequestedQuantity)
                {
                    requisition.Status = "Partially Settled";
                }
                else
                {
                    requisition.Status = "Settled";
                }

                _db.SaveChanges();

                TempData["SuccessMessage"] = "Issue deleted successfully.";
                return RedirectToAction("IssueList");
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the issue.";
                return RedirectToAction("IssueList");
            }
        }
        [HttpPost]
        [HasPermission("Issue Delete")]
        public ActionResult DeleteSelectedIssues(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var issuesToDelete = _db.WarehouseIssues
                        .Include(wi => wi.WarehouseIssueDetails)
                        .Where(wi => selectedItems.Contains(wi.IssueId))
                        .ToList();

                    foreach (var issue in issuesToDelete)
                    {
                        if (issue.Status == "Settled")
                        {
                            TempData["ErrorMessage"] = "An Issue status is settled and cannot be deleted.";
                            return Json(new { success = false, redirect = Url.Action("IssueList", "Warehouse") });
                        }
                        var requisition = _db.Requisitions.FirstOrDefault(r => r.RequisitionId == issue.RequisitionId);
                        if (requisition == null)
                        {
                            TempData["ErrorMessage"] = "Associated requisition not found.";
                            return RedirectToAction("IssueList");
                        }

                        foreach (var detail in issue.WarehouseIssueDetails)
                        {
                            var warehouseItemStock = _db.WarehouseItemStocks.FirstOrDefault(ws => ws.ItemId == detail.ItemId);

                            if (warehouseItemStock != null)
                            {
                                // Revert stock by adding the issued quantity back
                                var revertedQuantity = warehouseItemStock.Quantity + detail.Quantity;
                                var revertedTotalCost = (warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit) + (detail.Quantity * detail.CostApplied);

                                warehouseItemStock.Quantity = revertedQuantity;
                                warehouseItemStock.CostPerUnit = revertedQuantity > 0 ? (revertedTotalCost / revertedQuantity) : 0;
                            }
                        }

                        // Remove the issue and its details
                        _db.WarehouseIssueDetails.RemoveRange(issue.WarehouseIssueDetails);
                        _db.WarehouseIssues.Remove(issue);

                        // Update requisition status based on remaining issued quantities
                        var totalIssuedQuantity = _db.WarehouseIssues
                            .Where(wi => wi.RequisitionId == issue.RequisitionId && !selectedItems.Contains(wi.IssueId))
                            .SelectMany(wi => wi.WarehouseIssueDetails)
                            .Sum(wid => (decimal?)wid.Quantity) ?? 0;

                        var totalRequestedQuantity = _db.RequisitionDetails
                            .Where(rd => rd.RequisitionId == issue.RequisitionId)
                            .Sum(rd => rd.Quantity);

                        if (totalIssuedQuantity == 0)
                        {
                            requisition.Status = "Pending";
                        }
                        else if (totalIssuedQuantity < totalRequestedQuantity)
                        {
                            requisition.Status = "Partially Settled";
                        }
                        else
                        {
                            requisition.Status = "Settled";
                        }
                    }

                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Issues deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "An Issue is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Issues.";
                    }
                }
            }
            return RedirectToAction("IssueList");
        }

        [HasPermission("Requisition List")]
        public ActionResult RequisitionList(string search)
        {
            var requisition = _db.Requisitions.AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                requisition = requisition.Where(c => c.RequisitionDetails.Where(i => i.Item.ItemName.Contains(search)).Count() > 0 || c.RequisitionNo.Contains(search) || c.Description.Contains(search));
            }
            return View(requisition.ToList());
        }
        [HttpPost]
        [HasPermission("Requisition Status Update")]
        public ActionResult RejectRequisition(int requisitionId)
        {
             try
            {
                var requisiton = _db.Requisitions.Find(requisitionId);
                if (requisiton == null)
                {
                    TempData["ErrorMessage"] = "Requisition not found.";
                    return RedirectToAction("RequisitionList");
                }
                if (requisiton.Status == "Settled")
                {
                    TempData["ErrorMessage"] = "Status cannot be updated for Settled requisition.";
                    return RedirectToAction("RequisitionList");
                }
                if (requisiton.Status == "Partially Settled")
                {
                    TempData["ErrorMessage"] = "Status cannot be updated for Partially Settled requisition.";
                    return RedirectToAction("RequisitionList");
                }
                requisiton.Status = "Rejected";
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Requisition status has been updated successfully.";
            }
            catch (DbUpdateException ex)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the requisition status.";
            }
            return RedirectToAction("RequisitionList");
        }
        [HttpPost]
        [HasPermission("Requisition Status Update")]
        public ActionResult PendingRequisition(int requisitionId)
        {
            try
            {
                var requisiton = _db.Requisitions.Find(requisitionId);
                if (requisiton == null)
                {
                    TempData["ErrorMessage"] = "Requisition not found.";
                    return RedirectToAction("RequisitionList");
                }
                if (requisiton.Status == "Settled")
                {
                    TempData["ErrorMessage"] = "Status cannot be updated for Settled requisition.";
                    return RedirectToAction("RequisitionList");
                }
                if (requisiton.Status == "Partially Settled")
                {
                    TempData["ErrorMessage"] = "Status cannot be updated for Partially Settled requisition.";
                    return RedirectToAction("RequisitionList");
                }
                requisiton.Status = "Pending";
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Requisition status has been updated successfully.";
            }
            catch (DbUpdateException ex)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the requisition status.";
            }
            return RedirectToAction("RequisitionList");
        }
        [HttpPost]
        [HasPermission("Requisition Status Update")]
        public ActionResult RejectSelectedRequisitions(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var requisitionsToReject = _db.Requisitions.Where(r => selectedItems.Contains(r.RequisitionId)).ToList();

                    foreach (var requisition in requisitionsToReject)
                    {
                        if (requisition.Status == "Settled" || requisition.Status == "Partially Settled")
                        {
                            TempData["ErrorMessage"] = "Status cannot be updated for Settled or Partially Settled requisitions.";
                            return RedirectToAction("RequisitionList");
                        }
                        requisition.Status = "Rejected";
                    }

                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Selected requisitions have been rejected successfully.";
                }
                catch (DbUpdateException ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while updating the requisition status.";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "No requisitions selected for rejection.";
            }

            return RedirectToAction("RequisitionList");
        }

        [HasPermission("Stock Return List")]
        public ActionResult ReturnStockList(string search)
        {
            var returnStocks = _db.ReturnStocks.AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                returnStocks = returnStocks.Where(w => w.ReturnNo.Contains(search) || w.ReferenceNo.Contains(search) || w.Description.Contains(search) || w.Status.Contains(search));
            }
            return View(returnStocks);
        }
        [HasPermission("Stock Return View")]
        public ActionResult ViewReturnStock(int returnStockId)
        {
            var returnStock = _db.ReturnStocks.Find(returnStockId);

            if (returnStock == null)
            {
                TempData["ErrorMessage"] = "Return stock not found.";
                return RedirectToAction("ReceiveStockList");
            }

            return View(returnStock);
        }

        [HttpPost]
        [HasPermission("Stock Return Status Update")]
        public ActionResult ReceiveReturnStock(int returnStockId)
        {
            try
            {
                var returnStocks = _db.ReturnStocks.Find(returnStockId);
                if (returnStocks.Status == "Settled")
                {
                    TempData["ErrorMessage"] = "The return stock is already received";
                    return RedirectToAction("ReceiveStockList");
                }

                foreach (var item in returnStocks.ReturnStockDetails)
                {
                    var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);

                    if (warehouseItemStock != null)
                    {
                        var currentTotalCost = warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit;
                        var newTotalCost = item.ItemQuantity * item.CostPerUnit;

                        warehouseItemStock.Quantity += item.ItemQuantity;
                        warehouseItemStock.CostPerUnit = (currentTotalCost + newTotalCost) / warehouseItemStock.Quantity;

                        // Mark the branchItem as modified
                        _db.Entry(warehouseItemStock).State = EntityState.Modified;
                    }
                }

                returnStocks.Status = "Settled";
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Returned stock received successfully.";
                return RedirectToAction("ReturnStockList");
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occured while receiving the returned stock.";
                return RedirectToAction("ReturnStockList");
            }
        }

        [HttpPost]
        [HasPermission("Stock Return Status Update")]
        public ActionResult ReceiveSelectedStock(int[] selectedItems)
        {
            try
            {
                var returnStockToReceive = _db.ReturnStocks.Where(c => selectedItems.Contains(c.ReturnStockId)).ToList();
                foreach (var returnStocks in returnStockToReceive)
                {
                    if (returnStocks.Status == "Settled")
                    {
                        TempData["ErrorMessage"] = "A return stock record exist which is already received";
                        return RedirectToAction("ReceiveStockList");
                    }

                    foreach (var item in returnStocks.ReturnStockDetails)
                    {
                        var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);

                        if (warehouseItemStock != null)
                        {
                            var currentTotalCost = warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit;
                            var newTotalCost = item.ItemQuantity * item.CostPerUnit;

                            warehouseItemStock.Quantity += item.ItemQuantity;
                            warehouseItemStock.CostPerUnit = (currentTotalCost + newTotalCost) / warehouseItemStock.Quantity;

                            // Mark the branchItem as modified
                            _db.Entry(warehouseItemStock).State = EntityState.Modified;
                        }
                    }

                    returnStocks.Status = "Settled";
                }
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Returned stocks received successfully.";
                return RedirectToAction("ReturnStockList");
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occured while receiving the returned stock.";
                return RedirectToAction("ReturnStockList");
            }
        }

        [HasPermission("User List")]
        public ActionResult UserList(String search)
        {
            var users = _db.Users
             .Where(u => !u.Role.IsBranchRole)
             .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                users = users.Where(c => c.UserName.Contains(search));
            }
            return View(users.ToList());
        }
        [HasPermission("User Create")]
        public ActionResult CreateUser()
        {
            ViewBag.Roles = _db.Roles.Where(r => !r.IsBranchRole);
            ViewBag.Branches = _db.Branches.Where(b => b.IsActive).ToList();
            return View();
        }

        [HttpPost]
        [HasPermission("User Create")]
        public ActionResult CreateUser(User user, String ConfirmPassword, HttpPostedFileBase ProfileImage, int[] BranchPermissions)
        {
            try
            {
                if (user.Password != ConfirmPassword)
                {
                    ModelState.AddModelError("", "Password and Confirm Password do not match.");
                    TempData["ErrorMessage"] = "Password and Confirm Password do not match.";
                    return View(user);
                }
                string fileName = null;

                if (ProfileImage != null && ProfileImage.ContentLength > 0)
                {
                    // Generate a unique file name to prevent overwriting
                    fileName = Guid.NewGuid() + Path.GetExtension(ProfileImage.FileName);

                    // Define the path to save the file
                    string uploadsFolder = Server.MapPath("~/Uploads/ProfileImages");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string filePath = Path.Combine(uploadsFolder, fileName);

                    // Save the file to the server
                    ProfileImage.SaveAs(filePath);
                }
                user.ProfileImage = fileName;
                user.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                user.CreatedAt = DateTime.Now;
                _db.Users.Add(user);
                _db.SaveChanges();

                // Save branch permissions
                if (BranchPermissions != null)
                {
                    foreach (var branchId in BranchPermissions)
                    {
                        var userBranchPermission = new UserBranchPermission
                        {
                            UserId = user.UserId,
                            BranchId = branchId
                        };
                        _db.UserBranchPermissions.Add(userBranchPermission);
                    }
                    _db.SaveChanges();
                }

                TempData["SuccessMessage"] = "User created successfully!";
                return RedirectToAction("UserList");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while creating the user: " + ex.Message);
            }

            TempData["ErrorMessage"] = "An error occurred while creating the user";
            return View(user);
        }

        [HasPermission("User Edit")]
        public ActionResult EditUser(int userId)
        {
            var user = _db.Users.Find(userId);
            if (user == null)
            {
                return HttpNotFound();
            }
            ViewBag.Roles = _db.Roles.Where(r => !r.IsBranchRole).ToList();
            ViewBag.Branches = _db.Branches.Where(b => b.IsActive).ToList();
            ViewBag.UserBranchPermissions = _db.UserBranchPermissions.Where(ubp => ubp.UserId == userId).Select(ubp => ubp.BranchId).ToList();
            return View(user);
        }
        [HttpPost]
        [HasPermission("User Edit")]
        public ActionResult EditUser(User user, String ConfirmPassword, HttpPostedFileBase ProfileImage, int[] BranchPermissions)
        {
            try
            {
                var existingUser = _db.Users.Find(user.UserId);
                if (existingUser == null)
                {
                    return HttpNotFound();
                }
                var existingImageName = existingUser.ProfileImage;
                ConfirmPassword = ConfirmPassword == "" ? null : ConfirmPassword;

                if (user.Password != ConfirmPassword)
                {
                    ModelState.AddModelError("", "Password and Confirm Password do not match.");
                    TempData["ErrorMessage"] = "Password and Confirm Password do not match.";
                    return View(user);
                }

                user.Email = existingUser.Email;
                user.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                user.ModifiedAt = DateTime.Now;
                _db.Entry(existingUser).CurrentValues.SetValues(user);
                _db.Entry(existingUser).Property(x => x.CreatedBy).IsModified = false;
                _db.Entry(existingUser).Property(x => x.CreatedAt).IsModified = false;

                if (ProfileImage != null && ProfileImage.ContentLength > 0)
                {
                    string uploadsFolder = Server.MapPath("~/Uploads/ProfileImages");

                    if (!string.IsNullOrEmpty(existingImageName))
                    {
                        string existingFilePath = Path.Combine(uploadsFolder, existingImageName);

                        // Delete the existing file if it exists
                        if (System.IO.File.Exists(existingFilePath))
                        {
                            System.IO.File.Delete(existingFilePath);
                        }
                    }
                    string fileName = Guid.NewGuid() + Path.GetExtension(ProfileImage.FileName);

                    // Define the path to save the file
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string filePath = Path.Combine(uploadsFolder, fileName);

                    // Save the file to the server
                    ProfileImage.SaveAs(filePath);
                    existingUser.ProfileImage = fileName;

                    //Checking if the user is current user and updating the profile image in session
                    if (user.UserId == Convert.ToInt32(Helper.GetUserInfo("userId")))
                    {
                        AccountController.UpdateProfileImageClaim(this.HttpContext, fileName);
                    }
                }
                else
                {
                    _db.Entry(existingUser).Property(x => x.ProfileImage).IsModified = false;
                }

                if (user.Password == null)
                {
                    _db.Entry(existingUser).Property(x => x.Password).IsModified = false;
                }
                _db.Entry(existingUser).State = EntityState.Modified;
                _db.SaveChanges();

                // Update branch permissions
                var existingPermissions = _db.UserBranchPermissions.Where(ubp => ubp.UserId == user.UserId).ToList();
                _db.UserBranchPermissions.RemoveRange(existingPermissions);
                if (BranchPermissions != null)
                {
                    foreach (var branchId in BranchPermissions)
                    {
                        var userBranchPermission = new UserBranchPermission
                        {
                            UserId = user.UserId,
                            BranchId = branchId
                        };
                        _db.UserBranchPermissions.Add(userBranchPermission);
                    }
                }
                _db.SaveChanges();

                TempData["SuccessMessage"] = "User updated successfully!";
                return RedirectToAction("UserList");
            }
            catch (DbEntityValidationException ex)
            {
                foreach (var validationErrors in ex.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        System.Diagnostics.Debug.WriteLine($"Property: {validationError.PropertyName} Error: {validationError.ErrorMessage}");
                    }
                }
                ModelState.AddModelError("", "An error occurred while updating the user: " + ex.Message);
            }
            TempData["ErrorMessage"] = "An error occurred while updating the user";
            return View(user);
        }
        [HttpPost]
        [HasPermission("User Delete")]
        public ActionResult DeleteUser(int userId)
        {
            var user = _db.Users.Find(userId);
            _db.Users.Remove(user);
            _db.SaveChanges();
            return RedirectToAction("UserList");
        }
        [HttpPost]
        [HasPermission("User Delete")]
        public ActionResult DeleteSelectedUsers(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                var usersToDelete = _db.Users.Where(c => selectedItems.Contains(c.UserId)).ToList();
                _db.Users.RemoveRange(usersToDelete);
                _db.SaveChanges();
            }
            return RedirectToAction("UserList");
        }
        public ActionResult AccountSetting()
        {
            // Retrieve the logged-in user ID from the session
            int loggedInUserId = Convert.ToInt32(Helper.GetUserInfo("userId"));

            // Fetch the user details from the database
            var user = _db.Users.SingleOrDefault(u => u.UserId == loggedInUserId);
            if (user == null)
            {
                return HttpNotFound("User not found");
            }

            return View(user);
        }
        [HttpPost]
        public ActionResult AccountSetting(User model, string NewPassword, string ConfirmPassword)
        {
            // Retrieve the logged-in user ID from the session
            int loggedInUserId = Convert.ToInt32(Helper.GetUserInfo("userId"));

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid data submitted.";
                return View(model);
            }

            // Fetch the user from the database
            var user = _db.Users.SingleOrDefault(u => u.UserId == loggedInUserId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("AccountSetting");
            }

            // Verify current password
            if (user.Password != model.Password) // Ensure to hash passwords in production
            {
                TempData["ErrorMessage"] = "Current password is incorrect.";
                return View(model);
            }

            // Update password if new password and confirmation match
            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                if (NewPassword == ConfirmPassword)
                {
                    user.Password = NewPassword; // Hash the password in production
                }
                else
                {
                    TempData["ErrorMessage"] = "New Password and Confirm New Password do not match.";
                    return View(model);
                }
            }

            // Update other fields
            user.UserName = model.UserName;
            user.ModifiedBy = loggedInUserId;
            user.ModifiedAt = DateTime.UtcNow;

            _db.SaveChanges();

            TempData["SuccessMessage"] = "Account settings updated successfully.";
            return RedirectToAction("Index");
        }
        [HasPermission("Role List")]
        public ActionResult RoleList(String search)
        {
            var roles = _db.Roles.Where(r => !r.IsBranchRole).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                roles = roles.Where(c => c.RoleName.Contains(search));
            }
            return View(roles.ToList());
        }
        [HasPermission("Role Create")]
        public ActionResult CreateRole()
        {
            var model = new RoleViewModel
            {
                Role = new Role(),
                PermissionsCategories = _db.PermissionsCategories.Include("Permissions").ToList()
            };
            return View(model);
        }
        [HttpPost]
        [HasPermission("Role Create")]
        public ActionResult CreateRole(RoleViewModel model)
        {
            if (ModelState.IsValid)
            {
                model.Role.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                model.Role.CreatedAt = DateTime.Now;
                _db.Roles.Add(model.Role);
                _db.SaveChanges();


                if (model.SelectedPermissions != null)
                {
                    foreach (var permissionId in model.SelectedPermissions)
                    {
                        var rolePermission = new RolePermission
                        {
                            RoleId = model.Role.RoleId,
                            PermissionId = permissionId
                        };
                        _db.RolePermissions.Add(rolePermission);
                    }

                    _db.SaveChanges();
                }

                TempData["SuccessMessage"] = "Role created successfully.";
                return RedirectToAction("RoleList");
            }

            model.PermissionsCategories = _db.PermissionsCategories.Include("Permissions").ToList(); // Re-populate permissions in case of error
            return View(model);
        }

        [HasPermission("Role Edit")]
        public ActionResult EditRole(int roleId)
        {
            var role = _db.Roles.Include(r => r.RolePermissions).FirstOrDefault(r => r.RoleId == roleId);
            if (role == null)
            {
                return HttpNotFound();
            }

            var model = new RoleViewModel
            {
                Role = role,
                PermissionsCategories = _db.PermissionsCategories.Include("Permissions").ToList(),
                SelectedPermissions = role.RolePermissions.Select(rp => rp.PermissionId).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [HasPermission("Role Edit")]
        public ActionResult EditRole(RoleViewModel model)
        {
            if (ModelState.IsValid)
            {
                var existingRole = _db.Roles.Include(r => r.RolePermissions).FirstOrDefault(r => r.RoleId == model.Role.RoleId);
                if (existingRole == null)
                {
                    return HttpNotFound();
                }

                existingRole.RoleName = model.Role.RoleName;
                existingRole.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                existingRole.ModifiedAt = DateTime.Now;

                // Update role permissions
                var existingPermissions = existingRole.RolePermissions.ToList();
                foreach (var permission in existingPermissions)
                {
                    _db.RolePermissions.Remove(permission);
                }

                foreach (var permissionId in model.SelectedPermissions)
                {
                    var rolePermission = new RolePermission
                    {
                        RoleId = existingRole.RoleId,
                        PermissionId = permissionId
                    };
                    _db.RolePermissions.Add(rolePermission);
                }

                _db.SaveChanges();
                TempData["SuccessMessage"] = "Role updated successfully.";
                return RedirectToAction("RoleList");
            }

            model.PermissionsCategories = _db.PermissionsCategories.Include("Permissions").ToList(); // Re-populate permissions in case of error
            return View(model);
        }

        [HttpPost]
        [HasPermission("Role Delete")]
        public ActionResult DeleteRole(int RoleId)
        {
            try
            {
                var Role = _db.Roles.Find(RoleId);
                if (Role == null)
                {
                    TempData["ErrorMessage"] = "Role not found.";
                    return RedirectToAction("RoleList");
                }

                _db.Roles.Remove(Role);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Role deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This Role is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Role.";
                }
            }
            return RedirectToAction("RoleList");
        }
        [HttpPost]
        [HasPermission("Role Delete")]
        public ActionResult DeleteSelectedRoles(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var itemsToDelete = _db.Roles.Where(c => selectedItems.Contains(c.RoleId)).ToList();
                    _db.Roles.RemoveRange(itemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Role deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A Role is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Role.";
                    }
                }
            }
            return RedirectToAction("RoleList");
        }

        [HasPermission("Payment Voucher View PDF")]
        public ActionResult PaymentVoucherPDF(int id)
        {
            var paymentVoucher = _db.PaymentVouchers
                .Include(v => v.Vendor)
                .Include(a => a.Account)
                .FirstOrDefault(v => v.PaymentVoucherId == id);

            if (paymentVoucher == null)
            {
                TempData["ErrorMessage"] = "Payment Voucher not found.";
                return RedirectToAction("PaymentVoucherList");
            }

            ViewBag.AmountInWords = NumberToWords((int)Math.Floor(paymentVoucher.Amount)).ToUpper();

            // Generate PDF as a byte array
            var pdfResult = new Rotativa.ViewAsPdf("PaymentVoucherPDF", paymentVoucher)
            {
                PageSize = Rotativa.Options.Size.A4,
            };

            var pdfBytes = pdfResult.BuildFile(ControllerContext);

            // Set the response headers for inline display
            Response.ContentType = "application/pdf";
            Response.AddHeader("Content-Disposition", "inline; filename=PaymentVoucher.pdf");
            Response.OutputStream.Write(pdfBytes, 0, pdfBytes.Length);
            Response.Flush();
            Response.End();

            return new EmptyResult();
        }

        [HasPermission("Receipt Voucher View PDF")]
        public ActionResult ReceiptVoucherPDF(int id)
        {
            var receiptVoucher = _db.ReceiptVouchers
                .Include(v => v.Vendor)
                .Include(a => a.Account)
                .FirstOrDefault(v => v.ReceiptVoucherId == id);

            if (receiptVoucher == null)
            {
                TempData["ErrorMessage"] = "Receipt Voucher not found.";
                return RedirectToAction("ReceiptVoucherList");
            }

            ViewBag.AmountInWords = NumberToWords((int)Math.Floor(receiptVoucher.Amount)).ToUpper();

            // Generate PDF as a byte array
            var pdfResult = new Rotativa.ViewAsPdf("ReceiptVoucherPDF", receiptVoucher)
            {
                PageSize = Rotativa.Options.Size.A4,
            };

            var pdfBytes = pdfResult.BuildFile(ControllerContext);

            // Set the response headers for inline display
            Response.ContentType = "application/pdf";
            Response.AddHeader("Content-Disposition", "inline; filename=ReceiptVoucher.pdf");
            Response.OutputStream.Write(pdfBytes, 0, pdfBytes.Length);
            Response.Flush();
            Response.End();

            return new EmptyResult();
        }

        private static string NumberToWords(int number)
        {
            if (number == 0)
                return "zero";

            if (number < 0)
                return "minus " + NumberToWords(Math.Abs(number));

            string words = "";

            if ((number / 1000000) > 0)
            {
                words += NumberToWords(number / 1000000) + " million ";
                number %= 1000000;
            }

            if ((number / 1000) > 0)
            {
                words += NumberToWords(number / 1000) + " thousand ";
                number %= 1000;
            }

            if ((number / 100) > 0)
            {
                words += NumberToWords(number / 100) + " hundred ";
                number %= 100;
            }

            if (number > 0)
            {
                if (words != "")
                    words += "and ";

                var unitsMap = new[] { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
                var tensMap = new[] { "zero", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

                if (number < 20)
                    words += unitsMap[number];
                else
                {
                    words += tensMap[number / 10];
                    if ((number % 10) > 0)
                        words += "-" + unitsMap[number % 10];
                }
            }

            return words;
        }

        public JsonResult GetAccountsByPaymentMethod(string paymentMethod)
        {
            List<Account> accounts = new List<Account>();

            if (paymentMethod == "Cash")
            {
                // Fetch Cash Accounts from your database or predefined list
                accounts = _db.Accounts.Where(a => a.AccountType == "Cash").ToList();
            }
            else if (paymentMethod == "Bank")
            {
                // Fetch Bank Accounts from your database or predefined list
                accounts = _db.Accounts.Where(a => a.AccountType == "Bank").ToList();
            }

            return Json(new SelectList(accounts, "AccountId", "AccountTitle"), JsonRequestBehavior.AllowGet);
        }
        public ActionResult DefaultBranch()
        {
            var branches = _db.Branches.Where(b => b.IsActive).ToList();

            // Filter branches based on user access
            var accessibleBranches = branches.Where(b => Helper.HasBranchAccess(b.BranchId)).ToList();
            if (accessibleBranches.Count > 0)
            {
                var claimsIdentity = User.Identity as ClaimsIdentity;

                // Retrieve the last selected branch from the session or database
                var lastSelectedBranchId = Session["LastSelectedBranchId"] as int?;

                // If no branch was previously selected, default to the first accessible branch
                var selectedBranchId = lastSelectedBranchId ?? accessibleBranches[0].BranchId;

                // Remove existing branch claim if exists
                var existingClaim = claimsIdentity?.FindFirst("BranchId");
                if (existingClaim != null)
                {
                    claimsIdentity.RemoveClaim(existingClaim);
                }

                // Add the new branch claim
                claimsIdentity?.AddClaim(new Claim("BranchId", selectedBranchId.ToString()));

                // Update the authentication cookie
                var ctx = Request.GetOwinContext();
                var authenticationManager = ctx.Authentication;
                authenticationManager.AuthenticationResponseGrant = new AuthenticationResponseGrant(
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties() { IsPersistent = true }
                );

                // Store the selected branch in the session
                Session["LastSelectedBranchId"] = selectedBranchId;

                return RedirectToAction("Index", "Kitchen");
            }
            else
            {
                return RedirectToAction("Unauthorized", "Account");
            }
        }

        public List<Notification> CheckStockLevel(int itemId)
        {
            var notifications = new List<Notification>();
            var item = _db.Items.Find(itemId);
            if (item != null)
            {
                var warehouseItemStock = _db.WarehouseItemStocks.Find(itemId);
                
                // Define notification type and message based on stock level
                string notificationType = null;
                string title = null;
                string message = null;
                string newStockLevelState = null;
                
                if (warehouseItemStock.Quantity == 0)
                {
                    notificationType = "Out of Stock";
                    title = "Out of Stock";
                    message = item.Sku + " - " + item.ItemName + " is out of stock.";
                    newStockLevelState = "OutOfStock";
                }
                else if (warehouseItemStock.Quantity < item.MinimumStockLevel)
                {
                    notificationType = "Low Stock";
                    title = "Low Stock";
                    message = item.Sku + " - " + item.ItemName + " is low in stock.";
                    newStockLevelState = "LowStock";
                }
                else
                {
                    newStockLevelState = "Normal";
                }
                
                // Only create notifications if the stock level state has changed
                if (notificationType != null && item.StockLevelState != newStockLevelState)
                {
                    // Create a notification for each user with warehouse permission
                    var users = _db.Users.Where(u => u.HasWarehousePermission);
                    foreach (var user in users)
                    {
                        var userNotification = new Notification
                        {
                            DateTime = DateTime.Now,
                            Title = title,
                            Message = message,
                            RedirectUrl = "/Warehouse/ItemList?search=" + item.Sku,
                            Type = "Stock Level Alert",
                            IsRead = false,
                            BranchId = 0,
                            UserId = user.UserId,
                            ReferenceId = itemId
                        };
                        notifications.Add(userNotification);
                    }

                    // Update the item's stock level state
                    item.StockLevelState = newStockLevelState;
                    _db.Entry(item).State = EntityState.Modified;
                }
            }
            return notifications;
        }

        #region Vendor Ledger
        public ActionResult VendorLedger()
        {
            ViewBag.Vendors = new SelectList(_db.Vendors.OrderBy(v => v.Name), "VendorId", "Name");
            ViewBag.Accounts = new SelectList(_db.Accounts.Where(a => a.AccountType == "Bank" || a.AccountType == "Cash").OrderBy(a => a.AccountTitle), "AccountId", "AccountTitle");
            return View();
        }

        [HttpGet]
        public ActionResult GenerateVendorLedgerReport(int vendorId, DateTime? fromDate, DateTime? toDate, int? accountId)
        {
            try
            {
                // If vendorId is 0, it means "All Vendors"
                string vendorName = "All Vendors";
                
                // Get vendor information if specific vendor selected
                if (vendorId > 0)
                {
                    var vendor = _db.Vendors.Find(vendorId);
                    if (vendor == null)
                    {
                        TempData["ErrorMessage"] = "Vendor not found.";
                        return RedirectToAction("VendorLedger");
                    }
                    vendorName = vendor.Name;
                }

                // Setup ViewBag data
                ViewBag.VendorName = vendorName;
                ViewBag.VendorId = vendorId;
                ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
                ViewBag.AccountId = accountId;
                ViewBag.GeneratedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                ViewBag.IsPdfExport = false;

                // Get vendor ledger data
                var ledgerData = GetVendorLedgerData(vendorId, fromDate, toDate, accountId);

                return View("VendorLedgerReport", ledgerData);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating report: {ex.Message}";
                return RedirectToAction("VendorLedger");
            }
        }

        [HttpGet]
        public ActionResult ExportVendorLedgerToPDF(int vendorId, DateTime? fromDate, DateTime? toDate, int? accountId)
        {
            try
            {
                // If vendorId is 0, it means "All Vendors"
                string vendorName = "All Vendors";
                string fileName = "All_Vendors";
                
                // Get vendor information if specific vendor selected
                if (vendorId > 0)
                {
                    var vendor = _db.Vendors.Find(vendorId);
                    if (vendor == null)
                    {
                        TempData["ErrorMessage"] = "Vendor not found.";
                        return RedirectToAction("VendorLedger");
                    }
                    vendorName = vendor.Name;
                    fileName = vendor.Name;
                }

                // Setup ViewBag data
                ViewBag.VendorName = vendorName;
                ViewBag.VendorId = vendorId;
                ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
                ViewBag.AccountId = accountId;
                ViewBag.GeneratedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                ViewBag.IsPdfExport = true;

                // Get vendor ledger data
                var ledgerData = GetVendorLedgerData(vendorId, fromDate, toDate, accountId);

                // Generate PDF
                var pdfResult = new Rotativa.ViewAsPdf("VendorLedgerReport", ledgerData)
                {
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Landscape,
                    FileName = $"Vendor_Ledger_{fileName}_{DateTime.Now:yyyyMMdd}.pdf"
                };

                return pdfResult;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating PDF: {ex.Message}";
                return RedirectToAction("VendorLedger");
            }
        }

        [HttpGet]
        public ActionResult ExportVendorLedgerToExcel(int vendorId, DateTime? fromDate, DateTime? toDate, int? accountId)
        {
            try
            {
                // If vendorId is 0, it means "All Vendors"
                string vendorName = "All Vendors";
                string fileName = "All_Vendors";
                
                // Get vendor information if specific vendor selected
                if (vendorId > 0)
                {
                    var vendor = _db.Vendors.Find(vendorId);
                    if (vendor == null)
                    {
                        TempData["ErrorMessage"] = "Vendor not found.";
                        return RedirectToAction("VendorLedger");
                    }
                    vendorName = vendor.Name;
                    fileName = vendor.Name;
                }

                // Set ViewBag values in case we need to render a view
                ViewBag.VendorName = vendorName;
                ViewBag.VendorId = vendorId;
                ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
                ViewBag.AccountId = accountId;
                ViewBag.GeneratedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                ViewBag.IsPdfExport = false;

                // Get vendor ledger data
                var ledgerData = GetVendorLedgerData(vendorId, fromDate, toDate, accountId);


                ExcelPackage.License.SetNonCommercialOrganization("Ressential");
                using (var package = new ExcelPackage())
                {
                    // Add a worksheet
                    var worksheet = package.Workbook.Worksheets.Add("Vendor Ledger");
                    
                    // Set document properties
                    package.Workbook.Properties.Title = "Vendor Ledger Report";
                    package.Workbook.Properties.Author = "Ressential";
                    package.Workbook.Properties.Company = "Ressential";
                    package.Workbook.Properties.Created = DateTime.Now;
                    
                    // Set column widths for better readability
                    worksheet.Column(1).Width = 6;      // S.No
                    worksheet.Column(2).Width = 18;     // Reference No.
                    worksheet.Column(3).Width = 12;     // Date
                    worksheet.Column(4).Width = 25;     // Vendor Name
                    worksheet.Column(5).Width = 25;     // Memo
                    worksheet.Column(6).Width = 20;     // Account
                    worksheet.Column(7).Width = 18;     // Instrument No.
                    worksheet.Column(8).Width = 15;     // Instrument Date
                    worksheet.Column(9).Width = 14;     // Dr
                    worksheet.Column(10).Width = 14;    // Cr
                    worksheet.Column(11).Width = 14;    // Balance
                    
                    // Professional color scheme
                    var headerBackground = System.Drawing.Color.FromArgb(31, 78, 120);   // Dark blue
                    var titleBackground = System.Drawing.Color.FromArgb(55, 125, 185);   // Medium blue
                    var alternateRowColor = System.Drawing.Color.FromArgb(240, 244, 248); // Very light blue
                    var totalRowColor = System.Drawing.Color.FromArgb(217, 225, 242);    // Light blue-gray
                    var borderColor = System.Drawing.Color.FromArgb(180, 199, 231);      // Light blue for borders
                    
                    // Add company logo placeholder and title (row 1)
                    worksheet.Cells[1, 1, 1, 11].Merge = true;
                    worksheet.Cells[1, 1].Value = "RESSENTIAL";
                    worksheet.Cells[1, 1].Style.Font.Size = 20;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[1, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    worksheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(headerBackground);
                    worksheet.Row(1).Height = 30; // Increase row height
                    
                    // Add report title (row 2)
                    worksheet.Cells[2, 1, 2, 11].Merge = true;
                    worksheet.Cells[2, 1].Value = "Vendor Ledger Report";
                    worksheet.Cells[2, 1].Style.Font.Size = 16;
                    worksheet.Cells[2, 1].Style.Font.Bold = true;
                    worksheet.Cells[2, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[2, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[2, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    worksheet.Cells[2, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[2, 1].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Row(2).Height = 25; // Increase row height
                    
                    // Empty row for spacing
                    worksheet.Row(3).Height = 10;
                    
                    // Report information section with styled boxes (rows 4-5)
                    // Vendor info (left box)
                    worksheet.Cells[4, 1, 4, 3].Merge = true;
                    worksheet.Cells[4, 1].Value = "VENDOR";
                    worksheet.Cells[4, 1].Style.Font.Bold = true;
                    worksheet.Cells[4, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, 1].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    
                    worksheet.Cells[5, 1, 5, 3].Merge = true;
                    worksheet.Cells[5, 1].Value = vendorName;
                    worksheet.Cells[5, 1].Style.Font.Size = 11;
                    worksheet.Cells[5, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, 1].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, 1].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                    
                    // Date range (middle box)
                    worksheet.Cells[4, 5, 4, 7].Merge = true;
                    worksheet.Cells[4, 5].Value = "DATE RANGE";
                    worksheet.Cells[4, 5].Style.Font.Bold = true;
                    worksheet.Cells[4, 5].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, 5].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, 5].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, 5].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    
                    worksheet.Cells[5, 5, 5, 7].Merge = true;
                    worksheet.Cells[5, 5].Value = $"{(fromDate?.ToString("yyyy-MM-dd") ?? "All")} to {(toDate?.ToString("yyyy-MM-dd") ?? "All")}";
                    worksheet.Cells[5, 5].Style.Font.Size = 11;
                    worksheet.Cells[5, 5].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, 5].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, 5].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                    
                    // Generated info (right box)
                    worksheet.Cells[4, 9, 4, 11].Merge = true;
                    worksheet.Cells[4, 9].Value = "GENERATED ON";
                    worksheet.Cells[4, 9].Style.Font.Bold = true;
                    worksheet.Cells[4, 9].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, 9].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, 9].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, 9].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    
                    worksheet.Cells[5, 9, 5, 11].Merge = true;
                    worksheet.Cells[5, 9].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cells[5, 9].Style.Font.Size = 11;
                    worksheet.Cells[5, 9].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, 9].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, 9].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                    
                    // Space before data
                    worksheet.Row(6).Height = 15;
                    
                    // Add table headers (row 7)
                    string[] headers = new string[] { "S.No", "Reference No.", "Date", "Vendor Name", "Memo", "Account", "Instrument No.", "Instrument Date", "Dr", "Cr", "Balance" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cells[7, i + 1];
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                        cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(headerBackground);
                        cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        cell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                        cell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                    }
                    worksheet.Row(7).Height = 22; // Increase header row height
                    
                    // Freeze panes for better navigation (fix header row)
                    worksheet.View.FreezePanes(8, 1);
                    
                    // Add data rows (starting at row 8)
                    int row = 8;
                    int serialNo = 1;
                    foreach (var item in ledgerData)
                    {
                        // Check if this is the opening balance row
                        bool isOpeningBalance = item.ReferenceNo == "OPENING";

                        // Add cell values
                        worksheet.Cells[row, 1].Value = isOpeningBalance ? (object)"" : serialNo++;
                        worksheet.Cells[row, 2].Value = item.ReferenceNo;
                        worksheet.Cells[row, 3].Value = item.Date;
                        worksheet.Cells[row, 4].Value = item.VendorName;
                        worksheet.Cells[row, 5].Value = item.Memo;
                        worksheet.Cells[row, 6].Value = item.Account;
                        worksheet.Cells[row, 7].Value = item.InstrumentNo;
                        worksheet.Cells[row, 8].Value = item.InstrumentDate;
                        worksheet.Cells[row, 9].Value = item.Dr;
                        worksheet.Cells[row, 10].Value = item.Cr;
                        worksheet.Cells[row, 11].Value = item.Balance;
                        
                        // Format cells
                        worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        worksheet.Cells[row, 3].Style.Numberformat.Format = "yyyy-mm-dd";
                        worksheet.Cells[row, 3].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        
                        if (item.InstrumentDate.HasValue)
                        {
                            worksheet.Cells[row, 8].Style.Numberformat.Format = "yyyy-mm-dd";
                            worksheet.Cells[row, 8].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        }
                        
                        // Format currency columns with right alignment and thousands separator
                        worksheet.Cells[row, 9].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[row, 9].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                        worksheet.Cells[row, 10].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[row, 10].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                        worksheet.Cells[row, 11].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[row, 11].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                        
                        // Apply thin borders to all cells in the row
                        for (int i = 1; i <= 11; i++)
                        {
                            worksheet.Cells[row, i].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                        }
                        
                        // Special formatting for opening balance row
                        if (isOpeningBalance)
                        {
                            // Merge cells for opening balance description
                            worksheet.Cells[row, 2, row, 8].Merge = true;
                            worksheet.Cells[row, 2].Value = "Opening Balance as of " + item.Date.ToString("yyyy-MM-dd");
                            worksheet.Cells[row, 2].Style.Font.Bold = true;
                            worksheet.Cells[row, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                            
                            // Style the opening balance row
                            var openingRowRange = worksheet.Cells[row, 1, row, 11];
                            openingRowRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            openingRowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(235, 241, 222)); // Light green
                            openingRowRange.Style.Font.Bold = true;
                            
                            // Add a thicker border around the opening balance row
                            openingRowRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Medium, System.Drawing.Color.FromArgb(17, 120, 100)); // Dark green
                        }
                        else
                        {
                            // Apply alternating row color for regular rows
                            if (serialNo % 2 == 0)
                            {
                                var rowRange = worksheet.Cells[row, 1, row, 11];
                                rowRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                                rowRange.Style.Fill.BackgroundColor.SetColor(alternateRowColor);
                            }
                        }
                        
                        row++;
                    }
                    
                    // Add total row with enhanced styling
                    // Add a separator line before totals
                    var separatorRow = row;
                    worksheet.Cells[separatorRow, 1, separatorRow, 11].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                    worksheet.Cells[separatorRow, 1, separatorRow, 11].Style.Border.Top.Color.SetColor(headerBackground);
                    
                    // Totals row
                    worksheet.Cells[row, 1, row, 8].Merge = true;
                    worksheet.Cells[row, 1].Value = "TOTAL";
                    worksheet.Cells[row, 1].Style.Font.Bold = true;
                    worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                    
                    worksheet.Cells[row, 9].Formula = $"SUM(I8:I{row-1})";
                    worksheet.Cells[row, 9].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[row, 9].Style.Font.Bold = true;
                    worksheet.Cells[row, 9].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                    
                    worksheet.Cells[row, 10].Formula = $"SUM(J8:J{row-1})";
                    worksheet.Cells[row, 10].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[row, 10].Style.Font.Bold = true;
                    worksheet.Cells[row, 10].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                    
                    worksheet.Cells[row, 11].Formula = $"I{row}-J{row}";
                    worksheet.Cells[row, 11].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[row, 11].Style.Font.Bold = true;
                    worksheet.Cells[row, 11].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                    
                    // Style the total row
                    var totalRowRange = worksheet.Cells[row, 1, row, 11];
                    totalRowRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    totalRowRange.Style.Fill.BackgroundColor.SetColor(totalRowColor);
                    totalRowRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Medium, headerBackground);
                    worksheet.Row(row).Height = 22; // Increase total row height
                    
                    // Add a footer with company info (2 rows after totals)
                    row += 2;
                    worksheet.Cells[row, 1, row, 11].Merge = true;
                    worksheet.Cells[row, 1].Value = "© Ressential - Generated by Financial Management System";
                    worksheet.Cells[row, 1].Style.Font.Size = 10;
                    worksheet.Cells[row, 1].Style.Font.Italic = true;
                    worksheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(100, 100, 100));
                    worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    
                    // Apply professional print settings
                    worksheet.PrinterSettings.FitToPage = true;
                    worksheet.PrinterSettings.FitToWidth = 1;
                    worksheet.PrinterSettings.FitToHeight = 0;
                    worksheet.PrinterSettings.Orientation = eOrientation.Landscape;
                    worksheet.PrinterSettings.RepeatRows = new OfficeOpenXml.ExcelAddress("7:7"); // Repeat header row on each page
                    worksheet.PrinterSettings.PrintArea = worksheet.Cells[1, 1, row, 11];
                    
                    // Convert to bytes
                    byte[] fileBytes = package.GetAsByteArray();
                    string excelFileName = $"Vendor_Ledger_{fileName}_{DateTime.Now:yyyyMMdd}.xlsx";
                    
                    // Return file
                    return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelFileName);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating Excel: {ex.Message}";
                return RedirectToAction("VendorLedger");
            }
        }

        private List<VendorLedgerViewModel> GetVendorLedgerData(int vendorId, DateTime? fromDate, DateTime? toDate, int? accountId)
        {
            // Handle vendorId=0 (All Vendors)
            var isAllVendors = vendorId == 0;
            // Handle accountId=0 (All Accounts)
            var isAllAccounts = !accountId.HasValue || accountId == 0;

            // First, calculate the opening balance (all transactions BEFORE fromDate)
            decimal openingBalance = 0;
            if (fromDate.HasValue)
            {
                // Purchases before fromDate
                var purchasesBefore = _db.Purchases
                    .Where(p => (isAllVendors || p.VendorId == vendorId) &&
                               p.PurchaseDate < fromDate)
                    .Sum(p => (decimal?)p.PurchaseDetails.Sum(d => d.Quantity * d.UnitPrice)) ?? 0;

                // Purchase Returns before fromDate
                var purchaseReturnsBefore = _db.PurchaseReturns
                    .Where(pr => (isAllVendors || pr.VendorID == vendorId) &&
                                pr.PurchaseReturnDate < fromDate)
                    .Sum(pr => (decimal?)pr.PurchaseReturnDetails.Sum(d => d.Quantity * d.UnitPrice)) ?? 0;

                // Payment Vouchers before fromDate
                var paymentVouchersBefore = _db.PaymentVouchers
                    .Where(pv => (isAllVendors || pv.VendorId == vendorId) &&
                               pv.PaymentVoucherDate < fromDate &&
                               (isAllAccounts || pv.AccountId == accountId))
                    .Sum(pv => (decimal?)pv.Amount) ?? 0;

                // Receipt Vouchers before fromDate
                var receiptVouchersBefore = _db.ReceiptVouchers
                    .Where(rv => (isAllVendors || rv.VendorId == vendorId) &&
                               rv.ReceiptVoucherDate < fromDate &&
                               (isAllAccounts || rv.AccountId == accountId))
                    .Sum(rv => (decimal?)rv.Amount) ?? 0;

                // Calculate opening balance (Dr - Cr)
                openingBalance = (purchasesBefore + receiptVouchersBefore) - (purchaseReturnsBefore + paymentVouchersBefore);
            }

            // Purchases - filter only if specific vendor selected
            var purchases = _db.Purchases
                .Where(p => (isAllVendors || p.VendorId == vendorId) &&
                            (!fromDate.HasValue || p.PurchaseDate >= fromDate) &&
                            (!toDate.HasValue || p.PurchaseDate <= toDate))
                .Select(p => new VendorLedgerViewModel
                {
                    ReferenceNo = p.PurchaseNo,
                    Date = p.PurchaseDate,
                    VendorName = p.Vendor.Name,
                    Memo = p.Memo,
                    Account = null,
                    InstrumentNo = null,
                    InstrumentDate = null,
                    Dr = p.PurchaseDetails.Sum(d => d.Quantity * d.UnitPrice),
                    Cr = 0
                }).ToList();

            // Purchase Returns - filter only if specific vendor selected
            var purchaseReturns = _db.PurchaseReturns
                .Where(pr => (isAllVendors || pr.VendorID == vendorId) &&
                             (!fromDate.HasValue || pr.PurchaseReturnDate >= fromDate) &&
                             (!toDate.HasValue || pr.PurchaseReturnDate <= toDate))
                .Select(pr => new VendorLedgerViewModel
                {
                    ReferenceNo = pr.PurchaseReturnNo,
                    Date = pr.PurchaseReturnDate,
                    VendorName = pr.Vendor.Name,
                    Memo = pr.Memo,
                    Account = null,
                    InstrumentNo = null,
                    InstrumentDate = null,
                    Dr = 0,
                    Cr = pr.PurchaseReturnDetails.Sum(d => d.Quantity * d.UnitPrice)
                }).ToList();

            // Payment Vouchers - filter by vendor and account (if specified)
            var paymentVouchers = _db.PaymentVouchers
                .Where(pv => (isAllVendors || pv.VendorId == vendorId) &&
                             (!fromDate.HasValue || pv.PaymentVoucherDate >= fromDate) &&
                             (!toDate.HasValue || pv.PaymentVoucherDate <= toDate) &&
                           (isAllAccounts || pv.AccountId == accountId))
                .Select(pv => new VendorLedgerViewModel
                {
                    ReferenceNo = pv.PaymentVoucherNo,
                    Date = pv.PaymentVoucherDate,
                    VendorName = pv.Vendor.Name,
                    Memo = "",
                    Account = pv.Account.AccountTitle,
                    InstrumentNo = pv.InstrumentNo,
                    InstrumentDate = pv.InstrumentDate,
                    Dr = 0,
                    Cr = pv.Amount
                }).ToList();

            // Receipt vouchers - filter by vendor and account (if specified)
            var receiptVouchers = _db.ReceiptVouchers
                .Where(rv => (isAllVendors || rv.VendorId == vendorId) &&
                             (!fromDate.HasValue || rv.ReceiptVoucherDate >= fromDate) &&
                             (!toDate.HasValue || rv.ReceiptVoucherDate <= toDate) &&
                           (isAllAccounts || rv.AccountId == accountId))
                .Select(rv => new VendorLedgerViewModel
                {
                    ReferenceNo = rv.ReceiptVoucherNo,
                    Date = rv.ReceiptVoucherDate,
                    VendorName = rv.Vendor.Name,
                    Memo = "",
                    Account = rv.Account.AccountTitle,
                    InstrumentNo = rv.InstrumentNo,
                    InstrumentDate = rv.InstrumentDate,
                    Dr = rv.Amount,
                    Cr = 0
                }).ToList();

            // Combine and order data
            var vendorLedgerData = new List<VendorLedgerViewModel>();

            // Add opening balance as first row if fromDate is specified
            if (fromDate.HasValue)
            {
                vendorLedgerData.Add(new VendorLedgerViewModel
                {
                    ReferenceNo = "OPENING",
                    Date = fromDate.Value.AddDays(-1),
                    VendorName = isAllVendors ? "All Vendors" : _db.Vendors.FirstOrDefault(v => v.VendorId == vendorId)?.Name,
                    Memo = "Opening Balance",
                    Account = null,
                    InstrumentNo = null,
                    InstrumentDate = null,
                    Dr = openingBalance > 0 ? openingBalance : 0,
                    Cr = openingBalance < 0 ? Math.Abs(openingBalance) : 0,
                    Balance = openingBalance
                });
            }

            vendorLedgerData.AddRange(purchases);
            vendorLedgerData.AddRange(purchaseReturns);
            vendorLedgerData.AddRange(paymentVouchers);
            vendorLedgerData.AddRange(receiptVouchers);
            vendorLedgerData = vendorLedgerData.OrderBy(x => x.Date).ToList();

            // Calculate running balance
            decimal balance = fromDate.HasValue ? openingBalance : 0;
            foreach (var entry in vendorLedgerData)
            {
                // Skip the opening balance row as its balance is already set
                if (entry.ReferenceNo != "OPENING")
            {
                balance += entry.Dr - entry.Cr;
                entry.Balance = balance;
                }
            }

            return vendorLedgerData;
        }
        #endregion

        #region Requisition Report

        // Requisition Report Methods
        
        public ActionResult RequisitionReport()
        {
            // Populate branches for the dropdown
            ViewBag.Branches = new SelectList(_db.Branches, "BranchId", "BranchName");
            return View();
        }
        
        [HttpGet]
        public ActionResult GenerateRequisitionReport(int branchId, DateTime? fromDate, DateTime? toDate, string status, string reportType)
        {
            // Set view bag data for the report
            ViewBag.BranchId = branchId;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.Status = status;
            ViewBag.ReportType = reportType ?? "summary";
            ViewBag.GeneratedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            // Get branch name
            if (branchId > 0)
            {
                var branch = _db.Branches.Find(branchId);
                ViewBag.BranchName = branch?.BranchName ?? "Unknown Branch";
            }
            else
            {
                ViewBag.BranchName = "All Branches";
            }
            ViewBag.StatusText = status;
            // Get report data
            var reportData = GetRequisitionReportData(branchId, fromDate, toDate, status, reportType);
            
            return View("RequisitionReportDisplay", reportData);
        }
        
        [HttpGet]
        public ActionResult ExportRequisitionReportToPDF(int branchId, DateTime? fromDate, DateTime? toDate, string status, string reportType)
        {
            try
            {
                // Set branch name
                string branchName = "All Branches";
                string fileName = "All_Branches";

                if (branchId > 0)
                {
                    var branch = _db.Branches.Find(branchId);
                    if (branch == null)
                    {
                        TempData["ErrorMessage"] = "Branch not found.";
                        return RedirectToAction("RequisitionReport");
                    }
                    branchName = branch.BranchName;
                    fileName = branch.BranchName.Replace(" ", "_");
                }

                // Set ViewBag data
                ViewBag.BranchName = branchName;
                ViewBag.BranchId = branchId;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
                ViewBag.Status = status;
                ViewBag.ReportType = reportType ?? "summary";
                ViewBag.GeneratedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                ViewBag.IsPdfExport = true;

                // Get report data
                var reportData = GetRequisitionReportData(branchId, fromDate, toDate, status, reportType);

                // Generate PDF
                var pdfResult = new Rotativa.ViewAsPdf("RequisitionReportDisplay", reportData)
                {
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Landscape,
                    FileName = $"Requisition_Report_{fileName}_{DateTime.Now:yyyyMMdd}.pdf"
                };

                return pdfResult;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating PDF: {ex.Message}";
                return RedirectToAction("RequisitionReport");
            }
        }


        [HttpGet]
        public ActionResult ExportRequisitionReportToExcel(int branchId, DateTime? fromDate, DateTime? toDate, string status, string reportType)
        {
            try
            {
                // Get report data
                var reportData = GetRequisitionReportData(branchId, fromDate, toDate, status, reportType);

                // Set branch name
                string branchName = "All Branches";
                string fileName = "All_Branches";

                if (branchId > 0)
                {
                    var branch = _db.Branches.Find(branchId);
                    if (branch == null)
                    {
                        TempData["ErrorMessage"] = "Branch not found.";
                        return RedirectToAction("RequisitionReport");
                    }
                    branchName = branch.BranchName;
                    fileName = branch.BranchName.Replace(" ", "_");
                }

                // Create Excel package
                ExcelPackage.License.SetNonCommercialOrganization("Ressential");
                using (var package = new ExcelPackage())
                {
                    // Add a worksheet
                    var worksheet = package.Workbook.Worksheets.Add("Requisition Report");

                    // Set document properties
                    package.Workbook.Properties.Title = "Requisition Report";
                    package.Workbook.Properties.Author = "Ressential";
                    package.Workbook.Properties.Company = "Ressential";
                    package.Workbook.Properties.Created = DateTime.Now;
                    
                    // Professional color scheme
                    var headerBackground = System.Drawing.Color.FromArgb(31, 78, 120);   // Dark blue
                    var titleBackground = System.Drawing.Color.FromArgb(55, 125, 185);   // Medium blue
                    var alternateRowColor = System.Drawing.Color.FromArgb(240, 244, 248); // Very light blue
                    var totalRowColor = System.Drawing.Color.FromArgb(217, 225, 242);    // Light blue-gray
                    var borderColor = System.Drawing.Color.FromArgb(180, 199, 231);      // Light blue for borders
                    
                    // Set column widths based on report type
                    if (reportType == "summary")
                    {
                        // Summary report columns
                        worksheet.Column(1).Width = 6;      // S.No
                        worksheet.Column(2).Width = 18;     // Requisition No
                        worksheet.Column(3).Width = 12;     // Date
                        worksheet.Column(4).Width = 25;     // Branch Name
                        worksheet.Column(5).Width = 40;     // Description
                        worksheet.Column(6).Width = 15;     // Total Items
                        worksheet.Column(7).Width = 15;     // Status
                    }
                    else
                    {
                        // Detailed report columns
                        worksheet.Column(1).Width = 6;      // S.No
                        worksheet.Column(2).Width = 18;     // Requisition No
                        worksheet.Column(3).Width = 12;     // Date
                        worksheet.Column(4).Width = 25;     // Branch Name
                        worksheet.Column(5).Width = 25;     // Item Name
                        worksheet.Column(6).Width = 10;     // Unit
                        worksheet.Column(7).Width = 15;     // Requested Qty
                        worksheet.Column(8).Width = 15;     // Issued Qty
                        worksheet.Column(9).Width = 15;     // Remaining Qty
                        worksheet.Column(10).Width = 15;    // Status
                        worksheet.Column(11).Width = 25;    // Description
                    }
                    
                    // Max columns for merging - depends on report type
                    int maxCol = reportType == "summary" ? 7 : 11;
                    
                    // Add company logo placeholder and title (row 1)
                    worksheet.Cells[1, 1, 1, maxCol].Merge = true;
                    worksheet.Cells[1, 1].Value = "RESSENTIAL";
                    worksheet.Cells[1, 1].Style.Font.Size = 20;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[1, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    worksheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(headerBackground);
                    worksheet.Row(1).Height = 30; // Increase row height
                    
                    // Add report title (row 2)
                    worksheet.Cells[2, 1, 2, maxCol].Merge = true;
                    worksheet.Cells[2, 1].Value = reportType == "summary" ? "Requisition Report (Summary)" : "Requisition Report (Detailed)";
                    worksheet.Cells[2, 1].Style.Font.Size = 16;
                    worksheet.Cells[2, 1].Style.Font.Bold = true;
                    worksheet.Cells[2, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[2, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[2, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    worksheet.Cells[2, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[2, 1].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Row(2).Height = 25; // Increase row height
                    
                    // Empty row for spacing
                    worksheet.Row(3).Height = 10;
                    
                    // Calculate box widths based on report type
                    int box1End = reportType == "summary" ? 2 : 3;
                    int box2Start = box1End + 1;
                    int box2End = reportType == "summary" ? 5 : 8;
                    int box3Start = box2End + 1;
                    
                    // Report information section with styled boxes (rows 4-5)
                    // Branch info (left box)
                    worksheet.Cells[4, 1, 4, box1End].Merge = true;
                    worksheet.Cells[4, 1].Value = "BRANCH";
                    worksheet.Cells[4, 1].Style.Font.Bold = true;
                    worksheet.Cells[4, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, 1].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    
                    worksheet.Cells[5, 1, 5, box1End].Merge = true;
                    worksheet.Cells[5, 1].Value = branchName;
                    worksheet.Cells[5, 1].Style.Font.Size = 11;
                    worksheet.Cells[5, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, 1].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, 1].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                    
                    // Date range (middle box)
                    worksheet.Cells[4, box2Start, 4, box2End].Merge = true;
                    worksheet.Cells[4, box2Start].Value = "DATE RANGE";
                    worksheet.Cells[4, box2Start].Style.Font.Bold = true;
                    worksheet.Cells[4, box2Start].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, box2Start].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, box2Start].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, box2Start].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    
                    worksheet.Cells[5, box2Start, 5, box2End].Merge = true;
                    worksheet.Cells[5, box2Start].Value = $"{(fromDate?.ToString("yyyy-MM-dd") ?? "All")} to {(toDate?.ToString("yyyy-MM-dd") ?? "All")}";
                    worksheet.Cells[5, box2Start].Style.Font.Size = 11;
                    worksheet.Cells[5, box2Start].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, box2Start].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, box2Start].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                    
                    // Status info (right box)
                    worksheet.Cells[4, box3Start, 4, maxCol].Merge = true;
                    worksheet.Cells[4, box3Start].Value = "STATUS";
                    worksheet.Cells[4, box3Start].Style.Font.Bold = true;
                    worksheet.Cells[4, box3Start].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, box3Start].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, box3Start].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, box3Start].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    
                    worksheet.Cells[5, box3Start, 5, maxCol].Merge = true;
                    worksheet.Cells[5, box3Start].Value = status == "0" ? "All Status" : status;
                    worksheet.Cells[5, box3Start].Style.Font.Size = 11;
                    worksheet.Cells[5, box3Start].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, box3Start].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, box3Start].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                    
                    // Space before data
                    worksheet.Row(6).Height = 15;
                    
                    // Add table headers (row 7) based on report type
                    string[] headers;
                    if (reportType == "summary")
                    {
                        headers = new string[] { "S.No", "Requisition No", "Date", "Branch", "Description", "Total Items", "Status" };
                    }
                    else
                    {
                        headers = new string[] { 
                            "S.No", "Requisition No", "Date", "Branch", "Item Name", "Unit", 
                            "Requested Qty", "Issued Qty", "Remaining Qty", "Status", "Description" 
                        };
                    }
                    
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cells[7, i + 1];
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                        cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(headerBackground);
                        cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        cell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                        cell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                    }
                    worksheet.Row(7).Height = 22; // Increase header row height
                    
                    // Freeze panes for better navigation (fix header row)
                    worksheet.View.FreezePanes(8, 1);
                    
                    // Add data rows based on report type
                    int currentRow = 8;
                    int serialNo = 1;
                    
                    if (reportType == "summary")
                    {
                        // Summary report - one row per requisition
                        foreach (var requisition in reportData)
                        {
                            // Apply alternating row background for better readability
                            if (serialNo % 2 == 0)
                            {
                                var range = worksheet.Cells[currentRow, 1, currentRow, maxCol];
                                range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                                range.Style.Fill.BackgroundColor.SetColor(alternateRowColor);
                            }
                            
                            // Total items - either count the items collection or use TotalItems property
                            int totalItems = requisition.Items != null ? requisition.Items.Count : requisition.TotalItems;
                            
                            // Add row data - summary format
                            worksheet.Cells[currentRow, 1].Value = serialNo++;
                            worksheet.Cells[currentRow, 2].Value = requisition.RequisitionNo;
                            worksheet.Cells[currentRow, 3].Value = requisition.Date.ToString("yyyy-MM-dd");
                            worksheet.Cells[currentRow, 4].Value = requisition.BranchName;
                            worksheet.Cells[currentRow, 5].Value = requisition.Description;
                            worksheet.Cells[currentRow, 6].Value = totalItems;
                            worksheet.Cells[currentRow, 7].Value = requisition.Status;
                            
                            // Format cells
                            for (int i = 1; i <= maxCol; i++)
                            {
                                worksheet.Cells[currentRow, i].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                            }
                            
                            // Date format
                            worksheet.Cells[currentRow, 3].Style.Numberformat.Format = "yyyy-mm-dd";
                            
                            // Numeric format for total items
                            worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0";
                            
                            currentRow++;
                        }
                        
                        // Add summary row
                        if (reportData.Any())
                        {
                            // Total row style
                            var totalRange = worksheet.Cells[currentRow, 1, currentRow, maxCol];
                            totalRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            totalRange.Style.Fill.BackgroundColor.SetColor(totalRowColor);
                            totalRange.Style.Font.Bold = true;
                            totalRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                            
                            // Total row content
                            worksheet.Cells[currentRow, 1, currentRow, 5].Merge = true;
                            worksheet.Cells[currentRow, 1].Value = "Total Requisitions:";
                            worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                            
                            // Merge the last two cells
                            worksheet.Cells[currentRow, 6, currentRow, 7].Merge = true;
                            worksheet.Cells[currentRow, 6].Value = reportData.Count();
                            worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0";
                            worksheet.Cells[currentRow, 6].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                            worksheet.Cells[currentRow, 6].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        }
                    }
                    else
                    {
                        // Detailed report - multiple rows per requisition (one per item)
                        foreach (var requisition in reportData)
                        {
                            if (requisition.Items != null && requisition.Items.Any())
                            {
                                foreach (var item in requisition.Items)
                                {
                                    // Apply alternating row background
                                    if (serialNo % 2 == 0)
                                    {
                                        var range = worksheet.Cells[currentRow, 1, currentRow, maxCol];
                                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                                        range.Style.Fill.BackgroundColor.SetColor(alternateRowColor);
                                    }
                                    
                                    // Add row data - detailed format
                                    worksheet.Cells[currentRow, 1].Value = serialNo++;
                                    worksheet.Cells[currentRow, 2].Value = requisition.RequisitionNo;
                                    worksheet.Cells[currentRow, 3].Value = requisition.Date.ToString("yyyy-MM-dd");
                                    worksheet.Cells[currentRow, 4].Value = requisition.BranchName;
                                    worksheet.Cells[currentRow, 5].Value = item.ItemName;
                                    worksheet.Cells[currentRow, 6].Value = item.Unit;
                                    worksheet.Cells[currentRow, 7].Value = item.RequestedQuantity;
                                    worksheet.Cells[currentRow, 8].Value = item.IssuedQuantity;
                                    worksheet.Cells[currentRow, 9].Value = item.RemainingQuantity;
                                    worksheet.Cells[currentRow, 10].Value = requisition.Status;
                                    worksheet.Cells[currentRow, 11].Value = requisition.Description;
                                    
                                    // Format cells
                                    for (int i = 1; i <= maxCol; i++)
                                    {
                                        worksheet.Cells[currentRow, i].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                                    }
                                    
                                    // Date format
                                    worksheet.Cells[currentRow, 3].Style.Numberformat.Format = "yyyy-mm-dd";
                                    
                                    // Numeric format
                                    worksheet.Cells[currentRow, 7].Style.Numberformat.Format = "#,##0.00";
                                    worksheet.Cells[currentRow, 8].Style.Numberformat.Format = "#,##0.00";
                                    worksheet.Cells[currentRow, 9].Style.Numberformat.Format = "#,##0.00";
                                    
                                    currentRow++;
                                }
                            }
                            else
                            {
                                // Handle empty items list
                                // Apply alternating row background
                                if (serialNo % 2 == 0)
                                {
                                    var range = worksheet.Cells[currentRow, 1, currentRow, maxCol];
                                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                                    range.Style.Fill.BackgroundColor.SetColor(alternateRowColor);
                                }
                                
                                // Add basic row data with empty item details
                                worksheet.Cells[currentRow, 1].Value = serialNo++;
                                worksheet.Cells[currentRow, 2].Value = requisition.RequisitionNo;
                                worksheet.Cells[currentRow, 3].Value = requisition.Date.ToString("yyyy-MM-dd");
                                worksheet.Cells[currentRow, 4].Value = requisition.BranchName;
                                worksheet.Cells[currentRow, 5].Value = "N/A";
                                worksheet.Cells[currentRow, 6].Value = "N/A";
                                worksheet.Cells[currentRow, 7].Value = 0;
                                worksheet.Cells[currentRow, 8].Value = 0;
                                worksheet.Cells[currentRow, 9].Value = 0;
                                worksheet.Cells[currentRow, 10].Value = requisition.Status;
                                worksheet.Cells[currentRow, 11].Value = requisition.Description;
                                
                                // Format cells
                                for (int i = 1; i <= maxCol; i++)
                                {
                                    worksheet.Cells[currentRow, i].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                                }
                                
                                // Date format
                                worksheet.Cells[currentRow, 3].Style.Numberformat.Format = "yyyy-mm-dd";
                                
                                // Numeric format
                                worksheet.Cells[currentRow, 7].Style.Numberformat.Format = "#,##0.00";
                                worksheet.Cells[currentRow, 8].Style.Numberformat.Format = "#,##0.00";
                                worksheet.Cells[currentRow, 9].Style.Numberformat.Format = "#,##0.00";
                                
                                currentRow++;
                            }
                        }
                        
                        // Add detailed report summary row
                        if (reportData.Any())
                        {
                            // Total row style
                            var totalRange = worksheet.Cells[currentRow, 1, currentRow, maxCol];
                            totalRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            totalRange.Style.Fill.BackgroundColor.SetColor(totalRowColor);
                            totalRange.Style.Font.Bold = true;
                            totalRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                            
                            // Total row content
                            worksheet.Cells[currentRow, 1, currentRow, 6].Merge = true;
                            worksheet.Cells[currentRow, 1].Value = "TOTALS:";
                            worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                            
                            // Calculate totals for quantities
                            decimal totalRequested = reportData.SelectMany(r => r.Items ?? new List<RequisitionItemViewModel>()).Sum(i => i.RequestedQuantity);
                            decimal totalIssued = reportData.SelectMany(r => r.Items ?? new List<RequisitionItemViewModel>()).Sum(i => i.IssuedQuantity);
                            decimal totalRemaining = reportData.SelectMany(r => r.Items ?? new List<RequisitionItemViewModel>()).Sum(i => i.RemainingQuantity);
                            
                            // Add total values
                            worksheet.Cells[currentRow, 7].Value = totalRequested;
                            worksheet.Cells[currentRow, 7].Style.Numberformat.Format = "#,##0.00";
                            worksheet.Cells[currentRow, 7].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                            
                            worksheet.Cells[currentRow, 8].Value = totalIssued;
                            worksheet.Cells[currentRow, 8].Style.Numberformat.Format = "#,##0.00";
                            worksheet.Cells[currentRow, 8].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                            
                            worksheet.Cells[currentRow, 9].Value = totalRemaining;
                            worksheet.Cells[currentRow, 9].Style.Numberformat.Format = "#,##0.00";
                            worksheet.Cells[currentRow, 9].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                            
                            // Total requisitions
                            worksheet.Cells[currentRow, 10, currentRow, 11].Merge = true;
                            worksheet.Cells[currentRow, 10].Value = $"Total Requisitions: {reportData.Count()}";
                            worksheet.Cells[currentRow, 10].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            worksheet.Cells[currentRow, 10].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                        }
                    }
                    
                    // Return the Excel file
                    byte[] excelBytes = package.GetAsByteArray();
                    return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"RequisitionReport_{fileName}_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating Excel: {ex.Message}";
                return RedirectToAction("RequisitionReport");
            }
        }

        private List<RequisitionReportViewModel> GetRequisitionReportData(int branchId, DateTime? fromDate, DateTime? toDate, string status, string reportType)
        {
            // Start with base query
            var query = _db.Requisitions
                .Include(r => r.Branch)
                .Include(r => r.RequisitionDetails.Select(rd => rd.Item))
                .AsQueryable();

            // Apply filters
            if (branchId > 0)
            {
                query = query.Where(r => r.BranchId == branchId);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(r => r.RequisitionDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(r => r.RequisitionDate <= toDate.Value);
            }

            if (status != "0")
            {
                query = query.Where(r => r.Status == status);
            }

            // Execute query and fetch data
            var requisitions = query.ToList();

            // Transform to view model
            var result = requisitions.Select(r => new RequisitionReportViewModel
            {
                RequisitionId = r.RequisitionId,
                RequisitionNo = r.RequisitionNo,
                Date = r.RequisitionDate,
                BranchName = r.Branch.BranchName,
                BranchId = r.BranchId,
                Description = r.Description,
                Status = r.Status,
                CreatedBy = _db.Users.Find(r.CreatedBy).UserName,
                TotalItems = r.RequisitionDetails.Count,
                Items = reportType == "detail"
                    ? r.RequisitionDetails.Select(rd => new RequisitionItemViewModel
                    {
                        ItemId = rd.ItemId,
                        ItemName = rd.Item.ItemName,
                        Unit = rd.Item.UnitOfMeasure.Symbol,
                        RequestedQuantity = rd.Quantity,
                        // Calculate issued quantity from warehouse issues
                        IssuedQuantity = _db.WarehouseIssueDetails
                            .Where(wid => wid.WarehouseIssue.RequisitionId == r.RequisitionId && wid.ItemId == rd.ItemId)
                            .Sum(wid => (decimal?)wid.Quantity) ?? 0,
                        // Calculate remaining quantity
                        RemainingQuantity = rd.Quantity - (_db.WarehouseIssueDetails
                            .Where(wid => wid.WarehouseIssue.RequisitionId == r.RequisitionId && wid.ItemId == rd.ItemId)
                            .Sum(wid => (decimal?)wid.Quantity) ?? 0),
                        Status = r.Status
                    }).ToList()
                    : null
            }).ToList();

            return result;
        }

        #endregion

        #region Item Stock Report

        public ActionResult ItemStockReport()
        {
            // Populate categories for the dropdown
            ViewBag.Categories = new SelectList(_db.ItemCategories, "ItemCategoryId", "ItemCategoryName");

            return View();
        }

        [HttpGet]
        public ActionResult GenerateItemStockReport(int categoryId, string status, string searchTerm, string sortBy)
        {
            // Set view bag data for the report
            ViewBag.CategoryId = categoryId;
            ViewBag.Status = status ?? "all";
            ViewBag.SearchTerm = searchTerm;
            ViewBag.SortBy = sortBy ?? "name";
            ViewBag.GeneratedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            // Get category name
            if (categoryId > 0)
            {
                var category = _db.ItemCategories.Find(categoryId);
                ViewBag.CategoryName = category?.ItemCategoryName ?? "Unknown Category";
            }
            else
            {
                ViewBag.CategoryName = "All Categories";
            }
            
            // Get report data
            var reportData = GetItemStockReportData(categoryId, status, searchTerm, sortBy);
            
            return View("ItemStockReportDisplay", reportData);
        }
        
        [HttpGet]
        public ActionResult ExportItemStockReportToPDF(int categoryId, string status, string searchTerm, string sortBy)
        {
            try
            {
                // Set category name
                string categoryName = "All Categories";
                string fileName = "All_Categories";

                if (categoryId > 0)
                {
                    var category = _db.ItemCategories.Find(categoryId);
                    if (category == null)
                    {
                        TempData["ErrorMessage"] = "Category not found.";
                        return RedirectToAction("ItemStockReport");
                    }
                    categoryName = category.ItemCategoryName;
                    fileName = category.ItemCategoryName.Replace(" ", "_");
                }

                // Set ViewBag data
                ViewBag.CategoryName = categoryName;
                ViewBag.CategoryId = categoryId;
                ViewBag.Status = status ?? "all";
                ViewBag.SearchTerm = searchTerm;
                ViewBag.SortBy = sortBy ?? "name";
                ViewBag.GeneratedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                ViewBag.IsPdfExport = true;

                // Get report data
                var reportData = GetItemStockReportData(categoryId, status, searchTerm, sortBy);

                // Generate PDF
                var pdfResult = new Rotativa.ViewAsPdf("ItemStockReportDisplay", reportData)
                {
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Landscape,
                    FileName = $"Item_Stock_Report_{fileName}_{DateTime.Now:yyyyMMdd}.pdf"
                };

                return pdfResult;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating PDF: {ex.Message}";
                return RedirectToAction("ItemStockReport");
            }
        }

        [HttpGet]
        public ActionResult ExportItemStockReportToExcel(int categoryId, string status, string searchTerm, string sortBy)
        {
            try
            {
                // Get report data
                var reportData = GetItemStockReportData(categoryId, status, searchTerm, sortBy);

                // Set category name
                string categoryName = "All Categories";
                string fileName = "All_Categories";

                if (categoryId > 0)
                {
                    var category = _db.ItemCategories.Find(categoryId);
                    if (category == null)
                    {
                        TempData["ErrorMessage"] = "Category not found.";
                        return RedirectToAction("ItemStockReport");
                    }
                    categoryName = category.ItemCategoryName;
                    fileName = category.ItemCategoryName.Replace(" ", "_");
                }

                // Create Excel package
                ExcelPackage.License.SetNonCommercialOrganization("Ressential");
                using (var package = new ExcelPackage())
                {
                    // Add a worksheet
                    var worksheet = package.Workbook.Worksheets.Add("Item Stock Report");

                    // Set document properties
                    package.Workbook.Properties.Title = "Item Stock Report";
                    package.Workbook.Properties.Author = "Ressential";
                    package.Workbook.Properties.Company = "Ressential";
                    package.Workbook.Properties.Created = DateTime.Now;
                    
                    // Professional color scheme
                    var headerBackground = System.Drawing.Color.FromArgb(31, 78, 120);   // Dark blue
                    var titleBackground = System.Drawing.Color.FromArgb(55, 125, 185);   // Medium blue
                    var alternateRowColor = System.Drawing.Color.FromArgb(240, 244, 248); // Very light blue
                    var totalRowColor = System.Drawing.Color.FromArgb(217, 225, 242);    // Light blue-gray
                    var borderColor = System.Drawing.Color.FromArgb(180, 199, 231);      // Light blue for borders
                    
                    // Set column widths
                    worksheet.Column(1).Width = 6;      // S.No
                    worksheet.Column(2).Width = 15;     // Item Code
                    worksheet.Column(3).Width = 30;     // Item Name
                    worksheet.Column(4).Width = 20;     // Category
                    worksheet.Column(5).Width = 10;     // Unit
                    worksheet.Column(6).Width = 15;     // Current Stock
                    worksheet.Column(7).Width = 15;     // Min Stock
                    worksheet.Column(8).Width = 15;     // Price
                    worksheet.Column(9).Width = 15;     // Stock Value
                    worksheet.Column(10).Width = 15;    // Stock Status
                    worksheet.Column(11).Width = 15;    // Item Status
                    
                    // Add company logo placeholder and title (row 1)
                    worksheet.Cells[1, 1, 1, 11].Merge = true;
                    worksheet.Cells[1, 1].Value = "RESSENTIAL";
                    worksheet.Cells[1, 1].Style.Font.Size = 20;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[1, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    worksheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(headerBackground);
                    worksheet.Row(1).Height = 30; // Increase row height
                    
                    // Add report title (row 2)
                    worksheet.Cells[2, 1, 2, 11].Merge = true;
                    worksheet.Cells[2, 1].Value = "Item Stock Report";
                    worksheet.Cells[2, 1].Style.Font.Size = 16;
                    worksheet.Cells[2, 1].Style.Font.Bold = true;
                    worksheet.Cells[2, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[2, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[2, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    worksheet.Cells[2, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[2, 1].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Row(2).Height = 25; // Increase row height
                    
                    // Empty row for spacing
                    worksheet.Row(3).Height = 10;
                    
                    // Report information section with styled boxes (rows 4-5)
                    // Category info box
                    worksheet.Cells[4, 1, 4, 2].Merge = true;
                    worksheet.Cells[4, 1].Value = "CATEGORY";
                    worksheet.Cells[4, 1].Style.Font.Bold = true;
                    worksheet.Cells[4, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, 1].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    
                    worksheet.Cells[5, 1, 5, 2].Merge = true;
                    worksheet.Cells[5, 1].Value = categoryName;
                    worksheet.Cells[5, 1].Style.Font.Size = 11;
                    worksheet.Cells[5, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, 1].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, 1].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                    
                    // Status Box
                    worksheet.Cells[4, 3, 4, 4].Merge = true;
                    worksheet.Cells[4, 3].Value = "STATUS";
                    worksheet.Cells[4, 3].Style.Font.Bold = true;
                    worksheet.Cells[4, 3].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, 3].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, 3].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, 3].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    
                    worksheet.Cells[5, 3, 5, 4].Merge = true;
                    worksheet.Cells[5, 3].Value = status == "active" ? "Active" : (status == "inactive" ? "Inactive" : "All");
                    worksheet.Cells[5, 3].Style.Font.Size = 11;
                    worksheet.Cells[5, 3].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, 3].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, 3].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                    
                    // Search term box
                    worksheet.Cells[4, 5, 4, 7].Merge = true;
                    worksheet.Cells[4, 5].Value = "SEARCH TERM";
                    worksheet.Cells[4, 5].Style.Font.Bold = true;
                    worksheet.Cells[4, 5].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, 5].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, 5].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, 5].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    
                    worksheet.Cells[5, 5, 5, 7].Merge = true;
                    worksheet.Cells[5, 5].Value = string.IsNullOrEmpty(searchTerm) ? "None" : searchTerm;
                    worksheet.Cells[5, 5].Style.Font.Size = 11;
                    worksheet.Cells[5, 5].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, 5].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, 5].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                    
                    // Date generated box
                    worksheet.Cells[4, 8, 4, 11].Merge = true;
                    worksheet.Cells[4, 8].Value = "GENERATED ON";
                    worksheet.Cells[4, 8].Style.Font.Bold = true;
                    worksheet.Cells[4, 8].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, 8].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, 8].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, 8].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    
                    worksheet.Cells[5, 8, 5, 11].Merge = true;
                    worksheet.Cells[5, 8].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cells[5, 8].Style.Font.Size = 11;
                    worksheet.Cells[5, 8].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, 8].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, 8].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                    
                    // Space before data
                    worksheet.Row(6).Height = 15;
                    
                    // Add table headers (row 7)
                    string[] headers = new string[] { 
                        "S.No", "Item Code", "Item Name", "Category", "Unit", 
                        "Current Stock", "Reorder Level", "Cost Per Unit", "Stock Value", "Stock Status", "Item Status" 
                    };
                    
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cells[7, i + 1];
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                        cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(headerBackground);
                        cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        cell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                        cell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                    }
                    worksheet.Row(7).Height = 22; // Increase header row height
                    
                    // Freeze panes for better navigation (fix header row)
                    worksheet.View.FreezePanes(8, 1);
                    
                    // Add data rows
                    int currentRow = 8;
                    int serialNo = 1;
                    decimal totalStockValue = 0;
                    
                    foreach (var item in reportData)
                    {
                        // Apply alternating row background for better readability
                        if (serialNo % 2 == 0)
                        {
                            var range = worksheet.Cells[currentRow, 1, currentRow, 11];
                            range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            range.Style.Fill.BackgroundColor.SetColor(alternateRowColor);
                        }
                        
                        // Calculate stock value
                        decimal stockValue = item.Stock * item.Price;
                        totalStockValue += stockValue;
                        
                        // Determine stock status
                        string stockStatus = "";
                        if (item.Stock <= 0)
                        {
                            stockStatus = "Out of Stock";
                        }
                        else if (item.Stock < item.MinStock)
                        {
                            stockStatus = "Low Stock";
                        }
                        else
                        {
                            stockStatus = "Good";
                        }
                        
                        // Add row data
                        worksheet.Cells[currentRow, 1].Value = serialNo++;
                        worksheet.Cells[currentRow, 2].Value = item.ItemCode;
                        worksheet.Cells[currentRow, 3].Value = item.ItemName;
                        worksheet.Cells[currentRow, 4].Value = item.CategoryName;
                        worksheet.Cells[currentRow, 5].Value = item.Unit;
                        worksheet.Cells[currentRow, 6].Value = item.Stock;
                        worksheet.Cells[currentRow, 7].Value = item.MinStock;
                        worksheet.Cells[currentRow, 8].Value = item.Price;
                        worksheet.Cells[currentRow, 9].Value = stockValue;
                        worksheet.Cells[currentRow, 10].Value = stockStatus;
                        worksheet.Cells[currentRow, 11].Value = item.IsActive ? "Active" : "Inactive";
                        
                        // Format cells
                        for (int i = 1; i <= 11; i++)
                        {
                            worksheet.Cells[currentRow, i].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                        }
                        
                        // Numeric format for quantities and price
                        worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[currentRow, 7].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[currentRow, 8].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[currentRow, 9].Style.Numberformat.Format = "#,##0.00";
                        
                        currentRow++;
                    }
                    
                    // Add summary row
                    if (reportData.Any())
                    {
                        // Total row style
                        var totalRange = worksheet.Cells[currentRow, 1, currentRow, 11];
                        totalRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        totalRange.Style.Fill.BackgroundColor.SetColor(totalRowColor);
                        totalRange.Style.Font.Bold = true;
                        totalRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                        
                        // Total row content
                        worksheet.Cells[currentRow, 1, currentRow, 8].Merge = true;
                        worksheet.Cells[currentRow, 1].Value = "TOTAL STOCK VALUE:";
                        worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                        
                        worksheet.Cells[currentRow, 9].Value = totalStockValue;
                        worksheet.Cells[currentRow, 9].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[currentRow, 9].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                        worksheet.Cells[currentRow, 10, currentRow, 11].Merge = true;
                        
                        // Add another row with item count
                        currentRow++;
                        var countRange = worksheet.Cells[currentRow, 1, currentRow, 11];
                        countRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        countRange.Style.Fill.BackgroundColor.SetColor(alternateRowColor);
                        countRange.Style.Font.Bold = true;
                        countRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                        
                        worksheet.Cells[currentRow, 1, currentRow, 11].Merge = true;
                        worksheet.Cells[currentRow, 1].Value = $"Total Items: {reportData.Count()}";
                        worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    }
                    
                    // Return the Excel file
                    byte[] excelBytes = package.GetAsByteArray();
                    return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ItemStockReport_{fileName}_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating Excel: {ex.Message}";
                return RedirectToAction("ItemStockReport");
            }
        }

        private List<ItemStockReportViewModel> GetItemStockReportData(int categoryId, string status, string searchTerm, string sortBy)
        {
            // Start with base query
            var query = _db.Items
                .Include(i => i.ItemCategory)
                .AsQueryable();

            // Apply category filter
            if (categoryId > 0)
            {
                query = query.Where(i => i.ItemCategoryId == categoryId);
            }

            // Apply status filter
            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                bool isActive = status == "active";
                query = query.Where(i => i.IsActive == isActive);
            }

            // Apply search filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(i => i.ItemName.Contains(searchTerm) || 
                                         i.Sku.Contains(searchTerm) || 
                                         i.ItemCategory.ItemCategoryName.Contains(searchTerm));
            }

            // Apply sorting
            switch (sortBy)
            {
                case "name":
                    query = query.OrderBy(i => i.ItemName);
                    break;
                case "code":
                    query = query.OrderBy(i => i.Sku);
                    break;
                case "category":
                    query = query.OrderBy(i => i.ItemCategory.ItemCategoryName);
                    break;
                case "stock":
                    query = query.OrderByDescending(i => i.WarehouseItemStock.Quantity);
                    break;
                case "price":
                    query = query.OrderByDescending(i => i.WarehouseItemStock.CostPerUnit);
                    break;
                default:
                    query = query.OrderBy(i => i.ItemName);
                    break;
            }

            // Execute query and fetch data
            var items = query.ToList();

            // Transform to view model
            var result = items.Select(i => new ItemStockReportViewModel
            {
                ItemId = i.ItemId,
                ItemCode = i.Sku,
                ItemName = i.ItemName,
                CategoryId = i.ItemCategoryId,
                CategoryName = i.ItemCategory?.ItemCategoryName ?? "Unknown",
                Unit = i.UnitOfMeasure.Symbol,
                Stock = i.WarehouseItemStock.Quantity,
                MinStock = i.MinimumStockLevel,
                Price = i.WarehouseItemStock.CostPerUnit,
                IsActive = i.IsActive
            }).ToList();

            return result;
        }
        #endregion

        #region Purchase Report
        public ActionResult PurchaseReport()
        {
            ViewBag.Vendors = new SelectList(_db.Vendors.ToList(), "VendorId", "Name");
            return View();
        }

        [HttpGet]
        public ActionResult GeneratePurchaseReport(int vendorId, DateTime? fromDate, DateTime? toDate, string status, string reportType)
        {
            try
            {
                var reportData = GetPurchaseReportData(vendorId, fromDate, toDate, status, reportType);

                // Set ViewBag data for the report display
                ViewBag.VendorName = vendorId > 0
                    ? _db.Vendors.FirstOrDefault(v => v.VendorId == vendorId)?.Name ?? "All Vendors"
                    : "All Vendors";
                ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
                ViewBag.Status = status;
                ViewBag.ReportType = reportType;
                ViewBag.GeneratedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                ViewBag.VendorId = vendorId;

                return View("PurchaseReportDisplay", reportData);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error generating report: " + ex.Message;
                return RedirectToAction("PurchaseReport");
            }
        }

        [HttpGet]
        public ActionResult ExportPurchaseReportToExcel(int vendorId, DateTime? fromDate, DateTime? toDate, string status, string reportType)
        {
            try
            {
                // Get report data
                var reportData = GetPurchaseReportData(vendorId, fromDate, toDate, status, reportType);

                // Set vendor name
                string vendorName = "All Vendors";
                string fileName = "All_Vendors";

                if (vendorId > 0)
                {
                    var vendor = _db.Vendors.Find(vendorId);
                    if (vendor == null)
                    {
                        TempData["ErrorMessage"] = "Vendor not found.";
                        return RedirectToAction("PurchaseReport");
                    }
                    vendorName = vendor.Name;
                    fileName = vendor.Name.Replace(" ", "_");
                }

                // Create Excel package
                ExcelPackage.License.SetNonCommercialOrganization("Ressential");
                using (var package = new ExcelPackage())
                {
                    // Add a worksheet
                    var worksheet = package.Workbook.Worksheets.Add("Purchase Report");

                    // Set document properties
                    package.Workbook.Properties.Title = "Purchase Report";
                    package.Workbook.Properties.Author = "Ressential";
                    package.Workbook.Properties.Company = "Ressential";
                    package.Workbook.Properties.Created = DateTime.Now;

                    // Professional color scheme
                    var headerBackground = System.Drawing.Color.FromArgb(31, 78, 120);   // Dark blue
                    var titleBackground = System.Drawing.Color.FromArgb(55, 125, 185);   // Medium blue
                    var alternateRowColor = System.Drawing.Color.FromArgb(240, 244, 248); // Very light blue
                    var totalRowColor = System.Drawing.Color.FromArgb(217, 225, 242);    // Light blue-gray
                    var borderColor = System.Drawing.Color.FromArgb(180, 199, 231);      // Light blue for borders

                    // Set column widths based on report type
                    if (reportType == "summary")
                    {
                        // Summary report columns
                        worksheet.Column(1).Width = 6;      // S.No
                        worksheet.Column(2).Width = 18;     // Purchase No
                        worksheet.Column(3).Width = 12;     // Date
                        worksheet.Column(4).Width = 25;     // Vendor Name
                        worksheet.Column(5).Width = 18;     // Reference
                        worksheet.Column(6).Width = 18;     // Total Amount
                        worksheet.Column(7).Width = 15;     // Status
                    }
                    else
                    {
                        // Detailed report columns
                        worksheet.Column(1).Width = 6;      // S.No
                        worksheet.Column(2).Width = 18;     // Purchase No
                        worksheet.Column(3).Width = 12;     // Date
                        worksheet.Column(4).Width = 25;     // Vendor Name
                        worksheet.Column(5).Width = 25;     // Item Name
                        worksheet.Column(6).Width = 15;     // Quantity
                        worksheet.Column(7).Width = 15;     // Unit Price
                        worksheet.Column(8).Width = 15;     // Total
                        worksheet.Column(9).Width = 15;     // Status
                    }

                    // Max columns for merging - depends on report type
                    int maxCol = reportType == "summary" ? 7 : 9;

                    // Add company logo placeholder and title (row 1)
                    worksheet.Cells[1, 1, 1, maxCol].Merge = true;
                    worksheet.Cells[1, 1].Value = "RESSENTIAL";
                    worksheet.Cells[1, 1].Style.Font.Size = 20;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[1, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    worksheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(headerBackground);
                    worksheet.Row(1).Height = 30; // Increase row height

                    // Add report title (row 2)
                    worksheet.Cells[2, 1, 2, maxCol].Merge = true;
                    worksheet.Cells[2, 1].Value = reportType == "summary" ? "Purchase Report (Summary)" : "Purchase Report (Detailed)";
                    worksheet.Cells[2, 1].Style.Font.Size = 16;
                    worksheet.Cells[2, 1].Style.Font.Bold = true;
                    worksheet.Cells[2, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[2, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[2, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    worksheet.Cells[2, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[2, 1].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Row(2).Height = 25; // Increase row height

                    // Empty row for spacing
                    worksheet.Row(3).Height = 10;

                    // Calculate box widths based on report type
                    int box1End = reportType == "summary" ? 2 : 3;
                    int box2Start = box1End + 1;
                    int box2End = reportType == "summary" ? 5 : 5;
                    int box3Start = box2End + 1;

                    // Report information section with styled boxes (rows 4-5)
                    // Vendor info (left box)
                    worksheet.Cells[4, 1, 4, box1End].Merge = true;
                    worksheet.Cells[4, 1].Value = "VENDOR";
                    worksheet.Cells[4, 1].Style.Font.Bold = true;
                    worksheet.Cells[4, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, 1].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                    worksheet.Cells[5, 1, 5, box1End].Merge = true;
                    worksheet.Cells[5, 1].Value = vendorName;
                    worksheet.Cells[5, 1].Style.Font.Size = 11;
                    worksheet.Cells[5, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, 1].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, 1].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;

                    // Date range (middle box)
                    worksheet.Cells[4, box2Start, 4, box2End].Merge = true;
                    worksheet.Cells[4, box2Start].Value = "DATE RANGE";
                    worksheet.Cells[4, box2Start].Style.Font.Bold = true;
                    worksheet.Cells[4, box2Start].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, box2Start].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, box2Start].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, box2Start].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                    worksheet.Cells[5, box2Start, 5, box2End].Merge = true;
                    worksheet.Cells[5, box2Start].Value = $"{(fromDate?.ToString("yyyy-MM-dd") ?? "All")} to {(toDate?.ToString("yyyy-MM-dd") ?? "All")}";
                    worksheet.Cells[5, box2Start].Style.Font.Size = 11;
                    worksheet.Cells[5, box2Start].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, box2Start].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, box2Start].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;

                    // Status info (right box)
                    worksheet.Cells[4, box3Start, 4, maxCol].Merge = true;
                    worksheet.Cells[4, box3Start].Value = "STATUS";
                    worksheet.Cells[4, box3Start].Style.Font.Bold = true;
                    worksheet.Cells[4, box3Start].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, box3Start].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, box3Start].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, box3Start].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                    worksheet.Cells[5, box3Start, 5, maxCol].Merge = true;
                    worksheet.Cells[5, box3Start].Value = status == "0" ? "All Status" : status;
                    worksheet.Cells[5, box3Start].Style.Font.Size = 11;
                    worksheet.Cells[5, box3Start].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, box3Start].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, box3Start].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;

                    // Space before data
                    worksheet.Row(6).Height = 15;

                    // Add table headers (row 7) based on report type
                    string[] headers;
                    if (reportType == "summary")
                    {
                        headers = new string[] { "S.No", "Purchase No", "Date", "Vendor", "Reference", "Total Amount", "Status" };
                    }
                    else
                    {
                        headers = new string[] {
                            "S.No", "Purchase No", "Date", "Vendor", "Item", "Quantity",
                            "Unit Price", "Total", "Status"
                        };
                    }

                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cells[7, i + 1];
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                        cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(headerBackground);
                        cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        cell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                        cell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                    }
                    worksheet.Row(7).Height = 22; // Increase header row height

                    // Freeze panes for better navigation (fix header row)
                    worksheet.View.FreezePanes(8, 1);

                    // Add data rows based on report type
                    int currentRow = 8;
                    int serialNo = 1;

                    if (reportType == "summary")
                    {
                        // Summary report - one row per purchase
                        decimal totalAmount = 0;
                        foreach (var purchase in reportData)
                        {
                            // Apply alternating row background for better readability
                            if (serialNo % 2 == 0)
                            {
                                var range = worksheet.Cells[currentRow, 1, currentRow, maxCol];
                                range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                                range.Style.Fill.BackgroundColor.SetColor(alternateRowColor);
                            }

                            // Add row data - summary format
                            worksheet.Cells[currentRow, 1].Value = serialNo++;
                            worksheet.Cells[currentRow, 2].Value = purchase.PurchaseNo;
                            worksheet.Cells[currentRow, 3].Value = purchase.Date.ToString("yyyy-MM-dd");
                            worksheet.Cells[currentRow, 4].Value = purchase.VendorName;
                            worksheet.Cells[currentRow, 5].Value = purchase.Reference;
                            worksheet.Cells[currentRow, 6].Value = purchase.TotalAmount;
                            worksheet.Cells[currentRow, 7].Value = purchase.Status;

                            // Format cells
                            for (int i = 1; i <= maxCol; i++)
                            {
                                worksheet.Cells[currentRow, i].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                            }

                            // Date format
                            worksheet.Cells[currentRow, 3].Style.Numberformat.Format = "yyyy-mm-dd";

                            // Numeric format for total amount
                            worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0.00";

                            totalAmount += purchase.TotalAmount;
                            currentRow++;
                        }

                        // Add summary row
                        if (reportData.Any())
                        {
                            // Total row style
                            var totalRange = worksheet.Cells[currentRow, 1, currentRow, maxCol];
                            totalRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            totalRange.Style.Fill.BackgroundColor.SetColor(totalRowColor);
                            totalRange.Style.Font.Bold = true;
                            totalRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);

                            // Total row content
                            worksheet.Cells[currentRow, 1, currentRow, 5].Merge = true;
                            worksheet.Cells[currentRow, 1].Value = "Total Amount:";
                            worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                            worksheet.Cells[currentRow, 6].Value = totalAmount;
                            worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0.00";

                            currentRow++;

                            // Count row style
                            var countRange = worksheet.Cells[currentRow, 1, currentRow, maxCol];
                            countRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            countRange.Style.Fill.BackgroundColor.SetColor(totalRowColor);
                            countRange.Style.Font.Bold = true;
                            countRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);

                            // Count row content
                            worksheet.Cells[currentRow, 1, currentRow, 5].Merge = true;
                            worksheet.Cells[currentRow, 1].Value = "Total Purchases:";
                            worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                            worksheet.Cells[currentRow, 6].Value = reportData.Count();
                        }
                    }
                    else
                    {
                        // Detailed report - one row per purchase item
                        decimal totalQuantity = 0;
                        decimal totalValue = 0;

                        foreach (var purchase in reportData)
                        {
                            if (purchase.Items != null && purchase.Items.Any())
                            {
                                foreach (var item in purchase.Items)
                                {
                                    decimal itemTotal = item.Quantity * item.UnitPrice;

                                    // Apply alternating row background for better readability
                                    if (serialNo % 2 == 0)
                                    {
                                        var range = worksheet.Cells[currentRow, 1, currentRow, maxCol];
                                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                                        range.Style.Fill.BackgroundColor.SetColor(alternateRowColor);
                                    }

                                    // Add row data - detailed format
                                    worksheet.Cells[currentRow, 1].Value = serialNo++;
                                    worksheet.Cells[currentRow, 2].Value = purchase.PurchaseNo;
                                    worksheet.Cells[currentRow, 3].Value = purchase.Date.ToString("yyyy-MM-dd");
                                    worksheet.Cells[currentRow, 4].Value = purchase.VendorName;
                                    worksheet.Cells[currentRow, 5].Value = item.ItemName;
                                    worksheet.Cells[currentRow, 6].Value = item.Quantity;
                                    worksheet.Cells[currentRow, 7].Value = item.UnitPrice;
                                    worksheet.Cells[currentRow, 8].Value = itemTotal;
                                    worksheet.Cells[currentRow, 9].Value = purchase.Status;

                                    // Format cells
                                    for (int i = 1; i <= maxCol; i++)
                                    {
                                        worksheet.Cells[currentRow, i].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                                    }

                                    // Date format
                                    worksheet.Cells[currentRow, 3].Style.Numberformat.Format = "yyyy-mm-dd";

                                    // Numeric formats
                                    worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0.00";
                                    worksheet.Cells[currentRow, 7].Style.Numberformat.Format = "#,##0.00";
                                    worksheet.Cells[currentRow, 8].Style.Numberformat.Format = "#,##0.00";

                                    totalQuantity += item.Quantity;
                                    totalValue += itemTotal;
                                    currentRow++;
                                }
                            }
                        }

                        // Add summary row
                        if (reportData.Any())
                        {
                            // Total row style
                            var totalRange = worksheet.Cells[currentRow, 1, currentRow, maxCol];
                            totalRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            totalRange.Style.Fill.BackgroundColor.SetColor(totalRowColor);
                            totalRange.Style.Font.Bold = true;
                            totalRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);

                            // Total row content
                            worksheet.Cells[currentRow, 1, currentRow, 5].Merge = true;
                            worksheet.Cells[currentRow, 1].Value = "Grand Total:";
                            worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                            worksheet.Cells[currentRow, 6].Value = totalQuantity;
                            worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0.00";
                            worksheet.Cells[currentRow, 8].Value = totalValue;
                            worksheet.Cells[currentRow, 8].Style.Numberformat.Format = "#,##0.00";
                        }
                    }

                    // Add footer note (optional)
                    currentRow += 2;
                    worksheet.Cells[currentRow, 1, currentRow, maxCol].Merge = true;
                    worksheet.Cells[currentRow, 1].Value = $"Report generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    worksheet.Cells[currentRow, 1].Style.Font.Italic = true;
                    worksheet.Cells[currentRow, 1].Style.Font.Size = 9;
                    worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                    // Return the Excel file
                    string excelFileName = $"PurchaseReport_{fileName}_{DateTime.Now:yyyyMMdd}.xlsx";
                    byte[] fileBytes = package.GetAsByteArray();
                    return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelFileName);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error exporting report to Excel: " + ex.Message;
                return RedirectToAction("PurchaseReport");
            }
        }

        [HttpGet]
        public ActionResult ExportPurchaseReportToPDF(int vendorId, DateTime? fromDate, DateTime? toDate, string status, string reportType)
        {
            try
            {
                // Set vendor name
                string vendorName = "All Vendors";
                string fileName = "All_Vendors";

                if (vendorId > 0)
                {
                    var vendor = _db.Vendors.Find(vendorId);
                    if (vendor == null)
                    {
                        TempData["ErrorMessage"] = "Vendor not found.";
                        return RedirectToAction("PurchaseReport");
                    }
                    vendorName = vendor.Name;
                    fileName = vendor.Name.Replace(" ", "_");
                }

                // Set ViewBag data
                ViewBag.VendorName = vendorName;
                ViewBag.VendorId = vendorId;
                ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
                ViewBag.Status = status;
                ViewBag.ReportType = reportType ?? "summary";
                ViewBag.GeneratedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                ViewBag.IsPdfExport = true;

                // Get report data
                var reportData = GetPurchaseReportData(vendorId, fromDate, toDate, status, reportType);

                // Generate PDF
                var pdfResult = new Rotativa.ViewAsPdf("PurchaseReportDisplay", reportData)
                {
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Landscape,
                    FileName = $"Purchase_Report_{fileName}_{DateTime.Now:yyyyMMdd}.pdf"
                };

                return pdfResult;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating PDF: {ex.Message}";
                return RedirectToAction("PurchaseReport");
            }
        }

        

        private List<PurchaseReportViewModel> GetPurchaseReportData(int vendorId, DateTime? fromDate, DateTime? toDate, string status, string reportType)
        {
            // Start with all purchases
            var query = _db.Purchases.AsQueryable();

            // Apply filters
            if (vendorId > 0)
            {
                query = query.Where(p => p.VendorId == vendorId);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(p => p.PurchaseDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PurchaseDate <= toDate.Value);
            }

            if (!string.IsNullOrEmpty(status) && status != "0")
            {
                string statusFilter = status;
                query = query.Where(p => p.Status == statusFilter);
            }

            // Get purchase data
            var purchaseData = query
                .Select(p => new PurchaseReportViewModel
                {
                    PurchaseId = p.PurchaseId,
                    PurchaseNo = p.PurchaseNo,
                    Date = p.PurchaseDate,
                    VendorId = p.VendorId,
                    VendorName = p.Vendor.Name,
                    Reference = p.ReferenceNo,
                    Status = p.Status,
                    TotalAmount = p.PurchaseDetails
                            .Sum(pd => (decimal?)pd.Quantity * pd.UnitPrice) ?? 0
                })
                .OrderByDescending(p => p.Date)
                .ToList();

            // If we need detailed data, load items
            if (reportType == "detail")
            {
                foreach (var purchase in purchaseData)
                {
                    var items = _db.PurchaseDetails
                        .Where(pd => pd.PurchaseId == purchase.PurchaseId)
                        .Select(pd => new PurchaseItemDetail
                        {
                            ItemId = pd.ItemId,
                            ItemName = pd.Item.ItemName,
                            ItemCode = pd.Item.Sku,
                            Quantity = pd.Quantity,
                            Unit = pd.Item.UnitOfMeasure.Symbol,
                            UnitPrice = pd.UnitPrice,
                            TotalPrice = pd.Quantity * pd.UnitPrice
                        })
                        .ToList();

                    purchase.Items = items;
                }
            }

            return purchaseData;
        }
        #endregion

        #region Purchase Return Report
        public ActionResult PurchaseReturnReport()
        {
            ViewBag.Vendors = new SelectList(_db.Vendors.ToList(), "VendorId", "Name");
            return View();
        }

        [HttpGet]
        public ActionResult GeneratePurchaseReturnReport(int vendorId, DateTime? fromDate, DateTime? toDate, string status, string reportType)
        {
            try
            {
                var reportData = GetPurchaseReturnReportData(vendorId, fromDate, toDate, status, reportType);

                // Set ViewBag data for the report display
                ViewBag.VendorName = vendorId > 0
                    ? _db.Vendors.FirstOrDefault(v => v.VendorId == vendorId)?.Name ?? "All Vendors"
                    : "All Vendors";
                ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
                ViewBag.Status = status;
                ViewBag.ReportType = reportType;
                ViewBag.GeneratedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                ViewBag.VendorId = vendorId;

                return View("PurchaseReturnReportDisplay", reportData);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error generating report: " + ex.Message;
                return RedirectToAction("PurchaseReturnReport");
            }
        }

        [HttpGet]
        public ActionResult ExportPurchaseReturnReportToExcel(int vendorId, DateTime? fromDate, DateTime? toDate, string status, string reportType)
        {
            try
            {
                // Get report data
                var reportData = GetPurchaseReturnReportData(vendorId, fromDate, toDate, status, reportType);

                // Set vendor name
                string vendorName = "All Vendors";
                string fileName = "All_Vendors";

                if (vendorId > 0)
                {
                    var vendor = _db.Vendors.Find(vendorId);
                    if (vendor == null)
                    {
                        TempData["ErrorMessage"] = "Vendor not found.";
                        return RedirectToAction("PurchaseReturnReport");
                    }
                    vendorName = vendor.Name;
                    fileName = vendor.Name.Replace(" ", "_");
                }

                // Create Excel package
                ExcelPackage.License.SetNonCommercialOrganization("Ressential");
                using (var package = new ExcelPackage())
                {
                    // Add a worksheet
                    var worksheet = package.Workbook.Worksheets.Add("Purchase Return Report");

                    // Set document properties
                    package.Workbook.Properties.Title = "Purchase Return Report";
                    package.Workbook.Properties.Author = "Ressential";
                    package.Workbook.Properties.Company = "Ressential";
                    package.Workbook.Properties.Created = DateTime.Now;

                    // Professional color scheme
                    var headerBackground = System.Drawing.Color.FromArgb(31, 78, 120);   // Dark blue
                    var titleBackground = System.Drawing.Color.FromArgb(55, 125, 185);   // Medium blue
                    var alternateRowColor = System.Drawing.Color.FromArgb(240, 244, 248); // Very light blue
                    var totalRowColor = System.Drawing.Color.FromArgb(217, 225, 242);    // Light blue-gray
                    var borderColor = System.Drawing.Color.FromArgb(180, 199, 231);      // Light blue for borders

                    // Set column widths based on report type
                    if (reportType == "summary")
                    {
                        // Summary report columns
                        worksheet.Column(1).Width = 6;      // S.No
                        worksheet.Column(2).Width = 18;     // Return No
                        worksheet.Column(3).Width = 12;     // Date
                        worksheet.Column(4).Width = 25;     // Vendor Name
                        worksheet.Column(5).Width = 18;     // Reference
                        worksheet.Column(6).Width = 18;     // Total Amount
                        worksheet.Column(7).Width = 15;     // Status
                    }
                    else
                    {
                        // Detailed report columns
                        worksheet.Column(1).Width = 6;      // S.No
                        worksheet.Column(2).Width = 18;     // Return No
                        worksheet.Column(3).Width = 12;     // Date
                        worksheet.Column(4).Width = 25;     // Vendor Name
                        worksheet.Column(5).Width = 25;     // Item Name
                        worksheet.Column(6).Width = 15;     // Quantity
                        worksheet.Column(7).Width = 15;     // Unit Price
                        worksheet.Column(8).Width = 15;     // Total
                        worksheet.Column(9).Width = 15;     // Status
                    }

                    // Max columns for merging - depends on report type
                    int maxCol = reportType == "summary" ? 7 : 9;

                    // Add company logo placeholder and title (row 1)
                    worksheet.Cells[1, 1, 1, maxCol].Merge = true;
                    worksheet.Cells[1, 1].Value = "RESSENTIAL";
                    worksheet.Cells[1, 1].Style.Font.Size = 20;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[1, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    worksheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(headerBackground);
                    worksheet.Row(1).Height = 30; // Increase row height

                    // Add report title (row 2)
                    worksheet.Cells[2, 1, 2, maxCol].Merge = true;
                    worksheet.Cells[2, 1].Value = reportType == "summary" ? "Purchase Return Report (Summary)" : "Purchase Return Report (Detailed)";
                    worksheet.Cells[2, 1].Style.Font.Size = 16;
                    worksheet.Cells[2, 1].Style.Font.Bold = true;
                    worksheet.Cells[2, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[2, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[2, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    worksheet.Cells[2, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[2, 1].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Row(2).Height = 25; // Increase row height

                    // Empty row for spacing
                    worksheet.Row(3).Height = 10;

                    // Calculate box widths based on report type
                    int box1End = reportType == "summary" ? 2 : 3;
                    int box2Start = box1End + 1;
                    int box2End = reportType == "summary" ? 5 : 5;
                    int box3Start = box2End + 1;

                    // Report information section with styled boxes (rows 4-5)
                    // Vendor info (left box)
                    worksheet.Cells[4, 1, 4, box1End].Merge = true;
                    worksheet.Cells[4, 1].Value = "VENDOR";
                    worksheet.Cells[4, 1].Style.Font.Bold = true;
                    worksheet.Cells[4, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, 1].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                    worksheet.Cells[5, 1, 5, box1End].Merge = true;
                    worksheet.Cells[5, 1].Value = vendorName;
                    worksheet.Cells[5, 1].Style.Font.Size = 11;
                    worksheet.Cells[5, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, 1].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, 1].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;

                    // Date range (middle box)
                    worksheet.Cells[4, box2Start, 4, box2End].Merge = true;
                    worksheet.Cells[4, box2Start].Value = "DATE RANGE";
                    worksheet.Cells[4, box2Start].Style.Font.Bold = true;
                    worksheet.Cells[4, box2Start].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, box2Start].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, box2Start].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, box2Start].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                    worksheet.Cells[5, box2Start, 5, box2End].Merge = true;
                    worksheet.Cells[5, box2Start].Value = $"{(fromDate?.ToString("yyyy-MM-dd") ?? "All")} to {(toDate?.ToString("yyyy-MM-dd") ?? "All")}";
                    worksheet.Cells[5, box2Start].Style.Font.Size = 11;
                    worksheet.Cells[5, box2Start].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, box2Start].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, box2Start].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;

                    // Status info (right box)
                    worksheet.Cells[4, box3Start, 4, maxCol].Merge = true;
                    worksheet.Cells[4, box3Start].Value = "STATUS";
                    worksheet.Cells[4, box3Start].Style.Font.Bold = true;
                    worksheet.Cells[4, box3Start].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, box3Start].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, box3Start].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, box3Start].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                    worksheet.Cells[5, box3Start, 5, maxCol].Merge = true;
                    worksheet.Cells[5, box3Start].Value = status == "0" ? "All Status" : status;
                    worksheet.Cells[5, box3Start].Style.Font.Size = 11;
                    worksheet.Cells[5, box3Start].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, box3Start].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, box3Start].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;

                    // Space before data
                    worksheet.Row(6).Height = 15;

                    // Add table headers (row 7) based on report type
                    string[] headers;
                    if (reportType == "summary")
                    {
                        headers = new string[] { "S.No", "Return No", "Date", "Vendor", "Reference", "Total Amount", "Status" };
                    }
                    else
                    {
                        headers = new string[] {
                            "S.No", "Return No", "Date", "Vendor", "Item", "Quantity",
                            "Unit Price", "Total", "Status"
                        };
                    }

                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cells[7, i + 1];
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                        cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(headerBackground);
                        cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        cell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                        cell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                    }
                    worksheet.Row(7).Height = 22; // Increase header row height

                    // Freeze panes for better navigation (fix header row)
                    worksheet.View.FreezePanes(8, 1);

                    // Add data rows based on report type
                    int currentRow = 8;
                    int serialNo = 1;

                    if (reportType == "summary")
                    {
                        // Summary report - one row per purchase return
                        decimal totalAmount = 0;

                        if (reportData.Any())
                        {
                            foreach (var purchaseReturn in reportData)
                            {
                                // Apply alternating row background for better readability
                                if (serialNo % 2 == 0)
                                {
                                    var range = worksheet.Cells[currentRow, 1, currentRow, maxCol];
                                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                                    range.Style.Fill.BackgroundColor.SetColor(alternateRowColor);
                                }

                                // Add row data - summary format
                                worksheet.Cells[currentRow, 1].Value = serialNo++;
                                worksheet.Cells[currentRow, 2].Value = purchaseReturn.PurchaseReturnNo;
                                worksheet.Cells[currentRow, 3].Value = purchaseReturn.Date.ToString("yyyy-MM-dd");
                                worksheet.Cells[currentRow, 4].Value = purchaseReturn.VendorName;
                                worksheet.Cells[currentRow, 5].Value = purchaseReturn.Reference;
                                worksheet.Cells[currentRow, 6].Value = purchaseReturn.TotalAmount;
                                worksheet.Cells[currentRow, 7].Value = purchaseReturn.Status;

                                // Format cells
                                for (int i = 1; i <= maxCol; i++)
                                {
                                    worksheet.Cells[currentRow, i].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                                }

                                // Date format
                                worksheet.Cells[currentRow, 3].Style.Numberformat.Format = "yyyy-mm-dd";

                                // Numeric format for total amount
                                worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0.00";

                                totalAmount += purchaseReturn.TotalAmount;
                                currentRow++;
                            }

                            // Add summary row
                            // Total row style
                            var totalRange = worksheet.Cells[currentRow, 1, currentRow, maxCol];
                            totalRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            totalRange.Style.Fill.BackgroundColor.SetColor(totalRowColor);
                            totalRange.Style.Font.Bold = true;
                            totalRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);

                            // Total row content
                            worksheet.Cells[currentRow, 1, currentRow, 5].Merge = true;
                            worksheet.Cells[currentRow, 1].Value = "Total Amount:";
                            worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                            worksheet.Cells[currentRow, 6].Value = totalAmount;
                            worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0.00";

                            currentRow++;

                            // Count row style
                            var countRange = worksheet.Cells[currentRow, 1, currentRow, maxCol];
                            countRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            countRange.Style.Fill.BackgroundColor.SetColor(totalRowColor);
                            countRange.Style.Font.Bold = true;
                            countRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);

                            // Count row content
                            worksheet.Cells[currentRow, 1, currentRow, 5].Merge = true;
                            worksheet.Cells[currentRow, 1].Value = "Total Returns:";
                            worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                            worksheet.Cells[currentRow, 6].Value = reportData.Count();
                        }
                        else
                        {
                            // No data row
                            worksheet.Cells[currentRow, 1, currentRow, maxCol].Merge = true;
                            worksheet.Cells[currentRow, 1].Value = "No data available";
                            worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            worksheet.Cells[currentRow, 1].Style.Font.Italic = true;
                            worksheet.Cells[currentRow, 1].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                        }
                    }
                    else
                    {
                        // Detailed report - one row per purchase return item
                        decimal totalQuantity = 0;
                        decimal totalValue = 0;

                        if (reportData.Any())
                        {
                            foreach (var purchaseReturn in reportData)
                            {
                                if (purchaseReturn.Items != null && purchaseReturn.Items.Any())
                                {
                                    foreach (var item in purchaseReturn.Items)
                                    {
                                        decimal itemTotal = item.Quantity * item.UnitPrice;

                                        // Apply alternating row background for better readability
                                        if (serialNo % 2 == 0)
                                        {
                                            var range = worksheet.Cells[currentRow, 1, currentRow, maxCol];
                                            range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                                            range.Style.Fill.BackgroundColor.SetColor(alternateRowColor);
                                        }

                                        // Add row data - detailed format
                                        worksheet.Cells[currentRow, 1].Value = serialNo++;
                                        worksheet.Cells[currentRow, 2].Value = purchaseReturn.PurchaseReturnNo;
                                        worksheet.Cells[currentRow, 3].Value = purchaseReturn.Date.ToString("yyyy-MM-dd");
                                        worksheet.Cells[currentRow, 4].Value = purchaseReturn.VendorName;
                                        worksheet.Cells[currentRow, 5].Value = item.ItemName;
                                        worksheet.Cells[currentRow, 6].Value = item.Quantity;
                                        worksheet.Cells[currentRow, 7].Value = item.UnitPrice;
                                        worksheet.Cells[currentRow, 8].Value = itemTotal;
                                        worksheet.Cells[currentRow, 9].Value = purchaseReturn.Status;

                                        // Format cells
                                        for (int i = 1; i <= maxCol; i++)
                                        {
                                            worksheet.Cells[currentRow, i].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                                        }

                                        // Date format
                                        worksheet.Cells[currentRow, 3].Style.Numberformat.Format = "yyyy-mm-dd";

                                        // Numeric formats
                                        worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0.00";
                                        worksheet.Cells[currentRow, 7].Style.Numberformat.Format = "#,##0.00";
                                        worksheet.Cells[currentRow, 8].Style.Numberformat.Format = "#,##0.00";

                                        totalQuantity += item.Quantity;
                                        totalValue += itemTotal;
                                        currentRow++;
                                    }
                                }
                            }

                            // Add summary row
                            // Total row style
                            var totalRange = worksheet.Cells[currentRow, 1, currentRow, maxCol];
                            totalRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            totalRange.Style.Fill.BackgroundColor.SetColor(totalRowColor);
                            totalRange.Style.Font.Bold = true;
                            totalRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);

                            // Total row content
                            worksheet.Cells[currentRow, 1, currentRow, 5].Merge = true;
                            worksheet.Cells[currentRow, 1].Value = "Grand Total:";
                            worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                            worksheet.Cells[currentRow, 6].Value = totalQuantity;
                            worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0.00";
                            worksheet.Cells[currentRow, 8].Value = totalValue;
                            worksheet.Cells[currentRow, 8].Style.Numberformat.Format = "#,##0.00";
                        }
                        else
                        {
                            // No data row
                            worksheet.Cells[currentRow, 1, currentRow, maxCol].Merge = true;
                            worksheet.Cells[currentRow, 1].Value = "No data available";
                            worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            worksheet.Cells[currentRow, 1].Style.Font.Italic = true;
                            worksheet.Cells[currentRow, 1].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                        }
                    }

                    // Add footer note (optional)
                    currentRow += 2;
                    worksheet.Cells[currentRow, 1, currentRow, maxCol].Merge = true;
                    worksheet.Cells[currentRow, 1].Value = $"Report generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    worksheet.Cells[currentRow, 1].Style.Font.Italic = true;
                    worksheet.Cells[currentRow, 1].Style.Font.Size = 9;
                    worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                    // Return the Excel file
                    string excelFileName = $"PurchaseReturnReport_{fileName}_{DateTime.Now:yyyyMMdd}.xlsx";
                    byte[] fileBytes = package.GetAsByteArray();
                    return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelFileName);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error exporting report to Excel: " + ex.Message;
                return RedirectToAction("PurchaseReturnReport");
            }
        }

        [HttpGet]
        public ActionResult ExportPurchaseReturnReportToPDF(int vendorId, DateTime? fromDate, DateTime? toDate, string status, string reportType)
        {
            try
            {
                // Set vendor name
                string vendorName = "All Vendors";
                string fileName = "All_Vendors";

                if (vendorId > 0)
                {
                    var vendor = _db.Vendors.Find(vendorId);
                    if (vendor == null)
                    {
                        TempData["ErrorMessage"] = "Vendor not found.";
                        return RedirectToAction("PurchaseReturnReport");
                    }
                    vendorName = vendor.Name;
                    fileName = vendor.Name.Replace(" ", "_");
                }

                // Set ViewBag data
                ViewBag.VendorName = vendorName;
                ViewBag.VendorId = vendorId;
                ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
                ViewBag.Status = status;
                ViewBag.ReportType = reportType ?? "summary";
                ViewBag.GeneratedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                ViewBag.IsPdfExport = true;

                // Get report data
                var reportData = GetPurchaseReturnReportData(vendorId, fromDate, toDate, status, reportType);

                // Generate PDF
                var pdfResult = new Rotativa.ViewAsPdf("PurchaseReturnReportDisplay", reportData)
                {
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Landscape,
                    FileName = $"Purchase_Return_Report_{fileName}_{DateTime.Now:yyyyMMdd}.pdf"
                };

                return pdfResult;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating PDF: {ex.Message}";
                return RedirectToAction("PurchaseReturnReport");
            }
        }


        private List<PurchaseReturnReportViewModel> GetPurchaseReturnReportData(int vendorId, DateTime? fromDate, DateTime? toDate, string status, string reportType)
        {
            // Start with all purchase returns
            var query = _db.PurchaseReturns.AsQueryable();

            // Apply filters
            if (vendorId > 0)
            {
                query = query.Where(p => p.VendorID == vendorId);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(p => p.PurchaseReturnDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PurchaseReturnDate <= toDate.Value);
            }

            if (!string.IsNullOrEmpty(status) && status != "0")
            {
                string statusFilter = status;
                query = query.Where(p => p.Status == statusFilter);
            }

            // Get purchase return data
            var purchaseReturnData = query
                .Select(p => new PurchaseReturnReportViewModel
                {
                    PurchaseReturnId = p.PurchaseReturnId,
                    PurchaseReturnNo = p.PurchaseReturnNo,
                    Date = p.PurchaseReturnDate,
                    VendorId = p.VendorID,
                    VendorName = p.Vendor.Name,
                    Reference = p.ReferenceNo,
                    Status = p.Status,
                    TotalAmount = p.PurchaseReturnDetails
                            .Sum(pd => (decimal?)pd.Quantity * pd.UnitPrice) ?? 0
                })
                .OrderByDescending(p => p.Date)
                .ToList();

            // If we need detailed data, load items
            if (reportType == "detail")
            {
                foreach (var purchaseReturn in purchaseReturnData)
                {
                    var items = _db.PurchaseReturnDetails
                        .Where(pd => pd.PurchaseReturnId == purchaseReturn.PurchaseReturnId)
                        .Select(pd => new PurchaseReturnItemDetail
                        {
                            ItemId = pd.ItemId,
                            ItemName = pd.Item.ItemName,
                            ItemCode = pd.Item.Sku,
                            Quantity = pd.Quantity,
                            Unit = pd.Item.UnitOfMeasure.Symbol,
                            UnitPrice = pd.UnitPrice,
                            TotalPrice = pd.Quantity * pd.UnitPrice
                        })
                        .ToList();

                    purchaseReturn.Items = items;
                }
            }

            return purchaseReturnData;
        }
        #endregion

        // Update the places where CheckStockLevel is called to send notifications via SignalR
        private void SendStockLevelNotifications(List<Notification> notifications)
        {
            if (notifications != null && notifications.Any())
            {
                _db.Notifications.AddRange(notifications);
                _db.SaveChanges();
                
                // Send real-time notifications via SignalR
                var hubContext = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<Hub.RessentialHub>();
                foreach (var notification in notifications)
                {
                    hubContext.Clients.Client(notification.User?.ConnectionId)
                        .receiveStockAlert(notification.Title, notification.Message, notification.NotificationId, notification.RedirectUrl);
                }
            }
        }
    }
}
