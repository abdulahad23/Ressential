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
using System.IO;

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
            if (requisition.Status == "Rejected")
            {
                TempData["ErrorMessage"] = "The requisition cannot be updated due to 'Rejected' status";
                return RedirectToAction("RequisitionList");
            }
            else if (requisition.Status == "Settled")
            {
                TempData["ErrorMessage"] = "The requisition cannot be updated due to 'Settled' status";
                return RedirectToAction("RequisitionList");
            }
            else if (requisition.Status == "Partially Settled")
            {
                TempData["ErrorMessage"] = "The requisition cannot be updated due to 'Partially Settled' status";
                return RedirectToAction("RequisitionList");
            }
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
                else if (requisition.Status == "Rejected")
                {
                    TempData["ErrorMessage"] = "The requisition cannot be deleted due to 'Rejected' status";
                    return RedirectToAction("RequisitionList");
                }
                else if (requisition.Status == "Settled")
                {
                    TempData["ErrorMessage"] = "The requisition cannot be deleted due to 'Settled' status";
                    return RedirectToAction("RequisitionList");
                }
                else if (requisition.Status == "Partially Settled")
                {
                    TempData["ErrorMessage"] = "The requisition cannot be deleted due to 'Partially Settled' status";
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

                    if (requisitionsToDelete.Where(c => c.Status == "Rejected" || c.Status == "Settled" || c.Status == "Partially Settled").Count()>0)
                    {
                        TempData["ErrorMessage"] = "The requisitions cannot be deleted due to one or more requisition have not pending status";
                        return RedirectToAction("RequisitionList");
                    }

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

        public ActionResult CreateStockReturn()
        {
            var returnStock = new ReturnStock
            {
                ReturnStockDetails = new List<ReturnStockDetail>
                {
                    new ReturnStockDetail()
                }

            };
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();

            return View(returnStock);
        }
        [HttpPost]
        public ActionResult CreateStockReturn(ReturnStock returnStock)
        {
            try
            {
                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                if (ModelState.IsValid)
                {
                    string datePart = DateTime.Now.ToString("yyyyMMdd");
                    int nextReturnStockNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.ReturnStocks.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextReturnStockNumber = _db.ReturnStocks
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.ReturnNo.Substring(13)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    returnStock.ReturnNo = $"RET-{datePart}{nextReturnStockNumber:D4}";
                    returnStock.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    returnStock.CreatedAt = DateTime.Now;
                    returnStock.BranchId = selectedBranchId;
                    returnStock.Status = "Pending";
                    _db.ReturnStocks.Add(returnStock);
                    _db.SaveChanges();


                    return Json("0", JsonRequestBehavior.AllowGet);
                }

                ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
                return View(returnStock);
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                Console.WriteLine($"Error creating purchase: {ex.Message}");
                return RedirectToAction("Index", "Error");
            }
        }
        public ActionResult StockReturnList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var returnStock = _db.ReturnStocks.Where(c => c.BranchId == selectedBranchId);
            if (!string.IsNullOrEmpty(search))
            {
                returnStock = returnStock.Where(c => (c.ReturnStockDetails.Where(i => i.Item.ItemName.Contains(search)).Count() > 0 || c.ReturnNo.Contains(search) || c.Description.Contains(search)) && c.BranchId == selectedBranchId);
            }
            return View(returnStock.ToList());
        }
        [HttpPost]

        public ActionResult DeleteReturnStock(int ReturnStockId)
        {
            try
            {
                var returnStock = _db.ReturnStocks.Find(ReturnStockId);
                if (returnStock == null)
                {
                    TempData["ErrorMessage"] = "return Stock not found.";
                    return RedirectToAction("StockReturnList");
                }
                var returnStockDetails = _db.ReturnStockDetails.Select(p => p).Where(p => p.ReturnStockId == ReturnStockId);
                _db.ReturnStockDetails.RemoveRange(returnStockDetails);
                _db.ReturnStocks.Remove(returnStock);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Return Stock deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This Return Stock is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Return Stock.";
                }
            }
            return RedirectToAction("StockReturnList");

        }
        [HttpPost]
        public ActionResult DeleteSelectedStockReturns(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var returnStocksToDelete = _db.ReturnStocks.Where(c => selectedItems.Contains(c.ReturnStockId)).ToList();
                    var returnStockDetails = _db.ReturnStockDetails.Where(c => selectedItems.Contains(c.ReturnStockId)).ToList();

                    _db.ReturnStockDetails.RemoveRange(returnStockDetails);
                    _db.ReturnStocks.RemoveRange(returnStocksToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Return Stock deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A Return Stock is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Return Stock.";
                    }
                }
            }
            return RedirectToAction("StockReturnList");
        }
        public ActionResult EditReturnStock(int ReturnStockId)
        {
            ReturnStock returnStock = _db.ReturnStocks.Find(ReturnStockId);
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
            return View(returnStock);
        }
        [HttpPost]
        public ActionResult EditReturnStock(ReturnStock returnStock)
        {
            if (returnStock == null)
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
                    var existingReturnStock = _db.ReturnStocks.Include(p => p.ReturnStockDetails).FirstOrDefault(p => p.ReturnStockId == returnStock.ReturnStockId);
                    if (existingReturnStock == null)
                    {
                        return Json(new { status = "error", message = "Purchase not found" }, JsonRequestBehavior.AllowGet);
                    }

                    // Update purchase metadata
                    returnStock.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    returnStock.ModifiedAt = DateTime.Now;

                    // Update existing purchase values
                    _db.Entry(existingReturnStock).CurrentValues.SetValues(returnStock);
                    _db.Entry(existingReturnStock).Property(x => x.CreatedBy).IsModified = false;
                    _db.Entry(existingReturnStock).Property(x => x.CreatedAt).IsModified = false;
                    _db.Entry(existingReturnStock).Property(x => x.BranchId).IsModified = false;
                    _db.Entry(existingReturnStock).Property(x => x.Status).IsModified = false;

                    // Update PurchaseDetails
                    var existingDetails = existingReturnStock.ReturnStockDetails.ToList();
                    var newDetails = returnStock.ReturnStockDetails;

                    // Update or Add new details
                    foreach (var newDetail in newDetails)
                    {
                        var existingDetail = existingDetails.FirstOrDefault(d => d.ReturnStockDetailId == newDetail.ReturnStockDetailId);

                        if (existingDetail != null)
                        {

                            _db.Entry(existingDetail).CurrentValues.SetValues(newDetail);
                            existingDetails.Remove(existingDetail); // Remove from existing list once matched
                        }
                        else
                        {

                            existingReturnStock.ReturnStockDetails.Add(newDetail); // Add new detail
                        }
                    }

                    // Delete unmatched details
                    if (existingDetails != null)
                    {

                        _db.ReturnStockDetails.RemoveRange(existingDetails);
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
        public ActionResult ConsumeItemList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var consumeItem = _db.ConsumeItems.Where(c => c.BranchId == selectedBranchId);
            if (!string.IsNullOrEmpty(search))
            {
                consumeItem = consumeItem.Where(c => (c.ConsumeItemDetails.Where(i => i.Item.ItemName.Contains(search)).Count() > 0 || c.ReferenceNo.Contains(search) || c.Description.Contains(search)) && c.BranchId == selectedBranchId);
            }
            return View(consumeItem.ToList());
        }
        public ActionResult CreateConsumeItem()
        {
            var consumeItem = new ConsumeItem
            {
                ConsumeItemDetails = new List<ConsumeItemDetail>
                {
                    new ConsumeItemDetail()
                }

            };
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();

            return View(consumeItem);
        }
        [HttpPost]
        public ActionResult CreateConsumeItem(ConsumeItem consumeItem)
        {
            try
            {
                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                if (ModelState.IsValid)
                {
                    string datePart = DateTime.Now.ToString("yyyyMMdd");
                    int nextConsumeItemNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.ConsumeItems.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextConsumeItemNumber = _db.ConsumeItems
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.ReferenceNo.Substring(13)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    consumeItem.ReferenceNo = $"CON-{datePart}{nextConsumeItemNumber:D4}";
                    consumeItem.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    consumeItem.CreatedAt = DateTime.Now;
                    consumeItem.BranchId = selectedBranchId;
                    _db.ConsumeItems.Add(consumeItem);
                    _db.SaveChanges();

                    //foreach (var consumeItemDetails in consumeItem.ConsumeItemDetails)
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
                return View(consumeItem);
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                Console.WriteLine($"Error creating purchase: {ex.Message}");
                return RedirectToAction("Index", "Error");
            }
        }
        public ActionResult EditConsumeItem(int consumeItemId)
        {
            ConsumeItem consumeItem = _db.ConsumeItems.Find(consumeItemId);
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
            return View(consumeItem);
        }
        [HttpPost]
        public ActionResult EditConsumeItem(ConsumeItem consumeItem)
        {
            if (consumeItem == null)
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
                    var existingConsumeItem = _db.ConsumeItems.Include(p => p.ConsumeItemDetails).FirstOrDefault(p => p.ConsumeItemId == consumeItem.ConsumeItemId);
                    if (existingConsumeItem == null)
                    {
                        return Json(new { status = "error", message = "Purchase not found" }, JsonRequestBehavior.AllowGet);
                    }

                    // Update purchase metadata
                    consumeItem.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    consumeItem.ModifiedAt = DateTime.Now;

                    // Update existing purchase values
                    _db.Entry(existingConsumeItem).CurrentValues.SetValues(consumeItem);
                    _db.Entry(existingConsumeItem).Property(x => x.CreatedBy).IsModified = false;
                    _db.Entry(existingConsumeItem).Property(x => x.CreatedAt).IsModified = false;
                    _db.Entry(existingConsumeItem).Property(x => x.BranchId).IsModified = false;

                    // Update PurchaseDetails
                    var existingDetails = existingConsumeItem.ConsumeItemDetails.ToList();
                    var newDetails = consumeItem.ConsumeItemDetails;

                    // Update or Add new details
                    foreach (var newDetail in newDetails)
                    {
                        var existingDetail = existingDetails.FirstOrDefault(d => d.ConsumeItemDetailId == newDetail.ConsumeItemDetailId);
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
                            existingConsumeItem.ConsumeItemDetails.Add(newDetail); // Add new detail
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
                        _db.ConsumeItemDetails.RemoveRange(existingDetails);
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
        public ActionResult DeleteConsumeItem(int consumeItemId)
        {
            try
            {
                var consumeItem = _db.ConsumeItems.Find(consumeItemId);
                if (consumeItem == null)
                {
                    TempData["ErrorMessage"] = "ConsumeItem not found.";
                    return RedirectToAction("ConsumeItemList");
                }
                var consumeItemDetails = _db.ConsumeItemDetails.Select(p => p).Where(p => p.ConsumeItemId == consumeItemId);

                //if (consumeItemDetails != null)
                //{
                //    foreach (var item in consumeItemDetails)
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
                _db.ConsumeItemDetails.RemoveRange(consumeItemDetails);
                _db.ConsumeItems.Remove(consumeItem);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "ConsumeItem deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This consumeItem is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the consumeItem.";
                }
            }
            return RedirectToAction("ConsumeItemList");
        }
        [HttpPost]
        public ActionResult DeleteSelectedConsumeItems(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var consumeItemsToDelete = _db.ConsumeItems.Where(c => selectedItems.Contains(c.ConsumeItemId)).ToList();
                    var consumeItemDetails = _db.ConsumeItemDetails.Where(c => selectedItems.Contains(c.ConsumeItemId)).ToList();

                    //foreach (var item in consumeItemsToDelete)
                    //{
                    //    var warehouseItemTransaction = _db.WarehouseItemTransactions.Where(w => w.TransactionType == "ConsumeItem" && w.TransactionTypeId == item.ConsumeItemId);
                    //    _db.WarehouseItemTransactions.RemoveRange(warehouseItemTransaction);
                    //}

                    //if (consumeItemDetails != null)
                    //{
                    //    foreach (var item in consumeItemDetails)
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

                    _db.ConsumeItemDetails.RemoveRange(consumeItemDetails);
                    _db.ConsumeItems.RemoveRange(consumeItemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "ConsumeItems deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A consumeItem is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the consumeItem.";
                    }
                }
            }
            return RedirectToAction("ConsumeItemList");
        }

        public ActionResult WastageItemList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var wastageItem = _db.WastageItems.Where(c => c.BranchId == selectedBranchId);
            if (!string.IsNullOrEmpty(search))
            {
                wastageItem = wastageItem.Where(c => (c.WastageItemDetails.Where(i => i.Item.ItemName.Contains(search)).Count() > 0 || c.ReferenceNo.Contains(search) || c.Description.Contains(search)) && c.BranchId == selectedBranchId);
            }
            return View(wastageItem.ToList());
        }
        public ActionResult CreateWastageItem()
        {
            var wastageItem = new WastageItem
            {
                WastageItemDetails = new List<WastageItemDetail>
                {
                    new WastageItemDetail()
                }

            };
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();

            return View(wastageItem);
        }
        [HttpPost]
        public ActionResult CreateWastageItem(WastageItem wastageItem)
        {
            try
            {
                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                if (ModelState.IsValid)
                {
                    string datePart = DateTime.Now.ToString("yyyyMMdd");
                    int nextWastageItemNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.WastageItems.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextWastageItemNumber = _db.WastageItems
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.ReferenceNo.Substring(13)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    wastageItem.ReferenceNo = $"WAS-{datePart}{nextWastageItemNumber:D4}";
                    wastageItem.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    wastageItem.CreatedAt = DateTime.Now;
                    wastageItem.BranchId = selectedBranchId;
                    _db.WastageItems.Add(wastageItem);
                    _db.SaveChanges();

                    //foreach (var wastageItemDetails in wastageItem.WastageItemDetails)
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
                return View(wastageItem);
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                Console.WriteLine($"Error creating purchase: {ex.Message}");
                return RedirectToAction("Index", "Error");
            }
        }
        public ActionResult EditWastageItem(int wastageItemId)
        {
            WastageItem wastageItem = _db.WastageItems.Find(wastageItemId);
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
            return View(wastageItem);
        }
        [HttpPost]
        public ActionResult EditWastageItem(WastageItem wastageItem)
        {
            if (wastageItem == null)
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
                    var existingWastageItem = _db.WastageItems.Include(p => p.WastageItemDetails).FirstOrDefault(p => p.WastageItemId == wastageItem.WastageItemId);
                    if (existingWastageItem == null)
                    {
                        return Json(new { status = "error", message = "Purchase not found" }, JsonRequestBehavior.AllowGet);
                    }

                    // Update purchase metadata
                    wastageItem.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    wastageItem.ModifiedAt = DateTime.Now;

                    // Update existing purchase values
                    _db.Entry(existingWastageItem).CurrentValues.SetValues(wastageItem);
                    _db.Entry(existingWastageItem).Property(x => x.CreatedBy).IsModified = false;
                    _db.Entry(existingWastageItem).Property(x => x.CreatedAt).IsModified = false;
                    _db.Entry(existingWastageItem).Property(x => x.BranchId).IsModified = false;

                    // Update PurchaseDetails
                    var existingDetails = existingWastageItem.WastageItemDetails.ToList();
                    var newDetails = wastageItem.WastageItemDetails;

                    // Update or Add new details
                    foreach (var newDetail in newDetails)
                    {
                        var existingDetail = existingDetails.FirstOrDefault(d => d.WastageItemDetailId == newDetail.WastageItemDetailId);
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
                            existingWastageItem.WastageItemDetails.Add(newDetail); // Add new detail
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
                        _db.WastageItemDetails.RemoveRange(existingDetails);
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
        public ActionResult DeleteWastageItem(int wastageItemId)
        {
            try
            {
                var wastageItem = _db.WastageItems.Find(wastageItemId);
                if (wastageItem == null)
                {
                    TempData["ErrorMessage"] = "WastageItem not found.";
                    return RedirectToAction("WastageItemList");
                }
                var wastageItemDetails = _db.WastageItemDetails.Select(p => p).Where(p => p.WastageItemId == wastageItemId);

                //if (wastageItemDetails != null)
                //{
                //    foreach (var item in wastageItemDetails)
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
                _db.WastageItemDetails.RemoveRange(wastageItemDetails);
                _db.WastageItems.Remove(wastageItem);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "WastageItem deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This wastageItem is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the wastageItem.";
                }
            }
            return RedirectToAction("WastageItemList");
        }
        [HttpPost]
        public ActionResult DeleteSelectedWastageItems(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var wastageItemsToDelete = _db.WastageItems.Where(c => selectedItems.Contains(c.WastageItemId)).ToList();
                    var wastageItemDetails = _db.WastageItemDetails.Where(c => selectedItems.Contains(c.WastageItemId)).ToList();

                    //foreach (var item in wastageItemsToDelete)
                    //{
                    //    var warehouseItemTransaction = _db.WarehouseItemTransactions.Where(w => w.TransactionType == "WastageItem" && w.TransactionTypeId == item.WastageItemId);
                    //    _db.WarehouseItemTransactions.RemoveRange(warehouseItemTransaction);
                    //}

                    //if (wastageItemDetails != null)
                    //{
                    //    foreach (var item in wastageItemDetails)
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

                    _db.WastageItemDetails.RemoveRange(wastageItemDetails);
                    _db.WastageItems.RemoveRange(wastageItemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "WastageItems deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A wastageItem is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the wastageItem.";
                    }
                }
            }
            return RedirectToAction("WastageItemList");
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
        public ActionResult CreateProduct()
        {
            var product = new Product
            {
                ProductItemDetails = new List<ProductItemDetail>
                {
                    new ProductItemDetail()
                }

            };
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
            ViewBag.ProductCategories = _db.ProductCategories.ToList();


            return View(product);
        }

        [HttpPost]
        public ActionResult CreateProduct(Product product, HttpPostedFileBase imageFile)
        {
            try
            {
                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                if (ModelState.IsValid)
                {
                    string uploadFolder = Server.MapPath("~/Uploads/ProductImages");
                    if (!Directory.Exists(uploadFolder))
                    {
                        Directory.CreateDirectory(uploadFolder);
                    }

                    if (imageFile != null && imageFile.ContentLength > 0)
                    {
                        string fileName = DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + Path.GetFileName(imageFile.FileName);
                        string filePath = Path.Combine(uploadFolder, fileName);

                        try
                        {
                            imageFile.SaveAs(filePath);
                            product.ProductImage = fileName; // Save relative path
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error saving file: {ex.Message}");
                            product.ProductImage = "/Content/assets/no_image.png"; // Use default image
                        }
                    }
                    else
                    {
                        product.ProductImage = "/Content/assets/no_image.png"; // Default image path
                    }

                    product.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    product.CreatedAt = DateTime.Now;
                    product.BranchId = selectedBranchId;

                    _db.Products.Add(product);
                    _db.SaveChanges();

                    return Json("0", JsonRequestBehavior.AllowGet);
                }

                ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
                ViewBag.ProductCategories = _db.ProductCategories.ToList();

                return View(product);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating product: {ex.Message}");
                return RedirectToAction("Index", "Error");
            }
        }
        public ActionResult ProductList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var product = _db.Products.Where(c => c.BranchId == selectedBranchId);
            if (!string.IsNullOrEmpty(search))
            {

                product = product.Where(c => (c.ProductItemDetails.Where(i => i.Product.ProductName.Contains(search)).Count() > 0 || c.ProductCategory.ProductCategoryName.Contains(search)) && c.BranchId == selectedBranchId);
            }
            return View(product.ToList());
        }
        [HttpPost]

        public ActionResult DeleteProduct(int ProductId)
        {
            try
            {
                var product = _db.Products.Find(ProductId);
                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToAction("ProductList");
                }
                var productItemDetails = _db.ProductItemDetails.Select(p => p).Where(p => p.ProductId == ProductId);
                _db.ProductItemDetails.RemoveRange(productItemDetails);
                _db.Products.Remove(product);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Product deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This Product is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Product.";
                }
            }
            return RedirectToAction("ProductList");

        }
        [HttpPost]
        public ActionResult DeleteSelectedProducts(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var productToDelete = _db.Products.Where(c => selectedItems.Contains(c.ProductId)).ToList();
                    var productItemDetails = _db.ProductItemDetails.Where(c => selectedItems.Contains(c.ProductId)).ToList();

                    _db.ProductItemDetails.RemoveRange(productItemDetails);
                    _db.Products.RemoveRange(productToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Product deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A Product is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Product.";
                    }
                }
            }
            return RedirectToAction("ProductList");
        }

        public ActionResult EditProduct(int ProductId)
        {
            Product product = _db.Products.Find(ProductId);
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
            ViewBag.ProductCategories = _db.ProductCategories.ToList();

            return View(product);
        }
        [HttpPost]
        public ActionResult EditProduct(Product product, HttpPostedFileBase imageFile)
        {
            if (product == null)
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
                    var existingProduct = _db.Products.Include(p => p.ProductItemDetails)
                                                      .FirstOrDefault(p => p.ProductId == product.ProductId);
                    if (existingProduct == null)
                    {
                        return Json(new { status = "error", message = "Product not found" }, JsonRequestBehavior.AllowGet);
                    }

                    // Update metadata
                    product.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    product.ModifiedAt = DateTime.Now;

                    // Update existing product values
                    _db.Entry(existingProduct).CurrentValues.SetValues(product);
                    _db.Entry(existingProduct).Property(x => x.CreatedBy).IsModified = false;
                    _db.Entry(existingProduct).Property(x => x.CreatedAt).IsModified = false;
                    _db.Entry(existingProduct).Property(x => x.ProductImage).IsModified = false;
                    _db.Entry(existingProduct).Property(x => x.BranchId).IsModified = false;

                    // Update ProductItemDetails
                    var existingDetails = existingProduct.ProductItemDetails.ToList();
                    var newDetails = product.ProductItemDetails;

                    // Update or Add new details
                    foreach (var newDetail in newDetails)
                    {
                        var existingDetail = existingDetails.FirstOrDefault(d => d.ProductItemDetailId == newDetail.ProductItemDetailId);
                        if (existingDetail != null)
                        {
                            _db.Entry(existingDetail).CurrentValues.SetValues(newDetail);
                            existingDetails.Remove(existingDetail); // Remove from existing list once matched
                        }
                        else
                        {
                            existingProduct.ProductItemDetails.Add(newDetail); // Add new detail
                        }
                    }

                    // Delete unmatched details
                    if (existingDetails != null)
                    {
                        _db.ProductItemDetails.RemoveRange(existingDetails);
                    }

                    // Handle image update
                    if (imageFile != null && imageFile.ContentLength > 0)
                    {
                        string uploadFolder = Server.MapPath("~/Uploads/ProductImages");
                        if (!Directory.Exists(uploadFolder))
                        {
                            Directory.CreateDirectory(uploadFolder);
                        }

                        string fileName = DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + Path.GetFileName(imageFile.FileName);
                        string filePath = Path.Combine(uploadFolder, fileName);

                        try
                        {
                            // Save the new image
                            imageFile.SaveAs(filePath);
                            existingProduct.ProductImage = fileName; // Save relative path
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error saving file: {ex.Message}");
                        }
                    }

                    // Save changes
                    _db.SaveChanges();
                    transaction.Commit();

                    TempData["SuccessMessage"] = "Product updated successfully.";
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

                    TempData["ErrorMessage"] = "An error occurred while updating the product.";
                    return Json(new { status = "error", message = "Validation error occurred. Check logs for details." }, JsonRequestBehavior.AllowGet);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Console.WriteLine($"Error updating product: {ex.Message}");
                    TempData["ErrorMessage"] = "An unexpected error occurred.";
                    return Json(new { status = "error", message = "Unexpected error occurred." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        public ActionResult OrderList()
        {
            return View();
        }
        public ActionResult CreateOrder()
        {
            var branchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var order = new Order
            {
                OrderDetails = new List<OrderDetail>
                {
                    new OrderDetail()
                }

            };
            ViewBag.Products = _db.Products.Where(i => i.IsActive == true && i.BranchId == branchId).ToList();

            return View(order);
        }
        [HttpPost]
        public ActionResult CreateOrder(Order order)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var branchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                    string datePart = DateTime.Now.ToString("yyyyMMdd");
                    int nextOrderNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.Orders.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextOrderNumber = _db.Orders
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.OrderNo.Substring(13)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    order.OrderNo = $"ORD-{datePart}{nextOrderNumber:D4}";
                    order.BranchId = branchId;
                    order.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    order.OrderDate = DateTime.Now;
                    order.CreatedAt = DateTime.Now;
                    order.Status = "Preparing";
                    _db.Orders.Add(order);
                    _db.SaveChanges();

                    //foreach (var orderDetails in order.OrderDetails)
                    //{
                    //    var currentItemStock = _db.WarehouseItemStocks.Where(i => i.ItemId == purchaseDetails.ItemId).FirstOrDefault();
                    //    decimal currentQuantity = currentItemStock.Quantity;
                    //    currentItemStock.Quantity = currentQuantity + purchaseDetails.Quantity;
                    //    currentItemStock.CostPerUnit = ((currentQuantity * currentItemStock.CostPerUnit) + (purchaseDetails.Quantity * purchaseDetails.UnitPrice)) / (currentItemStock.Quantity)
                    //    _db.WarehouseItemStocks.AddOrUpdate(currentItemStock);
                    //}
                    _db.SaveChanges();

                    return Json(0, JsonRequestBehavior.AllowGet);
                }
                ViewBag.Vendors = _db.Vendors.Select(v => new { v.VendorId, v.Name }).ToList();
                ViewBag.Items = _db.Items.Select(i => new { i.ItemId, i.ItemName }).ToList();
                return View(order);
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                Console.WriteLine($"Error creating purchase: {ex.Message}");
                return RedirectToAction("Index", "Error");
            }
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
            user.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
            user.CreatedAt = DateTime.Now;
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
        public ActionResult EditUser(User user, String ConfirmPassword)
        {
            var existingUser = _db.Users.Find(user.UserId);
            if (existingUser == null)
            {
                return HttpNotFound();
            }

            ConfirmPassword = ConfirmPassword == "" ? null : ConfirmPassword;

            if (user.Password != ConfirmPassword)
            {
                ModelState.AddModelError("", "Password and Confirm Password do not match.");
                return View(user);
            }

            user.Email = existingUser.Email;
            user.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
            user.ModifiedAt = DateTime.Now;

            _db.Entry(existingUser).CurrentValues.SetValues(user);
            _db.Entry(existingUser).Property(x => x.CreatedBy).IsModified = false;
            _db.Entry(existingUser).Property(x => x.CreatedAt).IsModified = false;
            if (user.Password == null)
            {
                _db.Entry(existingUser).Property(x => x.Password).IsModified = false;
            }

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