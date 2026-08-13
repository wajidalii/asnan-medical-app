import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/auth_repository.dart';
import '../domain/auth_failure.dart';
import '../domain/auth_result.dart';
import '../domain/otp_channel.dart';

class SignupState {
  const SignupState({
    this.isSubmitting = false,
    this.failure,
    this.destination,
    this.channel,
    this.signupToken,
    this.isComplete = false,
  });

  final bool isSubmitting;
  final AuthFailure? failure;

  /// Remembered across steps so the OTP-verification screen doesn't need it
  /// passed through route arguments.
  final String? destination;
  final OtpChannel? channel;

  /// Set once OTP verification succeeds; consumed by the final "set password" step.
  final String? signupToken;

  final bool isComplete;

  SignupState copyWith({
    bool? isSubmitting,
    AuthFailure? failure,
    String? destination,
    OtpChannel? channel,
    String? signupToken,
    bool? isComplete,
  }) =>
      SignupState(
        isSubmitting: isSubmitting ?? false,
        failure: failure,
        destination: destination ?? this.destination,
        channel: channel ?? this.channel,
        signupToken: signupToken ?? this.signupToken,
        isComplete: isComplete ?? this.isComplete,
      );
}

class SignupController extends Notifier<SignupState> {
  @override
  SignupState build() => const SignupState();

  Future<bool> requestOtp(String destination, OtpChannel channel) async {
    state = state.copyWith(isSubmitting: true, destination: destination, channel: channel);

    final result = await ref.read(authRepositoryProvider).requestSignupOtp(destination, channel);

    switch (result) {
      case AuthSuccess<void>():
        state = state.copyWith();
        return true;
      case AuthError<void>(:final failure):
        state = state.copyWith(failure: failure);
        return false;
    }
  }

  Future<bool> verifyOtp(String code) async {
    final destination = state.destination;
    final channel = state.channel;
    if (destination == null || channel == null) {
      throw StateError('verifyOtp called before requestOtp.');
    }

    state = state.copyWith(isSubmitting: true);

    final result = await ref.read(authRepositoryProvider).verifySignupOtp(destination, code, channel);

    switch (result) {
      case AuthSuccess<String>(:final value):
        state = state.copyWith(signupToken: value);
        return true;
      case AuthError<String>(:final failure):
        state = state.copyWith(failure: failure);
        return false;
    }
  }

  Future<bool> setPassword(String password) async {
    final signupToken = state.signupToken;
    if (signupToken == null) {
      throw StateError('setPassword called before verifyOtp.');
    }

    state = state.copyWith(isSubmitting: true);

    final result = await ref.read(authRepositoryProvider).setPassword(signupToken, password);

    switch (result) {
      case AuthSuccess<void>():
        state = state.copyWith(isComplete: true);
        return true;
      case AuthError<void>(:final failure):
        state = state.copyWith(failure: failure);
        return false;
    }
  }
}

final signupControllerProvider = NotifierProvider<SignupController, SignupState>(SignupController.new);
