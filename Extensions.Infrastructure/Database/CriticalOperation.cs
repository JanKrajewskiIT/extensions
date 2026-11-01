using System.Collections.Immutable;

namespace Extensions.Infrastructure.Database;

/// <summary>Neutral immutable operation metadata; the owner supplies trace, version and evidence contracts.</summary>
public record CriticalOperation<TTrace, TVersion, TEvidence>(Guid Id, string Owner, string ActorKind, string ActorId,
	string Action, string ReasonCode, TTrace Trace, ImmutableArray<TVersion> Versions,
	ImmutableArray<TEvidence> Evidence, string? Reason = null);
