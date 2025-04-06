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