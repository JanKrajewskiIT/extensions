namespace Extensions.Jobs;

public sealed record CapacityReservation(DateTimeOffset StartedAt, long ReservedMilliseconds, bool Protected);
