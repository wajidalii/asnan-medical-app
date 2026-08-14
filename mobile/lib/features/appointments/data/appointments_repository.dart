import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../doctors/domain/paged_result.dart';
import '../domain/appointment_cancellation.dart';
import '../domain/appointment_list_scope.dart';
import '../domain/appointment_summary.dart';
import '../domain/appointments_failure.dart';
import '../domain/appointments_result.dart';
import '../domain/cancellation_preview.dart';
import 'appointments_api.dart';

class AppointmentsRepository {
  AppointmentsRepository(this._api);

  final AppointmentsApi _api;

  Future<AppointmentsResult<PagedResult<AppointmentSummary>>> list(AppointmentListScope scope, {int page = 1, int pageSize = 20}) =>
      _guard(() => _api.list(scope, page: page, pageSize: pageSize));

  Future<AppointmentsResult<AppointmentSummary>> getById(String appointmentId) => _guard(() => _api.getById(appointmentId));

  Future<AppointmentsResult<CancellationPreview>> previewCancellation(String appointmentId) =>
      _guard(() => _api.previewCancellation(appointmentId));

  Future<AppointmentsResult<AppointmentCancellation>> cancel(String appointmentId, String? reason) =>
      _guard(() => _api.cancel(appointmentId, reason));

  Future<AppointmentsResult<T>> _guard<T>(Future<T> Function() action) async {
    try {
      return AppointmentsSuccess(await action());
    } on DioException catch (e) {
      return AppointmentsError(AppointmentsFailure.fromDioException(e));
    }
  }
}

final appointmentsRepositoryProvider = Provider<AppointmentsRepository>((ref) => AppointmentsRepository(ref.watch(appointmentsApiProvider)));
