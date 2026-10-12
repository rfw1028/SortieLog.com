using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

public class AccountController : Controller
{

    private readonly IConfiguration _config;

    public AccountController(IConfiguration config)
    {
        _config = config;
    }

    private SqlConnection OpenConnection()
    {
        var cs = _config.GetConnectionString("FlightLogs")
            ?? throw new InvalidOperationException("ConnectionStrings:FlightLogs is not configured. See README.md.");
        var conn = new SqlConnection(cs);
        conn.Open();
        return conn;
    }

    // Registers the pilot if no row exists yet. Fully parameterized.
    private void EnsurePilot(string? firstName, string? lastName, string? email)
    {
        using var conn = OpenConnection();

        using (var check = new SqlCommand(
            "SELECT 1 FROM dbo.Pilots WHERE [LastName] = @last AND [EmailAddress] = @email", conn))
        {
            check.Parameters.AddWithValue("@last", (object?)lastName ?? DBNull.Value);
            check.Parameters.AddWithValue("@email", (object?)email ?? DBNull.Value);
            using var rdr = check.ExecuteReader();
            if (rdr.HasRows)
            {
                return; // already registered
            }
        }

        using var insert = new SqlCommand(
            "INSERT INTO dbo.Pilots ([FirstName], [LastName], [EmailAddress]) VALUES (@first, @last, @email)", conn);
        insert.Parameters.AddWithValue("@first", (object?)firstName ?? DBNull.Value);
        insert.Parameters.AddWithValue("@last", (object?)lastName ?? DBNull.Value);
        insert.Parameters.AddWithValue("@email", (object?)email ?? DBNull.Value);
        insert.ExecuteNonQuery();
    }
    
    // Initiates Google Authentication
    public IActionResult Login()
    {
        var redirectUrl = Url.Action(nameof(GoogleResponse), "Account");
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    // Handles Google Authentication Callback
    public async Task<IActionResult> GoogleResponse()
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!authenticateResult.Succeeded)
        {
            // Authentication failed, redirect to login page
            return RedirectToAction("Index", "Home");
        }
        else
        {
            var claims = authenticateResult.Principal.Identities.FirstOrDefault()?.Claims;
            string? email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            string? name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            string? firstName = name?.Split(" ")[0];
            string? lastName = name?.Split(' ', 2).ElementAtOrDefault(1);

            HttpContext.Session.SetString("Email", email ?? "");
            HttpContext.Session.SetString("FirstName", firstName ?? "");
            HttpContext.Session.SetString("LastName", lastName ?? "");

            var test = HttpContext.Session.GetString("Email");
            Console.WriteLine("Session set: Email = " + test);

            EnsurePilot(firstName, lastName, email);
            return View("Welcome", new { Name = name, Email = email });
        }
    }

    public IActionResult LoginMicrosoft()
    {
        var redirectUrl = Url.Action(nameof(MicrosoftResponse), "Account");
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, MicrosoftAccountDefaults.AuthenticationScheme);
    }

    public async Task<IActionResult> MicrosoftResponse()
    {
        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!result.Succeeded)
        {
            return RedirectToAction("Index", "Home");
        }
        else
        {
            var claims = result.Principal.Identities.FirstOrDefault()?.Claims;
            string? email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            string? name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            string? firstName = name?.Split(" ")[0];
            string? lastName = name?.Split(' ', 2).ElementAtOrDefault(1);


            EnsurePilot(firstName, lastName, email);
            HttpContext.Session.SetString("Email", email);
            HttpContext.Session.SetString("FirstName", firstName);
            HttpContext.Session.SetString("LastName", lastName);

            return View("Welcome", new { Name = name, Email = email });
        }
    }

    // Logs the user out
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }
}
