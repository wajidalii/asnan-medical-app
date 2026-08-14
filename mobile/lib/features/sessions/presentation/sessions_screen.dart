import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../auth/presentation/auth_controller.dart';
import '../domain/session_summary.dart';
import '../domain/sessions_result.dart';
import 'sessions_controller.dart';
import 'sessions_state.dart';

String _formatLastSeen(DateTime utc) {
  final local = utc.toLocal();
  final date = '${local.year}-${local.month.toString().padLeft(2, '0')}-${local.day.toString().padLeft(2, '0')}';
  final time = '${local.hour.toString().padLeft(2, '0')}:${local.minute.toString().padLeft(2, '0')}';
  return '$date $time';
}

Future<bool> _confirm(BuildContext context, {required String title, required String message}) async {
  final confirmed = await showDialog<bool>(
    context: context,
    builder: (context) => AlertDialog(
      title: Text(title),
      content: Text(message),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')),
        TextButton(
          onPressed: () => Navigator.pop(context, true),
          style: TextButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
          child: const Text('Log out'),
        ),
      ],
    ),
  );
  return confirmed ?? false;
}

/// Devices/sessions management — issue #35. Distinguishes the caller's own
/// current device (matched by the locally-stored device id against each
/// session's deviceId) from every other device: logging out the current
/// one goes through AuthController.logout() (clears local session state
/// and routes to login, per the issue's AC), logging out any other device
/// just revokes that session server-side and removes its row.
class SessionsScreen extends ConsumerStatefulWidget {
  const SessionsScreen({super.key});

  @override
  ConsumerState<SessionsScreen> createState() => _SessionsScreenState();
}

class _SessionsScreenState extends ConsumerState<SessionsScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(sessionsControllerProvider.notifier).load();
    });
  }

  Future<void> _logOutCurrentDevice() async {
    final confirmed = await _confirm(
      context,
      title: 'Log out this device?',
      message: "You'll need to sign in again on this device.",
    );
    if (!confirmed || !mounted) return;

    await ref.read(authControllerProvider.notifier).logout();
  }

  Future<void> _logOutOtherDevice(SessionSummary session) async {
    final confirmed = await _confirm(
      context,
      title: 'Log out this device?',
      message: '${session.deviceName ?? 'This device'} will need to sign in again.',
    );
    if (!confirmed || !mounted) return;

    final result = await ref.read(sessionsControllerProvider.notifier).revokeSession(session.id);
    if (!mounted) return;

    if (result case SessionsError(:final failure)) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(failure.message)));
    }
  }

  Future<void> _logOutAllDevices() async {
    final confirmed = await _confirm(
      context,
      title: 'Log out of all devices?',
      message: "You'll be signed out everywhere, including this device.",
    );
    if (!confirmed || !mounted) return;

    await ref.read(authControllerProvider.notifier).logoutAllDevices();
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(sessionsControllerProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Devices & Sessions'),
        actions: [
          if (state.sessions.isNotEmpty)
            TextButton(
              onPressed: _logOutAllDevices,
              child: Text('Log out all', style: TextStyle(color: Theme.of(context).colorScheme.onPrimary)),
            ),
        ],
      ),
      body: _buildBody(context, state),
    );
  }

  Widget _buildBody(BuildContext context, SessionsState state) {
    if (state.isLoading && state.sessions.isEmpty && state.failure == null) {
      return const Center(child: CircularProgressIndicator());
    }

    if (state.failure != null && state.sessions.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(state.failure!.message, textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton(onPressed: () => ref.read(sessionsControllerProvider.notifier).retry(), child: const Text('Retry')),
            ],
          ),
        ),
      );
    }

    if (state.sessions.isEmpty) {
      return const Center(child: Text('No active sessions.'));
    }

    return ListView.separated(
      itemCount: state.sessions.length,
      separatorBuilder: (_, _) => const Divider(height: 1),
      itemBuilder: (context, index) {
        final session = state.sessions[index];
        final isCurrent = state.currentDeviceId != null && session.deviceId == state.currentDeviceId;
        final isRevoking = state.revokingSessionId == session.id;

        return ListTile(
          leading: const Icon(Icons.devices),
          title: Text(session.deviceName ?? 'Unknown device'),
          subtitle: Text('Last seen ${_formatLastSeen(session.lastSeenAtUtc)}${isCurrent ? ' · This device' : ''}'),
          trailing: isRevoking
              ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
              : TextButton(
                  onPressed: isCurrent ? _logOutCurrentDevice : () => _logOutOtherDevice(session),
                  child: const Text('Log out'),
                ),
        );
      },
    );
  }
}
