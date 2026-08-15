import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/core/network/access_token_holder.dart';
import 'package:asnan/core/network/dio_client.dart';
import 'package:asnan/core/storage/secure_storage_service.dart';
import 'package:asnan/core/theme/app_theme.dart';
import 'package:asnan/features/notifications/presentation/notification_preferences_screen.dart';

import 'fakes/fake_secure_storage_service.dart';

class _StubHttpClientAdapter implements HttpClientAdapter {
  _StubHttpClientAdapter(this._handler);

  final Future<ResponseBody> Function(RequestOptions options) _handler;

  @override
  void close({bool force = false}) {}

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) =>
      _handler(options);
}

ResponseBody _json(Object json, {int statusCode = 200}) => ResponseBody.fromString(
      jsonEncode(json),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

List<Map<String, dynamic>> _preferencesJson() => [
      {'category': 1, 'isEnabled': true, 'isDisableable': false}, // AppointmentUpdates
      {'category': 2, 'isEnabled': true, 'isDisableable': false}, // PaymentUpdates
      {'category': 3, 'isEnabled': true, 'isDisableable': true}, // Reminders
      {'category': 4, 'isEnabled': false, 'isDisableable': true}, // ChatMessages
      {'category': 5, 'isEnabled': true, 'isDisableable': true}, // DoctorAvailability
    ];

Widget _wrap(HttpClientAdapter adapter) {
  return ProviderScope(
    overrides: [
      secureStorageProvider.overrideWithValue(FakeSecureStorageService()),
      dioProvider.overrideWith((ref) {
        final dio = Dio(BaseOptions(baseUrl: 'http://test.local'));
        dio.httpClientAdapter = adapter;
        dio.interceptors.add(
          AuthInterceptor(
            dio: dio,
            readAccessToken: () => ref.read(accessTokenHolderProvider),
            onRefresh: () async => null,
          ),
        );
        return dio;
      }),
    ],
    child: MaterialApp(theme: AppTheme.light, home: const NotificationPreferencesScreen()),
  );
}

void main() {
  testWidgets('renders every category with the right enabled state', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      expect(options.path, '/notifications/preferences');
      return _json(_preferencesJson());
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    expect(find.text('Appointment updates'), findsOneWidget);
    expect(find.text('Payment updates'), findsOneWidget);
    expect(find.text('Appointment reminders'), findsOneWidget);
    expect(find.text('Chat messages'), findsOneWidget);
    expect(find.text('Doctor availability changes'), findsOneWidget);

    final chatSwitch = tester.widget<SwitchListTile>(find.widgetWithText(SwitchListTile, 'Chat messages'));
    expect(chatSwitch.value, isFalse);
    final reminderSwitch = tester.widget<SwitchListTile>(find.widgetWithText(SwitchListTile, 'Appointment reminders'));
    expect(reminderSwitch.value, isTrue);
  });

  testWidgets('non-disableable categories are marked Required and cannot be toggled', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async => _json(_preferencesJson()));

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    expect(find.text('Required — cannot be turned off'), findsNWidgets(2)); // AppointmentUpdates + PaymentUpdates

    final appointmentUpdatesSwitch = tester.widget<SwitchListTile>(find.widgetWithText(SwitchListTile, 'Appointment updates'));
    expect(appointmentUpdatesSwitch.onChanged, isNull);

    final remindersSwitch = tester.widget<SwitchListTile>(find.widgetWithText(SwitchListTile, 'Appointment reminders'));
    expect(remindersSwitch.onChanged, isNotNull);
  });

  testWidgets('toggling a disableable category calls the PUT endpoint and updates immediately', (tester) async {
    final requestedBodies = <dynamic>[];
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/notifications/preferences') {
        return _json(_preferencesJson());
      }
      requestedBodies.add(options.data);
      return ResponseBody.fromString('', 204);
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(SwitchListTile, 'Appointment reminders'));
    await tester.pumpAndSettle();

    expect(requestedBodies, [
      {'isEnabled': false},
    ]);
    final remindersSwitch = tester.widget<SwitchListTile>(find.widgetWithText(SwitchListTile, 'Appointment reminders'));
    expect(remindersSwitch.value, isFalse);
  });

  testWidgets('a failed toggle reverts to the previous state and shows why', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/notifications/preferences') {
        return _json(_preferencesJson());
      }
      throw DioException(
        requestOptions: options,
        type: DioExceptionType.badResponse,
        response: Response(requestOptions: options, statusCode: 500, data: {'title': "Couldn't save that. Please try again."}),
      );
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(SwitchListTile, 'Appointment reminders'));
    await tester.pumpAndSettle();

    final remindersSwitch = tester.widget<SwitchListTile>(find.widgetWithText(SwitchListTile, 'Appointment reminders'));
    expect(remindersSwitch.value, isTrue); // reverted
    expect(find.text("Couldn't save that. Please try again."), findsOneWidget);
  });

  testWidgets('shows a loading spinner while preferences are being fetched', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      await Future<void>.delayed(const Duration(milliseconds: 20));
      return _json(_preferencesJson());
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pump();

    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    await tester.pumpAndSettle();
  });

  testWidgets('shows an error state with a retry button on load failure', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      throw DioException(
        requestOptions: options,
        type: DioExceptionType.badResponse,
        response: Response(requestOptions: options, statusCode: 500, data: {'title': 'Something went wrong.'}),
      );
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    expect(find.text('Something went wrong.'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Retry'), findsOneWidget);
  });
}
