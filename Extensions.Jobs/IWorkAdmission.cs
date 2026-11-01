namespace Extensions.Jobs;

public interface IWorkAdmission<in TRequest>
{
	ValueTask<IWorkReservation> ReserveAsync(TRequest request, CancellationToken token);
}
