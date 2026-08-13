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
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _restoreAndRoute());
  }

  Future<void> _restoreAndRoute() async {
    await ref.read(authControllerProvider.notifier).restoreSession();
    if (!mounted) return;

    final status = ref.read(authControllerProvider);
    context.goNamed(status == AuthStatus.authenticated ? AppRoutes.home : AppRoutes.login);
  }

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      body: Center(child: CircularProgressIndicator()),
    );
  }
}
