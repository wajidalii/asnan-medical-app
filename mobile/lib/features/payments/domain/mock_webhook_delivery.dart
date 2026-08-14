/// Mirrors the backend's `MockWebhookDelivery` — the signed payload the mock
/// provider's confirm endpoint hands back, which this app then relays to
/// POST /payments/webhook to simulate the provider's own webhook call
/// (there being no real provider yet to deliver it automatically — #19/#20).
class MockWebhookDelivery {
  const MockWebhookDelivery({
    required this.providerEventId,
    required this.rawBody,
    required this.signatureHeaderValue,
  });

  final String providerEventId;
  final String rawBody;
  final String signatureHeaderValue;

  factory MockWebhookDelivery.fromJson(Map<String, dynamic> json) => MockWebhookDelivery(
        providerEventId: json['providerEventId'] as String,
        rawBody: json['rawBody'] as String,
        signatureHeaderValue: json['signatureHeaderValue'] as String,
      );
}
