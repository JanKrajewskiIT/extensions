using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Extensions.Jobs;

public static class JobsRegistrationExtensions
{
	public static IServiceCollection AddJobs(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.TryAddSingleton(TimeProvider.System);
		services.TryAddSingleton(typeof(ScopedJobOperation<>));
		return services;
	}

	public static IServiceCollection AddJobHandlersFromAssembly(this IServiceCollection services, Assembly assembly, Func<Type, bool>? include = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(assembly);
		foreach (var type in assembly.GetTypes().Where(value => value is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } && (include is null || include(value))))
		{
			foreach (var contract in type.GetInterfaces().Where(value => value.IsGenericType && value.GetGenericTypeDefinition() == typeof(IJobHandler<>)))
			{
				services.TryAddEnumerable(ServiceDescriptor.Scoped(contract, type));
			}
		}

		return services;
	}
}
