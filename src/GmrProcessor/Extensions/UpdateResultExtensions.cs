using MongoDB.Driver;

namespace GmrProcessor.Extensions;

public static class UpdateResultExtensions
{
    public static bool IsUpdatedOrNew(this UpdateResult updateResult)
    {
        return updateResult.UpsertedId != null || updateResult.ModifiedCount == 1;
    }
}
