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
import 'package:asnan/features/doctors/presentation/doctor_list_screen.dart';

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
    child: MaterialApp(theme: AppTheme.light, home: const DoctorListScreen()),
  );
}

Map<String, dynamic> _doctorJson({required String id, required String name, List<Map<String, dynamic>> specialties = const []}) => {
      'id': id,
      'fullName': name,
      'bio': null,
      'consultationFee': 100.0,
      'currency': 'USD',
      'yearsOfExperience': 5,
      'clinicAddress': null,
      'isAcceptingNewPatients': true,
      'specialties': specialties,
    };

void main() {
  testWidgets('renders doctors returned by the search API', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/specialties') return _json([]);
      // The AppBar's unread badge (#29) also fetches appointment lists — irrelevant to these doctor-search assertions.
      if (options.path == '/appointments') return _json({'items': [], 'page': 1, 'pageSize': 20, 'totalCount': 0});
      return _json({
        'items': [_doctorJson(id: '1', name: 'Dr. Alice Example')],
        'page': 1,
        'pageSize': 20,
        'totalCount': 1,
      });
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    expect(find.text('Dr. Alice Example'), findsOneWidget);
  });

  testWidgets('shows an empty state when no doctors match', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/specialties') return _json([]);
      // The AppBar's unread badge (#29) also fetches appointment lists — irrelevant to these doctor-search assertions.
      if (options.path == '/appointments') return _json({'items': [], 'page': 1, 'pageSize': 20, 'totalCount': 0});
      return _json({'items': [], 'page': 1, 'pageSize': 20, 'totalCount': 0});
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    expect(find.text('No doctors match your filters'), findsOneWidget);
  });

  testWidgets('shows an error state with a retry button on failure', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/specialties') return _json([]);
      // The AppBar's unread badge (#29) also fetches appointment lists — irrelevant to these doctor-search assertions.
      if (options.path == '/appointments') return _json({'items': [], 'page': 1, 'pageSize': 20, 'totalCount': 0});
      throw DioException(
        requestOptions: options,
        type: DioExceptionType.badResponse,
        response: Response(
          requestOptions: options,
          statusCode: 500,
          data: {'title': 'Something went wrong. Please try again.'},
        ),
      );
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    expect(find.text('Something went wrong. Please try again.'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Retry'), findsOneWidget);
  });

  testWidgets('retry re-issues the search request', (tester) async {
    var callCount = 0;
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/specialties') return _json([]);
      // The AppBar's unread badge (#29) also fetches appointment lists — irrelevant to these doctor-search assertions.
      if (options.path == '/appointments') return _json({'items': [], 'page': 1, 'pageSize': 20, 'totalCount': 0});
      callCount++;
      if (callCount == 1) {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(requestOptions: options, statusCode: 500, data: {'title': 'Failed.'}),
        );
      }
      return _json({
        'items': [_doctorJson(id: '1', name: 'Dr. Retry Success')],
        'page': 1,
        'pageSize': 20,
        'totalCount': 1,
      });
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(OutlinedButton, 'Retry'));
    await tester.pumpAndSettle();

    expect(find.text('Dr. Retry Success'), findsOneWidget);
  });

  testWidgets('toggling a specialty filter chip re-issues the search request', (tester) async {
    final requestedSpecialtyIds = <List<String>>[];
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/specialties') {
        return _json([
          {'id': 'spec-1', 'name': 'Cardiology', 'description': null},
        ]);
      }
      if (options.path == '/appointments') return _json({'items': [], 'page': 1, 'pageSize': 20, 'totalCount': 0});
      final raw = options.queryParameters['specialtyIds'];
      requestedSpecialtyIds.add(raw == null ? const [] : List<String>.from(raw as List));
      return _json({'items': [], 'page': 1, 'pageSize': 20, 'totalCount': 0});
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(FilterChip, 'Cardiology'));
    await tester.pumpAndSettle();

    expect(requestedSpecialtyIds.last, ['spec-1']);
  });
}
