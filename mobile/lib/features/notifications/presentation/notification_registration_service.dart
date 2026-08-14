import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/fcm_service.dart';
import '../data/notifications_repository.dart';
import '../domain/device_platform.dart';

/// Registers/removes this device's FCM token with the backend — issue #32.
/// Called from AuthController at exactly the points ARCHITECTURE.md §10
/// specifies: on login/app-start (registerAsync) and on logout
/// (removeAsync, called *before* the access token is invalidated — the
/// backend endpoint requires it).
///
/// Every failure here is swallowed: a device that never registered (no
/// Firebase project configured, permission denied, a transient network
/// error) just means that device doesn't receive pushes yet — nothing else
/// in the app depends on registration succeeding.
class NotificationRegistrationService {
  NotificationRegistrationService(this._ref);

  final Ref _ref;
  StreamSubscription<String>? _tokenRefreshSub;

  Future<void> registerAsync() async {
    try {
      final fcm = _ref.read(fcmServiceProvider);
      if (fcm == null) return;

      final granted = await fcm.requestPermission();
      if (!granted) return;

      final token = await fcm.getToken();
      if (token != null) {
        await _ref.read(notificationsRepositoryProvider).registerDevice(token, DevicePlatform.current());
      }

      await _tokenRefreshSub?.cancel();
      _tokenRefreshSub = fcm.onTokenRefresh.listen((newToken) {
        unawaited(_ref.read(notificationsRepositoryProvider).registerDevice(newToken, DevicePlatform.current()));
      });
    } catch (_) {
      // Best-effort — see class doc comment.
    }
  }

  Future<void> removeAsync() async {
    try {
      final fcm = _ref.read(fcmServiceProvider);
      if (fcm == null) return;

      await _tokenRefreshSub?.cancel();
      _tokenRefreshSub = null;

      final token = await fcm.getToken();
      if (token != null) {
        await _ref.read(notificationsRepositoryProvider).removeDevice(token);
      }
    } catch (_) {
      // Best-effort — see class doc comment.
    }
  }
}

final notificationRegistrationServiceProvider = Provider<NotificationRegistrationService>((ref) => NotificationRegistrationService(ref));
