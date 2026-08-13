import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../auth/presentation/auth_controller.dart';

/// Temporary landing screen for an authenticated session. Replaced by the
/// real home feature (search, specialties, upcoming appointment, etc.) in
/// Milestone 3 — exists here only so login/signup/session-restore have a
/// real destination to route to and this feature is end-to-end testable.
class HomePlaceholderScreen extends ConsumerWidget {
  const HomePlaceholderScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Asnan'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Sign out',
            onPressed: () => ref.read(authControllerProvider.notifier).logout(),
          ),
        ],
      ),
      body: const Center(child: Text("You're signed in.")),
    );
  }
}
