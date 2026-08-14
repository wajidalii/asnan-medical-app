import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'notification_preferences_controller.dart';
import 'notification_preferences_state.dart';

/// Notification-category opt-outs — issue #32. Non-disableable (transactional)
/// categories render with a disabled, always-on switch and a "Required" label
/// rather than being hidden, per the issue's AC ("clearly marked as such").
class NotificationPreferencesScreen extends ConsumerStatefulWidget {
  const NotificationPreferencesScreen({super.key});

  @override
  ConsumerState<NotificationPreferencesScreen> createState() => _NotificationPreferencesScreenState();
}

class _NotificationPreferencesScreenState extends ConsumerState<NotificationPreferencesScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(notificationPreferencesControllerProvider.notifier).load();
    });
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(notificationPreferencesControllerProvider);
    final controller = ref.read(notificationPreferencesControllerProvider.notifier);

    ref.listen(notificationPreferencesControllerProvider, (previous, next) {
      if (next.toggleFailure != null) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(next.toggleFailure!.message)));
        controller.dismissToggleFailure();
      }
    });

    return Scaffold(
      appBar: AppBar(title: const Text('Notification preferences')),
      body: _buildBody(context, state, controller),
    );
  }

  Widget _buildBody(BuildContext context, NotificationPreferencesState state, NotificationPreferencesController controller) {
    if (state.isLoading && state.preferences.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }

    if (state.failure != null && state.preferences.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(state.failure!.message, textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton(onPressed: controller.retry, child: const Text('Retry')),
            ],
          ),
        ),
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.symmetric(vertical: 8),
      itemCount: state.preferences.length,
      separatorBuilder: (_, _) => const Divider(height: 1),
      itemBuilder: (context, index) {
        final preference = state.preferences[index];
        return SwitchListTile(
          title: Text(preference.category.label),
          subtitle: preference.isDisableable ? null : const Text('Required — cannot be turned off'),
          value: preference.isEnabled,
          onChanged: preference.isDisableable ? (value) => controller.setEnabled(preference.category, value) : null,
        );
      },
    );
  }
}
