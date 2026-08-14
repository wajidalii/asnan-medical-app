import 'dart:typed_data';

/// App-level view of a photo picked from the gallery/camera — decoupled
/// from `image_picker`'s `XFile` the same way RemoteMessagePayload
/// decouples from `firebase_messaging`'s `RemoteMessage`.
class PickedPhoto {
  const PickedPhoto({required this.bytes, required this.fileName});

  final Uint8List bytes;
  final String fileName;
}
