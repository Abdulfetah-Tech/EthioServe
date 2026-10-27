using System.Net;
using EthioServe.Domain.Enums;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Integration;

/// <summary>
/// These tests send real requests at the same moment. They prove the no-double-booking rule holds under a race,
/// which only the database constraint can guarantee.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AppointmentConcurrencyTests(ApiFactory factory)
{
    private readonly JobBuilder _jobs = new(factory);

    [Fact]
    public async Task Two_overlapping_bookings_at_the_same_moment_never_both_succeed()
    {
        using var world = await _jobs.NewWorldAsync();
        var http = world.Provider.Http;

        for (var round = 1; round <= 3; round++)
        {
            var jobA = await _jobs.AcceptedRequestAsync(world);
            var jobB = await _jobs.AcceptedRequestAsync(world);
            var start = AppointmentScenario.InHours(100 + (round * 10));

            var results = await Task.WhenAll(
                AppointmentScenario.ScheduleRawAsync(http, jobA, AppointmentScenario.Body(start)),
                AppointmentScenario.ScheduleRawAsync(http, jobB, AppointmentScenario.Body(start.AddMinutes(30))));

            Assert.Equal(1, results.Count(r => r.Status == HttpStatusCode.Created));
            Assert.All(
                results.Where(r => r.Status != HttpStatusCode.Created),
                r => Assert.True(r.Status == HttpStatusCode.Conflict, $"Round {round}: the losing booking returned {(int)r.Status}."));
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var active = await db.Appointments.AsNoTracking()
            .Where(a => a.ProviderId == world.Provider.ProfileId
                        && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.InProgress))
            .OrderBy(a => a.StartAt)
            .ToListAsync();

        Assert.Equal(3, active.Count);
        for (var i = 1; i < active.Count; i++)
        {
            Assert.True(active[i - 1].EndAt <= active[i].StartAt, "Two active appointments overlap.");
        }
    }

    [Fact]
    public async Task Scheduling_the_same_job_twice_at_once_creates_exactly_one_appointment()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world);

        var results = await Task.WhenAll(
            AppointmentScenario.ScheduleRawAsync(world.Provider.Http, requestId, AppointmentScenario.Body(AppointmentScenario.InHours(60))),
            AppointmentScenario.ScheduleRawAsync(world.Provider.Http, requestId, AppointmentScenario.Body(AppointmentScenario.InHours(90))));

        Assert.Equal(1, results.Count(r => r.Status == HttpStatusCode.Created));
        Assert.All(
            results.Where(r => r.Status != HttpStatusCode.Created),
            r => Assert.True(
                r.Status is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity,
                $"The losing booking returned {(int)r.Status}."));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.Appointments.AsNoTracking().CountAsync(a => a.ServiceRequestId == requestId);

        Assert.Equal(1, count);
    }
}