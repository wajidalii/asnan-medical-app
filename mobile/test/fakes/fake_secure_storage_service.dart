import 'package:asnan/core/storage/secure_storage_service.dart';

/// In-memory stand-in for tests — flutter_secure_storage needs platform
/// channels that plain `flutter test` doesn't provide.
class FakeSecureStorageService implements SecureStorageService {
  final _values = <String, String>{};

  static const _refreshTokenKey = 'refresh_token';
  static const _deviceIdKey = 'device_id';

  @override
  Future<void> saveRefreshToken(String token) async => _values[_refreshTokenKey] = token;

  @override
  Future<String?> readRefreshToken() async => _values[_refreshTokenKey];

  @override
  Future<void> deleteRefreshToken() async => _values.remove(_refreshTokenKey);

  @override
  Future<String?> readDeviceId() async => _values[_deviceIdKey];

  @override
  Future<void> saveDeviceId(String id) async => _values[_deviceIdKey] = id;

  @override
  Future<void> clearAll() async => _values.clear();
}
