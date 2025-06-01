using System;
using System.Collections.Generic;

namespace Ressential.ViewModels
{
    public class DashboardViewModel
    {
        // Summary Cards
        public int TotalItems { get; set; }
        public int TotalVendors { get; set; }
        public int TotalBranches { get; set; }
        public int TotalUsers { get; set; }
        
        
        // Analytics
        public int UnfulfilledRequisitions { get; set; }
        public int LowStockItems { get; set; }
        public int CompletedIssues { get; set; }
        public int ReturnedStocks { get; set; }

        // Recent Activities
        public List<RecentActivityViewModel> RecentActivities { get; set; }

        // Stock Value
        public decimal TotalStockValue { get; set; }
        public List<TopItemViewModel> TopItems { get; set; }
        
        public DashboardViewModel()
        {
            RecentActivities = new List<RecentActivityViewModel>();
            TopItems = new List<TopItemViewModel>();
        }
    }

    public class RecentActivityViewModel
    {
        public string ActivityType { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        public string Status { get; set; }
    }

    public class TopItemViewModel
    {
        public string ItemName { get; set; }
        public decimal Quantity { get; set; }
        public decimal Value { get; set; }
    }
} 