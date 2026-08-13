import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../domain/doctor.dart';
import '../domain/doctor_detail.dart';
import '../domain/doctor_sort_by.dart';
import '../domain/doctors_failure.dart';
import '../domain/doctors_result.dart';
import '../domain/paged_result.dart';
import '../domain/specialty.dart';
import 'doctors_api.dart';

class DoctorsRepository {
  DoctorsRepository(this._api);

  final DoctorsApi _api;

  Future<DoctorsResult<PagedResult<Doctor>>> search({
    String? search,
    List<String> specialtyIds = const [],
    DoctorSortBy sortBy = DoctorSortBy.name,
    bool descending = false,
    int page = 1,
    int pageSize = 20,
  }) =>
      _guard(() => _api.search(
            search: search,
            specialtyIds: specialtyIds,
            sortBy: sortBy,
            descending: descending,
            page: page,
            pageSize: pageSize,
          ));

  Future<DoctorsResult<DoctorDetail>> getById(String id) => _guard(() => _api.getById(id));

  Future<DoctorsResult<List<Specialty>>> listSpecialties() => _guard(() => _api.listSpecialties());

  Future<DoctorsResult<T>> _guard<T>(Future<T> Function() action) async {
    try {
      return DoctorsSuccess(await action());
    } on DioException catch (e) {
      return DoctorsError(DoctorsFailure.fromDioException(e));
    }
  }
}

final doctorsRepositoryProvider = Provider<DoctorsRepository>((ref) => DoctorsRepository(ref.watch(doctorsApiProvider)));
