import 'package:firebase_messaging/firebase_messaging.dart';

import '../domain/remote_message_payload.dart';
import 'fcm_service.dart';

RemoteMessagePayload _toPayload(RemoteMessage message) => RemoteMessagePayload(
      title: message.notification?.title,
      body: message.notification?.body,
      deepLink: message.data['deepLink'] as String?,
    );

class FirebaseFcmService implements FcmService {
  FirebaseFcmService(this._messaging);

  final FirebaseMessaging _messaging;

  @override
  Future<bool> requestPermission() async {
    final settings = await _messaging.requestPermission();
    return settings.authorizationStatus == AuthorizationStatus.authorized ||
        settings.authorizationStatus == AuthorizationStatus.provisional;
  }

  @override
  Future<String?> getToken() => _messaging.getToken();

  @override
  Stream<String> get onTokenRefresh => _messaging.onTokenRefresh;

  @override
  Stream<RemoteMessagePayload> get onForegroundMessage => FirebaseMessaging.onMessage.map(_toPayload);

  @override
  Future<RemoteMessagePayload?> getInitialMessage() async {
    final message = await _messaging.getInitialMessage();
    return message == null ? null : _toPayload(message);
  }

  @override
  Stream<RemoteMessagePayload> get onMessageOpenedApp => FirebaseMessaging.onMessageOpenedApp.map(_toPayload);
}
