import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../../core/widgets/error_banner.dart';
import '../domain/otp_channel.dart';
import 'signup_controller.dart';

class SignupScreen extends ConsumerStatefulWidget {
  const SignupScreen({super.key});

  @override
  ConsumerState<SignupScreen> createState() => _SignupScreenState();
}

class _SignupScreenState extends ConsumerState<SignupScreen> {
  final _formKey = GlobalKey<FormState>();
  final _destinationController = TextEditingController();
  OtpChannel _channel = OtpChannel.email;

  @override
  void dispose() {
    _destinationController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;

    final success = await ref
        .read(signupControllerProvider.notifier)
        .requestOtp(_destinationController.text.trim(), _channel);

    if (success && mounted) {
      context.goNamed(AppRoutes.signupOtp);
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(signupControllerProvider);

    return Scaffold(
      appBar: AppBar(leading: const BackButton()),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(24, 16, 24, 24),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text('Create account', style: Theme.of(context).textTheme.headlineLarge),
                const SizedBox(height: 6),
                Text(
                  "We'll send a verification code to get started.",
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(color: Theme.of(context).textTheme.bodySmall!.color!.withValues(alpha: 0.6)),
                ),
                const SizedBox(height: 28),
                if (state.failure != null) ...[
                  ErrorBanner(message: state.failure!.message),
                  const SizedBox(height: 16),
                ],
                SegmentedButton<OtpChannel>(
                  segments: const [
                    ButtonSegment(value: OtpChannel.email, label: Text('Email')),
                    ButtonSegment(value: OtpChannel.sms, label: Text('Mobile')),
                  ],
                  selected: {_channel},
                  onSelectionChanged: (selection) => setState(() => _channel = selection.first),
                ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: _destinationController,
                  decoration: InputDecoration(
                    labelText: _channel == OtpChannel.email ? 'Email address' : 'Mobile number',
                  ),
                  keyboardType: _channel == OtpChannel.email
                      ? TextInputType.emailAddress
                      : TextInputType.phone,
                  validator: (value) =>
                      (value == null || value.trim().isEmpty) ? 'Required' : null,
                ),
                const SizedBox(height: 24),
                FilledButton(
                  onPressed: state.isSubmitting ? null : _submit,
                  child: state.isSubmitting
                      ? const SizedBox(
                          height: 20,
                          width: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text('Send code'),
                ),
                const SizedBox(height: 12),
                TextButton(
                  onPressed: () => context.goNamed(AppRoutes.login),
                  child: const Text('Already have an account? Sign in'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
