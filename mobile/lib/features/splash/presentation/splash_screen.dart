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
    return Scaffold(
      body: Center(
        child: _couldNotVerify
            ? Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Text("Couldn't verify your session. Check your connection.", textAlign: TextAlign.center),
                    const SizedBox(height: 12),
                    FilledButton(onPressed: _restoreAndRoute, child: const Text('Retry')),
                  ],
                ),
              )
            : const CircularProgressIndicator(),
      ),
    );
  }
}
