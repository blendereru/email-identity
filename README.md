# email-identity
The project is intended to understand the way asp.net core identity works. The general idea is following:
1) User goes by default path "/"(e.g "Account/Login") where he enters his login and password
2) If user doesn't have account in the system he clicks the button for registering
3) Also can use google to authenticate to the system.

## How does asp.net core identity work ?
First of all, AddIdentity<TUser, TRole> method has the following implementation
 ```csharp
 // Services used by identity
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        })
        .AddCookie(IdentityConstants.ApplicationScheme, o =>
        {
            o.LoginPath = new PathString("/Account/Login");
            o.Events = new CookieAuthenticationEvents
            {
                OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync
            };
        })
        .AddCookie(IdentityConstants.ExternalScheme, o =>
        {
            o.Cookie.Name = IdentityConstants.ExternalScheme;
            o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        })
        .AddCookie(IdentityConstants.TwoFactorRememberMeScheme, o =>
        {
            o.Cookie.Name = IdentityConstants.TwoFactorRememberMeScheme;
            o.Events = new CookieAuthenticationEvents
            {
                OnValidatePrincipal = SecurityStampValidator.ValidateAsync<ITwoFactorSecurityStampValidator>
            };
        })
        .AddCookie(IdentityConstants.TwoFactorUserIdScheme, o =>
        {
            o.Cookie.Name = IdentityConstants.TwoFactorUserIdScheme;
            o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        });
```

* DefaultAuthenticateScheme: the scheme that is used when no parameter in `AuthenticateAsync()` method is specified. `AuthenticateAsync()`
is a method that reads and validates the user's identity.
* DefaultChallengeScheme: the scheme that is used when no parameter in `ChallengeAsync()` is specified. ChallengeAsync() is a method
that prompts the user to authenticate, and for the case if the user is not, redirects him to a login page or starts an external **OAuth** flow.
* DefaultSignInScheme: the scheme that is used to sign in user, the process to determine how the user's identity is persisted.

the separate cookies specified through `.AddCookie()` serve different purposes too.
1) `.AddCookie(IdentityConstants.ApplicationScheme, o => ...)`: used to persist the user's identity after login.
2) `.AddCookie(IdentityConstants.ExternalScheme, o => ...)`: the temporary cookie that is only used during external login. It
temporarily stores the external login information while the user is redirected between your app and the external provider.

## How does identity interact in code ?
The `ExternalLogin()` method has the following implementation
```csharp
[HttpPost]
public IActionResult ExternalLogin()
{
    var redirectUrl = Url.Action("ExternalLoginCallback", "Account");
    var properties = _signInManager.ConfigureExternalAuthenticationProperties(GoogleDefaults.AuthenticationScheme, redirectUrl);
    return Challenge(properties);
}
```
When clicking "Log in using Google", controller sends the user to Google for authentication from where the user is redirected to `ExternalLoginCallback()`.
The line `var info = await _signInManager.GetExternalLoginInfoAsync();` internally calls `AuthenticateAsync()` on the **ExternalScheme**, which reads the temporary cookie set during the external login process.
Here's really important to understand how Google transports your data to our app.

* When the app initiates the login request through the Challenge method:
```csharp
var redirectUrl = Url.Action("ExternalLoginCallback", "Account");
var properties = _signInManager.ConfigureExternalAuthenticationProperties(
     GoogleDefaults.AuthenticationScheme, 
     redirectUrl);
return Challenge(properties, GoogleDefaults.AuthenticationScheme);
```
* The user logs in on Google’s login page and grants permission to share data with your app (like their email or profile information).
If the login is successful, Google redirects the user back to your callback URL with an authorization code or ID token.
* Google sends a response back to your app's callback URL with query parameters like:
  > https://localhost:<your_port>/Account/ExternalLoginCallback?code=xyz&state=abc

  The authorization code in the URL is proof that Google successfully authenticated the user.
* In your ExternalLoginCallback method, the ASP.NET Core authentication middleware uses the authorization code to contact Google and retrieve the user’s profile information (like their email, name, etc.).
  This exchange happens behind the scenes, and ASP.NET Core doesn't expose this part to you directly.
* The Google authentication middleware:

       1) Uses the authorization code to request an access token from Google.
       2) Uses the access token to request the user’s profile data (like email, name, and other claims).
* The user’s claims (like their email and name) are wrapped into an ExternalLoginInfo object by the middleware. The ExternalScheme cookie is created at this point to temporarily store the user’s claims until the final sign-in step.

Another unfamiliar method is `await _signInManager.ExternalLoginSignInAsync();`
The method looks for a record in the AspNetUserLogins table where the LoginProvider and ProviderKey match the ones provided.
If found, it associates the external provider with a local user account (from the AspNetUsers table).
If the external login is valid (i.e., the user exists in the database with the same provider and key), it signs the user in.
If 2FA is enabled for the user and bypassTwoFactor is set to false, the user will be asked to provide a second factor (e.g., SMS code, authenticator app).
If bypassTwoFactor is true, it skips this step.
Then, the user is signed in by creating an authentication cookie, which maintains the user’s login session.

# ToDos
1. [ ] Add email validation system
2. [ ] Use MassTransit producer-consumer to send email
3. [ ] Use Hangfire to schedule emails 
4. [ ] Fix the issue of user's being unable to redirect to google's page for authentication



