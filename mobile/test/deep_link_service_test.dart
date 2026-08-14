import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/core/network/access_token_holder.dart';
import 'package:asnan/core/network/dio_client.dart';
import 'package:asnan/core/router/app_router.dart';
import 'package:asnan/core/storage/secure_storage_service.dart';
import 'package:asnan/core/theme/app_theme.dart';
import 'package:asnan/features/chat/presentation/chat_screen.dart';
import 'package:asnan/features/doctors/domain/doctor_detail.dart';
import 'package:asnan/features/doctors/presentation/doctor_detail_controller.dart';
import 'package:asnan/features/notifications/presentation/deep_link_service.dart';

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

Map<String, dynamic> _appointmentJson() => {
      'id': 'appt-1',
      'doctorProfileId': 'doctor-1',
      'doctorFullName': 'Dr. Deep Link Test',
      'slotStartUtc': '2026-09-01T09:00:00Z',
      'slotEndUtc': '2026-09-01T09:30:00Z',
      'status': 2,
      'consultationFee': 120.0,
      'currency': 'USD',
      'chatConversationId': null,
    };

DoctorDetail _doctor() => const DoctorDetail(
      id: 'doctor-1',
      fullName: 'Dr. Deep Link Test',
      bio: null,
      qualifications: null,
      specialties: [],
      yearsOfExperience: 5,
      consultationFee: 120,
      currency: 'USD',
      clinicAddress: '123 Health St',
      appointmentDurationMinutes: 30,
      isAcceptingNewPatients: true,
    );

ProviderContainer _buildContainer(HttpClientAdapter adapter) {
  return ProviderContainer(
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
      // AppointmentDetailsScreen separately fetches doctor detail — irrelevant to what this test verifies.
      doctorDetailProvider('doctor-1').overrideWith((ref) async => _doctor()),
    ],
  );
}

void main() {
  testWidgets('an appointment deep link fetches the appointment and navigates to its details', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      expect(options.path, '/appointments/appt-1');
      return _json(_appointmentJson());
    });
    final container = _buildContainer(adapter);
    addTearDown(container.dispose);

    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: MaterialApp.router(theme: AppTheme.light, routerConfig: container.read(appRouterProvider)),
      ),
    );
    await tester.pumpAndSettle();

    // Real Dio I/O needs a real event loop, not FakeAsync's — see runAsync's doc comment.
    await tester.runAsync(() => container.read(deepLinkServiceProvider).handleAsync('asnan://appointments/appt-1'));
    await tester.pumpAndSettle();

    expect(find.text('Dr. Deep Link Test'), findsOneWidget);
    expect(find.text('USD 120.00'), findsOneWidget);
  });

  testWidgets('a chat deep link navigates directly to the conversation', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async => _json({}));
    final container = _buildContainer(adapter);
    addTearDown(container.dispose);

    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: MaterialApp.router(theme: AppTheme.light, routerConfig: container.read(appRouterProvider)),
      ),
    );
    await tester.pumpAndSettle();

    await tester.runAsync(() => container.read(deepLinkServiceProvider).handleAsync('asnan://chat/conv-1'));
    // Not pumpAndSettle: with no access token set here, currentUserIdProvider
    // stays null, so ChatScreen never calls initialize() and its history
    // spinner is indeterminate forever — settling is never reached, only
    // the route push itself is under test here.
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));

    expect(find.byType(ChatScreen), findsOneWidget);
    final chatScreen = tester.widget<ChatScreen>(find.byType(ChatScreen));
    expect(chatScreen.conversationId, 'conv-1');
    expect(find.text('Chat'), findsWidgets); // fallback AppBar title — see DeepLinkService's doc comment
  });

  testWidgets('a malformed or unrecognized deep link is a no-op', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async => _json({}));
    final container = _buildContainer(adapter);
    addTearDown(container.dispose);

    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: MaterialApp.router(theme: AppTheme.light, routerConfig: container.read(appRouterProvider)),
      ),
    );
    await tester.pumpAndSettle();

    await tester.runAsync(() async {
      await container.read(deepLinkServiceProvider).handleAsync('not-a-uri-at-all');
      await container.read(deepLinkServiceProvider).handleAsync('asnan://unknown/x');
      await container.read(deepLinkServiceProvider).handleAsync(null);
    });
    await tester.pumpAndSettle();

    expect(find.byType(ChatScreen), findsNothing);
  });
}
