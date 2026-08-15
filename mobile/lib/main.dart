import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/router/app_router.dart';
import 'core/theme/app_theme.dart';
import 'features/auth/presentation/auth_controller.dart';
import 'features/notifications/data/fcm_service.dart';
import 'features/notifications/data/firebase_background_handler.dart';
import 'features/notifications/data/firebase_fcm_service.dart';
import 'features/notifications/presentation/deep_link_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  FcmService? fcmService;
  try {
    await Firebase.initializeApp();
    FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);
    fcmService = FirebaseFcmService(FirebaseMessaging.instance);
  } catch (_) {
    // No Firebase project configured yet in this environment (see issue
    // #71) — push notifications are unavailable, but the rest of the app
    // must keep working. Every consumer treats a null FcmService as "push
    // isn't available," never as a startup failure.
  }

  runApp(ProviderScope(
    overrides: [fcmServiceProvider.overrideWithValue(fcmService)],
    child: const AsnanApp(),
  ));
}

class AsnanApp extends ConsumerStatefulWidget {
  const AsnanApp({super.key});

  @override
  ConsumerState<AsnanApp> createState() => _AsnanAppState();
}

class _AsnanAppState extends ConsumerState<AsnanApp> {
  final _scaffoldMessengerKey = GlobalKey<ScaffoldMessengerState>();
  StreamSubscription? _foregroundSub;
  StreamSubscription? _openedAppSub;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _initializePushHandling());
  }

  /// Foreground pushes get an in-app banner (no system notification is shown
  /// automatically while the app is active); background/terminated taps
  /// deep-link via DeepLinkService. getInitialMessage covers "app was fully
  /// closed and launched by tapping a notification," onMessageOpenedApp
  /// covers "app was merely backgrounded."
  void _initializePushHandling() {
    final fcm = ref.read(fcmServiceProvider);
    if (fcm == null) return;

    _foregroundSub = fcm.onForegroundMessage.listen((payload) {
      final text = [payload.title, payload.body].nonNulls.join(' — ');
      if (text.isEmpty) return;
      _scaffoldMessengerKey.currentState?.showSnackBar(SnackBar(content: Text(text)));
    });

    _openedAppSub = fcm.onMessageOpenedApp.listen((payload) {
      ref.read(deepLinkServiceProvider).handleAsync(payload.deepLink);
    });

    fcm.getInitialMessage().then((payload) {
      if (payload != null) {
        ref.read(deepLinkServiceProvider).handleAsync(payload.deepLink);
      }
    });
  }

  @override
  void dispose() {
    _foregroundSub?.cancel();
    _openedAppSub?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final router = ref.watch(appRouterProvider);

    // A session ending anywhere (explicit logout, or a mid-use refresh
    // failure via AuthController.sessionExpired) always routes back to
    // login — a single place for that rule rather than duplicating it per
    // screen. sessionExpiredNoticeProvider distinguishes the latter case so
    // only an unannounced session end gets a "please sign in again" message
    // — an explicit logout already told the user what happened.
    ref.listen(authControllerProvider, (previous, next) {
      if (previous == AuthStatus.authenticated && next == AuthStatus.unauthenticated) {
        router.goNamed(AppRoutes.login);
        if (ref.read(sessionExpiredNoticeProvider)) {
          ref.read(sessionExpiredNoticeProvider.notifier).state = false;
          _scaffoldMessengerKey.currentState?.showSnackBar(
            const SnackBar(content: Text('Your session has expired. Please sign in again.')),
          );
        }
      }
    });

    return MaterialApp.router(
      title: 'Asnan',
      theme: AppTheme.light,
      darkTheme: AppTheme.dark,
      themeMode: ThemeMode.system,
      routerConfig: router,
      scaffoldMessengerKey: _scaffoldMessengerKey,
    );
  }
}
