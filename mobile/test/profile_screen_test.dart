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
import 'package:asnan/features/profile/data/photo_picker_service.dart';
import 'package:asnan/features/profile/domain/picked_photo.dart';
import 'package:asnan/features/profile/presentation/profile_screen.dart';

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

Map<String, dynamic> _profileJson({String fullName = 'Jane Patient'}) => {
      'userId': 'user-1',
      'email': 'jane@test.local',
      'mobile': null,
      'fullName': fullName,
      'dateOfBirth': '1990-05-01',
      'gender': 2,
      'phone': '555-0100',
      'addressLine': '1 Health St',
      'emergencyContactName': 'John Patient',
      'emergencyContactPhone': '555-0101',
      'hasPhoto': false,
    };

/// A real, minimal 1x1 PNG — MemoryImage actually decodes whatever bytes
/// the picker returns to render the avatar preview, so a garbage byte
/// array (fine for the reject-before-upload tests) isn't fine here.
final _tinyValidPngBytes = base64Decode(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==',
);

class _FakePhotoPickerService implements PhotoPickerService {
  _FakePhotoPickerService({this.galleryResult});

  final PickedPhoto? galleryResult;

  @override
  Future<PickedPhoto?> pickFromGallery() async => galleryResult;

  @override
  Future<PickedPhoto?> pickFromCamera() async => galleryResult;
}

Widget _wrap(HttpClientAdapter adapter, {PhotoPickerService? photoPicker}) {
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
      if (photoPicker != null) photoPickerServiceProvider.overrideWithValue(photoPicker),
    ],
    child: MaterialApp(theme: AppTheme.light, home: const ProfileScreen()),
  );
}

/// A tall viewport so the whole form (avatar down to "Delete my account")
/// fits without scrolling — avoids ensureVisible's edge-precision fragility
/// entirely, for both top (avatar) and bottom (Save/Delete) targets. Must
/// be set from inside the test body (addTearDown resets it), not
/// setUp/tearDown — TestWidgetsFlutterBinding.setSurfaceSize asserts it's
/// called while a test is actively running.
Future<void> _pumpTall(WidgetTester tester, Widget widget) async {
  await tester.binding.setSurfaceSize(const Size(800, 1600));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  await tester.pumpWidget(widget);
}

void main() {
  testWidgets('shows a loading indicator then the populated form', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      expect(options.path, '/users/me/profile');
      return _json(_profileJson());
    });

    await _pumpTall(tester, _wrap(adapter));
    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    await tester.pumpAndSettle();

    expect(find.text('jane@test.local'), findsOneWidget);
    expect(find.widgetWithText(TextFormField, 'Full name'), findsOneWidget);
    final fullNameField = tester.widget<TextFormField>(find.widgetWithText(TextFormField, 'Full name'));
    expect(fullNameField.controller!.text, 'Jane Patient');
  });

  testWidgets('shows an error state with a retry button on load failure', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      throw DioException(
        requestOptions: options,
        type: DioExceptionType.badResponse,
        response: Response(requestOptions: options, statusCode: 500, data: {'title': 'Something went wrong.'}),
      );
    });

    await _pumpTall(tester, _wrap(adapter));
    await tester.pumpAndSettle();

    expect(find.text('Something went wrong.'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Retry'), findsOneWidget);
  });

  testWidgets('empty full name is rejected without calling the save API', (tester) async {
    var putCalled = false;
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/users/me/profile' && options.method == 'PUT') {
        putCalled = true;
      }
      return _json(_profileJson());
    });

    await _pumpTall(tester, _wrap(adapter));
    await tester.pumpAndSettle();

    await tester.enterText(find.widgetWithText(TextFormField, 'Full name'), '');
    await tester.tap(find.widgetWithText(FilledButton, 'Save'));
    await tester.pumpAndSettle();

    expect(find.text('Full name is required.'), findsOneWidget);
    expect(putCalled, isFalse);
  });

  testWidgets('saving valid changes calls the update API and shows a success message', (tester) async {
    Map<String, dynamic>? putBody;
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.method == 'PUT') {
        putBody = (options.data as Map).cast<String, dynamic>();
        return _json(_profileJson(fullName: 'Jane Updated'));
      }
      return _json(_profileJson());
    });

    await _pumpTall(tester, _wrap(adapter));
    await tester.pumpAndSettle();

    await tester.enterText(find.widgetWithText(TextFormField, 'Full name'), 'Jane Updated');
    await tester.tap(find.widgetWithText(FilledButton, 'Save'));
    await tester.pumpAndSettle();

    expect(putBody, isNotNull);
    expect(putBody!['fullName'], 'Jane Updated');
    expect(find.text('Profile saved.'), findsOneWidget);
  });

  testWidgets('a failed save shows the backend error message', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.method == 'PUT') {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(requestOptions: options, statusCode: 400, data: {'title': 'Please check the highlighted fields.'}),
        );
      }
      return _json(_profileJson());
    });

    await _pumpTall(tester, _wrap(adapter));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(FilledButton, 'Save'));
    await tester.pumpAndSettle();

    expect(find.text('Please check the highlighted fields.'), findsOneWidget);
  });

  testWidgets('uploading a valid photo calls the photo endpoint and updates the avatar', (tester) async {
    var uploadCalled = false;
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/users/me/profile/photo' && options.method == 'POST') {
        uploadCalled = true;
        return ResponseBody.fromString('', 204);
      }
      return _json(_profileJson());
    });
    final fakePicker = _FakePhotoPickerService(
      galleryResult: PickedPhoto(bytes: _tinyValidPngBytes, fileName: 'photo.png'),
    );

    await _pumpTall(tester, _wrap(adapter, photoPicker: fakePicker));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.edit));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Choose from gallery'));
    await tester.pumpAndSettle();

    expect(uploadCalled, isTrue);
    expect(find.text('Photo updated.'), findsOneWidget);
  });

  testWidgets('an oversized photo is rejected client-side without calling the upload API', (tester) async {
    var uploadCalled = false;
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/users/me/profile/photo') uploadCalled = true;
      return _json(_profileJson());
    });
    final fakePicker = _FakePhotoPickerService(
      galleryResult: PickedPhoto(bytes: Uint8List.fromList(List.filled(6 * 1024 * 1024, 1)), fileName: 'huge.jpg'),
    );

    await _pumpTall(tester, _wrap(adapter, photoPicker: fakePicker));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.edit));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Choose from gallery'));
    await tester.pumpAndSettle();

    expect(uploadCalled, isFalse);
    expect(find.textContaining('too large'), findsOneWidget);
  });

  testWidgets('a disallowed file extension is rejected client-side without calling the upload API', (tester) async {
    var uploadCalled = false;
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/users/me/profile/photo') uploadCalled = true;
      return _json(_profileJson());
    });
    final fakePicker = _FakePhotoPickerService(
      galleryResult: PickedPhoto(bytes: Uint8List.fromList([1, 2, 3]), fileName: 'not-a-photo.exe'),
    );

    await _pumpTall(tester, _wrap(adapter, photoPicker: fakePicker));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.edit));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Choose from gallery'));
    await tester.pumpAndSettle();

    expect(uploadCalled, isFalse);
    expect(find.textContaining('JPEG, PNG, WEBP, or HEIC'), findsOneWidget);
  });

  testWidgets('confirming account deletion calls the delete endpoint', (tester) async {
    var deleteCalled = false;
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/users/me' && options.method == 'DELETE') {
        deleteCalled = true;
        return ResponseBody.fromString('', 204);
      }
      return _json(_profileJson());
    });

    await _pumpTall(tester, _wrap(adapter));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Delete my account'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(TextButton, 'Delete'));
    await tester.pumpAndSettle();

    expect(deleteCalled, isTrue);
  });
}
