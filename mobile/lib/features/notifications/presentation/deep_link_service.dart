import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/router/app_router.dart';
import '../../appointments/data/appointments_repository.dart';
import '../../appointments/domain/appointments_result.dart';

/// Routes a push notification's `asnan://...` deep link to the right screen
/// with real content loaded — issue #32. An appointment link fetches the
/// full AppointmentSummary first (a push only carries an id) since the
/// existing appointmentDetails route takes the whole object via `extra`; a
/// chat link only needs conversationId, its one dynamic route segment.
///
/// Known limitation: a chat deep link shows a generic "Chat" title instead
/// of the doctor's name — there's no "who's the other participant on this
/// conversation" lookup independent of already having the appointment, and
/// fetching one just for the AppBar title wasn't judged worth a new
/// endpoint for this pass.
class DeepLinkService {
  DeepLinkService(this._ref);

  final Ref _ref;

  Future<void> handleAsync(String? deepLink) async {
    if (deepLink == null) return;

    final uri = Uri.tryParse(deepLink);
    if (uri == null || uri.scheme != 'asnan' || uri.pathSegments.isEmpty) return;

    final router = _ref.read(appRouterProvider);

    switch (uri.host) {
      case 'appointments':
        final result = await _ref.read(appointmentsRepositoryProvider).getById(uri.pathSegments.first);
        if (result case AppointmentsSuccess(:final value)) {
          router.pushNamed(AppRoutes.appointmentDetails, extra: value);
        }
      case 'chat':
        router.pushNamed(AppRoutes.chat, pathParameters: {'conversationId': uri.pathSegments.first}, extra: 'Chat');
    }
  }
}

final deepLinkServiceProvider = Provider<DeepLinkService>((ref) => DeepLinkService(ref));
