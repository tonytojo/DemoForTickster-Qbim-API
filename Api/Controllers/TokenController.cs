using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QbimApi.Models;
using Microsoft.AspNetCore.Authorization;

namespace QbimApi.Controllers
{
    /// <summary>
    /// This class represent the helper for TokenController.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        private readonly IJwtTokenManager _tokenManager;

        /// <summary>
        /// Using dependency injection
        /// </summary>
        /// <param name="jwtTokenManager">An instansiated jwtTokenManager object</param>
        public TokenController(IJwtTokenManager jwtTokenManager)
        {
            _tokenManager = jwtTokenManager;
        }

        /// <summary>
        /// Accessed by https://domän/api/Token/Authenticate
        /// If passing correct credentials a valid token will be returned
        /// </summary>
        /// <param name="credential">The username,password and clientSecret from body</param>
        /// <returns> A token is returned if valid username and password else we return Unauthorized </returns>
        [AllowAnonymous]
        [HttpPost("Authenticate")]
        public IActionResult Authenticate([FromBody] UserCredential credential)
        {
            var token = _tokenManager.Authenticate(credential.UserName, credential.Password, credential.ClientSecret);
            if (string.IsNullOrEmpty(token))
                return Unauthorized();
            else
                return Ok(token);
        }
    }
}