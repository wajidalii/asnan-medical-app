import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/session_refresher.dart';
import '../../notifications/presentation/notification_registration_service.dart';
import '../data/auth_repository.dart';

enum AuthStatus { unknown, unauthenticated, authenticated }

/// Set right before [AuthController.sessionExpired] transitions to
/// unauthenticated because a mid-use token refresh failed (revoked/expired
/// session) — as opposed to an explicit user-initiated logout, which needs
/// no explanation. AsnanApp's top-level auth listener reads and clears this
/// to decide whether to show a "please sign in again" message alongside the
/// redirect to login.
final sessionExpiredNoticeProvider = StateProvider<bool>((ref) => false);

/// Tracks only the app-wide session status. Per-screen submit/loading/error
/// state (login form, signup wizard) lives in their own controllers, which
/// call [markAuthenticated] on success — keeping "am I logged in" separate
/// from "is this particular form currently submitting."
class AuthController extends Notifier<AuthStatus> {
  @override
  AuthStatus build() => AuthStatus.unknown;

  /// Called once from the splash screen — the silent refresh from
  /// ARCHITECTURE.md §4.5. Failure because the backend rejected the stored
  /// token routes to login. Failure because the backend couldn't be reached
  /// at all leaves [state] at [AuthStatus.unknown] instead — the splash
  /// screen reads that as "couldn't verify, offer a retry" rather than
  /// silently treating a connectivity problem as "not logged in."
  Future<void> restoreSession() async {
    final repository = ref.read(authRepositoryProvider);
    try {
      final restored = await repository.tryRestoreSession(() => refreshAccessToken(ref));
      state = restored ? AuthStatus.authenticated : AuthStatus.unauthenticated;
      if (restored) {
        unawaited(ref.read(notificationRegistrationServiceProvider).registerAsync());
      }
    } on SessionRestoreUnavailableException {
      // Leave state as AuthStatus.unknown.
    }
  }

  void markAuthenticated() {
    state = AuthStatus.authenticated;
    unawaited(ref.read(notificationRegistrationServiceProvider).registerAsync());
  }

  Future<void> logout() async {
    // Runs while the access token is still valid — the removal endpoint requires it.
    await ref.read(notificationRegistrationServiceProvider).removeAsync();
    await ref.read(authRepositoryProvider).logout();
    state = AuthStatus.unauthenticated;
  }

  Future<void> logoutAllDevices() async {
    await ref.read(notificationRegistrationServiceProvider).removeAsync();
    await ref.read(authRepositoryProvider).logoutAllDevices();
    state = AuthStatus.unauthenticated;
  }

  /// Called by the auth interceptor (DioClient's AuthInterceptor) when a
  /// mid-use 401 triggers a token refresh and that refresh itself fails —
  /// the session ended without the user explicitly signing out. Only
  /// authenticated -> unauthenticated is a real transition worth announcing;
  /// if we're already unauthenticated (or mid-restore) this is a no-op.
  void sessionExpired() {
    if (state != AuthStatus.authenticated) return;
    ref.read(sessionExpiredNoticeProvider.notifier).state = true;
    state = AuthStatus.unauthenticated;
  }
}

final authControllerProvider = NotifierProvider<AuthController, AuthStatus>(AuthController.new);
