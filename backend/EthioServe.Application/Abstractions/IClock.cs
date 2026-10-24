namespace EthioServe.Application.Abstractions;

/// <summary>Time source. Inject this instead of using DateTime.UtcNow so time can be faked in tests.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}