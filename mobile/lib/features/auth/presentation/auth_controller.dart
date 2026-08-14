import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/session_refresher.dart';
import '../../notifications/presentation/notification_registration_service.dart';
import '../data/auth_repository.dart';

enum AuthStatus { unknown, unauthenticated, authenticated }

/// Tracks only the app-wide session status. Per-screen submit/loading/error
/// state (login form, signup wizard) lives in their own controllers, which
/// call [markAuthenticated] on success — keeping "am I logged in" separate
/// from "is this particular form currently submitting."
class AuthController extends Notifier<AuthStatus> {
  @override
  AuthStatus build() => AuthStatus.unknown;

  /// Called once from the splash screen — the silent refresh from
  /// ARCHITECTURE.md §4.5. Failure (no stored token, or the backend rejects
  /// it) routes to login instead of any authenticated screen.
  Future<void> restoreSession() async {
    final repository = ref.read(authRepositoryProvider);
    final restored = await repository.tryRestoreSession(() => refreshAccessToken(ref));
    state = restored ? AuthStatus.authenticated : AuthStatus.unauthenticated;
    if (restored) {
      unawaited(ref.read(notificationRegistrationServiceProvider).registerAsync());
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
}

final authControllerProvider = NotifierProvider<AuthController, AuthStatus>(AuthController.new);
