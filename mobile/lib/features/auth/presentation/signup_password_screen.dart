import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../../core/widgets/error_banner.dart';
import 'signup_controller.dart';

class SignupPasswordScreen extends ConsumerStatefulWidget {
  const SignupPasswordScreen({super.key});

  @override
  ConsumerState<SignupPasswordScreen> createState() => _SignupPasswordScreenState();
}

class _SignupPasswordScreenState extends ConsumerState<SignupPasswordScreen> {
  final _formKey = GlobalKey<FormState>();
  final _passwordController = TextEditingController();

  @override
  void dispose() {
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;

    final success =
        await ref.read(signupControllerProvider.notifier).setPassword(_passwordController.text);

    if (success && mounted) {
      context.goNamed(AppRoutes.login);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Account created. Please sign in.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(signupControllerProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Create a password')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (state.failure != null) ...[
                  // Server-side field-level errors (e.g. "too common") — the
                  // client only checks length locally; the strength meter
                  // itself is a design-system concern layered on later.
                  ErrorBanner(message: state.failure!.message),
                  if (state.failure!.fieldErrors?['Password'] case final errors?)
                    ...errors.map((e) => Padding(
                          padding: const EdgeInsets.only(top: 8),
                          child: Text(e, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                        )),
                  const SizedBox(height: 16),
                ],
                TextFormField(
                  controller: _passwordController,
                  decoration: const InputDecoration(labelText: 'Password'),
                  obscureText: true,
                  validator: (value) {
                    if (value == null || value.isEmpty) return 'Required';
                    if (value.length < 8) return 'Must be at least 8 characters';
                    return null;
                  },
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
                      : const Text('Create account'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
