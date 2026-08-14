import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../domain/picked_photo.dart';

/// Wraps `image_picker` behind an app-level interface — issue #34. Kept
/// separate from the real implementation purely so a fake can stand in for
/// tests, which have no camera/gallery platform channel, same rationale as
/// FcmService/ChatHubClient/CalendarService.
abstract class PhotoPickerService {
  /// Null if the user cancelled the picker.
  Future<PickedPhoto?> pickFromGallery();

  Future<PickedPhoto?> pickFromCamera();
}

class ImagePickerPhotoPickerService implements PhotoPickerService {
  final _picker = ImagePicker();

  @override
  Future<PickedPhoto?> pickFromGallery() => _pick(ImageSource.gallery);

  @override
  Future<PickedPhoto?> pickFromCamera() => _pick(ImageSource.camera);

  Future<PickedPhoto?> _pick(ImageSource source) async {
    final file = await _picker.pickImage(source: source);
    if (file == null) return null;

    return PickedPhoto(bytes: await file.readAsBytes(), fileName: file.name);
  }
}

/// Unlike FcmService, this has no async/fallible startup step, so a real,
/// working default is fine here — no ProviderScope override required in
/// main.dart, only in tests that want a fake.
final photoPickerServiceProvider = Provider<PhotoPickerService>((ref) => ImagePickerPhotoPickerService());
