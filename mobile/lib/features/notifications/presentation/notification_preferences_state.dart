import '../domain/notification_failure.dart';
import '../domain/notification_preference.dart';

class NotificationPreferencesState {
  const NotificationPreferencesState({this.isLoading = true, this.failure, this.preferences = const []});

  final bool isLoading;
  final NotificationFailure? failure;
  final List<NotificationPreference> preferences;

  NotificationPreferencesState copyWith({bool? isLoading, NotificationFailure? failure, List<NotificationPreference>? preferences}) =>
      NotificationPreferencesState(
        isLoading: isLoading ?? this.isLoading,
        failure: failure,
        preferences: preferences ?? this.preferences,
      );
}
