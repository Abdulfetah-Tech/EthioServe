using EthioServe.Tests.Support;

namespace EthioServe.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public sealed class ExclusiveLockTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Operations_with_the_same_key_run_one_at_a_time()
    {
        var gate = new object();
        var running = 0;
        var most = 0;

        async Task<int> Work(int number)
        {
            await using var db = fixture.CreateContext();

            return await db.WithExclusiveLockAsync("same-key", async () =>
            {
                lock (gate)
                {
                    running++;
                    most = Math.Max(most, running);
                }

                await Task.Delay(150);

                lock (gate)
                {
                    running--;
                }

                return number;
            });
        }

        var results = await Task.WhenAll(Enumerable.Range(1, 4).Select(Work));

        Assert.Equal(new[] { 1, 2, 3, 4 }, results.Order().ToArray());
        Assert.Equal(1, most);
    }

    [Fact]
    public async Task Operations_with_different_keys_do_not_wait_for_each_other()
    {
        var firstReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task First()
        {
            await using var db = fixture.CreateContext();

            await db.WithExclusiveLockAsync("key-a", async () =>
            {
                firstReady.SetResult();
                await secondReady.Task.WaitAsync(TimeSpan.FromSeconds(10)); // would time out if the second one were blocked

                return 1;
            });
        }

        async Task Second()
        {
            await using var db = fixture.CreateContext();

            await db.WithExclusiveLockAsync("key-b", async () =>
            {
                secondReady.SetResult();
                await firstReady.Task.WaitAsync(TimeSpan.FromSeconds(10));

                return 2;
            });
        }

        await Task.WhenAll(First(), Second());
    }

    [Fact]
    public async Task A_failed_operation_releases_the_lock_for_the_next_one()
    {
        await using (var failing = fixture.CreateContext())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => failing.WithExclusiveLockAsync<int>("failing-key", () => throw new InvalidOperationException("boom")));
        }

        await using var next = fixture.CreateContext();

        var result = await next.WithExclusiveLockAsync("failing-key", () => Task.FromResult(42)).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(42, result);
    }
}