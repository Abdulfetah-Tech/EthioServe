namespace EthioServe.Domain.Common;

public interface ICreationAudited
{
    DateTimeOffset CreatedAt { get; }

    void MarkCreated(DateTimeOffset utcNow);
}

public interface IModificationAudited : ICreationAudited
{
    DateTimeOffset UpdatedAt { get; }

    void MarkUpdated(DateTimeOffset utcNow);
}