import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/appointments/domain/appointment_summary.dart';
import '../../features/appointments/presentation/appointment_details_screen.dart';
import '../../features/appointments/presentation/appointments_list_screen.dart';
import '../../features/auth/presentation/login_screen.dart';
import '../../features/auth/presentation/signup_otp_screen.dart';
import '../../features/auth/presentation/signup_password_screen.dart';
import '../../features/auth/presentation/signup_screen.dart';
import '../../features/booking/presentation/booking_screen.dart';
import '../../features/chat/presentation/chat_screen.dart';
import '../../features/doctors/presentation/doctor_detail_screen.dart';
import '../../features/doctors/presentation/doctor_list_screen.dart';
import '../../features/notifications/presentation/notification_preferences_screen.dart';
import '../../features/payments/presentation/payment_confirmation_screen.dart';
import '../../features/payments/presentation/payment_review_screen.dart';
import '../../features/profile/presentation/profile_screen.dart';
import '../../features/sessions/presentation/sessions_screen.dart';
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
  static const booking = 'booking';
  static const paymentReview = 'payment-review';
  static const paymentConfirmation = 'payment-confirmation';
  static const appointments = 'appointments';
  static const appointmentDetails = 'appointment-details';
  static const chat = 'chat';
  static const notificationPreferences = 'notification-preferences';
  static const profile = 'profile';
  static const sessions = 'sessions';
}

/// Push-notification deep links (`asnan://appointments/{id}`,
/// `asnan://chat/{conversationId}` — issue #32) are handled imperatively by
/// DeepLinkService via this same router's `pushNamed`, not via new route
/// entries: appointment links fetch the full AppointmentSummary first (see
/// AppointmentsRepository.getById, added for exactly this) then push
/// appointmentDetails with it as `extra`, and chat links push the chat
/// route directly since its only dynamic segment is conversationId.
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
            routes: [
              GoRoute(
                path: 'book',
                name: AppRoutes.booking,
                builder: (context, state) => BookingScreen(doctorId: state.pathParameters['id']!),
                routes: [
                  GoRoute(
                    path: 'review',
                    name: AppRoutes.paymentReview,
                    builder: (context, state) => PaymentReviewScreen(doctorId: state.pathParameters['id']!),
                  ),
                  GoRoute(
                    path: 'confirmation',
                    name: AppRoutes.paymentConfirmation,
                    builder: (context, state) => PaymentConfirmationScreen(doctorId: state.pathParameters['id']!),
                  ),
                ],
              ),
            ],
          ),
          GoRoute(
            path: 'notification-preferences',
            name: AppRoutes.notificationPreferences,
            builder: (context, state) => const NotificationPreferencesScreen(),
          ),
          GoRoute(
            path: 'profile',
            name: AppRoutes.profile,
            builder: (context, state) => const ProfileScreen(),
          ),
          GoRoute(
            path: 'sessions',
            name: AppRoutes.sessions,
            builder: (context, state) => const SessionsScreen(),
          ),
          GoRoute(
            path: 'appointments',
            name: AppRoutes.appointments,
            builder: (context, state) => const AppointmentsListScreen(),
            routes: [
              GoRoute(
                path: 'details',
                name: AppRoutes.appointmentDetails,
                // Passed via `extra` — the caller already has the full
                // AppointmentSummary, either from the list screen or (for a
                // deep link) a GET /appointments/{id} fetch done beforehand.
                builder: (context, state) => AppointmentDetailsScreen(appointment: state.extra! as AppointmentSummary),
              ),
            ],
          ),
          // A sibling of appointments/details, not nested under it — a chat
          // deep link (issue #32) only ever has a conversationId, and
          // nesting under appointment-details would require also supplying
          // that route's own `extra` (the full AppointmentSummary) just to
          // satisfy an ancestor page neither the link nor ChatScreen itself
          // needs.
          GoRoute(
            path: 'chat/:conversationId',
            name: AppRoutes.chat,
            builder: (context, state) => ChatScreen(
              conversationId: state.pathParameters['conversationId']!,
              title: state.extra! as String,
            ),
          ),
        ],
      ),
    ],
  );
});
