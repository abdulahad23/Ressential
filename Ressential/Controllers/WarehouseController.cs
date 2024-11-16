﻿using System;
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

namespace Ressential.Controllers
{
    [Authorize]
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
        public ActionResult CreateItem(Item item, decimal quantity, decimal cost, DateTime openingDate)
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
                        CostPerUnit = cost/quantity
                    };
                    var warehouseItemTransaction = new WarehouseItemTransaction
                    {
                        TransactionDate = openingDate,
                        ItemId = item.ItemId,
                        TransactionType = "Opening",
                        TransactionTypeId = item.ItemId,
                        Quantity = quantity,
                        CostPerUnit = cost/quantity
                    };
                    _db.WarehouseItemStocks.Add(warehouseItemStock);
                    _db.WarehouseItemTransactions.Add(warehouseItemTransaction);
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
            var warehouseItemTransaction = _db.WarehouseItemTransactions.Where(w => w.TransactionType == "Opening" && w.TransactionTypeId == itemId).Single();
            if (warehouseItemTransaction == null)
            {
                return HttpNotFound();
            }
            ViewBag.Units = _db.UnitOfMeasures.ToList();
            ViewBag.Categories = _db.ItemCategories.ToList();
            ViewBag.Quantity = warehouseItemTransaction.Quantity;
            ViewBag.Cost = warehouseItemTransaction.CostPerUnit * warehouseItemTransaction.Quantity;
            ViewBag.Date = warehouseItemTransaction.TransactionDate;

            return View(item);
        }
        [HttpPost]
        public ActionResult EditItem(Item item, decimal quantity, decimal cost, DateTime openingDate)
        {
            cost = cost / quantity;
            try
            {
                var existingItem = _db.Items.Find(item.ItemId);
                if (existingItem == null)
                {
                    return HttpNotFound();
                }
                var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);
                var warehouseItemTransaction = _db.WarehouseItemTransactions.Where(w => w.TransactionType == "Opening" && w.TransactionTypeId == item.ItemId).Single();
                
                decimal oldTotalCost = warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit;
                decimal oldTransactionCost = warehouseItemTransaction.Quantity * warehouseItemTransaction.CostPerUnit;

                decimal newTotalCost = oldTotalCost - oldTransactionCost;
                decimal newQuantity = warehouseItemStock.Quantity - warehouseItemTransaction.Quantity;

                decimal previousAverageCost = newTotalCost / (newQuantity==0? 1: newQuantity);

                decimal updatedQuantity = newQuantity + quantity;

                warehouseItemStock.Quantity = updatedQuantity;
                warehouseItemStock.CostPerUnit = ((newQuantity * previousAverageCost) + (quantity * cost))/updatedQuantity;

                warehouseItemTransaction.Quantity = quantity;
                warehouseItemTransaction.CostPerUnit = cost;
                warehouseItemTransaction.TransactionDate = openingDate;

                item.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                item.ModifiedAt = DateTime.Now;
                _db.Entry(existingItem).CurrentValues.SetValues(item);
                _db.Entry(existingItem).State = EntityState.Modified;
                _db.Entry(existingItem).Property(x => x.CreatedBy).IsModified = false;
                _db.Entry(existingItem).Property(x => x.CreatedAt).IsModified = false;
                
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
                var warehouseItemTransaction = _db.WarehouseItemTransactions.Where(w => w.TransactionType != "Opening" && w.TransactionTypeId == itemId);

                if (warehouseItemTransaction.Count()==0) {
                    _db.WarehouseItemStocks.Remove(warehouseItemStock);
                    var itemTransactionToDelete = _db.WarehouseItemTransactions.Where(w => w.TransactionType == "Opening" && w.TransactionTypeId == itemId);
                    _db.WarehouseItemTransactions.RemoveRange(itemTransactionToDelete);
                    _db.Items.Remove(item);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Item deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "This Item is already in use and cannot be deleted.";
                }
                
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

                    foreach (var item in itemsToDelete)
                    {
                        var warehouseItemStock = _db.WarehouseItemStocks.Where(w => w.ItemId == item.ItemId);
                        var warehouseItemTransaction = _db.WarehouseItemTransactions.Where(w => w.TransactionType != "Opening" && w.TransactionTypeId == item.ItemId);

                        if (warehouseItemTransaction.Count() == 0)
                        {
                            _db.WarehouseItemStocks.RemoveRange(warehouseItemStock);
                            var itemTransactionToDelete = _db.WarehouseItemTransactions.Where(w => w.TransactionType == "Opening" && w.TransactionTypeId == item.ItemId);
                            _db.WarehouseItemTransactions.RemoveRange(itemTransactionToDelete);
                            _db.Items.Remove(item);
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "An Item is already in use and cannot be deleted.";
                            return RedirectToAction("ItemList");
                        }
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

        [HttpPost]
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
        public ActionResult BranchList(string search)
        {
            var Branch = _db.Branches.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                Branch = Branch.Where(c => c.BranchName.Contains(search) || c.OwnerName.Contains(search));
            }
            return View(Branch.ToList());
        }
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

        public ActionResult CreateBankAndCash()
        {
            return View();
        }
        [HttpPost]
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
        public ActionResult BankAndCashList(string search)
        {
            var Account = _db.Accounts.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                Account = Account.Where(c => c.AccountTitle.Contains(search) || c.AccountType.Contains(search) || c.AccountNumber.Contains(search) || c.BankName.Contains(search));
            }
            return View(Account.ToList());
        }
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
        public ActionResult EditBankAndCash(Account account)
        {
            try
            {
                var existingAccount = _db.Accounts.Find(account.AccountId);
                if (existingAccount == null)
                {
                    return HttpNotFound();
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

        public ActionResult CreatePaymentVoucher()
        {
            ViewBag.Vendors = _db.Vendors.ToList();
            ViewBag.Accounts = _db.Accounts.ToList();
            return View();
        }
        [HttpPost]
        public ActionResult CreatePaymentVoucher(PaymentVoucher model, IEnumerable<HttpPostedFileBase> files)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    model.PaymentVoucherNo = GenerateVoucherNumber();
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

        private string GenerateVoucherNumber()
        {
            return "PV-" + DateTime.Now.Ticks;
        }

        public ActionResult PaymentVoucherList(string search)
        {

            var PaymentVoucher = _db.PaymentVouchers.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                PaymentVoucher = PaymentVoucher.Where(c => c.PaymentVoucherNo.Contains(search) || c.InstrumentNo.Contains(search));
            }
            return View(PaymentVoucher.ToList());
        }
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
        public ActionResult EditPaymentVoucher(PaymentVoucher paymentVoucher, IEnumerable<HttpPostedFileBase> files)
        {
            try
            {
                var existingPaymentVoucher = _db.PaymentVouchers.Find(paymentVoucher.PaymentVoucherId);

                if (existingPaymentVoucher == null)
                {
                    return HttpNotFound(); // Return 404 if the voucher is not found
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


        public ActionResult CreateReceiptVoucher()
        {
            ViewBag.Vendors = _db.Vendors.ToList();
            ViewBag.Accounts = _db.Accounts.ToList();
            return View();

        }
        [HttpPost]
        public ActionResult CreateReceiptVoucher(ReceiptVoucher model, IEnumerable<HttpPostedFileBase> files)
        {

            if (ModelState.IsValid)
            {
                try
                {
                    model.ReceiptVoucherNo = GenerateReceiptVoucherNumber();
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
        public ActionResult EditReceiptVoucher(ReceiptVoucher ReceiptVoucher, IEnumerable<HttpPostedFileBase> files)
        {
            try
            {
                var existingReceiptVoucher = _db.ReceiptVouchers.Find(ReceiptVoucher.ReceiptVoucherId);

                if (existingReceiptVoucher == null)
                {
                    return HttpNotFound(); // Return 404 if the voucher is not found
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
        public ActionResult CreatePurchase()
        {

            var purchase = new Purchase
            {
                PurchaseDetails = new List<PurchaseDetail>
                {
                    new PurchaseDetail()
                }

            };
            ViewBag.Items = _db.Items.ToList();
            ViewBag.Vendors = _db.Vendors.ToList();

            return View(purchase);
        }
        [HttpPost]
        public ActionResult CreatePurchase(Purchase purchase)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    string datePart = DateTime.Now.ToString("yyyyMMdd");
                    int nextPurchaseNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.Purchases.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextPurchaseNumber = _db.Purchases
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => Convert.ToInt32(p.PurchaseNo.Substring(11)))  // Now we can safely use Convert.ToInt32
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

                        var warehouseItemTransaction = new WarehouseItemTransaction
                        {
                            TransactionDate = purchase.PurchaseDate,
                            ItemId = purchaseDetails.ItemId,
                            TransactionType = "Purchase",
                            TransactionTypeId = purchase.PurchaseId,
                            Quantity = purchaseDetails.Quantity,
                            CostPerUnit = purchaseDetails.UnitPrice
                        };
                        _db.WarehouseItemStocks.AddOrUpdate(currentItemStock);
                        _db.WarehouseItemTransactions.Add(warehouseItemTransaction);
                    }
                    _db.SaveChanges();

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
                 TotalAmount = p.PurchaseDetails.Sum(pd => pd.Quantity * pd.UnitPrice)
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
                 TotalAmount = p.PurchaseDetails.Sum(pd => pd.Quantity * pd.UnitPrice)
                    }).ToList();
            return View(purchaseList2);
        }
        public ActionResult EditPurchase(int purchaseId)
        {
            Purchase purchase = _db.Purchases.Find(purchaseId);
            ViewBag.Items = _db.Items.ToList();
            ViewBag.Vendors = _db.Vendors.ToList();
            return View(purchase);
        }
        [HttpPost]
        public ActionResult EditPurchase(Purchase purchase)
        {
            if (purchase == null)
            {
                return Json("1", JsonRequestBehavior.AllowGet);
            }

            using (var transaction = _db.Database.BeginTransaction())
            {
                try
                {
                    var existingPurchase = _db.Purchases.Find(purchase.PurchaseId);
                    if (existingPurchase == null)
                    {
                        return Json("1", JsonRequestBehavior.AllowGet);
                    }

                    // Update purchase metadata
                    purchase.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    purchase.ModifiedAt = DateTime.Now;

                    // Update existing purchase values
                    _db.Entry(existingPurchase).CurrentValues.SetValues(purchase);
                    _db.Entry(existingPurchase).Property(x => x.CreatedBy).IsModified = false;
                    _db.Entry(existingPurchase).Property(x => x.CreatedAt).IsModified = false;

                    // Remove existing purchase details
                    var purchaseDetails = _db.PurchaseDetails.Where(x => x.PurchaseId == purchase.PurchaseId).ToList();
                    _db.PurchaseDetails.RemoveRange(purchaseDetails);

                    // Add new purchase details
                    _db.PurchaseDetails.AddRange(purchase.PurchaseDetails);

                    // Save changes
                    _db.SaveChanges();
                    transaction.Commit();

                    TempData["SuccessMessage"] = "Item updated successfully.";
                    return Json("0", JsonRequestBehavior.AllowGet);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["ErrorMessage"] = "An error occurred while updating the Item.";
                    // Log the exception (ex) for debugging purposes
                    return Json("0", JsonRequestBehavior.AllowGet);
                }
            }
        }
        [HttpPost]
        public ActionResult DeletePurchase(int purchaseId)
        {
            try
            {
                var purchase = _db.Purchases.Find(purchaseId);
                if (purchase == null)
                {
                    TempData["ErrorMessage"] = "Item not found.";
                    return RedirectToAction("ItemList");
                }
                var purchaseDetails = _db.PurchaseDetails.Select(p => p).Where(p => p.PurchaseId == purchaseId);
                _db.PurchaseDetails.RemoveRange(purchaseDetails);
                _db.Purchases.Remove(purchase);
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
            return RedirectToAction("PurchaseList");
        }
        [HttpPost]
        public ActionResult DeleteSelectedPurchases(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var purchasesToDelete = _db.Purchases.Where(c => selectedItems.Contains(c.PurchaseId)).ToList();
                    var purchaseDetails = _db.PurchaseDetails.Where(c => selectedItems.Contains(c.PurchaseId)).ToList();
                    _db.PurchaseDetails.RemoveRange(purchaseDetails);
                    _db.Purchases.RemoveRange(purchasesToDelete);
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
            return RedirectToAction("PurchaseList");
        }
        public ActionResult CreatePurchaseReturn()
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