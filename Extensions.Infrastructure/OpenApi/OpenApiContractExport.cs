using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Extensions.Infrastructure.OpenApi;

internal static class OpenApiContractExport
{
    private const string Command = "--export-openapi";
    private const string DocumentName = "v1";

    public static string? GetOutputPath(string[] args)
    {
        var commandIndex = Array.IndexOf(args, Command);
        if (commandIndex == -1)
        {
            return null;
        }
        if (commandIndex != 0 || args.Length != 2)
        {
            throw new ArgumentException($"Expected {Command} <output.json>.", nameof(args));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(args[1]);
        var output = Path.GetFullPath(args[1]);
        if (!string.Equals(Path.GetExtension(output), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("OpenAPI output must be a JSON file.", nameof(args));
        }
        return output;
    }

    public static async Task WriteAsync(WebApplication app, string outputPath, CancellationToken cancellationToken)
    {
        var provider = app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(DocumentName);
        var document = await provider.GetOpenApiDocumentAsync(cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await using var stream = File.Create(outputPath);
        await document.SerializeAsJsonAsync(stream, OpenApiSpecVersion.OpenApi3_1, cancellationToken);
    }
}
