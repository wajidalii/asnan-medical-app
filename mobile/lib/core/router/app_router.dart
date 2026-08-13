import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/auth/presentation/login_screen.dart';
import '../../features/auth/presentation/signup_otp_screen.dart';
import '../../features/auth/presentation/signup_password_screen.dart';
import '../../features/auth/presentation/signup_screen.dart';
import '../../features/doctors/presentation/doctor_detail_screen.dart';
import '../../features/doctors/presentation/doctor_list_screen.dart';
import '../../features/splash/presentation/splash_screen.dart';

/// Route names are used (not raw path strings) everywhere a route is
/// referenced from outside this file, so a path can change without a
/// find-and-replace across features.
abstract final class AppRoutes {
  static const splash = 'splash';
  static const login = 'login';
  static const signup = 'signup';
  static const signupOtp = 'signup-otp';
  static const signupPassword = 'signup-password';
  static const home = 'home';
  static const doctorDetail = 'doctor-detail';
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
        path: '/login',
        name: AppRoutes.login,
        builder: (context, state) => const LoginScreen(),
      ),
      GoRoute(
        path: '/signup',
        name: AppRoutes.signup,
        builder: (context, state) => const SignupScreen(),
      ),
      GoRoute(
        path: '/signup/otp',
        name: AppRoutes.signupOtp,
        builder: (context, state) => const SignupOtpScreen(),
      ),
      GoRoute(
        path: '/signup/password',
        name: AppRoutes.signupPassword,
        builder: (context, state) => const SignupPasswordScreen(),
      ),
      GoRoute(
        path: '/home',
        name: AppRoutes.home,
        builder: (context, state) => const DoctorListScreen(),
        routes: [
          GoRoute(
            path: 'doctors/:id',
            name: AppRoutes.doctorDetail,
            builder: (context, state) => DoctorDetailScreen(doctorId: state.pathParameters['id']!),
          ),
        ],
      ),
    ],
  );
});
