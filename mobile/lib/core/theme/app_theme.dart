import 'package:flutter/material.dart';

/// A single placeholder theme. Real design-system tokens (colors, type
/// scale) land alongside the first real UI work rather than being guessed
/// here.
abstract final class AppTheme {
  static ThemeData light = ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(seedColor: Colors.teal),
  );
}
