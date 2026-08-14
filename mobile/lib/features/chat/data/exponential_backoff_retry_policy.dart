import 'dart:math';

import 'package:signalr_netcore/iretry_policy.dart';

/// Reconnect-with-backoff for ChatHubClient — issue #29. Unlike
/// signalr_netcore's built-in DefaultRetryPolicy (a finite delay list that
/// gives up once exhausted), this retries indefinitely: delay doubles from
/// [initialDelayMs] up to a ceiling of [maxDelayMs], then holds there. A
/// chat connection should keep trying to come back once the device is
/// online again, not silently stop after a handful of attempts.
class ExponentialBackoffRetryPolicy implements IRetryPolicy {
  const ExponentialBackoffRetryPolicy({this.initialDelayMs = 1000, this.maxDelayMs = 30000, this.multiplier = 2});

  final int initialDelayMs;
  final int maxDelayMs;
  final num multiplier;

  @override
  int nextRetryDelayInMilliseconds(RetryContext retryContext) {
    // pow() on two ints computes with exact (wrapping) int arithmetic, so a
    // large previousRetryCount overflows and can wrap back to a small — even
    // zero — delay. Force double exponentiation instead: it saturates to
    // infinity rather than wrapping, so the >= comparison below always caps
    // correctly no matter how many attempts have been made.
    final delay = initialDelayMs * pow(multiplier.toDouble(), retryContext.previousRetryCount);
    return delay >= maxDelayMs ? maxDelayMs : delay.toInt();
  }
}
