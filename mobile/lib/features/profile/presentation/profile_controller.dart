import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/profile_repository.dart';
import '../domain/picked_photo.dart';
import '../domain/profile_result.dart';
import '../domain/update_profile_request.dart';
import 'profile_state.dart';

class ProfileController extends Notifier<ProfileState> {
  @override
  ProfileState build() => const ProfileState();

  Future<void> load() async {
    state = state.copyWith(isLoading: true, loadFailure: null);

    final result = await ref.read(profileRepositoryProvider).getProfile();

    switch (result) {
      case ProfileSuccess(:final value):
        state = state.copyWith(isLoading: false, profile: value);
        if (value.hasPhoto) {
          unawaited(_loadPhoto());
        }
      case ProfileError(:final failure):
        state = state.copyWith(isLoading: false, loadFailure: failure);
    }
  }

  Future<void> retry() => load();

  Future<void> _loadPhoto() async {
    final result = await ref.read(profileRepositoryProvider).getPhoto();
    if (result case ProfileSuccess(:final value)) {
      state = state.copyWith(photoBytes: value);
    }
    // A failed photo fetch is silently ignored — the form itself is still fully usable without it.
  }

  Future<bool> save(UpdateProfileRequest request) async {
    state = state.copyWith(isSaving: true, saveFailure: null);

    final result = await ref.read(profileRepositoryProvider).updateProfile(request);

    switch (result) {
      case ProfileSuccess(:final value):
        state = state.copyWith(isSaving: false, profile: value);
        return true;
      case ProfileError(:final failure):
        state = state.copyWith(isSaving: false, saveFailure: failure);
        return false;
    }
  }

  Future<bool> uploadPhoto(PickedPhoto photo) async {
    state = state.copyWith(isUploadingPhoto: true, photoUploadFailure: null);

    final result = await ref.read(profileRepositoryProvider).uploadPhoto(photo.bytes, photo.fileName);

    switch (result) {
      case ProfileSuccess():
        state = state.copyWith(isUploadingPhoto: false, photoBytes: photo.bytes);
        return true;
      case ProfileError(:final failure):
        state = state.copyWith(isUploadingPhoto: false, photoUploadFailure: failure);
        return false;
    }
  }

  Future<bool> deleteAccount() async {
    final result = await ref.read(profileRepositoryProvider).deleteAccount();
    return result is ProfileSuccess;
  }
}

final profileControllerProvider = NotifierProvider<ProfileController, ProfileState>(ProfileController.new);
