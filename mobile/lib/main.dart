import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/router/app_router.dart';
import 'core/theme/app_theme.dart';
import 'features/auth/presentation/auth_controller.dart';

void main() {
  runApp(const ProviderScope(child: AsnanApp()));
}

class AsnanApp extends ConsumerWidget {
  const AsnanApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(appRouterProvider);

    // A session ending anywhere (explicit logout, or a background refresh
    // failure once that's wired to a foreground event) always routes back
    // to login — a single place for that rule rather than duplicating it
    // per screen.
    ref.listen(authControllerProvider, (previous, next) {
      if (previous == AuthStatus.authenticated && next == AuthStatus.unauthenticated) {
        router.goNamed(AppRoutes.login);
      }
    });

    return MaterialApp.router(
      title: 'Asnan',
      theme: AppTheme.light,
      routerConfig: router,
    );
  }
}
