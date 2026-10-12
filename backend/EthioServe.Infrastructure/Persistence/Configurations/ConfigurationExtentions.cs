using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EthioServe.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    /// <summary>Maps a uint property to PostgreSQL's xmin system column as an optimistic concurrency token.</summary>
    public static PropertyBuilder<uint> UseXminVersion(this PropertyBuilder<uint> property) =>
        property
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

    /// <summary>Stores an enum as readable text (safe against reordering the enum members).</summary>
    public static PropertyBuilder<TEnum> AsStringColumn<TEnum>(this PropertyBuilder<TEnum> property, int maxLength)
        where TEnum : struct, Enum =>
        property.HasConversion<string>().HasMaxLength(maxLength);

    /// <summary>Builds "column IN ('A', 'B', ...)" for a CHECK constraint from the enum's members.</summary>
    public static string InList<TEnum>(string column) where TEnum : struct, Enum =>
        $"{column} IN ({string.Join(", ", Enum.GetNames<TEnum>().Select(name => $"'{name}'"))})";
}