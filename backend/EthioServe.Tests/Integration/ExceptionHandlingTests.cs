using System.Net;
using System.Text.Json;
using EthioServe.Tests.Support;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class ExceptionHandlingTests(ApiFactory factory)
{
    [Theory]
    [InlineData("not-found", HttpStatusCode.NotFound, "Resource not found")]
    [InlineData("forbidden", HttpStatusCode.Forbidden, "Access denied")]
    [InlineData("conflict", HttpStatusCode.Conflict, "Conflict")]
    [InlineData("domain", HttpStatusCode.UnprocessableEntity, "Business rule violation")]
    public async Task Known_exceptions_become_problem_details(
        string kind, HttpStatusCode expectedStatus, string expectedTitle)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/test-support/errors/{kind}");

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal((int)expectedStatus, root.GetProperty("status").GetInt32());
        Assert.Equal(expectedTitle, root.GetProperty("title").GetString());
        Assert.True(root.TryGetProperty("traceId", out _), "ProblemDetails must include a traceId.");
    }

    [Fact]
    public async Task Unexpected_exceptions_return_500_without_leaking_internal_details()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/test-support/errors/boom");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("SENSITIVE-INTERNAL-DETAIL", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("   at ", body);
    }
}