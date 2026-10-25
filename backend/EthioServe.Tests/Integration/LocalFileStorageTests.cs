using System.Text;
using EthioServe.Infrastructure.Storage;

namespace EthioServe.Tests.Integration;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ethioserve-test-" + Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _storage = new LocalFileStorage(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string NewKey(string extension = ".png") => Guid.NewGuid().ToString("N") + extension;

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));

    private async Task<string?> ReadAsync(string key)
    {
        await using var stream = await _storage.OpenReadAsync(key);

        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);

        return await reader.ReadToEndAsync();
    }

    [Fact]
    public void The_folder_is_created_when_it_is_missing()
    {
        Assert.True(Directory.Exists(_root));
    }

    [Fact]
    public void Without_a_configured_folder_a_temporary_one_is_used()
    {
        var storage = new LocalFileStorage(null);

        Assert.NotNull(storage);
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".png")]
    [InlineData(".webp")]
    public async Task A_saved_file_can_be_read_back_unchanged(string extension)
    {
        var key = NewKey(extension);

        await _storage.SaveAsync(key, Bytes("hello photo"));

        Assert.Equal("hello photo", await ReadAsync(key));
    }

    [Fact]
    public async Task A_file_that_does_not_exist_reads_as_null()
    {
        Assert.Null(await ReadAsync(NewKey()));
    }

    [Fact]
    public async Task Deleting_removes_the_file_and_deleting_twice_is_fine()
    {
        var key = NewKey();
        await _storage.SaveAsync(key, Bytes("temporary"));

        await _storage.DeleteAsync(key);
        await _storage.DeleteAsync(key);

        Assert.Null(await ReadAsync(key));
    }

    [Fact]
    public async Task A_key_can_never_overwrite_an_existing_file()
    {
        var key = NewKey();
        await _storage.SaveAsync(key, Bytes("original"));

        await Assert.ThrowsAsync<IOException>(() => _storage.SaveAsync(key, Bytes("replacement")));

        Assert.Equal("original", await ReadAsync(key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../evil.png")]
    [InlineData("..\\evil.png")]
    [InlineData("abc.png")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF.png")]
    [InlineData("0123456789abcdef0123456789abcdef.exe")]
    [InlineData("0123456789abcdef0123456789abcdef.png/../x")]
    [InlineData("0123456789abcdef0123456789abcdef.png\n")]
    public async Task Keys_that_are_not_generated_by_the_application_are_refused(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.SaveAsync(key, Bytes("x")));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.OpenReadAsync(key));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.DeleteAsync(key));
    }
}