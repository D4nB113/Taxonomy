using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

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

var authClientId = builder.Configuration["Authentication:ClientId"] ?? builder.Configuration["AuthenticationClientId"] ?? "not-configured";
var authClientSecret = builder.Configuration["Authentication:ClientSecret"] ?? builder.Configuration["AuthenticationClientSecret"] ?? string.Empty;
var authAuthority = builder.Configuration["Authentication:Authority"] ?? builder.Configuration["AuthenticationAuthority"] ?? "https://github.com/login";
var authLoginUrl = builder.Configuration["Authentication:LoginUrl"] ?? builder.Configuration["AuthenticationLoginUrl"] ?? "https://github.com/login";

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
.AddOAuth("github", options =>
{
	options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
	options.TokenEndpoint = "https://github.com/login/oauth/access_token";
	options.UserInformationEndpoint = "https://api.github.com/user";
	options.CallbackPath = "/signin-github";
	options.ClientId = authClientId;
	options.ClientSecret = authClientSecret;
	options.SaveTokens = false;
	options.Scope.Clear();
	options.Scope.Add("read:user");
	options.Scope.Add("user:email");
	options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
	options.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
	options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
	options.Events = new OAuthEvents
	{
		OnCreatingTicket = async context =>
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

			using var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
			response.EnsureSuccessStatusCode();

			await using var stream = await response.Content.ReadAsStreamAsync(context.HttpContext.RequestAborted);
			using var json = await JsonDocument.ParseAsync(stream, cancellationToken: context.HttpContext.RequestAborted);
			context.RunClaimActions(json.RootElement);

			if (!context.Identity!.HasClaim(c => c.Type == ClaimTypes.Email))
			{
				using var emailRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
				emailRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				emailRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

				using var emailResponse = await context.Backchannel.SendAsync(emailRequest, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
				emailResponse.EnsureSuccessStatusCode();
				await using var emailStream = await emailResponse.Content.ReadAsStreamAsync(context.HttpContext.RequestAborted);
				using var emailJson = await JsonDocument.ParseAsync(emailStream, cancellationToken: context.HttpContext.RequestAborted);
				var primaryEmail = emailJson.RootElement.EnumerateArray()
					.FirstOrDefault(entry => entry.TryGetProperty("primary", out var primary) && primary.GetBoolean())
					.TryGetProperty("email", out var emailProperty) ? emailProperty.GetString() : null;

				if (!string.IsNullOrWhiteSpace(primaryEmail))
				{
					context.Identity.AddClaim(new Claim(ClaimTypes.Email, primaryEmail));
				}
			}
		}
	};
});

builder.Services.AddAuthorization();

var app = builder.Build();
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/auth/login", async (HttpContext context, string? returnUrl) =>
{
	var githubClientId = builder.Configuration["Authentication:ClientId"] ?? builder.Configuration["AuthenticationClientId"];
	var githubClientSecret = builder.Configuration["Authentication:ClientSecret"] ?? builder.Configuration["AuthenticationClientSecret"];
	if (string.IsNullOrWhiteSpace(githubClientId) || string.IsNullOrWhiteSpace(githubClientSecret))
	{
		context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
		await context.Response.WriteAsync("Sign-in is not configured. Set Authentication__ClientId and Authentication__ClientSecret in the server environment.");
		return;
	}

	var destination = IsLocalReturnUrl(returnUrl) ? returnUrl! : "/dashboard";
	await context.ChallengeAsync("github", new AuthenticationProperties { RedirectUri = destination });
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
	var loginUrl = builder.Configuration["Authentication:LoginUrl"] ?? builder.Configuration["AuthenticationLoginUrl"] ?? "https://github.com/login";
	context.Response.Redirect(loginUrl);
}).AllowAnonymous();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapStaticAssets();
app.MapFallbackToFile("index.html");

app.Run();

static bool IsLocalReturnUrl(string? returnUrl) =>
	!string.IsNullOrWhiteSpace(returnUrl)
	&& returnUrl.StartsWith('/')
	&& !returnUrl.StartsWith("//", StringComparison.Ordinal)
	&& !returnUrl.Contains('\\')
	&& !Uri.TryCreate(returnUrl, UriKind.Absolute, out _);