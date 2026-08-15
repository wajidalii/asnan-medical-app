import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../auth/presentation/auth_controller.dart';

/// Attempts a silent token refresh (§4.5 of ARCHITECTURE.md) before deciding
/// whether to land on the authenticated app shell or the login screen.
class SplashScreen extends ConsumerStatefulWidget {
  const SplashScreen({super.key});

  @override
  ConsumerState<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends ConsumerState<SplashScreen> {
  bool _couldNotVerify = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _restoreAndRoute());
  }

  Future<void> _restoreAndRoute() async {
    setState(() => _couldNotVerify = false);
    await ref.read(authControllerProvider.notifier).restoreSession();
    if (!mounted) return;

    final status = ref.read(authControllerProvider);
    if (status == AuthStatus.unknown) {
      // restoreSession() couldn't reach the backend at all (offline/timeout)
      // — leave the user here with a retry rather than silently treating a
      // connectivity problem as "not logged in."
      setState(() => _couldNotVerify = true);
      return;
    }
    context.goNamed(status == AuthStatus.authenticated ? AppRoutes.home : AppRoutes.login);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      body: Center(
        child: _couldNotVerify ? _OfflineState(onRetry: _restoreAndRoute) : _CheckingSessionState(theme: theme),
      ),
    );
  }
}

class _CheckingSessionState extends StatelessWidget {
  const _CheckingSessionState({required this.theme});

  final ThemeData theme;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 64,
          height: 64,
          alignment: Alignment.center,
          decoration: BoxDecoration(border: Border.all(color: theme.colorScheme.primary)),
          child: Text('A', style: theme.textTheme.headlineMedium?.copyWith(color: theme.colorScheme.primary)),
        ),
        const SizedBox(height: 18),
        Text('Asnan', style: theme.textTheme.titleLarge?.copyWith(letterSpacing: -0.2)),
        const SizedBox(height: 18),
        const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2)),
        const SizedBox(height: 18),
        Text(
          'Checking your session…',
          style: theme.textTheme.bodySmall?.copyWith(color: theme.textTheme.bodySmall!.color!.withValues(alpha: 0.55)),
        ),
      ],
    );
  }
}

class _OfflineState extends StatelessWidget {
  const _OfflineState({required this.onRetry});

  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final divider = theme.colorScheme.outlineVariant;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 40),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 56,
            height: 56,
            alignment: Alignment.center,
            decoration: BoxDecoration(border: Border.all(color: divider)),
            child: Icon(Icons.wifi_off, size: 24, color: theme.textTheme.bodyMedium?.color?.withValues(alpha: 0.5)),
          ),
          const SizedBox(height: 16),
          Text("You're offline", style: theme.textTheme.titleLarge, textAlign: TextAlign.center),
          const SizedBox(height: 8),
          Text(
            "We couldn't verify your session. Check your connection and try again.",
            style: theme.textTheme.bodySmall?.copyWith(color: theme.textTheme.bodySmall!.color!.withValues(alpha: 0.6)),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 16),
          OutlinedButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh, size: 16),
            label: const Text('Retry'),
          ),
        ],
      ),
    );
  }
}
