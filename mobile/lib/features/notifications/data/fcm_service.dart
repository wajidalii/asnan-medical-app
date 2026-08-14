import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../domain/remote_message_payload.dart';

/// Wraps `firebase_messaging` behind app-level streams/methods — issue #32.
/// Kept separate from FirebaseFcmService purely so a fake can stand in for
/// tests, which have no Firebase project to talk to, same rationale as
/// ChatHubClient (#29) and CalendarService (#26).
abstract class FcmService {
  /// True if the user granted permission (or permission isn't required on this platform).
  Future<bool> requestPermission();

  /// Null if permission hasn't been granted, or no token is available yet.
  Future<String?> getToken();

  /// Fires whenever the underlying FCM token rotates (device restore, app reinstall, token expiry).
  Stream<String> get onTokenRefresh;

  /// A push that arrived while the app was in the foreground — no system notification is shown automatically, this is the hook for an in-app banner.
  Stream<RemoteMessagePayload> get onForegroundMessage;

  /// The push that launched the app from a fully-terminated state by being tapped, if any. Null on a normal cold start.
  Future<RemoteMessagePayload?> getInitialMessage();

  /// Fires when a push is tapped while the app was backgrounded (not terminated).
  Stream<RemoteMessagePayload> get onMessageOpenedApp;
}

/// Unlike ChatHubClient's factory provider, this can't construct a working
/// default on demand: the real FirebaseFcmService needs FirebaseMessaging.instance,
/// valid only after Firebase.initializeApp() — an async, fallible call that
/// must happen once in main() before runApp. So main.dart always overrides
/// this via ProviderScope, with either a real FirebaseFcmService or null
/// (no Firebase project configured — see FcmService's doc comment on why a
/// null value everywhere just means "push isn't available," never an error).
final fcmServiceProvider = Provider<FcmService?>((ref) {
  throw UnimplementedError('fcmServiceProvider must be overridden in main.dart once Firebase has (or has not) initialized.');
});
