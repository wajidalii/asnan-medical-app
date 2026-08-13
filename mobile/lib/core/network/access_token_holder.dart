import 'package:flutter_riverpod/flutter_riverpod.dart';

/// In-memory-only holder for the current short-lived access token.
///
/// Deliberately not persisted: the access token is reacquired via a refresh
/// on every cold start (see ARCHITECTURE.md §4.5), so there is no need to
/// write it to disk, and every byte of persisted-token surface is one more
/// thing that can leak.
class AccessTokenHolder extends Notifier<String?> {
  @override
  String? build() => null;

  void set(String? token) => state = token;
}

final accessTokenHolderProvider =
    NotifierProvider<AccessTokenHolder, String?>(AccessTokenHolder.new);
