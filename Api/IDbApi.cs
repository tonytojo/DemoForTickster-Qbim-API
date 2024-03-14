using QbimApi.Models;
using System.Collections.Generic;

namespace QbimApi
{
    public interface IDbApi
    {
        public UserCredential GetUser(string UserName, string Password, string ClientSecret);
        public void GetGuestNights(string start, string end);
        public void GetLodgingOccupancy(string start, string end);
        public IEnumerable<Booked> GetCarCounter(string start, string end);
    }
}
