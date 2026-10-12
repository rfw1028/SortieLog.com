using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.SignalR;
using FlightLogs.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddSignalR();
// Register HttpClient service
builder.Services.AddHttpClient();
// Add session services
builder.Services.AddDistributedMemoryCache(); // Required for session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(90);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie()
    .AddGoogle(googleOptions =>
    {
        googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"];
        googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
        googleOptions.Scope.Add("email");
        googleOptions.Scope.Add("profile");

        googleOptions.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                var identity = (ClaimsIdentity)context.Principal.Identity;

                var firstName = context.User.GetProperty("given_name").GetString();
                var lastName = context.User.GetProperty("family_name").GetString();
                var email = context.User.GetProperty("email").GetString();

                identity.AddClaim(new Claim(ClaimTypes.GivenName, firstName ?? ""));
                identity.AddClaim(new Claim(ClaimTypes.Surname, lastName ?? ""));
                identity.AddClaim(new Claim(ClaimTypes.Email, email ?? ""));

                // Optional: store in session or cookie
                var httpContext = context.HttpContext;
                httpContext.Session.SetString("FirstName", firstName ?? "");
                httpContext.Session.SetString("LastName", lastName ?? "");
                httpContext.Session.SetString("Email", email ?? "");

                await Task.CompletedTask;
            }
        };
    })


.AddMicrosoftAccount(microsoftOptions =>
 {
     microsoftOptions.ClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
     microsoftOptions.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
     microsoftOptions.Scope.Add("email");
     microsoftOptions.Scope.Add("profile");
     microsoftOptions.Scope.Add("openid");


     microsoftOptions.Events = new OAuthEvents
     {
         OnCreatingTicket = async context =>
         {
             var identity = (ClaimsIdentity)context.Principal.Identity;
             
             var user = context.User;

             var firstName = user.TryGetProperty("givenName", out var givenNameProp)
                        ? givenNameProp.GetString()
                        : null;

             var lastName = user.TryGetProperty("surname", out var surnameProp)
                             ? surnameProp.GetString()
                             : null;

             var email = user.TryGetProperty("mail", out var mailProp)
                             ? mailProp.GetString()
                             : (user.TryGetProperty("userPrincipalName", out var upnProp)
                                 ? upnProp.GetString()
                                 : null);

             identity.AddClaim(new Claim(ClaimTypes.GivenName, firstName ?? ""));
             identity.AddClaim(new Claim(ClaimTypes.Surname, lastName ?? ""));
             identity.AddClaim(new Claim(ClaimTypes.Email, email ?? ""));

             var httpContext = context.HttpContext;
             httpContext.Session.SetString("FirstName", firstName ?? "");
             httpContext.Session.SetString("LastName", lastName ?? "");
             httpContext.Session.SetString("Email", email ?? "");

             await Task.CompletedTask;
         }
     };
 });


var app = builder.Build();
app.UseRouting();
app.UseSession(); // Enable session middleware

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();
app.MapHub<ProgressHub>("/progressHub");


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();

public class ProgressHub : Hub
{   
}