using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QbimApi.Models
{
    public class UserCredential
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string ClientSecret { get; set; }
    }
}
