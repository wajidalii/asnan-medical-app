import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/appointments_repository.dart';
import '../domain/appointment_list_scope.dart';
import '../domain/appointment_summary.dart';
import '../domain/appointments_result.dart';
import '../../doctors/domain/paged_result.dart';
import 'appointments_list_state.dart';

const _pageSize = 20;

/// Per-scope (Upcoming/Past) pagination state — family so each tab keeps
/// its own independent list/scroll position, same convention as
/// DoctorDetailScreen's per-id provider.
class AppointmentsListController extends FamilyNotifier<AppointmentsListState, AppointmentListScope> {
  @override
  AppointmentsListState build(AppointmentListScope arg) => const AppointmentsListState();

  /// Called once from the screen's initState — same convention as DoctorListController.loadInitial().
  Future<void> loadInitial() async {
    state = const AppointmentsListState(isLoading: true);

    final result = await ref.read(appointmentsRepositoryProvider).list(arg, page: 1, pageSize: _pageSize);

    switch (result) {
      case AppointmentsSuccess<PagedResult<AppointmentSummary>>(:final value):
        state = AppointmentsListState(isLoading: false, appointments: value.items, hasMore: value.hasMore, page: value.page);
      case AppointmentsError<PagedResult<AppointmentSummary>>(:final failure):
        state = AppointmentsListState(isLoading: false, failure: failure);
    }
  }

  Future<void> retry() => loadInitial();

  Future<void> loadMore() async {
    if (state.isLoading || state.isLoadingMore || !state.hasMore) return;

    state = AppointmentsListState(
      isLoading: false,
      isLoadingMore: true,
      appointments: state.appointments,
      hasMore: state.hasMore,
      page: state.page,
    );

    final result = await ref.read(appointmentsRepositoryProvider).list(arg, page: state.page + 1, pageSize: _pageSize);

    switch (result) {
      case AppointmentsSuccess<PagedResult<AppointmentSummary>>(:final value):
        state = AppointmentsListState(
          isLoading: false,
          appointments: [...state.appointments, ...value.items],
          hasMore: value.hasMore,
          page: value.page,
        );
      case AppointmentsError<PagedResult<AppointmentSummary>>():
        // "Load more" failures keep the existing list visible.
        state = AppointmentsListState(isLoading: false, appointments: state.appointments, hasMore: state.hasMore, page: state.page);
    }
  }
}

final appointmentsListControllerProvider =
    NotifierProvider.family<AppointmentsListController, AppointmentsListState, AppointmentListScope>(AppointmentsListController.new);
