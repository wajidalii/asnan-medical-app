import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/dio_client.dart';
import '../domain/device_platform.dart';
import '../domain/notification_category.dart';
import '../domain/notification_preference.dart';

class NotificationsApi {
  NotificationsApi(this._dio);

  final Dio _dio;

  Future<void> registerDevice(String fcmToken, DevicePlatform platform) =>
      _dio.post<void>('/notifications/devices', data: {'fcmToken': fcmToken, 'platform': platform.value});

  Future<void> removeDevice(String fcmToken) => _dio.delete<void>('/notifications/devices', data: {'fcmToken': fcmToken});

  Future<List<NotificationPreference>> getPreferences() async {
    final response = await _dio.get<List<dynamic>>('/notifications/preferences');
    return response.data!.map((e) => NotificationPreference.fromJson((e as Map).cast<String, dynamic>())).toList();
  }

  Future<void> setPreference(NotificationCategory category, bool isEnabled) =>
      _dio.put<void>('/notifications/preferences/${category.name}', data: {'isEnabled': isEnabled});
}

final notificationsApiProvider = Provider<NotificationsApi>((ref) => NotificationsApi(ref.watch(dioProvider)));
