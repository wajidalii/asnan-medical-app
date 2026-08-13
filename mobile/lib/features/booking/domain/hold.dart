class Hold {
  const Hold({
    required this.id,
    required this.doctorId,
    required this.slotStartUtc,
    required this.slotEndUtc,
    required this.holdToken,
    required this.expiresAtUtc,
  });

  final String id;
  final String doctorId;
  final DateTime slotStartUtc;
  final DateTime slotEndUtc;
  final String holdToken;
  final DateTime expiresAtUtc;

  factory Hold.fromJson(Map<String, dynamic> json) => Hold(
        id: json['id'] as String,
        doctorId: json['doctorId'] as String,
        slotStartUtc: DateTime.parse(json['slotStartUtc'] as String),
        slotEndUtc: DateTime.parse(json['slotEndUtc'] as String),
        holdToken: json['holdToken'] as String,
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
      );
}
