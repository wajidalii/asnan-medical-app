import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/dio_client.dart';
import '../domain/patient_profile.dart';
import '../domain/update_profile_request.dart';

class ProfileApi {
  ProfileApi(this._dio);

  final Dio _dio;

  Future<PatientProfile> getProfile() async {
    final response = await _dio.get<Map<String, dynamic>>('/users/me/profile');
    return PatientProfile.fromJson(response.data!);
  }

  Future<PatientProfile> updateProfile(UpdateProfileRequest request) async {
    final response = await _dio.put<Map<String, dynamic>>('/users/me/profile', data: request.toJson());
    return PatientProfile.fromJson(response.data!);
  }

  Future<void> uploadPhoto(Uint8List bytes, String fileName) async {
    final formData = FormData.fromMap({'file': MultipartFile.fromBytes(bytes, filename: fileName)});
    await _dio.post<void>('/users/me/profile/photo', data: formData);
  }

  /// Null on a 404 (no photo uploaded yet) — not an error, see ProfileRepository.getPhoto.
  Future<Uint8List?> getPhoto() async {
    try {
      final response = await _dio.get<List<int>>('/users/me/profile/photo', options: Options(responseType: ResponseType.bytes));
      return Uint8List.fromList(response.data!);
    } on DioException catch (e) {
      if (e.response?.statusCode == 404) return null;
      rethrow;
    }
  }

  Future<void> deleteAccount() => _dio.delete<void>('/users/me');
}

final profileApiProvider = Provider<ProfileApi>((ref) => ProfileApi(ref.watch(dioProvider)));
