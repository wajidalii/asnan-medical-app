import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/doctors_repository.dart';
import '../domain/doctor.dart';
import '../domain/doctor_sort_by.dart';
import '../domain/doctors_result.dart';
import '../domain/paged_result.dart';
import '../domain/specialty.dart';
import 'doctor_list_state.dart';

const _pageSize = 20;

/// Search/filter/sort/pagination state for the doctor directory. Search is
/// applied on submit (not live-as-you-type) — simpler and more predictable
/// than a timer-based debounce, matching this codebase's preference for
/// straightforward state over cleverness at this scope.
class DoctorListController extends Notifier<DoctorListState> {
  @override
  DoctorListState build() => const DoctorListState();

  /// Called once from the screen's initState — same convention as
  /// AuthController.restoreSession(), not triggered from inside build().
  Future<void> loadInitial() async {
    final repo = ref.read(doctorsRepositoryProvider);
    final specialtiesResult = await repo.listSpecialties();
    final specialties = switch (specialtiesResult) {
      DoctorsSuccess<List<Specialty>>(:final value) => value,
      DoctorsError<List<Specialty>>() => const <Specialty>[],
    };

    await _reload(specialties: specialties);
  }

  Future<void> retry() => _reload(specialties: state.specialties);

  Future<void> setSearchText(String text) => _reload(specialties: state.specialties, searchText: text);

  Future<void> toggleSpecialty(String id) {
    final updated = {...state.selectedSpecialtyIds};
    if (!updated.remove(id)) updated.add(id);
    return _reload(specialties: state.specialties, selectedSpecialtyIds: updated);
  }

  Future<void> setSortBy(DoctorSortBy sortBy) {
    final descending = sortBy == state.sortBy ? !state.descending : false;
    return _reload(specialties: state.specialties, sortBy: sortBy, descending: descending);
  }

  Future<void> loadMore() async {
    if (state.isLoading || state.isLoadingMore || !state.hasMore) return;

    state = DoctorListState(
      isLoading: false,
      isLoadingMore: true,
      doctors: state.doctors,
      specialties: state.specialties,
      selectedSpecialtyIds: state.selectedSpecialtyIds,
      searchText: state.searchText,
      sortBy: state.sortBy,
      descending: state.descending,
      hasMore: state.hasMore,
      page: state.page,
    );

    final result = await ref.read(doctorsRepositoryProvider).search(
          search: state.searchText,
          specialtyIds: state.selectedSpecialtyIds.toList(),
          sortBy: state.sortBy,
          descending: state.descending,
          page: state.page + 1,
          pageSize: _pageSize,
        );

    switch (result) {
      case DoctorsSuccess<PagedResult<Doctor>>(:final value):
        state = DoctorListState(
          isLoading: false,
          isLoadingMore: false,
          doctors: [...state.doctors, ...value.items],
          specialties: state.specialties,
          selectedSpecialtyIds: state.selectedSpecialtyIds,
          searchText: state.searchText,
          sortBy: state.sortBy,
          descending: state.descending,
          hasMore: value.hasMore,
          page: value.page,
        );
      case DoctorsError<PagedResult<Doctor>>():
        // "Load more" failures keep the existing list visible — surfacing a
        // full-screen error would throw away results the user already has.
        state = DoctorListState(
          isLoading: false,
          isLoadingMore: false,
          doctors: state.doctors,
          specialties: state.specialties,
          selectedSpecialtyIds: state.selectedSpecialtyIds,
          searchText: state.searchText,
          sortBy: state.sortBy,
          descending: state.descending,
          hasMore: state.hasMore,
          page: state.page,
        );
    }
  }

  Future<void> _reload({
    required List<Specialty> specialties,
    String? searchText,
    Set<String>? selectedSpecialtyIds,
    DoctorSortBy? sortBy,
    bool? descending,
  }) async {
    final effectiveSearchText = searchText ?? state.searchText;
    final effectiveSelectedSpecialtyIds = selectedSpecialtyIds ?? state.selectedSpecialtyIds;
    final effectiveSortBy = sortBy ?? state.sortBy;
    final effectiveDescending = descending ?? state.descending;

    state = DoctorListState(
      isLoading: true,
      specialties: specialties,
      searchText: effectiveSearchText,
      selectedSpecialtyIds: effectiveSelectedSpecialtyIds,
      sortBy: effectiveSortBy,
      descending: effectiveDescending,
    );

    final result = await ref.read(doctorsRepositoryProvider).search(
          search: effectiveSearchText,
          specialtyIds: effectiveSelectedSpecialtyIds.toList(),
          sortBy: effectiveSortBy,
          descending: effectiveDescending,
          page: 1,
          pageSize: _pageSize,
        );

    switch (result) {
      case DoctorsSuccess<PagedResult<Doctor>>(:final value):
        state = DoctorListState(
          isLoading: false,
          doctors: value.items,
          specialties: specialties,
          searchText: effectiveSearchText,
          selectedSpecialtyIds: effectiveSelectedSpecialtyIds,
          sortBy: effectiveSortBy,
          descending: effectiveDescending,
          hasMore: value.hasMore,
          page: value.page,
        );
      case DoctorsError<PagedResult<Doctor>>(:final failure):
        state = DoctorListState(
          isLoading: false,
          specialties: specialties,
          searchText: effectiveSearchText,
          selectedSpecialtyIds: effectiveSelectedSpecialtyIds,
          sortBy: effectiveSortBy,
          descending: effectiveDescending,
          failure: failure,
        );
    }
  }
}

final doctorListControllerProvider = NotifierProvider<DoctorListController, DoctorListState>(DoctorListController.new);
