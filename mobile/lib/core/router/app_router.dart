import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/auth/presentation/auth_screen.dart';
import '../../features/splash/presentation/splash_screen.dart';

/// Route names are used (not raw path strings) everywhere a route is
/// referenced from outside this file, so a path can change without a
/// find-and-replace across features.
abstract final class AppRoutes {
  static const splash = 'splash';
  static const auth = 'auth';
}

/// Deep links use the `asnan://` custom scheme (registered in
/// AndroidManifest.xml's intent-filter and Info.plist's CFBundleURLTypes).
/// Real targets (asnan://appointments/{id}, asnan://chat/{conversationId})
/// are added as those features land; only the scheme plumbing exists here.
final appRouterProvider = Provider<GoRouter>((ref) {
  return GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(
        path: '/',
        name: AppRoutes.splash,
        builder: (context, state) => const SplashScreen(),
      ),
      GoRoute(
        path: '/auth',
        name: AppRoutes.auth,
        builder: (context, state) => const AuthScreen(),
      ),
    ],
  );
});
