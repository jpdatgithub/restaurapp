using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Restaurapp.BlazorServer.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AuthController(SignInManager<ApplicationUser> signInManager)
        {
            _signInManager = signInManager;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _signInManager.PasswordSignInAsync(
                request.Email,
                request.Senha,
                request.LembrarMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                return Ok(new { success = true });
            }
            else if (result.IsLockedOut)
            {
                return BadRequest(new { message = "Sua conta foi bloqueada. Tente novamente mais tarde." });
            }
            else
            {
                return BadRequest(new { message = "Email ou senha inválidos." });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok(new { success = true });
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
        public bool LembrarMe { get; set; }
    }
}
