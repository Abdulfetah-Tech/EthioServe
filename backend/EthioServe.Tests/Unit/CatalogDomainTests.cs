using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;

namespace EthioServe.Tests.Unit;

public sealed class CatalogDomainTests
{
    private static Service NewService() =>
        new(Guid.NewGuid(), "Pipe repair", PricingType.Fixed, 300m, "Fix leaking pipes");

    [Fact]
    public void New_categories_and_services_start_active()
    {
        Assert.True(new ServiceCategory("Plumbing").IsActive);
        Assert.True(NewService().IsActive);
    }

    [Fact]
    public void Category_update_changes_the_editable_fields()
    {
        var category = new ServiceCategory("Plumbing", "Old", "old-icon");

        category.Update("Plumbing & Heating", "New", "new-icon");

        Assert.Equal("Plumbing & Heating", category.Name);
        Assert.Equal("New", category.Description);
        Assert.Equal("new-icon", category.IconKey);
    }

    [Fact]
    public void Category_update_rejects_a_blank_name()
    {
        var category = new ServiceCategory("Plumbing");

        Assert.Throws<DomainException>(() => category.Update("   ", null, null));
        Assert.Equal("Plumbing", category.Name);
    }

    [Fact]
    public void Category_can_be_deactivated_and_reactivated()
    {
        var category = new ServiceCategory("Plumbing");

        category.Deactivate();
        Assert.False(category.IsActive);

        category.Activate();
        Assert.True(category.IsActive);
    }

    [Fact]
    public void Service_update_changes_the_editable_fields_but_not_the_category()
    {
        var service = NewService();
        var categoryId = service.CategoryId;

        service.Update("Pipe replacement", null, null, PricingType.Negotiable);

        Assert.Equal("Pipe replacement", service.Name);
        Assert.Null(service.BasePrice);
        Assert.Equal(PricingType.Negotiable, service.PricingType);
        Assert.Equal(categoryId, service.CategoryId);
    }

    [Fact]
    public void Service_update_rejects_a_negative_price()
    {
        var service = NewService();

        Assert.Throws<DomainException>(() => service.Update("Pipe repair", null, -1m, PricingType.Fixed));
        Assert.Equal(300m, service.BasePrice);
    }

    [Fact]
    public void Service_can_be_deactivated_and_reactivated()
    {
        var service = NewService();

        service.Deactivate();
        Assert.False(service.IsActive);

        service.Activate();
        Assert.True(service.IsActive);
    }
}