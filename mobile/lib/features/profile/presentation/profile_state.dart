import 'dart:typed_data';

import '../domain/patient_profile.dart';
import '../domain/profile_failure.dart';

class _Unset {
  const _Unset();
}

const _unset = _Unset();

class ProfileState {
  const ProfileState({
    this.isLoading = true,
    this.loadFailure,
    this.profile,
    this.photoBytes,
    this.isSaving = false,
    this.saveFailure,
    this.isUploadingPhoto = false,
    this.photoUploadFailure,
  });

  final bool isLoading;
  final ProfileFailure? loadFailure;
  final PatientProfile? profile;
  final Uint8List? photoBytes;
  final bool isSaving;
  final ProfileFailure? saveFailure;
  final bool isUploadingPhoto;
  final ProfileFailure? photoUploadFailure;

  ProfileState copyWith({
    bool? isLoading,
    Object? loadFailure = _unset,
    PatientProfile? profile,
    Object? photoBytes = _unset,
    bool? isSaving,
    Object? saveFailure = _unset,
    bool? isUploadingPhoto,
    Object? photoUploadFailure = _unset,
  }) {
    return ProfileState(
      isLoading: isLoading ?? this.isLoading,
      loadFailure: identical(loadFailure, _unset) ? this.loadFailure : loadFailure as ProfileFailure?,
      profile: profile ?? this.profile,
      photoBytes: identical(photoBytes, _unset) ? this.photoBytes : photoBytes as Uint8List?,
      isSaving: isSaving ?? this.isSaving,
      saveFailure: identical(saveFailure, _unset) ? this.saveFailure : saveFailure as ProfileFailure?,
      isUploadingPhoto: isUploadingPhoto ?? this.isUploadingPhoto,
      photoUploadFailure: identical(photoUploadFailure, _unset) ? this.photoUploadFailure : photoUploadFailure as ProfileFailure?,
    );
  }
}
