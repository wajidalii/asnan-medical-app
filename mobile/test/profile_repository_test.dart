import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/features/profile/data/profile_api.dart';
import 'package:asnan/features/profile/data/profile_repository.dart';
import 'package:asnan/features/profile/domain/gender.dart';
import 'package:asnan/features/profile/domain/profile_result.dart';
import 'package:asnan/features/profile/domain/update_profile_request.dart';

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

ProfileApi _apiWithAdapter(Future<ResponseBody> Function(RequestOptions options) handler) {
  final dio = Dio(BaseOptions(baseUrl: 'http://test.local/api/v1'));
  dio.httpClientAdapter = _StubHttpClientAdapter(handler);
  return ProfileApi(dio);
}

ResponseBody _jsonBody(Object json, {int statusCode = 200}) => ResponseBody.fromString(
      jsonEncode(json),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

Map<String, dynamic> _profileJson() => {
      'userId': 'user-1',
      'email': 'jane@test.local',
      'mobile': null,
      'fullName': 'Jane Patient',
      'dateOfBirth': '1990-05-01',
      'gender': 2,
      'phone': '555-0100',
      'addressLine': '1 Health St',
      'emergencyContactName': 'John Patient',
      'emergencyContactPhone': '555-0101',
      'hasPhoto': true,
    };

void main() {
  group('ProfileRepository.getProfile', () {
    test('maps a successful response to the parsed profile', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.path, '/users/me/profile');
        return _jsonBody(_profileJson());
      });
      final repository = ProfileRepository(api);

      final result = await repository.getProfile();

      switch (result) {
        case ProfileSuccess(:final value):
          expect(value.fullName, 'Jane Patient');
          expect(value.gender, Gender.female);
          expect(value.hasPhoto, isTrue);
        case ProfileError():
          fail('expected success');
      }
    });

    test('maps a failure response to ProfileError', () async {
      final api = _apiWithAdapter((options) async {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(requestOptions: options, statusCode: 500, data: {'title': 'Something went wrong.'}),
        );
      });
      final repository = ProfileRepository(api);

      final result = await repository.getProfile();

      switch (result) {
        case ProfileSuccess():
          fail('expected error');
        case ProfileError(:final failure):
          expect(failure.message, 'Something went wrong.');
      }
    });
  });

  group('ProfileRepository.updateProfile', () {
    test('sends the date of birth as an ISO date string', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.method, 'PUT');
        expect(options.data['dateOfBirth'], '1990-05-01');
        expect(options.data['gender'], 2);
        return _jsonBody(_profileJson());
      });
      final repository = ProfileRepository(api);

      final result = await repository.updateProfile(UpdateProfileRequest(
        fullName: 'Jane Patient',
        dateOfBirth: DateTime(1990, 5, 1),
        gender: Gender.female,
      ));

      expect(result, isA<ProfileSuccess>());
    });

    test('maps a 400 validation failure to ProfileError', () async {
      final api = _apiWithAdapter((options) async {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(requestOptions: options, statusCode: 400, data: {'title': 'Please check the highlighted fields.'}),
        );
      });
      final repository = ProfileRepository(api);

      final result = await repository.updateProfile(const UpdateProfileRequest(fullName: ''));

      switch (result) {
        case ProfileSuccess():
          fail('expected error');
        case ProfileError(:final failure):
          expect(failure.isBadRequest, isTrue);
      }
    });
  });

  group('ProfileRepository.getPhoto', () {
    test('maps a 404 (no photo yet) to Success(null), not an error', () async {
      final api = _apiWithAdapter((options) async {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(requestOptions: options, statusCode: 404, data: {'title': 'Not found.'}),
        );
      });
      final repository = ProfileRepository(api);

      final result = await repository.getPhoto();

      switch (result) {
        case ProfileSuccess(:final value):
          expect(value, isNull);
        case ProfileError():
          fail('expected success(null)');
      }
    });

    test('returns the photo bytes on success', () async {
      final bytes = [1, 2, 3, 4];
      final api = _apiWithAdapter((options) async => ResponseBody.fromString(String.fromCharCodes(bytes), 200));
      final repository = ProfileRepository(api);

      final result = await repository.getPhoto();

      switch (result) {
        case ProfileSuccess(:final value):
          expect(value, isNotNull);
        case ProfileError():
          fail('expected success');
      }
    });
  });

  group('ProfileRepository.deleteAccount', () {
    test('succeeds on a 204 response', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.method, 'DELETE');
        expect(options.path, '/users/me');
        return ResponseBody.fromString('', 204);
      });
      final repository = ProfileRepository(api);

      final result = await repository.deleteAccount();

      expect(result, isA<ProfileSuccess>());
    });
  });
}
