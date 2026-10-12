namespace EthioServe.Tests.Support;

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}