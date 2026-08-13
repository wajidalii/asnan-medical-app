import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/dio_client.dart';
import '../domain/doctor.dart';
import '../domain/doctor_detail.dart';
import '../domain/doctor_sort_by.dart';
import '../domain/paged_result.dart';
import '../domain/specialty.dart';

/// Thin wrapper over the raw HTTP calls to the public doctor-discovery API
/// (issue #12/#13) and the public specialty list — no business logic here,
/// same convention as AuthApi.
class DoctorsApi {
  DoctorsApi(this._dio);

  final Dio _dio;

  Future<PagedResult<Doctor>> search({
    String? search,
    List<String> specialtyIds = const [],
    DoctorSortBy sortBy = DoctorSortBy.name,
    bool descending = false,
    int page = 1,
    int pageSize = 20,
  }) async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/doctors',
      queryParameters: {
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
        if (specialtyIds.isNotEmpty) 'specialtyIds': specialtyIds,
        'sortBy': sortBy.apiValue,
        'descending': descending,
        'page': page,
        'pageSize': pageSize,
      },
    );
    return PagedResult.fromJson(response.data!, Doctor.fromJson);
  }

  Future<DoctorDetail> getById(String id) async {
    final response = await _dio.get<Map<String, dynamic>>('/doctors/$id');
    return DoctorDetail.fromJson(response.data!);
  }

  Future<List<Specialty>> listSpecialties() async {
    final response = await _dio.get<List<dynamic>>('/specialties');
    return response.data!.map((e) => Specialty.fromJson(e as Map<String, dynamic>)).toList();
  }
}

/// Public endpoints — the shared intercepted client works fine unauthenticated
/// (no access token means no Authorization header, which is exactly right here).
final doctorsApiProvider = Provider<DoctorsApi>((ref) => DoctorsApi(ref.watch(dioProvider)));
