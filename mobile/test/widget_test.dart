import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/core/storage/secure_storage_service.dart';
import 'package:asnan/features/notifications/data/fcm_service.dart';
import 'package:asnan/main.dart';

import 'fakes/fake_secure_storage_service.dart';

void main() {
  testWidgets('splash screen navigates to login when no session is stored', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          secureStorageProvider.overrideWithValue(FakeSecureStorageService()),
          // No Firebase project in the test environment — same as a real device with no Firebase config (see main.dart's bootstrap).
          fcmServiceProvider.overrideWithValue(null),
        ],
        child: const AsnanApp(),
      ),
    );

    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    await tester.pumpAndSettle();

    expect(find.text('Sign in'), findsWidgets);
  });
}
