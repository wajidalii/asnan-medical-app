/// Mirrors the backend's `SessionSummary` record.
class SessionSummary {
  const SessionSummary({
    required this.id,
    required this.deviceId,
    required this.deviceName,
    required this.lastSeenAtUtc,
    required this.absoluteExpiresAtUtc,
  });

  final String id;
  final String deviceId;
  final String? deviceName;
  final DateTime lastSeenAtUtc;
  final DateTime absoluteExpiresAtUtc;

  factory SessionSummary.fromJson(Map<String, dynamic> json) => SessionSummary(
        id: json['id'] as String,
        deviceId: json['deviceId'] as String,
        deviceName: json['deviceName'] as String?,
        lastSeenAtUtc: DateTime.parse(json['lastSeenAtUtc'] as String),
        absoluteExpiresAtUtc: DateTime.parse(json['absoluteExpiresAtUtc'] as String),
      );
}
