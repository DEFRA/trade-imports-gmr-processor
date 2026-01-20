namespace GmrProcessor.Data;

public interface IMatchReferenceRepository
{
    Task<List<string>> GetChedsByMrn(string mrn, CancellationToken cancellationToken);
}
