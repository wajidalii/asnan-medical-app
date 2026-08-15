import 'package:flutter/material.dart';

/// Tokens ported 1:1 from the "Modernist" design system (design.zip):
/// flat, architectural, near-mono red-on-white, Archivo throughout, zero
/// corner radius, strong 2px dividers. See design.zip's readme.md for the
/// full rationale — this file only carries the numbers.
abstract final class AppColors {
  // Light ground.
  static const lightBg = Color(0xFFF3F2F2);
  static const lightSurface = Color(0xFFEAE9E9);
  static const lightText = Color(0xFF201E1D);
  static const lightDivider = Color(0x66201E1D); // color-mix(#201e1d 40%, transparent)
  static const lightDividerHairline = Color(0x29201E1D); // rgba(32,30,29,.16)

  // Dark ground — bg/text swap per the system's own ramp logic (it ships
  // light-only; dark mode is derived, not authored separately).
  static const darkBg = Color(0xFF201E1D);
  static const darkSurface = Color(0xFF2D2B2B); // == neutral-900
  static const darkText = Color(0xFFF3F2F2);
  static const darkDivider = Color(0x2EF5F5F5); // rgba(245,245,245,.18)
  static const darkDividerHairline = Color(0x1FF5F5F5);

  // The one accent — mono scheme, no second hue.
  static const accent = Color(0xFFEC3013);

  // Accent ramp (OKLCH-derived tonal steps).
  static const accent100 = Color(0xFFFFF2EF);
  static const accent200 = Color(0xFFFFE0D9);
  static const accent300 = Color(0xFFFFC4B8);
  static const accent400 = Color(0xFFFF9783);
  static const accent500 = Color(0xFFFF563C);
  static const accent600 = Color(0xFFDD2B0F);
  static const accent700 = Color(0xFFAE1800);
  static const accent800 = Color(0xFF7C1405);
  static const accent900 = Color(0xFF4D170E);

  // Neutral ramp.
  static const neutral100 = Color(0xFFF8F4F4);
  static const neutral200 = Color(0xFFEAE7E7);
  static const neutral300 = Color(0xFFD7D3D3);
  static const neutral400 = Color(0xFFBAB6B6);
  static const neutral500 = Color(0xFF9B9797);
  static const neutral600 = Color(0xFF7D7979);
  static const neutral700 = Color(0xFF605D5D);
  static const neutral800 = Color(0xFF444141);
  static const neutral900 = Color(0xFF2D2B2B);

  /// Text-on-tinted-fill (e.g. status pills) — dark steps of the ramp, per
  /// the system's "500 as base, 700-900 for text on tinted fills" rule.
  static const accentTextOnTint = accent800;
  static const neutralTextOnTint = neutral800;

  /// Destructive/urgency text on a dark ground reads as accent-400 (the
  /// system's own "accent-600 on light, accent-400 on dark" hover rule
  /// extended to any accent-colored text for contrast).
  static const accentOnDark = accent400;
}

abstract final class AppSpacing {
  static const s1 = 4.0;
  static const s2 = 8.0;
  static const s3 = 12.0;
  static const s4 = 16.0;
  static const s6 = 24.0;
  static const s8 = 32.0;
}

/// Zero everywhere, on purpose — the system's defining trait ("do not round
/// a corner anywhere"). Named rather than inlined as 0 so intent reads at
/// the call site.
abstract final class AppRadius {
  static const none = 0.0;
  static const zero = BorderRadius.zero;
}

abstract final class AppBorders {
  static const hairline = 1.0;
  static const strong = 2.0;
}
