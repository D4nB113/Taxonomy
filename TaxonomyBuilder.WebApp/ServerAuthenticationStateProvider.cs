using System.Security.Claims;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace TaxonomyBuilder.WebApp;

public sealed class ServerAuthenticationStateProvider(HttpClient httpClient) : AuthenticationStateProvider
{
	private static readonly AuthenticationState AnonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));

	public override async Task<AuthenticationState> GetAuthenticationStateAsync()
	{
		try
		{
			var identity = await httpClient.GetFromJsonAsync<AuthenticatedUser>("auth/me");
			if (identity is null)
				return AnonymousState;

			var claims = new List<Claim>();
			if (!string.IsNullOrWhiteSpace(identity.Name))
				claims.Add(new Claim(ClaimTypes.Name, identity.Name));
			if (!string.IsNullOrWhiteSpace(identity.Email))
				claims.Add(new Claim(ClaimTypes.Email, identity.Email));

			return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "ServerCookie")));
		}
		catch (HttpRequestException)
		{
			return AnonymousState;
		}
	}

	private sealed record AuthenticatedUser(string? Name, string? Email);
}