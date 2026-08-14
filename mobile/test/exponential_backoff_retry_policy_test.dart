import 'package:flutter_test/flutter_test.dart';
import 'package:signalr_netcore/iretry_policy.dart';

import 'package:asnan/features/chat/data/exponential_backoff_retry_policy.dart';

RetryContext _context(int previousRetryCount) => RetryContext(0, previousRetryCount, Exception('connection lost'));

void main() {
  group('ExponentialBackoffRetryPolicy', () {
    test('doubles the delay on each successive attempt', () {
      const policy = ExponentialBackoffRetryPolicy(initialDelayMs: 1000, maxDelayMs: 30000, multiplier: 2);

      expect(policy.nextRetryDelayInMilliseconds(_context(0)), 1000);
      expect(policy.nextRetryDelayInMilliseconds(_context(1)), 2000);
      expect(policy.nextRetryDelayInMilliseconds(_context(2)), 4000);
      expect(policy.nextRetryDelayInMilliseconds(_context(3)), 8000);
    });

    test('caps the delay at maxDelayMs and never gives up retrying', () {
      const policy = ExponentialBackoffRetryPolicy(initialDelayMs: 1000, maxDelayMs: 30000, multiplier: 2);

      expect(policy.nextRetryDelayInMilliseconds(_context(10)), 30000);
      expect(policy.nextRetryDelayInMilliseconds(_context(100)), 30000);
    });

    test('respects custom initial delay and multiplier', () {
      const policy = ExponentialBackoffRetryPolicy(initialDelayMs: 500, maxDelayMs: 5000, multiplier: 3);

      expect(policy.nextRetryDelayInMilliseconds(_context(0)), 500);
      expect(policy.nextRetryDelayInMilliseconds(_context(1)), 1500);
      expect(policy.nextRetryDelayInMilliseconds(_context(2)), 4500);
      expect(policy.nextRetryDelayInMilliseconds(_context(3)), 5000);
    });
  });
}
