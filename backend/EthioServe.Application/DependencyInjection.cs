using EthioServe.Application.Auth;
using EthioServe.Application.Catalog;
using EthioServe.Application.Common;
using EthioServe.Application.Locations;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using EthioServe.Application.Providers;
using EthioServe.Application.Appointments;
using EthioServe.Application.Attachments;
using EthioServe.Application.Notifications;
using EthioServe.Application.Quotes;
using EthioServe.Application.Requests;
using EthioServe.Application.Reviews;

namespace EthioServe.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterCustomerRequestValidator>();

        services.AddScoped<ILocationQueries, LocationQueries>();
        services.AddScoped<ICatalogQueries, CatalogQueries>();
        services.AddScoped<IAdminCatalogService, AdminCatalogService>();
        services.AddScoped<IProviderSelfService, ProviderSelfService>();
        services.AddScoped<IAdminProviderService, AdminProviderService>();
        services.AddScoped<IProviderSearchQueries, ProviderSearchQueries>();
        services.AddScoped<ICustomerRequestService, CustomerRequestService>();
        services.AddScoped<IProviderFeedService, ProviderFeedService>();
        services.AddScoped<IRequestAttachmentService, RequestAttachmentService>();
        services.AddScoped<IProviderQuoteService, ProviderQuoteService>();
        services.AddScoped<ICustomerQuoteService, CustomerQuoteService>();
        services.AddScoped<IProviderAppointmentService, ProviderAppointmentService>();
        services.AddScoped<ICustomerAppointmentService, CustomerAppointmentService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<INotifier, Notifier>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        return services;
    }
}
