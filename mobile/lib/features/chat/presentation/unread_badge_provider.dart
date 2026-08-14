import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../appointments/data/appointments_repository.dart';
import '../../appointments/domain/appointment_list_scope.dart';
import '../../appointments/domain/appointments_result.dart';
import '../data/chat_repository.dart';
import '../domain/chat_result.dart';

/// Total unread count across the caller's own appointment conversations —
/// issue #29's "unread badge... reflects server-derived unread state".
///
/// There's no aggregate "my conversations" endpoint, so this fetches the
/// (small, paginated) upcoming/past appointment lists and sums each known
/// conversation's own read-status — an N+1 pattern that's fine at this
/// app's scale but wouldn't be the right shape for a large conversation
/// count; a dedicated aggregate endpoint would be the fix if that's ever needed.
///
/// Refreshed on demand (screen entry / pull-to-refresh via ref.invalidate),
/// not live-pushed — a message arriving while this isn't visible doesn't
/// update the badge until the next refresh.
final unreadBadgeProvider = FutureProvider<int>((ref) async {
  final appointmentsRepo = ref.watch(appointmentsRepositoryProvider);
  final chatRepo = ref.watch(chatRepositoryProvider);

  final scopesResults = await Future.wait([
    appointmentsRepo.list(AppointmentListScope.upcoming),
    appointmentsRepo.list(AppointmentListScope.past),
  ]);

  final conversationIds = <String>{};
  for (final result in scopesResults) {
    if (result case AppointmentsSuccess(:final value)) {
      for (final appointment in value.items) {
        final conversationId = appointment.chatConversationId;
        if (conversationId != null) {
          conversationIds.add(conversationId);
        }
      }
    }
  }

  final readStatusResults = await Future.wait(conversationIds.map(chatRepo.getReadStatus));

  var total = 0;
  for (final result in readStatusResults) {
    if (result case ChatSuccess(:final value)) {
      total += value.unreadCount;
    }
  }

  return total;
});
