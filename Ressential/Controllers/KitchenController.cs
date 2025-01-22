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
using Microsoft.AspNet.Identity;
using System.Net.Security;

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
            BranchItem branchItem = new BranchItem {
                OpeningStockDate = DateTime.Today,
                IsActive = true,
            };
            ViewBag.items = _db.Items.ToList();

            return View(branchItem);
        }
        [HttpPost]
        public ActionResult CreateItem(BranchItem branchItems)
        {
            try
            {
                branchItems.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                branchItems.CreatedAt = DateTime.Now;
                branchItems.BranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                branchItems.Quantity = branchItems.OpeningStockQuantity;
                if (branchItems.Quantity == 0)
                {
                    branchItems.CostPerUnit = 0;
                }
                else
                {
                    branchItems.CostPerUnit = branchItems.OpeningStockValue / branchItems.OpeningStockQuantity;
                }
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
                _db.Entry(existingItem).Property(x => x.OpeningStockQuantity).IsModified = false;
                _db.Entry(existingItem).Property(x => x.OpeningStockValue).IsModified = false;
                _db.Entry(existingItem).Property(x => x.OpeningStockDate).IsModified = false;
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
                    string datePart = DateTime.Now.ToString("yyyyMM");
                    int nextRequisitionNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.Requisitions.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextRequisitionNumber = _db.Requisitions
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.RequisitionNo.Substring(11)))  // Now we can safely use Convert.ToInt32
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
        public ActionResult ReceiveStockList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var warehouseIssues = _db.WarehouseIssues.Where(w => w.BranchID == selectedBranchId);
            if (!string.IsNullOrEmpty(search))
            {
                warehouseIssues = warehouseIssues.Where(w => w.IssueNo.Contains(search) || w.Requisition.RequisitionNo.Contains(search) || w.Memo.Contains(search) || w.Status.Contains(search));
            }
            return View(warehouseIssues);
        }
        public ActionResult ViewIssue(int issueId)
        {
            var issue = _db.WarehouseIssues.Find(issueId);

            if (issue == null)
            {
                TempData["ErrorMessage"] = "Issue not found.";
                return RedirectToAction("ReceiveStockList");
            }

            return View(issue);
        }

        [HttpPost]
        public ActionResult ReceiveIssue(int IssueId)
        {
            try
            {
                var warehouseIssues = _db.WarehouseIssues.Find(IssueId);
                if (warehouseIssues.Status == "Settled")
                {
                    TempData["ErrorMessage"] = "The issue is already received";
                    return RedirectToAction("ReceiveStockList");
                }

                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                foreach (var item in warehouseIssues.WarehouseIssueDetails)
                {
                    var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();

                    if (branchItem != null)
                    {
                        var currentTotalCost = branchItem.Quantity * branchItem.CostPerUnit;
                        var newTotalCost = item.Quantity * item.CostApplied;

                        branchItem.Quantity += item.Quantity;
                        branchItem.CostPerUnit = (currentTotalCost + newTotalCost) / branchItem.Quantity;

                        // Mark the branchItem as modified
                        _db.Entry(branchItem).State = EntityState.Modified;
                    }
                }

                warehouseIssues.Status = "Settled";

                // Mark warehouseIssues as modified if needed
                _db.Entry(warehouseIssues).State = EntityState.Modified;

                _db.SaveChanges();

                TempData["SuccessMessage"] = "Stock received successfully.";
                return RedirectToAction("ReceiveStockList");
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while receiving the stock.";
                return RedirectToAction("ReceiveStockList");
            }
        }

        [HttpPost]
        public ActionResult ReceiveSelectedIssues(int[] selectedItems)
        {
            try
            {
                var issuesToReceive = _db.WarehouseIssues
                                         .Where(c => selectedItems.Contains(c.IssueId))
                                         .ToList();

                var receivedIssues = issuesToReceive.Where(i => i.Status == "Settled");
                if (receivedIssues.Count() > 0)
                {
                    TempData["ErrorMessage"] = "An issue exist which is already received";
                    return RedirectToAction("ReceiveStockList");
                }
                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));

                foreach (var warehouseIssue in issuesToReceive)
                {
                    foreach (var item in warehouseIssue.WarehouseIssueDetails)
                    {
                        var branchItem = _db.BranchItems.FirstOrDefault(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId);

                        if (branchItem != null)
                        {
                            var currentTotalCost = branchItem.Quantity * branchItem.CostPerUnit;
                            var newTotalCost = item.Quantity * item.CostApplied;

                            branchItem.Quantity += item.Quantity;
                            branchItem.CostPerUnit = (currentTotalCost + newTotalCost) / branchItem.Quantity;

                            // Mark the branchItem as modified
                            _db.Entry(branchItem).State = EntityState.Modified;
                        }
                    }

                    warehouseIssue.Status = "Settled";

                    // Mark warehouseIssue as modified
                    _db.Entry(warehouseIssue).State = EntityState.Modified;
                }

                _db.SaveChanges();

                TempData["SuccessMessage"] = "Stock received successfully.";
                return RedirectToAction("ReceiveStockList");
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while receiving the stock.";
                return RedirectToAction("ReceiveStockList");
            }
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
                    foreach (var item in returnStock.ReturnStockDetails)
                    {
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();
                        if (item.ItemQuantity > branchItem.Quantity)
                        {
                            return Json(new { success = false, errorMessage = $"Insufficient stock for {item.Item.ItemName}. The available quantity is {branchItem.Quantity}" });
                        }

                        item.CostPerUnit = branchItem.CostPerUnit;
                        branchItem.Quantity = branchItem.Quantity - item.ItemQuantity;
                    }

                    string datePart = DateTime.Now.ToString("yyyyMM");
                    int nextReturnStockNumber = 1;

                    // Check if there are any existing return stock record first
                    if (_db.ReturnStocks.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextReturnStockNumber = _db.ReturnStocks
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.ReturnNo.Substring(11)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    returnStock.ReturnNo = $"RET-{datePart}{nextReturnStockNumber:D4}";
                    returnStock.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    returnStock.CreatedAt = DateTime.Now;
                    returnStock.BranchId = selectedBranchId;
                    returnStock.Status = "Pending";
                    _db.ReturnStocks.Add(returnStock);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Stock return record created successfully!";
                    return Json(new { success = true });
                }

                return Json(new { success = false, redirect = Url.Action("Index", "Error") });
            }
            catch (Exception)
            {
                // Log the exception if needed
                TempData["ErrorMessage"] = "An error occurred while creating the return stock.";
                return Json(new { success = false, redirect = Url.Action("StockReturnList", "Kitchen") });
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
                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                var returnStock = _db.ReturnStocks.Find(ReturnStockId);
                if (returnStock == null)
                {
                    TempData["ErrorMessage"] = "Return stock not found.";
                    return RedirectToAction("StockReturnList");
                }
                var returnStockDetails = _db.ReturnStockDetails.Select(p => p).Where(p => p.ReturnStockId == ReturnStockId);

                if (returnStockDetails != null)
                {
                    foreach (var item in returnStockDetails)
                    {
                        if (item.ReturnStock.Status == "Settled")
                        {
                            TempData["ErrorMessage"] = "Cannot delete settled stock return record.";
                            return RedirectToAction("StockReturnList");
                        }
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();
                        decimal currentQuantity = branchItem.Quantity;
                        branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                        if (branchItem.Quantity == 0)
                        {
                            branchItem.CostPerUnit = 0;
                        }
                        else if (branchItem.Quantity < 0)
                        {
                            TempData["ErrorMessage"] = "Unable to delete the stock return. Quantity of " + branchItem.Item.ItemName + " cannot be < 0.";
                            return RedirectToAction("StockReturnList");
                        }
                        else
                        {
                            branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                        }
                    }
                }

                _db.ReturnStockDetails.RemoveRange(returnStockDetails);
                _db.ReturnStocks.Remove(returnStock);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Return stock record deleted successfully.";
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
                    var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                    var returnStocksToDelete = _db.ReturnStocks.Where(c => selectedItems.Contains(c.ReturnStockId)).ToList();
                    var returnStockDetails = _db.ReturnStockDetails.Where(c => selectedItems.Contains(c.ReturnStockId)).ToList();

                    if (returnStockDetails != null)
                    {
                        foreach (var item in returnStockDetails)
                        {
                            if (item.ReturnStock.Status == "Settled")
                            {
                                TempData["ErrorMessage"] = "Cannot delete settled stock return record.";
                                return RedirectToAction("StockReturnList");
                            }
                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to delete the stock return "+ item.ReturnStock.ReturnNo +". Quantity of " + branchItem.Item.ItemName + " cannot be < 0.";
                                return RedirectToAction("StockReturnList");
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                            }
                        }
                    }

                    _db.ReturnStockDetails.RemoveRange(returnStockDetails);
                    _db.ReturnStocks.RemoveRange(returnStocksToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Return stock records deleted successfully.";
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
            using (var transaction = _db.Database.BeginTransaction())
            {
                try
                {
                    var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));

                    var existingReturnStock = _db.ReturnStocks.Include(p => p.ReturnStockDetails).FirstOrDefault(p => p.ReturnStockId == returnStock.ReturnStockId);
                    if (existingReturnStock == null)
                    {
                        return Json(new { status = "error", message = "Stock return record not found" }, JsonRequestBehavior.AllowGet);
                    }
                    if (existingReturnStock.Status == "Settled")
                    {
                        TempData["ErrorMessage"] = "Cannot update settled stock return record.";
                        return RedirectToAction("StockReturnList");
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
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == newDetail.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();
                        if (existingDetail != null)
                        {
                            // Revert stock to previous state before applying new changes
                            var revertedQuantity = branchItem.Quantity + existingDetail.ItemQuantity;
                            var revertedTotalCost = (branchItem.Quantity * branchItem.CostPerUnit) + (existingDetail.ItemQuantity * existingDetail.CostPerUnit);
                            var revertedPerUnitCost = revertedTotalCost / revertedQuantity;

                            branchItem.Quantity = revertedQuantity;
                            branchItem.CostPerUnit = revertedPerUnitCost;

                            branchItem.Quantity -= newDetail.ItemQuantity;
                            var newTotalCost = newDetail.ItemQuantity * newDetail.CostPerUnit;
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to update the stock return record. Quantity of " + branchItem.Item.ItemName + " cannot be < 0.";
                                return Json(new { success = false, message = "Unable to update the stock return record. Quantity of " + branchItem.Item.ItemName + " cannot be < 0." });
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((revertedTotalCost - newTotalCost) / branchItem.Quantity);
                            }

                            _db.Entry(existingDetail).CurrentValues.SetValues(newDetail);
                            existingDetails.Remove(existingDetail); // Remove from existing list once matched
                        }
                        else
                        {
                            // For new details, update stock directly
                            var totalCostBeforeChange = (branchItem.Quantity * branchItem.CostPerUnit);

                            branchItem.Quantity -= newDetail.ItemQuantity; // Subtract new quantity

                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to update the stock return record. Quantity of " + branchItem.Item.ItemName + " cannot be < 0.";
                                return Json(new { success = false, message = "Unable to update the stock return record. Quantity of " + branchItem.Item.ItemName + " cannot be < 0." });
                            }
                            else
                            {
                                var totalCostAfterChange = totalCostBeforeChange + (newDetail.ItemQuantity * newDetail.CostPerUnit);
                                branchItem.CostPerUnit = totalCostAfterChange / branchItem.Quantity;
                            }

                            existingReturnStock.ReturnStockDetails.Add(newDetail); // Add new detail
                        }
                    }

                    // Delete unmatched details
                    if (existingDetails != null)
                    {
                        foreach (var item in existingDetails)
                        {
                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to update the stock return record. Quantity of " + branchItem.Item.ItemName + " cannot be < 0.";
                                return Json(new { success = false, message = "Unable to update the stock return record. Quantity of " + branchItem.Item.ItemName + " cannot be < 0." });
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                            }
                        }
                        
                        _db.ReturnStockDetails.RemoveRange(existingDetails);
                    }


                    // Save changes
                    _db.SaveChanges();
                    transaction.Commit();

                    TempData["SuccessMessage"] = "Stock return record updated successfully!";
                    return Json(new { success = true });
                }
                catch (DbEntityValidationException)
                {
                    transaction.Rollback();
                    TempData["ErrorMessage"] = "An error occurred while updating the return stock record.";
                    return Json(new { success = false, redirect = Url.Action("StockReturnList", "Kitchen") });
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
                    string datePart = DateTime.Now.ToString("yyyyMM");
                    int nextConsumeItemNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.ConsumeItems.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextConsumeItemNumber = _db.ConsumeItems
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.ReferenceNo.Substring(11)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    consumeItem.ReferenceNo = $"CON-{datePart}{nextConsumeItemNumber:D4}";
                    consumeItem.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    consumeItem.CreatedAt = DateTime.Now;
                    consumeItem.BranchId = selectedBranchId;

                    foreach (var item in consumeItem.ConsumeItemDetails)
                    {
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();

                        if (branchItem != null)
                        {
                            if (branchItem.Quantity >= item.ItemQuantity)
                            {
                                branchItem.Quantity -= item.ItemQuantity;
                                item.CostPerUnit = branchItem.CostPerUnit;

                                // Mark the branchItem as modified
                                _db.Entry(branchItem).State = EntityState.Modified;
                            }
                            else
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of "+ branchItem.Item.ItemName +" to create consumption";
                                return Json("0", JsonRequestBehavior.AllowGet);
                            }

                        }
                    }

                    _db.ConsumeItems.Add(consumeItem);
                    _db.SaveChanges();

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
            using (var transaction = _db.Database.BeginTransaction())
            {
                try
                {
                    var existingConsumeItem = _db.ConsumeItems.Include(p => p.ConsumeItemDetails).FirstOrDefault(p => p.ConsumeItemId == consumeItem.ConsumeItemId);
                    if (existingConsumeItem == null)
                    {
                        TempData["ErrorMessage"] = "Record not found";
                        return Json(new { status = "error", message = "Record not found" }, JsonRequestBehavior.AllowGet);
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
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == existingDetail.ItemId && b.BranchId == consumeItem.BranchId).FirstOrDefault();
                        if (existingDetail != null)
                        {
                            decimal RevertedQuantity = branchItem.Quantity + existingDetail.ItemQuantity;
                            decimal currentQuantity = branchItem.Quantity;

                            branchItem.Quantity = RevertedQuantity - newDetail.ItemQuantity; //Updated Quantity
                            if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to update the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (existingDetail.ItemQuantity * existingDetail.CostPerUnit) - (newDetail.ItemQuantity * newDetail.CostPerUnit)) / branchItem.Quantity; //Updated Per Unit Cost
                            }

                            _db.Entry(existingDetail).CurrentValues.SetValues(newDetail);
                            existingDetails.Remove(existingDetail); // Remove from existing list once matched
                        }
                        else
                        {
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity - newDetail.ItemQuantity; //Updated Quantity
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to update the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) - (newDetail.ItemQuantity * newDetail.CostPerUnit)) / branchItem.Quantity; //Updated Per Unit Cost
                            }
                            existingConsumeItem.ConsumeItemDetails.Add(newDetail); // Add new detail
                        }
                    }

                    // Delete unmatched details
                    if (existingDetails != null)
                    {
                        foreach (var item in existingDetails)
                        {
                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == consumeItem.BranchId).FirstOrDefault();
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to update the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                            }
                        }
                        _db.ConsumeItemDetails.RemoveRange(existingDetails);
                    }


                    // Save changes
                    _db.SaveChanges();
                    transaction.Commit();

                    TempData["SuccessMessage"] = "Consume record updated successfully.";
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

                    TempData["ErrorMessage"] = "An error occurred while updating the consume record.";
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

                if (consumeItemDetails != null)
                {
                    foreach (var item in consumeItemDetails)
                    {
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == consumeItem.BranchId).FirstOrDefault();
                        decimal currentQuantity = branchItem.Quantity;
                        branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                        if (branchItem.Quantity == 0)
                        {
                            branchItem.CostPerUnit = 0;
                        }
                        else if (branchItem.Quantity < 0)
                        {
                            TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to delete the record.";
                            return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                        }
                        else
                        {
                            branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                        }
                    }
                }

                _db.ConsumeItemDetails.RemoveRange(consumeItemDetails);
                _db.ConsumeItems.Remove(consumeItem);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Consume item record deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This consume item record is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the consume item record.";
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

                    if (consumeItemDetails != null)
                    {
                        foreach (var item in consumeItemDetails)
                        {
                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == item.ConsumeItem.BranchId).FirstOrDefault();
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to delete the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                            }
                        }
                    }

                    _db.ConsumeItemDetails.RemoveRange(consumeItemDetails);
                    _db.ConsumeItems.RemoveRange(consumeItemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Consume item records deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A consume item record is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the consume item records.";
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
                    string datePart = DateTime.Now.ToString("yyyyMM");
                    int nextWastageItemNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.WastageItems.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextWastageItemNumber = _db.WastageItems
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.ReferenceNo.Substring(11)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    wastageItem.ReferenceNo = $"WAS-{datePart}{nextWastageItemNumber:D4}";
                    wastageItem.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    wastageItem.CreatedAt = DateTime.Now;
                    wastageItem.BranchId = selectedBranchId;

                    foreach (var item in wastageItem.WastageItemDetails)
                    {
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();

                        if (branchItem != null)
                        {
                            if (branchItem.Quantity >= item.ItemQuantity)
                            {
                                branchItem.Quantity -= item.ItemQuantity;
                                item.CostPerUnit = branchItem.CostPerUnit;

                                // Mark the branchItem as modified
                                _db.Entry(branchItem).State = EntityState.Modified;
                            }
                            else
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to create wastage record";
                                return Json("0", JsonRequestBehavior.AllowGet);
                            }
                            
                        }
                    }

                    _db.WastageItems.Add(wastageItem);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Wastage item record created successfully.";
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
            using (var transaction = _db.Database.BeginTransaction())
            {
                try
                {
                    var existingWastageItem = _db.WastageItems.Include(p => p.WastageItemDetails).FirstOrDefault(p => p.WastageItemId == wastageItem.WastageItemId);
                    if (existingWastageItem == null)
                    {
                        TempData["ErrorMessage"] = "Record not found";
                        return Json(new { status = "error", message = "Record not found" }, JsonRequestBehavior.AllowGet);
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
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == existingDetail.ItemId && b.BranchId == wastageItem.BranchId).FirstOrDefault();
                        if (existingDetail != null)
                        {
                            decimal RevertedQuantity = branchItem.Quantity + existingDetail.ItemQuantity;
                            decimal currentQuantity = branchItem.Quantity;

                            branchItem.Quantity = RevertedQuantity - newDetail.ItemQuantity; //Updated Quantity
                            if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to update the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (existingDetail.ItemQuantity * existingDetail.CostPerUnit) - (newDetail.ItemQuantity * newDetail.CostPerUnit)) / branchItem.Quantity; //Updated Per Unit Cost
                            }

                            _db.Entry(existingDetail).CurrentValues.SetValues(newDetail);
                            existingDetails.Remove(existingDetail); // Remove from existing list once matched
                        }
                        else
                        {
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity - newDetail.ItemQuantity; //Updated Quantity
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to update the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) - (newDetail.ItemQuantity * newDetail.CostPerUnit)) / branchItem.Quantity; //Updated Per Unit Cost
                            }
                            existingWastageItem.WastageItemDetails.Add(newDetail); // Add new detail
                        }
                    }

                    // Delete unmatched details
                    if (existingDetails != null)
                    {
                        foreach (var item in existingDetails)
                        {
                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == wastageItem.BranchId).FirstOrDefault();
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to update the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                            }
                        }
                        _db.WastageItemDetails.RemoveRange(existingDetails);
                    }


                    // Save changes
                    _db.SaveChanges();
                    transaction.Commit();

                    TempData["SuccessMessage"] = "Wastage record updated successfully.";
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

                    TempData["ErrorMessage"] = "An error occurred while updating the wastage record.";
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

                if (wastageItemDetails != null)
                {
                    foreach (var item in wastageItemDetails)
                    {
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == wastageItem.BranchId).FirstOrDefault();
                        decimal currentQuantity = branchItem.Quantity;
                        branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                        if (branchItem.Quantity == 0)
                        {
                            branchItem.CostPerUnit = 0;
                        }
                        else if (branchItem.Quantity < 0)
                        {
                            TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to delete the record.";
                            return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                        }
                        else
                        {
                            branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                        }
                    }
                }

                _db.WastageItemDetails.RemoveRange(wastageItemDetails);
                _db.WastageItems.Remove(wastageItem);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Wastage item record deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This wastage item record is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the wastage item record.";
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

                    if (wastageItemDetails != null)
                    {
                        foreach (var item in wastageItemDetails)
                        {
                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == item.WastageItem.BranchId).FirstOrDefault();
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to delete the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                            }
                        }
                    }

                    _db.WastageItemDetails.RemoveRange(wastageItemDetails);
                    _db.WastageItems.RemoveRange(wastageItemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Wastage item record deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A wastage item record is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the wastage item records.";
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
                IsActive = true,
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
            var product = _db.Products.AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {

                product = product.Where(c => (c.ProductItemDetails.Where(i => i.Product.ProductName.Contains(search)).Count() > 0 || c.ProductCategory.ProductCategoryName.Contains(search)));
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
        public ActionResult OrderList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var orders = _db.Orders.Where(o => o.BranchId == selectedBranchId);
            if (!string.IsNullOrEmpty(search))
            {
                orders = orders.Where(c => c.OrderNo.Contains(search) ||
                                           c.OrderTotal.ToString().Equals(search) ||
                                           c.Status.Contains(search));
            }
            return View(orders.ToList());
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
        public ActionResult CreateOrder(Order order, decimal grandTotal)
        {
            var branchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            try
            {
                if (ModelState.IsValid)
                {
                    order.OrderTotalCost = 0;
                    foreach (var orderDetail in order.OrderDetails)
                    {
                        orderDetail.ProductCost = 0;
                        var orderDetailProduct = _db.Products.Find(orderDetail.ProductId);
                        foreach (var item in orderDetailProduct.ProductItemDetails)
                        {
                            var itemQuantityToLess = item.ItemQuantity * orderDetail.ProductQuantity;
                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == order.BranchId).FirstOrDefault();
                            if (branchItem != null)
                            {
                                if (branchItem.Quantity >= itemQuantityToLess)
                                {
                                    branchItem.Quantity -= itemQuantityToLess;
                                    orderDetail.ProductCost += (branchItem.CostPerUnit * itemQuantityToLess);
                                    // Mark the branchItem as modified
                                    _db.Entry(branchItem).State = EntityState.Modified;
                                }
                                else
                                {
                                    TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to prepare " + orderDetailProduct.ProductName;
                                    return RedirectToAction("OrderList");
                                }
                            }
                        }
                        order.OrderTotalCost += orderDetail.ProductCost;
                    }

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
                    order.OrderNo = nextOrderNumber.ToString("D5");
                    order.BranchId = branchId;
                    order.OrderTotal = grandTotal;
                    order.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    order.OrderDate = DateTime.Now;
                    order.CreatedAt = DateTime.Now;
                    order.Status = "Preparing";

                    _db.Orders.Add(order);
                    _db.SaveChanges();

                    TempData["SuccessMessage"] = "Order placed successfully.";
                    return RedirectToAction("CreateOrder", "Kitchen");
                }
                //else
                //{
                //    // Log ModelState errors
                //    foreach (var state in ModelState)
                //    {
                //        var key = state.Key; // Property name
                //        var errors = state.Value.Errors; // List of errors for this property

                //        foreach (var error in errors)
                //        {
                //            System.Diagnostics.Debug.WriteLine($"Key: {key}, Error: {error.ErrorMessage}");
                //        }
                //    }

                //    TempData["ErrorMessage"] = "There were validation errors. Please check the input values.";
                //}
                ViewBag.Products = _db.Products.Where(i => i.IsActive && i.BranchId == branchId).ToList();
                return View(order);
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                Console.WriteLine($"Error creating order: {ex.Message}");
                return RedirectToAction("Index", "Error");
            }
        }
        [HttpPost]
        public ActionResult CancelOrder(int orderId)
        {
            try
            {
                var order = _db.Orders.Find(orderId);
                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToAction("OrderList");
                }

                if (order.Status == "Cancelled")
                {
                    TempData["ErrorMessage"] = "Order is already cancelled.";
                }
                else if (order.Status == "Pending")
                {
                    order.Status = "Cancelled";
                    TempData["SuccessMessage"] = "Order cancelled successfully.";
                }
                else if (order.Status == "Preparing" || order.Status == "Ready" || order.Status == "Out for Delivery" || order.Status == "Confirmed")
                {
                    //Revert the stock

                    foreach (var orderDetail in order.OrderDetails)
                    {
                        foreach (var item in orderDetail.Product.ProductItemDetails)
                        {
                            var itemQuantityToAdd = item.ItemQuantity * orderDetail.ProductQuantity;

                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == order.BranchId).FirstOrDefault();
                            if (branchItem != null)
                            {
                                branchItem.Quantity += itemQuantityToAdd;
                                // Mark the branchItem as modified
                                _db.Entry(branchItem).State = EntityState.Modified;
                            }
                        }
                    }

                    order.Status = "Cancelled";
                    TempData["SuccessMessage"] = "Order cancelled successfully.";
                }
                else if (order.Status == "Completed")
                {
                    TempData["ErrorMessage"] = "Completed order can not be cancelled.";
                }

                _db.SaveChanges();
                
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while cancelling the order.";
                Console.WriteLine($"Error cancelling order: {ex.Message}");
            }

            return RedirectToAction("OrderList");
        }

        [HttpPost]
        public ActionResult ConfirmOrder(int orderId)
        {
            try
            {
                var order = _db.Orders.Find(orderId);
                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToAction("OrderList");
                }
                order.OrderTotalCost = 0;
                if (order.Status == "Cancelled")
                {
                    TempData["ErrorMessage"] = "Can not confirm the cancelled order.";
                    return RedirectToAction("OrderList");
                }
                foreach (var orderDetail in order.OrderDetails)
                {
                    orderDetail.ProductCost = 0;
                    foreach (var item in orderDetail.Product.ProductItemDetails)
                    {
                        var itemQuantityToLess = item.ItemQuantity * orderDetail.ProductQuantity;
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == order.BranchId).FirstOrDefault();
                        if (branchItem != null)
                        {
                            if (branchItem.Quantity >= itemQuantityToLess)
                            {
                                branchItem.Quantity -= itemQuantityToLess;
                                orderDetail.ProductCost += (branchItem.CostPerUnit * itemQuantityToLess);
                                // Mark the branchItem as modified
                                _db.Entry(branchItem).State = EntityState.Modified;
                            }
                            else
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to prepare " + orderDetail.Product.ProductName;
                                return RedirectToAction("OrderList");
                            }
                        }
                    }
                    order.OrderTotalCost += orderDetail.ProductCost;
                }
                order.Status = "Preparing";
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Order confirmed successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while confirming the order.";
                Console.WriteLine($"Error confirming order: {ex.Message}");
            }

            return RedirectToAction("OrderList");
        }

        [HttpPost]
        public ActionResult CompleteOrder(int orderId)
        {
            try
            {
                var order = _db.Orders.Find(orderId);
                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToAction("OrderList");
                }

                if (order.Status == "Cancelled")
                {
                    TempData["ErrorMessage"] = "Unable to update the status for cancelled order.";
                }
                else if (order.Status == "Pending")
                {
                    TempData["ErrorMessage"] = "Unable to update the status for Pending order.";
                }
                else if (order.Status == "Preparing" || order.Status == "Ready" || order.Status == "Out for Delivery")
                {
                    order.Status = "Completed";
                    TempData["SuccessMessage"] = "Order completed successfully.";
                }
                else if (order.Status == "Completed")
                {
                    TempData["ErrorMessage"] = "The order is already completed.";
                }

                _db.SaveChanges();

            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the order status.";
                Console.WriteLine($"Error updating order status: {ex.Message}");
            }

            return RedirectToAction("OrderList");
        }

        [HttpPost]
        public ActionResult OutForDelivery(int orderId)
        {
            try
            {
                var order = _db.Orders.Find(orderId);
                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToAction("OrderList");
                }

                if (order.Status == "Cancelled")
                {
                    TempData["ErrorMessage"] = "Unable to update the status for cancelled order.";
                }
                else if (order.Status == "Pending")
                {
                    TempData["ErrorMessage"] = "Unable to update the status for Pending order.";
                }
                else if (order.Status == "Preparing" || order.Status == "Ready")
                {
                    order.Status = "Out For Delivery";
                    TempData["SuccessMessage"] = "Order marked as Out for Delivery.";
                }
                else if (order.Status == "Completed")
                {
                    TempData["ErrorMessage"] = "Unable to update the status for completed order.";
                }

                _db.SaveChanges();

            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the order status.";
                Console.WriteLine($"Error updating order status: {ex.Message}");
            }

            return RedirectToAction("OrderList");
        }

        [HttpPost]
        public ActionResult CancelSelectedOrders(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var ordersToCancel = _db.Orders.Where(o => selectedItems.Contains(o.OrderId)).ToList();

                    foreach (var order in ordersToCancel)
                    {
                        if (order.Status == "Cancelled")
                        {
                            TempData["ErrorMessage"] = "An order exist which is already cancelled.";
                            return RedirectToAction("OrderList");

                        }
                        else if (order.Status == "Pending")
                        {
                            order.Status = "Cancelled";
                        }
                        else if (order.Status == "Preparing" || order.Status == "Ready" || order.Status == "Out for Delivery" || order.Status == "Confirmed")
                        {
                            //Revert the stock

                            foreach (var orderDetail in order.OrderDetails)
                            {
                                foreach (var item in orderDetail.Product.ProductItemDetails)
                                {
                                    var itemQuantityToAdd = item.ItemQuantity * orderDetail.ProductQuantity;

                                    var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == order.BranchId).FirstOrDefault();
                                    if (branchItem != null)
                                    {
                                        branchItem.Quantity += itemQuantityToAdd;
                                        // Mark the branchItem as modified
                                        _db.Entry(branchItem).State = EntityState.Modified;
                                    }
                                }
                            }

                            order.Status = "Cancelled";
                        }
                        else if (order.Status == "Completed")
                        {
                            TempData["ErrorMessage"] = "An order exist which is completed and can not be cancelled.";
                            return RedirectToAction("OrderList");
                        }
                    }

                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Selected orders have been cancelled successfully.";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while cancelling the selected orders.";
                    Console.WriteLine($"Error cancelling selected orders: {ex.Message}");
                }
            }
            else
            {
                TempData["ErrorMessage"] = "No orders selected for cancellation.";
            }

            return RedirectToAction("OrderList");
        }

        [HttpPost]
        public ActionResult ConfirmSelectedOrders(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var ordersToConfirm = _db.Orders.Where(o => selectedItems.Contains(o.OrderId)).ToList();

                    foreach (var order in ordersToConfirm)
                    {
                        if (order.Status == "Cancelled")
                        {
                            TempData["ErrorMessage"] = "Can not confirm the order. An order exist which is cancelled.";
                            return RedirectToAction("OrderList");
                        }
                        order.OrderTotalCost = 0;
                        foreach (var orderDetail in order.OrderDetails)
                        {
                            orderDetail.ProductCost = 0;
                            foreach (var item in orderDetail.Product.ProductItemDetails)
                            {
                                var itemQuantityToLess = item.ItemQuantity * orderDetail.ProductQuantity;
                                var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == order.BranchId).FirstOrDefault();
                                if (branchItem != null)
                                {
                                    if (branchItem.Quantity >= itemQuantityToLess)
                                    {
                                        branchItem.Quantity -= itemQuantityToLess;
                                        orderDetail.ProductCost += (branchItem.CostPerUnit * itemQuantityToLess);
                                        // Mark the branchItem as modified
                                        _db.Entry(branchItem).State = EntityState.Modified;
                                    }
                                    else
                                    {
                                        TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to prepare " + orderDetail.Product.ProductName;
                                        return RedirectToAction("OrderList");
                                    }
                                }
                            }
                            order.OrderTotalCost += orderDetail.ProductCost;
                        }
                        order.Status = "Preparing";
                    }

                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Selected orders have been confirmed successfully.";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while confirming the selected orders.";
                    Console.WriteLine($"Error confirming selected orders: {ex.Message}");
                }
            }
            else
            {
                TempData["ErrorMessage"] = "No orders selected for confirmation.";
            }

            return RedirectToAction("OrderList");
        }

        public ActionResult OrderDetail(int orderId)
        {
            var order = _db.Orders.Include(o => o.OrderDetails.Select(od => od.Product)).FirstOrDefault(o => o.OrderId == orderId);
            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToAction("OrderList");
            }
            return View(order);
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
            //Convert.ToInt32(Helper.GetUserInfo("branchId"));
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