import 'dart:io';

/// Mirrors the backend's `DevicePlatform` enum (int-valued in JSON).
enum DevicePlatform {
  android(1),
  ios(2);

  const DevicePlatform(this.value);

  final int value;

  static DevicePlatform current() => Platform.isIOS ? DevicePlatform.ios : DevicePlatform.android;
}
