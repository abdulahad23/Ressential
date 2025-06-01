using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Ressential.Models;
using Ressential.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Ressential.Hub
{
    [HubName("ressentialHub")]
    public class RessentialHub : Microsoft.AspNet.SignalR.Hub
    {
        
        private readonly DB_RessentialEntities _db;
        public RessentialHub()
        {
            _db = new DB_RessentialEntities();
        }
        public void SendProductUpdate()
        {
            Clients.All.ReceiveProductUpdate();
        }

        // Method to send stock alert notifications to a specific user
        public void SendStockAlert(int userId, string title, string message, int notificationId)
        {
            var user = _db.Users.FirstOrDefault(u => u.UserId == userId);
            if (user != null && !string.IsNullOrEmpty(user.ConnectionId))
            {
                // Add a small delay to ensure database changes are saved
                Task.Delay(100).ContinueWith(_ =>
                {
                    Clients.Client(user.ConnectionId).receiveStockAlert(title, message, notificationId);
                });
            }
        }

        // Method to notify ChefView of new orders or order updates
        public void NotifyChefView(int branchId)
        {
            var branchUsers = _db.Users
                .Where(u => _db.UserBranchPermissions
                    .Any(ubp => ubp.UserId == u.UserId && ubp.BranchId == branchId))
                .ToList();

            foreach (var user in branchUsers.Where(u => !string.IsNullOrEmpty(u.ConnectionId)))
            {
                Clients.Client(user.ConnectionId).updateChefView();
                Clients.Client(user.ConnectionId).updateOrderView();
            }
        }

        // Method to notify specific chef about their orders
        public void NotifySpecificChef(int chefId, int branchId)
        {
            var chef = _db.Users.FirstOrDefault(u => u.UserId == chefId && 
                _db.UserBranchPermissions.Any(ubp => ubp.UserId == u.UserId && ubp.BranchId == branchId));

            if (chef != null && !string.IsNullOrEmpty(chef.ConnectionId))
            {
                Clients.Client(chef.ConnectionId).updateChefView();
                Clients.Client(chef.ConnectionId).updateOrderView();
            }
        }

        // Method to notify all users in a branch about order updates
        public void NotifyOrderUpdate(int branchId)
        {
            var branchUsers = _db.Users
                .Where(u => _db.UserBranchPermissions
                    .Any(ubp => ubp.UserId == u.UserId && ubp.BranchId == branchId))
                .ToList();

            foreach (var user in branchUsers.Where(u => !string.IsNullOrEmpty(u.ConnectionId)))
            {
                Clients.Client(user.ConnectionId).updateOrderView();
            }
        }

        public override Task OnConnected()
        {
            Clients.Caller.SendAsync("OnConnected");
            return base.OnConnected();
        }

        public void SaveUserConnection()
        {
            int loginUserId = Convert.ToInt32(Helper.GetUserInfo("userId"));
            var user = _db.Users.FirstOrDefault(x => x.UserId == loginUserId);
            if (user != null)
            {
                user.ConnectionId = Context.ConnectionId;
                _db.SaveChanges();
            }
        }
    }
}