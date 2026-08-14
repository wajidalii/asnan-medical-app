/// Mirrors the backend's `CancellationPreviewDto` — shown before the user
/// confirms cancellation, never assumed client-side (the refund-window
/// policy is server-configurable, see backend issue #24).
class CancellationPreview {
  const CancellationPreview({
    required this.appointmentId,
    required this.isAllowed,
    required this.refundPercentage,
    required this.refundAmount,
    required this.currency,
  });

  final String appointmentId;
  final bool isAllowed;
  final int refundPercentage;
  final double refundAmount;
  final String currency;

  factory CancellationPreview.fromJson(Map<String, dynamic> json) => CancellationPreview(
        appointmentId: json['appointmentId'] as String,
        isAllowed: json['isAllowed'] as bool,
        refundPercentage: json['refundPercentage'] as int,
        refundAmount: (json['refundAmount'] as num).toDouble(),
        currency: json['currency'] as String,
      );
}
