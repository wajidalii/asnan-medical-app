import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/doctors_repository.dart';
import '../domain/doctor_detail.dart';
import '../domain/doctors_result.dart';

/// A single read with no interactive state (unlike the list, which needs
/// search/filter/sort/pagination) — AsyncValue via FutureProvider covers
/// loading/data/error for free, so no hand-rolled controller class here.
/// Retry is `ref.invalidate(doctorDetailProvider(id))` from the screen.
final doctorDetailProvider = FutureProvider.family<DoctorDetail, String>((ref, id) async {
  final result = await ref.watch(doctorsRepositoryProvider).getById(id);
  return switch (result) {
    DoctorsSuccess<DoctorDetail>(:final value) => value,
    DoctorsError<DoctorDetail>(:final failure) => throw failure,
  };
});
