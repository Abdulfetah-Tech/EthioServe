using EthioServe.Application.Abstractions;

namespace EthioServe.Tests.Support;

/// <summary>A clock tests can move forward, to prove timestamps come from IClock.</summary>
public sealed class TestClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = start;
}