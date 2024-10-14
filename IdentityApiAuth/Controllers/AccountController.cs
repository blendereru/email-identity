using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Web;
using Hangfire;
using IdentityApiAuth.Models;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IdentityApiAuth.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly IEmailSender _emailSender;
    public AccountController(UserManager<IdentityUser> userManager, 
        SignInManager<IdentityUser> signInManager, IEmailSender emailSender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender  = emailSender;
    }
    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register([FromForm] RegisterModel model)
    {
        if (ModelState.IsValid)
        {
            var user = new IdentityUser()
            {
                UserName = model.Email,
                Email = model.Email
            };
            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await SendConfirmationEmailAsync(user);
                await _signInManager.PasswordSignInAsync(user, model.Password!, true, false);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login([FromForm] LoginModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, false);
            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Home");
            }
            ModelState.AddModelError(string.Empty, "Invalid login attempt");
            return View(model);
        }

        return View(model);
    }
    [HttpPost]
    public IActionResult ExternalLogin()
    {
        var redirectUrl = Url.Action("ExternalLoginCallback", "Account");
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(GoogleDefaults.AuthenticationScheme, redirectUrl);
        return new ChallengeResult(GoogleDefaults.AuthenticationScheme, properties);
    }
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string? remoteError)
    {
        if (remoteError != null)
        {
            ModelState.AddModelError(string.Empty, $"Error from external provider: {remoteError}");
            return View("Login");
        }
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            ModelState.AddModelError(string.Empty, "Error loading external login information.");
            return View("Login");
        }

        var signInResult =
            await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey,
                false, true);
        if (signInResult.Succeeded)
        {
            return RedirectToAction("Index", "Home");
        }
        else
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (email != null)
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new IdentityUser()
                    {
                        UserName = info.Principal.FindFirstValue(ClaimTypes.Email),
                        Email = info.Principal.FindFirstValue(ClaimTypes.Email)
                    };
                    await _userManager.CreateAsync(user);
                }

                await _userManager.AddLoginAsync(user, info);
                await _signInManager.SignInAsync(user, false);
                return RedirectToAction("Index", "Home");
            }
        }

        return NotFound();
    }
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        if (userId == null || token == null)
        {
            ViewBag.Message = "The link is Invalid or Expired";
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound("The user is not found");
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (result.Succeeded)
        {
            ViewBag.Message = "Thank you for confirming your email";
            return View();
        }
        ViewBag.Message = "Email cannot be confirmed";
        return View();
    }
    [NonAction]
    private async Task SendConfirmationEmailAsync(IdentityUser user)
    {
        ArgumentNullException.ThrowIfNull(user,nameof(user));
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationLink = Url.Action("ConfirmEmail", "Account",
            new { userId = user.Id, token = token }, protocol: HttpContext.Request.Scheme);
        var body = $"Please confirm your email by <a href='{HtmlEncoder.Default.Encode(confirmationLink)}'>clicking here</a>.";
        await _emailSender.SendEmailAsync(user.Email!, "Confirm your email", body, true);
    }

}