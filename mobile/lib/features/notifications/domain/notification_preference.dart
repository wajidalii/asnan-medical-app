import 'notification_category.dart';

/// Mirrors the backend's `NotificationPreferenceDto`.
class NotificationPreference {
  const NotificationPreference({required this.category, required this.isEnabled, required this.isDisableable});

  final NotificationCategory category;
  final bool isEnabled;
  final bool isDisableable;

  NotificationPreference copyWith({bool? isEnabled}) =>
      NotificationPreference(category: category, isEnabled: isEnabled ?? this.isEnabled, isDisableable: isDisableable);

  factory NotificationPreference.fromJson(Map<String, dynamic> json) => NotificationPreference(
        category: NotificationCategory.fromValue(json['category'] as int),
        isEnabled: json['isEnabled'] as bool,
        isDisableable: json['isDisableable'] as bool,
      );
}
