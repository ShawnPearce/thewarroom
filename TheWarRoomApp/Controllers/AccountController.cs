using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TheWarRoomApp.Infrastructure;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    public class AccountController : WarRoomController
    {
        private const string RegistrationIsOff = "New teams cannot be registered on this site. Ask your head coach for an account.";

        private readonly HttpClient _client;
        private readonly SiteOptions _site;

        public AccountController(IHttpClientFactory httpClientFactory, IOptions<SiteOptions> site)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
            _site = site.Value;
        }

        // GET: Account/Register
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Dashboard", "Home");
            }

            // A public demo site has registration switched off. The API refuses it too.
            if (!_site.AllowRegistration)
            {
                TempData["Error"] = RegistrationIsOff;
                return RedirectToAction(nameof(Login));
            }

            return View();
        }

        // POST: Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterTeamViewModel model)
        {
            if (ModelState.IsValid)   // check for validation errors
            {
                var dto = new RegisterTeamDto
                {
                    TeamName = model.TeamName,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    Password = model.Password
                };

                var response = await _client.PostAsJsonAsync("api/Team/register", dto);
                if (response.IsSuccessStatusCode)   // 201 Created
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResultDto>();
                    await SignInAsync(result!);

                    TempData["Message"] = $"{result!.TeamName} is registered and you are its head coach.";
                    return RedirectToAction("Dashboard", "Home");
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 email already used
                {
                    ModelState.AddModelError("Email", await response.Content.ReadAsStringAsync());
                }
                else if (response.StatusCode == HttpStatusCode.Forbidden)   // 403 registration is switched off on this site
                {
                    ModelState.AddModelError(string.Empty, await response.Content.ReadAsStringAsync());
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "An error occurred while registering the team.");
                }
            }

            return View(model);   // Register View with errors
        }

        // GET: Account/Login
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Dashboard", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDto login, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var response = await _client.PostAsJsonAsync("api/Auth/login", login);
                if (response.IsSuccessStatusCode)   // 200 OK
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResultDto>();
                    await SignInAsync(result!);

                    // Go back to the page that asked for the sign-in, but only if it is a page of this site
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }

                    return RedirectToAction("Dashboard", "Home");
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)   // 401 wrong email or password
                {
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                }
                else if (response.StatusCode == HttpStatusCode.Forbidden)   // 403 account deactivated
                {
                    ModelState.AddModelError(string.Empty, await response.Content.ReadAsStringAsync());
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "An error occurred while signing in.");
                }
            }

            return View(login);   // Login View with errors
        }

        // POST: Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Tell the API the session has ended, then remove the login cookie
            try
            {
                await _client.PostAsync("api/Auth/logout", null);
            }
            catch (HttpRequestException)
            {
                // The API is not running. Signing out of this site must still work.
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
        }

        // GET: Account/ChangePassword
        public IActionResult ChangePassword()
        {
            return View();
        }

        // POST: Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var dto = new ChangePasswordDto
                {
                    CurrentPassword = model.CurrentPassword,
                    NewPassword = model.NewPassword
                };

                var response = await _client.PostAsJsonAsync("api/Auth/change-password", dto);
                if (response.IsSuccessStatusCode)   // 204 No Content
                {
                    // The cookie still says the password must be changed, so it is issued again without that
                    await RefreshSignInAfterPasswordChangeAsync();

                    TempData["Message"] = "Your password has been changed.";
                    return RedirectToAction("Dashboard", "Home");
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 wrong current password, or same as the old one
                {
                    ModelState.AddModelError(string.Empty, await response.Content.ReadAsStringAsync());
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)   // 401
                {
                    return await ApiFailureAsync(response);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "An error occurred while changing the password.");
                }
            }

            return View(model);   // ChangePassword View with errors
        }

        // GET: Account/AccessDenied
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // Creates the login cookie from what the API returned
        private async Task SignInAsync(LoginResultDto result)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, result.UserId.ToString()),
                new Claim(ClaimTypes.Name, $"{result.FirstName} {result.LastName}"),
                new Claim(ClaimTypes.Email, result.Email),
                new Claim(ClaimTypes.Role, result.Role.ToString()),
                new Claim(WarRoomClaimTypes.TeamId, result.TeamId.ToString()),
                new Claim(WarRoomClaimTypes.TeamName, result.TeamName),
                new Claim(WarRoomClaimTypes.SessionToken, result.SessionToken),
                new Claim(WarRoomClaimTypes.MustChangePassword, result.MustChangePassword ? "true" : "false")
            };

            if (result.CoordinatorUnit != null)
            {
                claims.Add(new Claim(WarRoomClaimTypes.CoordinatorUnit, result.CoordinatorUnit.Value.ToString()));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }

        private async Task RefreshSignInAfterPasswordChangeAsync()
        {
            var claims = User.Claims
                .Where(c => c.Type != WarRoomClaimTypes.MustChangePassword)
                .ToList();
            claims.Add(new Claim(WarRoomClaimTypes.MustChangePassword, "false"));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }
    }
}
