import '../domain/doctor.dart';
import '../domain/doctor_sort_by.dart';
import '../domain/doctors_failure.dart';
import '../domain/specialty.dart';

class DoctorListState {
  const DoctorListState({
    this.isLoading = true,
    this.isLoadingMore = false,
    this.doctors = const [],
    this.specialties = const [],
    this.selectedSpecialtyIds = const {},
    this.searchText = '',
    this.sortBy = DoctorSortBy.name,
    this.descending = false,
    this.failure,
    this.hasMore = false,
    this.page = 1,
  });

  final bool isLoading;
  final bool isLoadingMore;
  final List<Doctor> doctors;
  final List<Specialty> specialties;
  final Set<String> selectedSpecialtyIds;
  final String searchText;
  final DoctorSortBy sortBy;
  final bool descending;
  final DoctorsFailure? failure;
  final bool hasMore;
  final int page;

  bool get isEmpty => !isLoading && failure == null && doctors.isEmpty;
}
