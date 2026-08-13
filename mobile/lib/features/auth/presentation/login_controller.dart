import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/auth_repository.dart';
import '../domain/auth_failure.dart';
import '../domain/auth_result.dart';
import 'auth_controller.dart';

class LoginState {
  const LoginState({this.isSubmitting = false, this.failure});

  final bool isSubmitting;
  final AuthFailure? failure;
}

class LoginController extends Notifier<LoginState> {
  @override
  LoginState build() => const LoginState();

  Future<void> submit(String identifier, String password) async {
    state = const LoginState(isSubmitting: true);

    final result = await ref.read(authRepositoryProvider).login(identifier, password);

    switch (result) {
      case AuthSuccess<void>():
        state = const LoginState();
        ref.read(authControllerProvider.notifier).markAuthenticated();
      case AuthError<void>(:final failure):
        state = LoginState(failure: failure);
    }
  }
}

final loginControllerProvider = NotifierProvider<LoginController, LoginState>(LoginController.new);
