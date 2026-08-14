import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/notifications_repository.dart';
import '../domain/notification_category.dart';
import '../domain/notification_result.dart';
import 'notification_preferences_state.dart';

class NotificationPreferencesController extends Notifier<NotificationPreferencesState> {
  @override
  NotificationPreferencesState build() => const NotificationPreferencesState();

  Future<void> load() async {
    state = state.copyWith(isLoading: true);

    final result = await ref.read(notificationsRepositoryProvider).getPreferences();

    switch (result) {
      case NotificationSuccess(:final value):
        state = state.copyWith(isLoading: false, preferences: value);
      case NotificationError(:final failure):
        state = state.copyWith(isLoading: false, failure: failure);
    }
  }

  Future<void> retry() => load();

  /// Optimistic — reverts if the backend rejects it (e.g. a non-disableable category, which the UI shouldn't offer but is guarded here too).
  Future<void> setEnabled(NotificationCategory category, bool isEnabled) async {
    final previous = state.preferences;
    state = state.copyWith(
      preferences: [for (final p in previous) if (p.category == category) p.copyWith(isEnabled: isEnabled) else p],
    );

    final result = await ref.read(notificationsRepositoryProvider).setPreference(category, isEnabled);
    if (result is NotificationError) {
      state = state.copyWith(preferences: previous);
    }
  }
}

final notificationPreferencesControllerProvider = NotifierProvider<NotificationPreferencesController, NotificationPreferencesState>(
  NotificationPreferencesController.new,
);
