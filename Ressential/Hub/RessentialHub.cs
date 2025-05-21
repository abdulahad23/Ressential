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