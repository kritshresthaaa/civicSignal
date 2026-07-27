using CivicSignal.Application.Common;

namespace CivicSignal.Infrastructure.Persistence;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
