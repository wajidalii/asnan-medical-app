import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';

/// Must be a top-level (or static) function — FCM invokes it in a separate
/// isolate when a push arrives while the app is fully backgrounded or
/// terminated. There's nothing to do here: every push this backend sends
/// includes a `notification` block (see FcmNotificationSender server-side),
/// so the OS shows the system-tray notification automatically without any
/// app code running. Re-tapping that notification is handled by
/// FcmService.getInitialMessage/onMessageOpenedApp instead, once the app is
/// actually running again.
@pragma('vm:entry-point')
Future<void> firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  await Firebase.initializeApp();
}
