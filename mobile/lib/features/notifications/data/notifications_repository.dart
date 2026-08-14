import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../domain/device_platform.dart';
import '../domain/notification_category.dart';
import '../domain/notification_failure.dart';
import '../domain/notification_preference.dart';
import '../domain/notification_result.dart';
import 'notifications_api.dart';

class NotificationsRepository {
  NotificationsRepository(this._api);

  final NotificationsApi _api;

  Future<NotificationResult<void>> registerDevice(String fcmToken, DevicePlatform platform) =>
      _guard(() => _api.registerDevice(fcmToken, platform));

  Future<NotificationResult<void>> removeDevice(String fcmToken) => _guard(() => _api.removeDevice(fcmToken));

  Future<NotificationResult<List<NotificationPreference>>> getPreferences() => _guard(() => _api.getPreferences());

  Future<NotificationResult<void>> setPreference(NotificationCategory category, bool isEnabled) =>
      _guard(() => _api.setPreference(category, isEnabled));

  Future<NotificationResult<T>> _guard<T>(Future<T> Function() action) async {
    try {
      return NotificationSuccess(await action());
    } on DioException catch (e) {
      return NotificationError(NotificationFailure.fromDioException(e));
    }
  }
}

final notificationsRepositoryProvider = Provider<NotificationsRepository>((ref) => NotificationsRepository(ref.watch(notificationsApiProvider)));
