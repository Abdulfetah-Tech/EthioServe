namespace EthioServe.Domain.Enums;

public enum PricingType { Fixed, Hourly, Negotiable, StartingFrom }

public enum VerificationStatus { Unverified, Pending, Verified, Rejected, Suspended }

public enum DocumentType { NationalId, Certificate, License, BusinessRegistration, Other }

public enum DocumentStatus { Pending, Approved, Rejected }

public enum ServiceRequestStatus { Pending, Quoted, Accepted, Scheduled, InProgress, Completed, Cancelled, Rejected }

public enum QuoteStatus { Pending, Accepted, Rejected, Expired, Cancelled }

public enum AppointmentStatus { Scheduled, Confirmed, InProgress, Completed, Cancelled, NoShow }

public enum PaymentStatus { Pending, Processing, Completed, Failed, Refunded, Cancelled }

public enum PaymentMethod { Mock, Chapa, Telebirr, BankTransfer, Cash }

public enum NotificationType
{
    General,
    NewServiceRequest,
    QuoteReceived,
    QuoteAccepted,
    AppointmentUpdated,
    PaymentCompleted,
    ReviewReceived,
    ProviderVerified
}