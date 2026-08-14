import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../domain/patient_profile.dart';
import '../domain/profile_failure.dart';
import '../domain/profile_result.dart';
import '../domain/update_profile_request.dart';
import 'profile_api.dart';

class ProfileRepository {
  ProfileRepository(this._api);

  final ProfileApi _api;

  Future<ProfileResult<PatientProfile>> getProfile() => _guard(() => _api.getProfile());

  Future<ProfileResult<PatientProfile>> updateProfile(UpdateProfileRequest request) => _guard(() => _api.updateProfile(request));

  Future<ProfileResult<void>> uploadPhoto(Uint8List bytes, String fileName) => _guard(() => _api.uploadPhoto(bytes, fileName));

  /// Success(null) for "no photo yet" (the API layer maps a 404 to null) — never surfaced as an error.
  Future<ProfileResult<Uint8List?>> getPhoto() => _guard(() => _api.getPhoto());

  Future<ProfileResult<void>> deleteAccount() => _guard(() => _api.deleteAccount());

  Future<ProfileResult<T>> _guard<T>(Future<T> Function() action) async {
    try {
      return ProfileSuccess(await action());
    } on DioException catch (e) {
      return ProfileError(ProfileFailure.fromDioException(e));
    }
  }
}

final profileRepositoryProvider = Provider<ProfileRepository>((ref) => ProfileRepository(ref.watch(profileApiProvider)));
