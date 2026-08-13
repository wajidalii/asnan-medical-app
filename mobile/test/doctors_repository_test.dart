import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/features/doctors/data/doctors_api.dart';
import 'package:asnan/features/doctors/data/doctors_repository.dart';
import 'package:asnan/features/doctors/domain/doctors_result.dart';

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

DoctorsApi _apiWithAdapter(Future<ResponseBody> Function(RequestOptions options) handler) {
  final dio = Dio(BaseOptions(baseUrl: 'http://test.local'));
  dio.httpClientAdapter = _StubHttpClientAdapter(handler);
  return DoctorsApi(dio);
}

ResponseBody _jsonBody(Map<String, dynamic> json, {int statusCode = 200}) => ResponseBody.fromString(
      jsonEncode(json),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

void main() {
  group('DoctorsRepository.search', () {
    test('maps a successful response to DoctorsSuccess with parsed items', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.path, '/doctors');
        return _jsonBody({
          'items': [
            {
              'id': '1',
              'fullName': 'Dr. Jane',
              'bio': null,
              'consultationFee': 100.0,
              'currency': 'USD',
              'yearsOfExperience': 5,
              'clinicAddress': null,
              'isAcceptingNewPatients': true,
              'specialties': <Map<String, dynamic>>[],
            },
          ],
          'page': 1,
          'pageSize': 20,
          'totalCount': 1,
        });
      });
      final repository = DoctorsRepository(api);

      final result = await repository.search(search: 'Jane');

      switch (result) {
        case DoctorsSuccess(:final value):
          expect(value.items, hasLength(1));
          expect(value.items.first.fullName, 'Dr. Jane');
          expect(value.totalCount, 1);
        case DoctorsError():
          fail('expected success');
      }
    });

    test('maps a DioException to DoctorsError with a network-error message when there is no response', () async {
      final api = _apiWithAdapter((options) async {
        throw DioException(requestOptions: options, type: DioExceptionType.connectionError);
      });
      final repository = DoctorsRepository(api);

      final result = await repository.search();

      switch (result) {
        case DoctorsSuccess():
          fail('expected error');
        case DoctorsError(:final failure):
          expect(failure.message, contains('Network error'));
          expect(failure.isNotFound, isFalse);
      }
    });
  });

  group('DoctorsRepository.getById', () {
    test('maps a 404 response to a not-found DoctorsError', () async {
      final api = _apiWithAdapter((options) async {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(
            requestOptions: options,
            statusCode: 404,
            data: {'title': 'Doctor not found.'},
          ),
        );
      });
      final repository = DoctorsRepository(api);

      final result = await repository.getById('unknown-id');

      switch (result) {
        case DoctorsSuccess():
          fail('expected error');
        case DoctorsError(:final failure):
          expect(failure.isNotFound, isTrue);
          expect(failure.message, 'Doctor not found.');
      }
    });
  });
}
