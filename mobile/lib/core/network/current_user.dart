import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'access_token_holder.dart';

/// Decodes the `sub` claim from a JWT access token — no signature
/// verification (the token is only ever used locally to know "which
/// messages are mine" for chat UI purposes; every actual authorization
/// decision happens server-side against the verified token).
String? decodeJwtUserId(String? token) {
  if (token == null) return null;

  final parts = token.split('.');
  if (parts.length != 3) return null;

  try {
    final payload = utf8.decode(base64Url.decode(base64Url.normalize(parts[1])));
    final claims = jsonDecode(payload) as Map<String, dynamic>;
    return claims['sub'] as String?;
  } catch (_) {
    return null;
  }
}

/// Re-derives whenever the access token changes (login, refresh, logout).
final currentUserIdProvider = Provider<String?>((ref) => decodeJwtUserId(ref.watch(accessTokenHolderProvider)));
