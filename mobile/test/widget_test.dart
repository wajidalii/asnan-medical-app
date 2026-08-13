import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/main.dart';

void main() {
  testWidgets('splash screen navigates to the auth screen', (tester) async {
    await tester.pumpWidget(const ProviderScope(child: AsnanApp()));

    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    await tester.pumpAndSettle();

    expect(find.text('Sign in — coming soon'), findsOneWidget);
  });
}
