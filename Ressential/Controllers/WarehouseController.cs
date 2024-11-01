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

namespace Ressential.Controllers
{
    public class WarehouseController : Controller
    {
        DB_RessentialEntities _db = new DB_RessentialEntities();

        public ActionResult Index()
        {
            string userName = Helper.GetUserInfo("userName");
            return View();
        }
        public ActionResult CreateItemCategory()
        {
            return View();
        }
        [HttpPost]
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
        public ActionResult ItemCategoryList(string search)
        {
            var itemCategories = _db.ItemCategories.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                itemCategories = itemCategories.Where(c => c.ItemCategoryName.Contains(search) || c.Description.Contains(search));
            }
            return View(itemCategories.ToList());
        }
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
        public ActionResult EditItemCategory(ItemCategory itemCategory)
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
        public ActionResult CreateItem()
        {
            ViewBag.Units = _db.UnitOfMeasures.ToList();
            ViewBag.Categories = _db.ItemCategories.ToList();
            return View();
        }
        [HttpPost]
        public ActionResult CreateItem(Item item, decimal quantity, decimal cost)
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
                        Quantity = quantity,
                        Cost = cost
                    };
                    _db.WarehouseItemStocks.Add(warehouseItemStock);
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
        public ActionResult ItemList(string search)
        {
            var items = _db.Items.AsQueryable();
            
            if (!string.IsNullOrEmpty(search))
            {
                items = items.Where(c => c.ItemName.Contains(search) || c.Sku.Contains(search));
            }
            return View(items.ToList());
        }
        public ActionResult EditItem(int itemId)
        {
            var item = _db.Items.Find(itemId);
            if (item == null)
            {
                return HttpNotFound();
            }
            var warehouseItemStock = _db.WarehouseItemStocks.Find(itemId);
            if (warehouseItemStock == null)
            {
                return HttpNotFound();
            }
            ViewBag.Units = _db.UnitOfMeasures.ToList();
            ViewBag.Categories = _db.ItemCategories.ToList();
            ViewBag.Quantity = warehouseItemStock.Quantity;
            ViewBag.Cost = warehouseItemStock.Cost;

            return View(item);
        }
        [HttpPost]
        public ActionResult EditItem(Item item, decimal quantity, decimal cost)
        {
            try
            {
                var existingItem = _db.Items.Find(item.ItemId);
                if (existingItem == null)
                {
                    return HttpNotFound();
                }
                item.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                item.ModifiedAt = DateTime.Now;
                _db.Entry(existingItem).CurrentValues.SetValues(item);
                _db.Entry(existingItem).State = EntityState.Modified;
                _db.Entry(existingItem).Property(x => x.CreatedBy).IsModified = false;
                _db.Entry(existingItem).Property(x => x.CreatedAt).IsModified = false;
                var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);
                warehouseItemStock.Quantity = quantity;
                warehouseItemStock.Cost = cost;
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Item updated successfully.";
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while updating the Item.";
            }
            return RedirectToAction("ItemList");
        }
        [HttpPost]
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
        public ActionResult DeleteSelectedItems(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var itemsToDelete = _db.Items.Where(c => selectedItems.Contains(c.ItemId)).ToList();
                    var warehouseItemStock = _db.WarehouseItemStocks.Where(c => selectedItems.Contains(c.ItemId)).ToList();
                    _db.WarehouseItemStocks.RemoveRange(warehouseItemStock);
                    _db.Items.RemoveRange(itemsToDelete);
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
        public ActionResult CreateVendor()
        {
            return View();
        }
        [HttpPost]
        public ActionResult CreateVendor(Vendor vendor)
        {
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
        public ActionResult VendorList(string search)
        {

            var Vendors = _db.Vendors.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                Vendors = Vendors.Where(c => c.Name.Contains(search) || c.CompanyName.Contains(search) || c.Email.Contains(search));
            }
            return View(Vendors.ToList());
        }
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
        public ActionResult CreateBranch()
        {
            return View();
        }
        public ActionResult BranchList()
        {
            return View();
        }
        public ActionResult CreatePurchase()
        {
            return View();
        }
        public ActionResult CreatePurchaseReturn()
        {
            return View();
        }
        public ActionResult PurchaseList()
        {
            return View();
        }
      
        public ActionResult PurchaseReturnList()
        {
            return View();
        }
        public ActionResult CreateIssue()
        {
            return View();
        }
        public ActionResult IssueList()
        {
            return View();
        }
        public ActionResult CreateBankAndCash()
        {
            return View();
        }
        public ActionResult BankAndCashList()
        {
            return View();
        }
        public ActionResult CreatePaymentVouncher()
        {
            return View();
        }
        public ActionResult PaymentVouncherList()
        {
            return View();
        }
        public ActionResult CreateReceiptVouncher()
        {
            return View();
        }
        public ActionResult ReceiptVouncherList()
        {
            return View();
        }
        public ActionResult RequisitionList()
        {
            return View();
        }
        public ActionResult StockReturnList()
        {
            return View();
        }
        public ActionResult UserList(String search)
        {
            var users = _db.Users.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                users = users.Where(c => c.UserName.Contains(search));
            }
            return View(users.ToList());
        }
        public ActionResult CreateUser()
        {
            return View();
        }

        [HttpPost]
        public ActionResult CreateUser(User user, String ConfirmPassword)
        {
            if (user.Password != ConfirmPassword)
            {
                ModelState.AddModelError("", "Password and Confirm Password do not match.");
                return View(user);
            }
            _db.Users.Add(user);
            _db.SaveChanges();
            return RedirectToAction("UserList");
        }
        public ActionResult EditUser(int userId)
        {
            var User = _db.Users.Find(userId);
            if (User == null)
            {
                return HttpNotFound();
            }
            return View(User);
        }
        [HttpPost]
        public ActionResult EditUser(User user)
        {
            var existingUser = _db.Users.Find(user.UserId);
            if (existingUser == null)
            {
                return HttpNotFound();
            }
            user.Email = existingUser.Email;
            _db.Entry(existingUser).CurrentValues.SetValues(user);
            _db.Entry(existingUser).State = EntityState.Modified;
            _db.SaveChanges();
            return RedirectToAction("UserList");
        }
        [HttpPost]
        public ActionResult DeleteUser(int userId)
        {
            var user = _db.Users.Find(userId);
            _db.Users.Remove(user);
            _db.SaveChanges();
            return RedirectToAction("UserList");
        }
        [HttpPost]
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
    }
}