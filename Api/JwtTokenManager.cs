using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Claims;

namespace QbimApi
{
    public class JwtTokenManager : IJwtTokenManager
    {
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Using dependency injection
        /// Is used for reading appsettings.json
        /// </summary>
        /// <param name="configuration">An instansiated configuration object</param>
        public JwtTokenManager(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Validate the passed userName, password and clientSecret. If valid create and return a token
        /// </summary>
        /// <param name="userName">The username from called</param>
        /// <param name="password">The password from called</param>
        /// <param name="clientSecret">The clientSecret from caller</param>
        /// <returns>If valid Credential return the token </returns>
        public string Authenticate(string userName, string password,string clientSecret)
        {
            var UserCredential = new DbApi(_configuration,null,null).GetUser(userName, password, clientSecret);

            //Validate if correct credentials has been passed
            if (!(UserCredential.UserName == userName && UserCredential.Password == password && UserCredential.ClientSecret == clientSecret))
                return null;

            var key = _configuration.GetValue<string>("JwtConfig:Key");
            var keyBytes = Encoding.ASCII.GetBytes(key);

            var tokenHandler = new JwtSecurityTokenHandler();

            //The parameter to be used to generate the token
            var tokenDescriptor = new SecurityTokenDescriptor()
            {
                Subject = new ClaimsIdentity(new Claim[] {
                    new Claim(ClaimTypes.NameIdentifier, userName)
                }),
                Expires = DateTime.UtcNow.AddDays(30),
                SigningCredentials = new SigningCredentials
                (new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
            };

            //Create and return the token
            var token = tokenHandler.CreateToken(tokenDescriptor);
            
            return tokenHandler.WriteToken(token);
        }
    }
}
