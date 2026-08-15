import 'package:flutter/material.dart';

/// A flat, accent-bordered inline error banner — replaces the default
/// Material rounded `errorContainer` pill everywhere per the Modernist
/// system's "do not round a corner anywhere" rule.
class ErrorBanner extends StatelessWidget {
  const ErrorBanner({super.key, required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    final error = Theme.of(context).colorScheme.error;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(border: Border.all(color: error)),
      child: Text(message, style: Theme.of(context).textTheme.bodySmall?.copyWith(color: error)),
    );
  }
}
