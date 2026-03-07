
using Extensions.Domain.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Extensions.Infrastructure.Security;

public static class AuthExtensions
{
	private const string Audience = "account";
	private const string Scheme = JwtBearerDefaults.AuthenticationScheme;

	public static class Policies
	{
		public const string RequireAdmin = nameof(RequireAdmin);
		public const string RequireModerator = nameof(RequireModerator);
		public const string RequireUser = nameof(RequireUser);
		public const string RequireAdminOrModerator = nameof(RequireAdminOrModerator);
		public const string RequireAnyRole = nameof(RequireAnyRole);
	}

	internal static IServiceCollection AddAuth(
		this IServiceCollection services,
		IConfigurationSection keycloakSection)
	{
		services
			.AddOptions<KeycloakOptions>()
			.Bind(keycloakSection)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		var keycloakOptions = keycloakSection.Get<KeycloakOptions>()!;

		services
			.AddAuthentication(Scheme)
			.AddKeycloakJwtBearer(
				serviceName: "keycloak",
				realm: keycloakOptions.Realm,
				configureOptions: options =>
				{
					options.Authority = keycloakOptions.AuthorityUrl;
					options.Audience = Audience;
					options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;
					options.TokenValidationParameters = new TokenValidationParameters
					{
						ValidateIssuer = true,
						ValidateAudience = true,
						ValidateLifetime = true,
						ValidateIssuerSigningKey = true,
						ValidIssuer = keycloakOptions.AuthorityUrl,
						ValidAudience = Audience,
						ClockSkew = TokenValidationParameters.DefaultClockSkew,
						RoleClaimType = System.Security.Claims.ClaimTypes.Role,
					};
				});

		// Register claims transformation to map Keycloak roles to standard role claims
		services.AddTransient<IClaimsTransformation, KeycloakClaimsTransformation>();

		// Configure authorization policies using standard role-based authorization
		services.AddAuthorizationBuilder()
			.AddPolicy(Policies.RequireAdmin, policy =>
				policy.RequireRole(Roles.Admin))
			.AddPolicy(Policies.RequireModerator, policy =>
				policy.RequireRole(Roles.Moderator))
			.AddPolicy(Policies.RequireUser, policy =>
				policy.RequireRole(Roles.User))
			.AddPolicy(Policies.RequireAdminOrModerator, policy =>
				policy.RequireRole(Roles.Admin, Roles.Moderator))
			.AddPolicy(Policies.RequireAnyRole, policy =>
				policy.RequireRole(Roles.Admin, Roles.Moderator, Roles.User));

		return services;
	}

	internal static IApplicationBuilder UseAuth(this IApplicationBuilder app)
	{
		app.UseAuthentication();
		app.UseAuthorization();

		return app;
	}
}
