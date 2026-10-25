using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EthioServe.Application.Abstractions;
using EthioServe.Application.Attachments;
using EthioServe.Domain.Entities;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class AttachmentApiTests(ApiFactory factory)
{
    private const string UnknownId = "00000000-0000-0000-0000-000000000001";

    private readonly ApiSession _session = new(factory);
    private readonly ProviderScenario _scenario = new(factory, new ApiSession(factory));

    private IFileStorage Storage => factory.Services.GetRequiredService<IFileStorage>();

    private static string Unique(string prefix) => ProviderScenario.Unique(prefix);

    private static bool HasField(JsonElement problem, string field) =>
        problem.GetProperty("errors").EnumerateObject().Any(p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase));

    private static string ListUrl(Guid requestId) => $"/api/requests/{requestId}/attachments";

    private static string OneUrl(Guid requestId, Guid attachmentId) => $"/api/requests/{requestId}/attachments/{attachmentId}";

    // A customer with one open request for a brand-new service in Addis Ababa.
    private sealed record Setup(SignedInClient Customer, Guid RequestId, Guid CategoryId, Guid ServiceId, int RegionId, int CityId)
        : IDisposable
    {
        public void Dispose() => Customer.Dispose();
    }

    private async Task<Setup> NewRequestAsync()
    {
        using var admin = await _session.AdminAsync();
        var (categoryId, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        using var anonymous = _session.CreateAnonymousClient();
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        var customer = await _session.CustomerAsync();
        var requestId = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, Unique("Photos"));

        return new Setup(customer, requestId, categoryId, serviceId, regionId, cityId);
    }

    private static async Task<Guid> UploadOkAsync(HttpClient client, Guid requestId, byte[]? bytes = null, string name = "photo.png")
    {
        var (status, body) = await AttachmentScenario.UploadAsync(client, requestId, bytes ?? AttachmentScenario.Png(), name);
        Assert.Equal(HttpStatusCode.Created, status);

        return body.GetProperty("id").GetGuid();
    }

    private async Task<string> StorageKeyAsync(Guid attachmentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.RequestAttachments.AsNoTracking()
            .Where(a => a.Id == attachmentId)
            .Select(a => a.StorageKey)
            .SingleAsync();
    }

    public static IEnumerable<object[]> BadFiles()
    {
        yield return new object[] { "notes.png", Encoding.ASCII.GetBytes("This is plain text, not a picture."), "image/png" };
        yield return new object[] { "run.jpg", new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 }, "image/jpeg" };
        yield return new object[] { "anim.gif", Encoding.ASCII.GetBytes("GIF89a......"), "image/gif" };
        yield return new object[] { "logo.svg", Encoding.ASCII.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'></svg>"), "image/svg+xml" };
        yield return new object[] { "doc.pdf", Encoding.ASCII.GetBytes("%PDF-1.7 fake"), "application/pdf" };
        yield return new object[] { "empty.png", Array.Empty<byte>(), "image/png" };
        yield return new object[] { "truncated.png", new byte[] { 0x89, 0x50, 0x4E }, "image/png" };
    }

    // ------------------------------------------------------------- access control

    [Fact]
    public async Task Only_signed_in_customers_and_verified_providers_can_reach_the_photo_routes()
    {
        using var setup = await NewRequestAsync();
        var attachmentId = await UploadOkAsync(setup.Customer.Http, setup.RequestId);
        using var anonymous = _session.CreateAnonymousClient();
        using var admin = await _session.AdminAsync();
        using var unverified = await _session.RegisteredProviderAsync();

        var routes = new[]
        {
            (HttpMethod.Get, ListUrl(setup.RequestId)),
            (HttpMethod.Get, OneUrl(setup.RequestId, attachmentId)),
            (HttpMethod.Delete, OneUrl(setup.RequestId, attachmentId))
        };

        foreach (var (method, url) in routes)
        {
            var (anonymousStatus, _) = await ApiJson.SendAsync(anonymous, method, url);
            var (adminStatus, _) = await ApiJson.SendAsync(admin.Http, method, url);
            var (providerStatus, _) = await ApiJson.SendAsync(unverified.Http, method, url);

            Assert.Equal(HttpStatusCode.Unauthorized, anonymousStatus);
            Assert.Equal(HttpStatusCode.Forbidden, adminStatus);
            Assert.Equal(HttpStatusCode.Forbidden, providerStatus);
        }
    }

    [Fact]
    public async Task Only_customers_can_upload()
    {
        using var setup = await NewRequestAsync();
        using var anonymous = _session.CreateAnonymousClient();
        using var admin = await _session.AdminAsync();
        using var provider = await _session.RegisteredProviderAsync();

        var (anonymousStatus, _) = await AttachmentScenario.UploadAsync(anonymous, setup.RequestId, AttachmentScenario.Png());
        var (adminStatus, _) = await AttachmentScenario.UploadAsync(admin.Http, setup.RequestId, AttachmentScenario.Png());
        var (providerStatus, _) = await AttachmentScenario.UploadAsync(provider.Http, setup.RequestId, AttachmentScenario.Png());

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousStatus);
        Assert.Equal(HttpStatusCode.Forbidden, adminStatus);
        Assert.Equal(HttpStatusCode.Forbidden, providerStatus);
    }

    // ------------------------------------------------------------- uploading

    [Fact]
    public async Task Jpeg_png_and_webp_photos_are_accepted_and_the_type_comes_from_the_bytes()
    {
        using var setup = await NewRequestAsync();
        var http = setup.Customer.Http;

        var (jpegStatus, jpeg) = await AttachmentScenario.UploadAsync(http, setup.RequestId, AttachmentScenario.Jpeg(), "kitchen.jpg", "image/jpeg");
        var (pngStatus, png) = await AttachmentScenario.UploadAsync(http, setup.RequestId, AttachmentScenario.Png(100), "leak.png", "image/png");
        var (webpStatus, webp) = await AttachmentScenario.UploadAsync(http, setup.RequestId, AttachmentScenario.WebP(), "sink.webp", "image/webp");
        // The name and the browser both say PNG, the bytes say JPEG: the bytes win.
        var (lyingStatus, lying) = await AttachmentScenario.UploadAsync(http, setup.RequestId, AttachmentScenario.Jpeg(), "pretend.png", "image/png");

        Assert.All(new[] { jpegStatus, pngStatus, webpStatus, lyingStatus }, status => Assert.Equal(HttpStatusCode.Created, status));
        Assert.Equal("image/jpeg", jpeg.GetProperty("contentType").GetString());
        Assert.Equal("kitchen.jpg", jpeg.GetProperty("fileName").GetString());
        Assert.Equal(68, jpeg.GetProperty("sizeBytes").GetInt64());
        Assert.Equal("image/png", png.GetProperty("contentType").GetString());
        Assert.Equal(108, png.GetProperty("sizeBytes").GetInt64());
        Assert.Equal("image/webp", webp.GetProperty("contentType").GetString());
        Assert.Equal("image/jpeg", lying.GetProperty("contentType").GetString());
    }

    [Theory]
    [MemberData(nameof(BadFiles))]
    public async Task Files_that_are_not_real_photos_are_refused_whatever_they_are_called(string name, byte[] bytes, string declaredType)
    {
        using var setup = await NewRequestAsync();

        var (status, body) = await AttachmentScenario.UploadAsync(setup.Customer.Http, setup.RequestId, bytes, name, declaredType);
        var (_, list) = await ApiJson.SendAsync(setup.Customer.Http, HttpMethod.Get, ListUrl(setup.RequestId));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.True(HasField(body, "file"));
        Assert.Equal(0, list.GetArrayLength());
    }

    [Fact]
    public async Task A_photo_over_five_megabytes_is_refused_but_exactly_five_is_fine()
    {
        using var setup = await NewRequestAsync();

        var (tooBig, tooBigBody) = await AttachmentScenario.UploadAsync(
            setup.Customer.Http, setup.RequestId, AttachmentScenario.Png(AttachmentRules.MaxBytes));
        var (exact, _) = await AttachmentScenario.UploadAsync(
            setup.Customer.Http, setup.RequestId, AttachmentScenario.Png(AttachmentRules.MaxBytes - 8));

        Assert.Equal(HttpStatusCode.BadRequest, tooBig);
        Assert.True(HasField(tooBigBody, "file"));
        Assert.Equal(HttpStatusCode.Created, exact);
    }

    [Fact]
    public async Task A_request_without_a_file_part_is_a_bad_request()
    {
        using var setup = await NewRequestAsync();

        using var response = await setup.Customer.Http.PostAsync(ListUrl(setup.RequestId), new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_request_can_have_at_most_five_photos_and_removing_one_frees_a_place()
    {
        using var setup = await NewRequestAsync();
        var http = setup.Customer.Http;
        var first = await UploadOkAsync(http, setup.RequestId);

        for (var i = 2; i <= AttachmentRules.MaxFiles; i++)
        {
            await UploadOkAsync(http, setup.RequestId);
        }

        var (sixth, _) = await AttachmentScenario.UploadAsync(http, setup.RequestId, AttachmentScenario.Png());
        await ApiJson.SendAsync(http, HttpMethod.Delete, OneUrl(setup.RequestId, first));
        var (afterRemoval, _) = await AttachmentScenario.UploadAsync(http, setup.RequestId, AttachmentScenario.Png());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, sixth);
        Assert.Equal(HttpStatusCode.Created, afterRemoval);
    }

    [Fact]
    public async Task Photos_can_only_be_added_or_removed_while_the_request_is_pending()
    {
        using var setup = await NewRequestAsync();
        var attachmentId = await UploadOkAsync(setup.Customer.Http, setup.RequestId);
        await ApiJson.SendAsync(setup.Customer.Http, HttpMethod.Post, $"/api/requests/{setup.RequestId}/cancel", new { });

        var (upload, _) = await AttachmentScenario.UploadAsync(setup.Customer.Http, setup.RequestId, AttachmentScenario.Png());
        var (remove, _) = await ApiJson.SendAsync(setup.Customer.Http, HttpMethod.Delete, OneUrl(setup.RequestId, attachmentId));
        var (_, list) = await ApiJson.SendAsync(setup.Customer.Http, HttpMethod.Get, ListUrl(setup.RequestId));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, upload);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, remove);
        Assert.Equal(1, list.GetArrayLength()); // the owner can still see what was attached
    }

    [Fact]
    public async Task The_stored_name_is_random_and_the_shown_name_is_cleaned()
    {
        using var setup = await NewRequestAsync();

        var (status, body) = await AttachmentScenario.UploadAsync(
            setup.Customer.Http, setup.RequestId, AttachmentScenario.Png(), "../../evil.png");
        var key = await StorageKeyAsync(body.GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal("evil.png", body.GetProperty("fileName").GetString());
        Assert.Matches(@"^[0-9a-f]{32}\.png$", key);
        Assert.DoesNotContain("evil", key);
    }

    // ------------------------------------------------------------- ownership

    [Fact]
    public async Task Other_customers_cannot_add_see_download_or_remove_photos()
    {
        using var setup = await NewRequestAsync();
        using var intruder = await _session.CustomerAsync();
        var attachmentId = await UploadOkAsync(setup.Customer.Http, setup.RequestId);

        var (upload, _) = await AttachmentScenario.UploadAsync(intruder.Http, setup.RequestId, AttachmentScenario.Png());
        var (list, _) = await ApiJson.SendAsync(intruder.Http, HttpMethod.Get, ListUrl(setup.RequestId));
        var download = await AttachmentScenario.DownloadAsync(intruder.Http, setup.RequestId, attachmentId);
        var (remove, _) = await ApiJson.SendAsync(intruder.Http, HttpMethod.Delete, OneUrl(setup.RequestId, attachmentId));
        var (_, ownerList) = await ApiJson.SendAsync(setup.Customer.Http, HttpMethod.Get, ListUrl(setup.RequestId));

        Assert.Equal(HttpStatusCode.NotFound, upload);
        Assert.Equal(HttpStatusCode.NotFound, list);
        Assert.Equal(HttpStatusCode.NotFound, download.Status);
        Assert.Equal(HttpStatusCode.NotFound, remove);
        Assert.Equal(1, ownerList.GetArrayLength());
    }

    [Fact]
    public async Task A_photo_cannot_be_reached_through_a_different_request()
    {
        using var setup = await NewRequestAsync();
        var otherRequest = await RequestScenario.CreateAsync(
            setup.Customer.Http, setup.ServiceId, setup.RegionId, setup.CityId, Unique("Second"));
        var attachmentId = await UploadOkAsync(setup.Customer.Http, setup.RequestId);

        var download = await AttachmentScenario.DownloadAsync(setup.Customer.Http, otherRequest, attachmentId);
        var (remove, _) = await ApiJson.SendAsync(setup.Customer.Http, HttpMethod.Delete, OneUrl(otherRequest, attachmentId));
        var unknown = await AttachmentScenario.DownloadAsync(setup.Customer.Http, setup.RequestId, Guid.Parse(UnknownId));

        Assert.Equal(HttpStatusCode.NotFound, download.Status);
        Assert.Equal(HttpStatusCode.NotFound, remove);
        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
    }

    // ------------------------------------------------------------- viewing

    [Fact]
    public async Task A_photo_downloads_unchanged_with_locked_down_headers()
    {
        using var setup = await NewRequestAsync();
        var bytes = AttachmentScenario.Jpeg(500);
        var attachmentId = await UploadOkAsync(setup.Customer.Http, setup.RequestId, bytes, "kitchen.jpg");

        var download = await AttachmentScenario.DownloadAsync(setup.Customer.Http, setup.RequestId, attachmentId);

        Assert.Equal(HttpStatusCode.OK, download.Status);
        Assert.Equal(bytes, download.Bytes);
        Assert.Equal("image/jpeg", download.ContentType);
        Assert.Equal("nosniff", download.NoSniff);
        Assert.Contains("default-src 'none'", download.ContentSecurityPolicy);
    }

    [Fact]
    public async Task Providers_see_photos_only_for_requests_in_their_feed()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (categoryId, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var otherService = await _scenario.AddServiceAsync(admin.Http, categoryId);
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");

        using var matching = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Match"), serviceId, 300m);
        using var otherOffer = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Other"), otherService, 100m);
        using var unverified = await _session.RegisteredProviderAsync();
        using var customer = await _session.CustomerAsync();

        var requestId = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, Unique("Shared"));
        var bytes = AttachmentScenario.Png(300);
        var attachmentId = await UploadOkAsync(customer.Http, requestId, bytes);

        var (listStatus, list) = await ApiJson.SendAsync(matching.Http, HttpMethod.Get, ListUrl(requestId));
        var download = await AttachmentScenario.DownloadAsync(matching.Http, requestId, attachmentId);
        var (otherList, _) = await ApiJson.SendAsync(otherOffer.Http, HttpMethod.Get, ListUrl(requestId));
        var otherDownload = await AttachmentScenario.DownloadAsync(otherOffer.Http, requestId, attachmentId);
        var (unverifiedList, _) = await ApiJson.SendAsync(unverified.Http, HttpMethod.Get, ListUrl(requestId));

        Assert.Equal(HttpStatusCode.OK, listStatus);
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, download.Status);
        Assert.Equal(bytes, download.Bytes);
        Assert.Equal(HttpStatusCode.NotFound, otherList);
        Assert.Equal(HttpStatusCode.NotFound, otherDownload.Status);
        Assert.Equal(HttpStatusCode.Forbidden, unverifiedList);

        // Providers can look but never change anything.
        var (providerUpload, _) = await AttachmentScenario.UploadAsync(matching.Http, requestId, AttachmentScenario.Png());
        var (providerRemove, _) = await ApiJson.SendAsync(matching.Http, HttpMethod.Delete, OneUrl(requestId, attachmentId));
        Assert.Equal(HttpStatusCode.Forbidden, providerUpload);
        Assert.Equal(HttpStatusCode.Forbidden, providerRemove);
    }

    [Fact]
    public async Task A_suspended_provider_and_a_closed_request_lose_access_to_photos()
    {
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();
        var (_, serviceId) = await _scenario.NewServiceAsync(admin.Http);
        var (regionId, cityId) = await ProviderScenario.LocationAsync(anonymous, "Addis Ababa", "Addis Ababa");
        using var provider = await _scenario.VerifiedProviderAsync(admin.Http, Unique("Close"), serviceId, 300m);
        using var customer = await _session.CustomerAsync();
        var requestId = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId, Unique("Closing"));
        await UploadOkAsync(customer.Http, requestId);

        var (before, _) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, ListUrl(requestId));
        await ApiJson.SendAsync(customer.Http, HttpMethod.Post, $"/api/requests/{requestId}/cancel", new { });
        var (afterCancel, _) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, ListUrl(requestId));
        var (ownerAfterCancel, _) = await ApiJson.SendAsync(customer.Http, HttpMethod.Get, ListUrl(requestId));

        await ProviderScenario.SuspendAsync(admin.Http, provider);
        var (afterSuspend, _) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, ListUrl(requestId));

        Assert.Equal(HttpStatusCode.OK, before);
        Assert.Equal(HttpStatusCode.NotFound, afterCancel);
        Assert.Equal(HttpStatusCode.OK, ownerAfterCancel);
        Assert.Equal(HttpStatusCode.Forbidden, afterSuspend);
    }

    // ------------------------------------------------------------- removing and audit

    [Fact]
    public async Task Removing_a_photo_deletes_the_record_and_the_stored_file_and_both_steps_are_audited()
    {
        using var setup = await NewRequestAsync();
        var attachmentId = await UploadOkAsync(setup.Customer.Http, setup.RequestId, AttachmentScenario.Jpeg(), "kitchen-sink.jpg");
        var key = await StorageKeyAsync(attachmentId);

        await using (var before = await Storage.OpenReadAsync(key))
        {
            Assert.NotNull(before);
        }

        var (deleted, _) = await ApiJson.SendAsync(setup.Customer.Http, HttpMethod.Delete, OneUrl(setup.RequestId, attachmentId));
        var (_, list) = await ApiJson.SendAsync(setup.Customer.Http, HttpMethod.Get, ListUrl(setup.RequestId));
        var gone = await AttachmentScenario.DownloadAsync(setup.Customer.Http, setup.RequestId, attachmentId);

        Assert.Equal(HttpStatusCode.NoContent, deleted);
        Assert.Equal(0, list.GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, gone.Status);
        Assert.Null(await Storage.OpenReadAsync(key));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entries = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(RequestAttachment) && a.EntityId == attachmentId.ToString())
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(new[] { "RequestAttachmentAdded", "RequestAttachmentRemoved" }, entries.Select(e => e.Action).ToArray());
        Assert.All(entries, e => Assert.Equal(setup.Customer.UserId, e.UserId));
        Assert.All(entries, e => Assert.DoesNotContain("kitchen", (e.NewValues ?? string.Empty) + (e.OldValues ?? string.Empty)));
    }
}