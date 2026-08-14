import '../domain/notification_failure.dart';
import '../domain/notification_preference.dart';

class NotificationPreferencesState {
  const NotificationPreferencesState({this.isLoading = true, this.failure, this.preferences = const [], this.toggleFailure});

  final bool isLoading;
  final NotificationFailure? failure;
  final List<NotificationPreference> preferences;

  /// Set when an optimistic [preferences] toggle gets reverted because the
  /// backend rejected it — distinct from [failure] (the initial load
  /// failing), which replaces the whole screen rather than one switch.
  final NotificationFailure? toggleFailure;

  NotificationPreferencesState copyWith({
    bool? isLoading,
    NotificationFailure? failure,
    List<NotificationPreference>? preferences,
    NotificationFailure? toggleFailure,
  }) =>
      NotificationPreferencesState(
        isLoading: isLoading ?? this.isLoading,
        failure: failure,
        preferences: preferences ?? this.preferences,
        toggleFailure: toggleFailure,
      );
}
