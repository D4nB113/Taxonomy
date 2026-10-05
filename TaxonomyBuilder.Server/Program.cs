using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);
var railwayPort = Environment.GetEnvironmentVariable("PORT");
if (int.TryParse(railwayPort, out var port))
	builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
	options.KnownIPNetworks.Clear();
	options.KnownProxies.Clear();
});

var dataProtection = builder.Services.AddDataProtection().SetApplicationName("TaxonomyBuilder");
var dataProtectionPath = builder.Configuration["DataProtection:KeysPath"]
	?? (builder.Environment.IsProduction() ? "/data/dp-keys" : null);
if (!string.IsNullOrWhiteSpace(dataProtectionPath))
{
	Directory.CreateDirectory(dataProtectionPath);
	dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
}

builder.Services.AddAuthentication(options =>
{
	options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
	options.Cookie.Name = "__Host-Taxonomy.Session";
	options.Cookie.Path = "/";
	options.Cookie.HttpOnly = true;
	options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
	options.Cookie.SameSite = SameSiteMode.Lax;
	options.ExpireTimeSpan = TimeSpan.FromHours(8);
	options.SlidingExpiration = true;
	options.Events.OnRedirectToLogin = context =>
	{
		if (context.Request.Path.StartsWithSegments("/auth"))
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return Task.CompletedTask;
		}

		context.Response.Redirect(context.RedirectUri);
		return Task.CompletedTask;
	};
})
.AddOpenIdConnect("oidc", options =>
{
	options.Authority = builder.Configuration["Authentication:Authority"] ?? "https://login.ontorious.co.uk";
	options.ClientId = builder.Configuration["Authentication:ClientId"] ?? "not-configured";
	options.ClientSecret = builder.Configuration["Authentication:ClientSecret"] ?? string.Empty;
	options.ResponseType = OpenIdConnectResponseType.Code;
	options.UsePkce = true;
	options.SaveTokens = false;
	options.GetClaimsFromUserInfoEndpoint = true;
	options.Scope.Clear();
	options.Scope.Add("openid");
	options.Scope.Add("profile");
	options.Scope.Add("email");
	options.CallbackPath = "/signin-oidc";
});

builder.Services.AddAuthorization();

var app = builder.Build();
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/auth/login", async (HttpContext context, string? returnUrl) =>
{
	if (string.IsNullOrWhiteSpace(builder.Configuration["Authentication:ClientId"])
		|| string.IsNullOrWhiteSpace(builder.Configuration["Authentication:ClientSecret"]))
	{
		context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
		await context.Response.WriteAsync("Sign-in is not configured. Set Authentication__ClientId and Authentication__ClientSecret in the server environment.");
		return;
	}

	var destination = IsLocalReturnUrl(returnUrl) ? returnUrl! : "/dashboard";
	await context.ChallengeAsync("oidc", new AuthenticationProperties { RedirectUri = destination });
}).AllowAnonymous();

app.MapGet("/auth/me", (ClaimsPrincipal user) => Results.Json(new
{
	name = user.FindFirst(ClaimTypes.Name)?.Value ?? user.FindFirst("name")?.Value,
	email = user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value
}))
.RequireAuthorization();

app.MapGet("/auth/logout", async (HttpContext context) =>
{
	await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
	var loginUrl = builder.Configuration["Authentication:LoginUrl"] ?? "https://login.ontorious.co.uk";
	context.Response.Redirect(loginUrl);
}).AllowAnonymous();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapFallbackToFile("index.html");

app.Run();

static bool IsLocalReturnUrl(string? returnUrl) =>
	!string.IsNullOrWhiteSpace(returnUrl)
	&& returnUrl.StartsWith('/')
	&& !returnUrl.StartsWith("//", StringComparison.Ordinal)
	&& !returnUrl.Contains('\\')
	&& !Uri.TryCreate(returnUrl, UriKind.Absolute, out _);