/// Mirrors the backend's OtpChannel enum (Asnan.Domain.Enums.OtpChannel) —
/// System.Text.Json serializes C# enums as their underlying int by default,
/// so these values must stay in sync with the backend definition.
enum OtpChannel {
  email(1),
  sms(2);

  const OtpChannel(this.value);

  final int value;
}
