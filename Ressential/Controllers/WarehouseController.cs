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
        public ActionResult CreateVendor()
        {
            return View();
        }
        [HttpPost]
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
            ViewBag.Items = _db.Items.Where(i => i.IsActive == true).ToList();
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
                            .Select(p => int.Parse(p.PurchaseNo.Substring(13)))  // Now we can safely use Convert.ToInt32
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
            ViewBag.Items = _db.Items.Where(i => i.IsActive == true).ToList();
            ViewBag.Vendors = _db.Vendors.ToList();
            return View(purchase);
        }
        [HttpPost]
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
                            //decimal RevertedAverageCost = RevertedTotalCost / (RevertedQuantity == 0 ? 1 : RevertedQuantity);
                            //decimal RevertedAverageCost = (warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit) - (existingDetail.Quantity * existingDetail.UnitPrice) / (RevertedQuantity == 0 ? 1 : RevertedQuantity);

                            warehouseItemStock.Quantity = RevertedQuantity + newDetail.Quantity; //Updated Quantity
                            if (warehouseItemStock.Quantity == 0)
                            {
                                warehouseItemStock.CostPerUnit = 0;
                            }
                            else
                            {
                                warehouseItemStock.CostPerUnit = ((warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit) - (existingDetail.Quantity * existingDetail.UnitPrice) + (newDetail.Quantity * newDetail.UnitPrice)) / warehouseItemStock.Quantity; //Updated Per Unit Cost
                            }
                            
                            _db.Entry(existingDetail).CurrentValues.SetValues(newDetail);
                            existingDetails.Remove(existingDetail); // Remove from existing list once matched
                        }
                        else
                        {
                            decimal oldQuantity = warehouseItemStock.Quantity;
                            warehouseItemStock.Quantity = warehouseItemStock.Quantity + newDetail.Quantity; //Updated Quantity
                            if (warehouseItemStock.Quantity == 0)
                            {
                                warehouseItemStock.CostPerUnit = 0;
                            }
                            else
                            {
                                warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) + (newDetail.Quantity * newDetail.UnitPrice)) / warehouseItemStock.Quantity; //Updated Per Unit Cost
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
                            decimal oldQuantity = warehouseItemStock.Quantity;
                            warehouseItemStock.Quantity = warehouseItemStock.Quantity - item.Quantity;
                            if (warehouseItemStock.Quantity == 0)
                            {
                                warehouseItemStock.CostPerUnit = 0;
                            }
                            else
                            {
                                warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) - (item.Quantity * item.UnitPrice)) / (warehouseItemStock.Quantity == 0 ? 1 : warehouseItemStock.Quantity);
                            }
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
                        decimal oldQuantity = warehouseItemStock.Quantity;
                        warehouseItemStock.Quantity = warehouseItemStock.Quantity - item.Quantity;
                        if (warehouseItemStock.Quantity == 0)
                        {
                            warehouseItemStock.CostPerUnit = 0;
                        }
                        else
                        {
                            warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) - (item.Quantity * item.UnitPrice)) / warehouseItemStock.Quantity;
                        }
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
        public ActionResult DeleteSelectedPurchases(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var purchasesToDelete = _db.Purchases.Where(c => selectedItems.Contains(c.PurchaseId)).ToList();
                    var purchaseDetails = _db.PurchaseDetails.Where(c => selectedItems.Contains(c.PurchaseId)).ToList();

                    //foreach (var item in purchasesToDelete)
                    //{
                    //    var warehouseItemTransaction = _db.WarehouseItemTransactions.Where(w => w.TransactionType == "Purchase" && w.TransactionTypeId == item.PurchaseId);
                    //    _db.WarehouseItemTransactions.RemoveRange(warehouseItemTransaction);
                    //}

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
                            else
                            {
                                warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) - (item.Quantity * item.UnitPrice)) / warehouseItemStock.Quantity;
                            }
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
        public ActionResult CreatePurchaseReturn(PurchaseReturn purchaseReturn)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    string datePart = DateTime.Now.ToString("yyyyMMdd");
                    int nextPurchaseNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.PurchaseReturns.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextPurchaseNumber = _db.PurchaseReturns
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.PurchaseReturnNo.Substring(13)))  // Now we can safely use Convert.ToInt32
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

                        //var warehouseItemTransaction = new WarehouseItemTransaction
                        //{
                        //    TransactionDate = purchaseReturn.PurchaseReturnDate,
                        //    ItemId = purchaseReturnDetails.ItemId,
                        //    TransactionType = "PurchaseReturn",
                        //    TransactionTypeId = purchaseReturn.PurchaseReturnId,
                        //    Quantity = purchaseReturnDetails.Quantity,
                        //    CostPerUnit = purchaseReturnDetails.UnitPrice
                        //};
                        _db.WarehouseItemStocks.AddOrUpdate(currentItemStock);
                        //_db.WarehouseItemTransactions.Add(warehouseItemTransaction);
                    }
                    _db.SaveChanges();

                    return Json("0", JsonRequestBehavior.AllowGet);
                }
                ViewBag.Vendors = _db.Vendors.Select(v => new { v.VendorId, v.Name }).ToList();
                ViewBag.Items = _db.Items.Select(i => new { i.ItemId, i.ItemName }).ToList();
                return View(purchaseReturn);
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                Console.WriteLine($"Error creating purchase: {ex.Message}");
                return RedirectToAction("Index", "Error");
            }
        }
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
        public ActionResult EditPurchaseReturn(int purchaseReturnId)
        {
            PurchaseReturn purchaseReturn = _db.PurchaseReturns.Find(purchaseReturnId);
            ViewBag.Items = _db.Items.Where(i => i.IsActive == true).ToList();
            ViewBag.Vendors = _db.Vendors.ToList();
            return View(purchaseReturn);
        }
        [HttpPost]
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
                            if (warehouseItemStock.Quantity <= 0)
                            {
                                warehouseItemStock.CostPerUnit = 0;
                            }
                            else
                            {
                                warehouseItemStock.CostPerUnit = ((revertedTotalCost - newTotalCost) / warehouseItemStock.Quantity);
                            }

                            _db.Entry(existingDetail).CurrentValues.SetValues(newDetail);
                            existingDetails.Remove(existingDetail); // Remove from existing list once matched
                        }
                        else
                        {
                            // For new details, update stock directly
                            var totalCostBeforeChange = (warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit);

                            warehouseItemStock.Quantity -= newDetail.Quantity; // Subtract new quantity

                            if (warehouseItemStock.Quantity > 0)
                            {
                                var totalCostAfterChange = totalCostBeforeChange + (newDetail.Quantity * newDetail.UnitPrice);
                                warehouseItemStock.CostPerUnit = totalCostAfterChange / warehouseItemStock.Quantity;
                            }
                            else
                            {
                                warehouseItemStock.CostPerUnit = 0; // No stock left
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
                            decimal oldQuantity = warehouseItemStock.Quantity;
                            warehouseItemStock.Quantity = warehouseItemStock.Quantity + item.Quantity;
                            if (warehouseItemStock.Quantity == 0)
                            {
                                warehouseItemStock.CostPerUnit = 0;
                            }
                            else
                            {
                                warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) + (item.Quantity * item.UnitPrice)) / (warehouseItemStock.Quantity == 0 ? 1 : warehouseItemStock.Quantity);
                            }
                        }
                        _db.PurchaseReturnDetails.RemoveRange(existingDetails);
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
        public ActionResult DeletePurchaseReturn(int purchaseReturnId)
        {
            try
            {
                var purchaseReturn = _db.PurchaseReturns.Find(purchaseReturnId);
                if (purchaseReturn == null)
                {
                    TempData["ErrorMessage"] = "Item not found.";
                    return RedirectToAction("PurchaseReturnList");
                }
                var purchaseReturnDetails = _db.PurchaseReturnDetails.Select(p => p).Where(p => p.PurchaseReturnId == purchaseReturnId);

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
                        else
                        {
                            warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) + (item.Quantity * item.UnitPrice)) / warehouseItemStock.Quantity;
                        }
                    }
                }
                _db.PurchaseReturnDetails.RemoveRange(purchaseReturnDetails);
                _db.PurchaseReturns.Remove(purchaseReturn);
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
            return RedirectToAction("PurchaseReturnList");
        }
        [HttpPost]
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
                            else
                            {
                                warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) + (item.Quantity * item.UnitPrice)) / warehouseItemStock.Quantity;
                            }
                        }
                    }

                    _db.PurchaseReturnDetails.RemoveRange(purchaseReturnDetails);
                    _db.PurchaseReturns.RemoveRange(purchaseReturnToDelete);
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
            return RedirectToAction("PurchaseReturnList");
        }
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
        public ActionResult CreateIssue(WarehouseIssueHelper warehouseIssueHelper)
        {
            try
            {
                // Check if all issued quantities are 0 or null
                if (warehouseIssueHelper.WarehouseIssueDetails.All(item => item.IssuedQuantity == 0 || item.IssuedQuantity == null))
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
                    string datePart = DateTime.Now.ToString("yyyyMMdd");
                    int nextIssueNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.WarehouseIssues.Any())
                    {
                        // Bring the IssueNo values into memory, then extract the numeric part and calculate the max
                        nextIssueNumber = _db.WarehouseIssues
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.IssueNo.Substring(13)))  // Now we can safely use Convert.ToInt32
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
                    }
                    if (isSettled)
                    {
                        requisition.Status = "Settled";
                    }
                    warehouseIssue.WarehouseIssueDetails = warehouseIssueDetails;

                    _db.WarehouseIssues.Add(warehouseIssue);
                    _db.SaveChanges();
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

        public ActionResult IssueList(string search)
        {
            var warehouseIssue = _db.WarehouseIssues.AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                warehouseIssue = warehouseIssue.Where(c => c.WarehouseIssueDetails.Where(i => i.Item.ItemName.Contains(search)).Count() > 0 || c.Requisition.RequisitionNo.Contains(search) || c.IssueNo.Contains(search) || c.ReferenceNo.Contains(search) || c.Branch.BranchName.Contains(search) || c.Status.Contains(search));
            }
            return View(warehouseIssue.ToList());
        }
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
        public ActionResult EditIssue(WarehouseIssueHelper warehouseIssueHelper)
        {
            try
            {
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
        public ActionResult CreateUser(User user, String ConfirmPassword, HttpPostedFileBase ProfileImage)
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
                TempData["SuccessMessage"] = "User created successfully!";
                return RedirectToAction("UserList");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while creating the user: " + ex.Message);
            }

            TempData["ErrorMessage"] = "An error occurred while creating the user";
            return View();

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
        public ActionResult EditUser(User user, String ConfirmPassword, HttpPostedFileBase ProfileImage)
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
                    string uploadsFolder = Server.MapPath(TextConstraints.ProfileImagesPath);

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
                TempData["SuccessMessage"] = "User updated successfully!";
                return RedirectToAction("UserList");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while creating the user: " + ex.Message);
            }
            TempData["ErrorMessage"] = "An error occurred while updating the user";
            return View();
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
    }
}