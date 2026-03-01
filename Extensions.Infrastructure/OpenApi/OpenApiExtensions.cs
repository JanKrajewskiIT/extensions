using Extensions.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Extensions.Infrastructure.OpenApi;

public static class OpenApiExtensions
{
	public static IServiceCollection AddOpenApiWithAuth(
		this IServiceCollection services,
		KeycloakOptions keycloakOptions)
	{
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

				foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations!))
				{
					operation.Value.Security ??= [];
					operation.Value.Security.Add(new OpenApiSecurityRequirement
					{
						[new OpenApiSecuritySchemeReference("oauth2", document)] = []
					});
				}

				return Task.CompletedTask;
			});
		});
	}

	public static IApplicationBuilder UseOpenApiWithAuth(this WebApplication app)
	{
		app.MapOpenApi();

		return app;
	}
}