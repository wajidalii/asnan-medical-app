/// App-level view of an incoming push — decoupled from `firebase_messaging`'s
/// `RemoteMessage` the same way ChatMessage decouples from SignalR's raw
/// arguments, so FcmService's consumers (and their tests) never touch the
/// third-party type directly.
class RemoteMessagePayload {
  const RemoteMessagePayload({this.title, this.body, this.deepLink});

  final String? title;
  final String? body;

  /// The `asnan://...` URI from the message's data payload (`data['deepLink']`), if present.
  final String? deepLink;
}
