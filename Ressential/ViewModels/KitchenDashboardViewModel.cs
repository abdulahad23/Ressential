using System;
using System.Collections.Generic;

namespace Ressential.ViewModels
{
    public class KitchenDashboardViewModel
    {
        // Summary Cards
        public int TotalProducts { get; set; }
        public int TotalOrders { get; set; }
        public int TotalItems { get; set; }
        public int TotalUsers { get; set; }

        // Analytics
        public int PendingOrders { get; set; }
        public int LowStockItems { get; set; }
        public int CompletedOrders { get; set; }
        public int WastageItems { get; set; }

        // Recent Activities
        public List<KitchenActivityViewModel> RecentActivities { get; set; }

        // Top Products
        public List<TopProductViewModel> TopProducts { get; set; }

        public KitchenDashboardViewModel()
        {
            RecentActivities = new List<KitchenActivityViewModel>();
            TopProducts = new List<TopProductViewModel>();
        }
    }

    public class KitchenActivityViewModel
    {
        public string ActivityType { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        public string Status { get; set; }
    }

    public class TopProductViewModel
    {
        public string ProductName { get; set; }
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
    }
} 