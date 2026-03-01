using Extensions.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Extensions.Infrastructure.OpenApi;

public static class OpenApiExtensions
{
	public static IServiceCollection AddOpenApiWithAuth(
		this IServiceCollection services,
		IConfigurationSection keycloakSection)
	{
		var keycloakOptions = keycloakSection.Get<KeycloakOptions>()
			?? throw new InvalidOperationException("Keycloak configuration section is missing or invalid.");

		return services.AddOpenApi(options =>
		{
			options.AddDocumentTransformer((document, _, _) =>
			{
				document.Components ??= new OpenApiComponents();
				document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
				{
					["oauth2"] = new OpenApiSecurityScheme
					{
						Type = SecuritySchemeType.OAuth2,
						Flows = new OpenApiOAuthFlows
						{
							AuthorizationCode = new OpenApiOAuthFlow
							{
								AuthorizationUrl = keycloakOptions.AuthorizationUrl,
								TokenUrl = keycloakOptions.TokenUrl
							}
						}
					}
				};

				foreach (var path in document.Paths.Values)
				{
					if (path.Operations is null) continue;

					foreach (var operation in path.Operations.Values)
					{
						operation.Security ??= [];
						operation.Security.Add(new OpenApiSecurityRequirement
						{
							[new OpenApiSecuritySchemeReference("oauth2", document)] = []
						});
					}
				}

				return Task.CompletedTask;
			});
		});
	}

	public static WebApplication UseOpenApiWithAuth(this WebApplication app)
	{
		app.MapOpenApi();

		return app;
	}
}
