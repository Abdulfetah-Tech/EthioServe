using System.Net;
using System.Text.Json;
using EthioServe.Tests.Support;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class RealtimeTests(ApiFactory factory)
{
    private const string HubUrl = "/hubs/notifications";

    private readonly ApiSession _session = new(factory);
    private readonly JobBuilder _jobs = new(factory);

    private static string TokenOf(SignedInClient user) => user.Http.DefaultRequestHeaders.Authorization!.Parameter!;

    private static async Task<HttpStatusCode> NegotiateAsync(HttpClient client, string query = "")
    {
        using var response = await client.PostAsync($"{HubUrl}/negotiate?negotiateVersion=1{query}", null);

        return response.StatusCode;
    }

    /// <summary>A real SignalR client, talking to the test server over long polling.</summary>
    private sealed class Listener(HubConnection connection) : IAsyncDisposable
    {
        private readonly TaskCompletionSource<JsonElement> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<JsonElement> First => _first.Task;

        public async Task StartAsync()
        {
            connection.On<JsonElement>("notification", message => _first.TrySetResult(message));
            await connection.StartAsync();
        }

        public async Task<JsonElement> NextAsync() => await _first.Task.WaitAsync(TimeSpan.FromSeconds(15));

        public async Task<bool> StaysSilentAsync()
        {
            await Task.Delay(TimeSpan.FromSeconds(1.5));

            return !_first.Task.IsCompleted;
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }

    private async Task<Listener> ListenAsync(SignedInClient user)
    {
        var server = factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri("https://localhost" + HubUrl), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(TokenOf(user));
            })
            .Build();

        var listener = new Listener(connection);
        await listener.StartAsync();

        return listener;
    }

    // ------------------------------------------------------------- who may connect

    [Fact]
    public async Task The_hub_needs_a_signed_in_user()
    {
        using var anonymous = _session.CreateAnonymousClient();
        using var customer = await _session.CustomerAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, await NegotiateAsync(anonymous));
        Assert.Equal(HttpStatusCode.OK, await NegotiateAsync(customer.Http));
    }

    [Fact]
    public async Task A_token_in_the_address_works_for_the_hub_only()
    {
        using var anonymous = _session.CreateAnonymousClient();
        using var customer = await _session.CustomerAsync();
        var token = TokenOf(customer);

        var hub = await NegotiateAsync(anonymous, $"&access_token={token}");
        using var other = await anonymous.GetAsync($"/api/notifications?access_token={token}");
        using var garbage = await anonymous.PostAsync($"{HubUrl}/negotiate?negotiateVersion=1&access_token=not-a-token", null);

        Assert.Equal(HttpStatusCode.OK, hub);
        Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, garbage.StatusCode);
    }

    // ------------------------------------------------------------- live delivery

    [Fact]
    public async Task A_new_request_reaches_a_matching_provider_who_is_online_and_nobody_else()
    {
        using var world = await _jobs.NewWorldAsync();
        using var stranger = await _jobs.ExtraProviderAsync(world);
        using var customer2 = await _session.CustomerAsync();
        await using var provider = await ListenAsync(world.Provider);
        await using var wrongCustomer = await ListenAsync(customer2);

        var title = ProviderScenario.Unique("Live job");
        var requestId = await RequestScenario.CreateAsync(world.Customer.Http, world.ServiceId, world.RegionId, world.CityId, title);

        var message = await provider.NextAsync();

        Assert.Equal("NewServiceRequest", message.GetProperty("type").GetString());
        Assert.Equal("New request in your city", message.GetProperty("title").GetString());
        Assert.Contains(title, message.GetProperty("message").GetString());
        Assert.False(message.GetProperty("isRead").GetBoolean());
        Assert.Equal("ServiceRequest", message.GetProperty("relatedEntityType").GetString());
        Assert.Equal(requestId, message.GetProperty("relatedEntityId").GetGuid());
        Assert.True(await wrongCustomer.StaysSilentAsync());
    }

    [Fact]
    public async Task A_quote_reaches_only_the_customer_it_is_for()
    {
        using var world = await _jobs.NewWorldAsync();
        using var otherCustomer = await _session.CustomerAsync();
        var requestId = await RequestScenario.CreateAsync(
            world.Customer.Http, world.ServiceId, world.RegionId, world.CityId, ProviderScenario.Unique("Quoted live"));

        await using var owner = await ListenAsync(world.Customer);
        await using var bystander = await ListenAsync(otherCustomer);

        await QuoteScenario.SubmitAsync(world.Provider.Http, requestId, 320m);

        var message = await owner.NextAsync();

        Assert.Equal("QuoteReceived", message.GetProperty("type").GetString());
        Assert.Contains("320 ETB", message.GetProperty("message").GetString());
        Assert.True(await bystander.StaysSilentAsync());
    }

    [Fact]
    public async Task A_refused_change_pushes_nothing()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await RequestScenario.CreateAsync(
            world.Customer.Http, world.ServiceId, world.RegionId, world.CityId, ProviderScenario.Unique("Refused live"));
        await using var customer = await ListenAsync(world.Customer);

        var (status, _) = await QuoteScenario.SubmitRawAsync(world.Provider.Http, requestId, QuoteScenario.Body(amount: 0m));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.True(await customer.StaysSilentAsync());
    }

    [Fact]
    public async Task The_message_that_is_pushed_matches_the_one_that_was_saved()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await RequestScenario.CreateAsync(
            world.Customer.Http, world.ServiceId, world.RegionId, world.CityId, ProviderScenario.Unique("Same"));
        await using var customer = await ListenAsync(world.Customer);

        await QuoteScenario.SubmitAsync(world.Provider.Http, requestId, 410m);

        var pushed = await customer.NextAsync();
        var saved = (await NotificationScenario.ListAsync(world.Customer.Http)).Single();

        Assert.Equal(saved.GetProperty("id").GetGuid(), pushed.GetProperty("id").GetGuid());
        Assert.Equal(saved.GetProperty("title").GetString(), pushed.GetProperty("title").GetString());
        Assert.Equal(saved.GetProperty("message").GetString(), pushed.GetProperty("message").GetString());
        Assert.Equal(saved.GetProperty("createdAt").GetDateTimeOffset(), pushed.GetProperty("createdAt").GetDateTimeOffset());
    }
}