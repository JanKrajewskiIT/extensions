using Extensions.Domain.Abstractions;

namespace Extensions.Infrastructure.Database;

internal sealed class CurrentUserAuditActorProvider : IAuditActorProvider
{
	private readonly ICurrentUserService currentUser;

	public CurrentUserAuditActorProvider(ICurrentUserService currentUser)
	{
		ArgumentNullException.ThrowIfNull(currentUser);
		this.currentUser = currentUser;
	}

	public Guid? ActorId => currentUser.UserId;
}
