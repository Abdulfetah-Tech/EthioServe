using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;

namespace EthioServe.Tests.Unit;

public sealed class NotificationDomainTests
{
    private static Notification NewNotification() =>
        new(Guid.NewGuid(), NotificationType.General, "A title", "A message", "ServiceRequest", Guid.NewGuid());

    [Fact]
    public void A_new_notification_is_unread_with_trimmed_text()
    {
        var notification = new Notification(Guid.NewGuid(), NotificationType.QuoteReceived, "  A title  ", "  A message  ");

        Assert.False(notification.IsRead);
        Assert.Equal("A title", notification.Title);
        Assert.Equal("A message", notification.Message);
        Assert.Equal(NotificationType.QuoteReceived, notification.Type);
        Assert.Null(notification.RelatedEntityType);
        Assert.Null(notification.RelatedEntityId);
    }

    [Fact]
    public void The_related_item_is_kept()
    {
        var id = Guid.NewGuid();

        var notification = new Notification(Guid.NewGuid(), NotificationType.AppointmentUpdated, "Title", "Message", "Appointment", id);

        Assert.Equal("Appointment", notification.RelatedEntityType);
        Assert.Equal(id, notification.RelatedEntityId);
    }

    [Theory]
    [InlineData("", "Message")]
    [InlineData("   ", "Message")]
    [InlineData("Title", "")]
    [InlineData("Title", "   ")]
    public void A_notification_needs_a_title_and_a_message(string title, string message)
    {
        Assert.Throws<DomainException>(() => new Notification(Guid.NewGuid(), NotificationType.General, title, message));
    }

    [Fact]
    public void Marking_it_read_works_and_is_safe_to_repeat()
    {
        var notification = NewNotification();

        notification.MarkRead();
        notification.MarkRead();

        Assert.True(notification.IsRead);
    }
}