import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/dio_client.dart';
import '../../doctors/domain/paged_result.dart';
import '../domain/appointment_cancellation.dart';
import '../domain/appointment_list_scope.dart';
import '../domain/appointment_summary.dart';
import '../domain/cancellation_preview.dart';

/// Thin wrapper over the raw HTTP calls — same convention as BookingApi/PaymentsApi.
class AppointmentsApi {
  AppointmentsApi(this._dio);

  final Dio _dio;

  Future<PagedResult<AppointmentSummary>> list(AppointmentListScope scope, {int page = 1, int pageSize = 20}) async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/appointments',
      queryParameters: {'scope': scope.apiValue, 'page': page, 'pageSize': pageSize},
    );
    return PagedResult.fromJson(response.data!, AppointmentSummary.fromJson);
  }

  Future<CancellationPreview> previewCancellation(String appointmentId) async {
    final response = await _dio.get<Map<String, dynamic>>('/appointments/$appointmentId/cancellation-preview');
    return CancellationPreview.fromJson(response.data!);
  }

  Future<AppointmentCancellation> cancel(String appointmentId, String? reason) async {
    final response = await _dio.post<Map<String, dynamic>>('/appointments/$appointmentId/cancel', data: {'reason': reason});
    return AppointmentCancellation.fromJson(response.data!);
  }
}

final appointmentsApiProvider = Provider<AppointmentsApi>((ref) => AppointmentsApi(ref.watch(dioProvider)));
