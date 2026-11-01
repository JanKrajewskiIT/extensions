namespace Extensions.Infrastructure.Database;

public sealed class DatabaseActorScope(Func<string?> authenticatedSubject)
{
	private readonly Func<string?> authenticated = authenticatedSubject ?? throw new ArgumentNullException(nameof(authenticatedSubject));
	private string? jobSubject;
	private ReviewScope? review;
	private MutationScope? mutation;

	public string ReadSubject => review?.Owner ?? Subject;
	public string? AvailableSubject => authenticated() ?? jobSubject;

	public string Subject => authenticated() ?? jobSubject
		?? throw new UnauthorizedAccessException("The operation requires an authenticated or delegated actor.");

	public bool CanMutate(string owner) => owner == AvailableSubject || mutation?.Owner == owner;

	public IDisposable AuthorizeOwnedMutation(string owner)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(owner);
		_ = Subject;
		return mutation = new MutationScope(this, owner, mutation);
	}

	public void SetJobSubject(string subject)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subject);
		if (jobSubject is not null && jobSubject != subject || authenticated() is { } user && user != subject)
		{
			throw new InvalidOperationException("An operation scope cannot change its actor.");
		}

		jobSubject = subject;
	}

	// The application must authorize the assigned review before opening this read-only scope.
	public IDisposable ReviewOwnedEvidence(string owner)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(owner);
		_ = Subject;
		return review = new ReviewScope(this, owner, review);
	}

	private sealed class ReviewScope(DatabaseActorScope actor, string owner, ReviewScope? parent) : IDisposable
	{
		public string Owner => owner;

		public void Dispose()
		{
			if (actor.review != this)
			{
				throw new InvalidOperationException("Evidence review scopes must close in ownership order.");
			}

			actor.review = parent;
		}
	}

	private sealed class MutationScope(DatabaseActorScope actor, string owner, MutationScope? parent) : IDisposable
	{
		public string Owner => owner;

		public void Dispose()
		{
			if (actor.mutation != this)
			{
				throw new InvalidOperationException("Authorized mutation scopes must close in ownership order.");
			}

			actor.mutation = parent;
		}
	}
}
