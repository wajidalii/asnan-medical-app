import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import 'design_tokens.dart';

/// "Modernist" design system, ported from design.zip: flat, architectural,
/// mono red-on-white/black accent, Archivo throughout, zero corner radius,
/// strong dividers instead of shadows/rounding to organize the page. Button
/// labels are never centered (see readme.md's "Do"/"Don't") — every button
/// theme below sets `alignment: Alignment.centerLeft` for this reason, which
/// only has a visible effect on buttons stretched wider than their label
/// (the app's full-width CTAs); intrinsic-width buttons are unaffected.
abstract final class AppTheme {
  static ThemeData get light => _build(Brightness.light);
  static ThemeData get dark => _build(Brightness.dark);

  static ThemeData _build(Brightness brightness) {
    final isDark = brightness == Brightness.dark;

    final bg = isDark ? AppColors.darkBg : AppColors.lightBg;
    final surface = isDark ? AppColors.darkSurface : AppColors.lightSurface;
    final text = isDark ? AppColors.darkText : AppColors.lightText;
    final divider = isDark ? AppColors.darkDivider : AppColors.lightDivider;
    final dividerHairline = isDark ? AppColors.darkDividerHairline : AppColors.lightDividerHairline;
    // .btn-primary's color is literally var(--color-bg) in the source
    // system — the ground color of whichever theme is active, not a fixed
    // white/black. On a red fill this reads as near-white text in light
    // mode and near-black text in dark mode.
    final onAccent = bg;
    // Legible accent-as-text needs a deep ramp step in light mode (readme:
    // "for paragraph-size text in the accent use accent-700, not the
    // accent itself") and the accent-400 step in dark mode, matching the
    // mockup's destructive/urgency text color on a dark ground.
    final accentText = isDark ? AppColors.accentOnDark : AppColors.accent700;

    final base = ThemeData(
      useMaterial3: true,
      brightness: brightness,
      scaffoldBackgroundColor: bg,
      canvasColor: bg,
      splashFactory: NoSplash.splashFactory,
      highlightColor: Colors.transparent,
      colorScheme: ColorScheme(
        brightness: brightness,
        primary: AppColors.accent,
        onPrimary: onAccent,
        secondary: AppColors.accent,
        onSecondary: onAccent,
        error: accentText,
        onError: onAccent,
        surface: surface,
        onSurface: text,
        outline: divider,
        outlineVariant: dividerHairline,
      ),
    );

    final textTheme = _textTheme(text, base.textTheme);

    final primaryButtonStyle = ButtonStyle(
      alignment: Alignment.centerLeft,
      elevation: const WidgetStatePropertyAll(0),
      minimumSize: const WidgetStatePropertyAll(Size(64, 48)),
      padding: const WidgetStatePropertyAll(EdgeInsets.symmetric(horizontal: AppSpacing.s4)),
      shape: const WidgetStatePropertyAll(RoundedRectangleBorder(borderRadius: AppRadius.zero)),
      textStyle: WidgetStatePropertyAll(textTheme.labelLarge),
      backgroundColor: WidgetStateProperty.resolveWith((states) {
        if (states.contains(WidgetState.disabled)) return AppColors.accent.withValues(alpha: 0.45);
        if (states.contains(WidgetState.pressed)) return AppColors.accent700;
        if (states.contains(WidgetState.hovered)) return AppColors.accent600;
        return AppColors.accent;
      }),
      foregroundColor: WidgetStateProperty.resolveWith((states) {
        if (states.contains(WidgetState.disabled)) return onAccent.withValues(alpha: 0.45);
        return onAccent;
      }),
      overlayColor: const WidgetStatePropertyAll(Colors.transparent),
    );

    return base.copyWith(
      textTheme: textTheme,
      primaryTextTheme: textTheme,
      dividerTheme: DividerThemeData(color: dividerHairline, thickness: AppBorders.hairline, space: AppBorders.hairline),
      appBarTheme: AppBarTheme(
        backgroundColor: bg,
        foregroundColor: text,
        elevation: 0,
        scrolledUnderElevation: 0,
        surfaceTintColor: Colors.transparent,
        centerTitle: false,
        titleTextStyle: textTheme.titleLarge,
        iconTheme: IconThemeData(color: text),
      ),
      cardTheme: CardThemeData(
        color: surface,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: const RoundedRectangleBorder(borderRadius: AppRadius.zero),
      ),
      chipTheme: ChipThemeData(
        backgroundColor: isDark ? AppColors.neutral800 : AppColors.neutral100,
        labelStyle: textTheme.labelSmall?.copyWith(color: isDark ? AppColors.neutral300 : AppColors.neutral800),
        selectedColor: isDark ? AppColors.accent900 : AppColors.accent100,
        secondarySelectedColor: isDark ? AppColors.accent900 : AppColors.accent100,
        checkmarkColor: isDark ? AppColors.accent300 : AppColors.accentTextOnTint,
        side: BorderSide(color: divider, width: AppBorders.hairline),
        shape: const RoundedRectangleBorder(borderRadius: AppRadius.zero),
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3, vertical: AppSpacing.s1),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: surface,
        contentPadding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3, vertical: AppSpacing.s3),
        labelStyle: textTheme.labelMedium?.copyWith(color: text.withValues(alpha: 0.7)),
        hintStyle: textTheme.bodyMedium?.copyWith(color: text.withValues(alpha: 0.45)),
        errorStyle: textTheme.labelMedium?.copyWith(color: accentText),
        border: OutlineInputBorder(
          borderRadius: AppRadius.zero,
          borderSide: BorderSide(color: divider, width: AppBorders.hairline),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: AppRadius.zero,
          borderSide: BorderSide(color: divider, width: AppBorders.hairline),
        ),
        focusedBorder: const OutlineInputBorder(
          borderRadius: AppRadius.zero,
          borderSide: BorderSide(color: AppColors.accent, width: AppBorders.hairline),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: AppRadius.zero,
          borderSide: BorderSide(color: accentText, width: AppBorders.hairline),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: AppRadius.zero,
          borderSide: BorderSide(color: accentText, width: AppBorders.hairline),
        ),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(style: primaryButtonStyle),
      // FilledButton is the primary CTA in most of this app's existing
      // screens — same accent-fill treatment as ElevatedButton so neither
      // choice of widget produces an off-brand button.
      filledButtonTheme: FilledButtonThemeData(style: primaryButtonStyle),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: ButtonStyle(
          alignment: Alignment.centerLeft,
          elevation: const WidgetStatePropertyAll(0),
          minimumSize: const WidgetStatePropertyAll(Size(64, 48)),
          padding: const WidgetStatePropertyAll(EdgeInsets.symmetric(horizontal: AppSpacing.s4)),
          shape: const WidgetStatePropertyAll(RoundedRectangleBorder(borderRadius: AppRadius.zero)),
          textStyle: WidgetStatePropertyAll(textTheme.labelLarge),
          side: WidgetStateProperty.resolveWith((states) {
            if (states.contains(WidgetState.disabled)) return BorderSide(color: divider.withValues(alpha: 0.45));
            return BorderSide(color: divider);
          }),
          foregroundColor: WidgetStateProperty.resolveWith((states) {
            if (states.contains(WidgetState.disabled)) return text.withValues(alpha: 0.45);
            return text;
          }),
          overlayColor: WidgetStateProperty.resolveWith((states) {
            if (states.contains(WidgetState.pressed)) return text.withValues(alpha: 0.14);
            if (states.contains(WidgetState.hovered)) return text.withValues(alpha: 0.07);
            return Colors.transparent;
          }),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: ButtonStyle(
          alignment: Alignment.centerLeft,
          elevation: const WidgetStatePropertyAll(0),
          padding: const WidgetStatePropertyAll(EdgeInsets.symmetric(horizontal: AppSpacing.s1, vertical: AppSpacing.s2)),
          shape: const WidgetStatePropertyAll(RoundedRectangleBorder(borderRadius: AppRadius.zero)),
          textStyle: WidgetStatePropertyAll(textTheme.labelLarge),
          foregroundColor: WidgetStateProperty.resolveWith((states) {
            if (states.contains(WidgetState.disabled)) return AppColors.accent.withValues(alpha: 0.45);
            return AppColors.accent;
          }),
          overlayColor: WidgetStateProperty.resolveWith((states) {
            if (states.contains(WidgetState.pressed)) return AppColors.accent.withValues(alpha: 0.18);
            if (states.contains(WidgetState.hovered)) return AppColors.accent.withValues(alpha: 0.1);
            return Colors.transparent;
          }),
        ),
      ),
      iconButtonTheme: IconButtonThemeData(
        style: ButtonStyle(
          shape: const WidgetStatePropertyAll(RoundedRectangleBorder(borderRadius: AppRadius.zero)),
          foregroundColor: WidgetStatePropertyAll(text),
        ),
      ),
      snackBarTheme: SnackBarThemeData(
        backgroundColor: surface,
        contentTextStyle: textTheme.bodyMedium?.copyWith(color: text),
        actionTextColor: AppColors.accent,
        behavior: SnackBarBehavior.floating,
        elevation: 0,
        shape: RoundedRectangleBorder(borderRadius: AppRadius.zero, side: BorderSide(color: divider, width: AppBorders.hairline)),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: surface,
        surfaceTintColor: Colors.transparent,
        elevation: 8,
        shape: const RoundedRectangleBorder(borderRadius: AppRadius.zero),
        titleTextStyle: textTheme.headlineSmall,
        contentTextStyle: textTheme.bodyMedium?.copyWith(color: text.withValues(alpha: 0.85)),
      ),
      progressIndicatorTheme: const ProgressIndicatorThemeData(
        color: AppColors.accent,
        linearTrackColor: Colors.transparent,
      ),
      switchTheme: SwitchThemeData(
        trackColor: WidgetStateProperty.resolveWith((states) {
          if (states.contains(WidgetState.selected)) return AppColors.accent;
          return divider;
        }),
        thumbColor: const WidgetStatePropertyAll(Colors.white),
        trackOutlineColor: const WidgetStatePropertyAll(Colors.transparent),
      ),
      tabBarTheme: TabBarThemeData(
        labelColor: text,
        unselectedLabelColor: text.withValues(alpha: 0.55),
        labelStyle: textTheme.labelLarge,
        unselectedLabelStyle: textTheme.labelLarge,
        indicatorColor: AppColors.accent,
        indicatorSize: TabBarIndicatorSize.tab,
        dividerColor: dividerHairline,
      ),
      listTileTheme: ListTileThemeData(
        shape: const RoundedRectangleBorder(borderRadius: AppRadius.zero),
        textColor: text,
        iconColor: text,
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: surface,
        surfaceTintColor: Colors.transparent,
        shape: const RoundedRectangleBorder(borderRadius: AppRadius.zero),
      ),
      segmentedButtonTheme: SegmentedButtonThemeData(
        style: ButtonStyle(
          shape: const WidgetStatePropertyAll(RoundedRectangleBorder(borderRadius: AppRadius.zero)),
          side: WidgetStatePropertyAll(BorderSide(color: divider)),
          textStyle: WidgetStatePropertyAll(textTheme.labelMedium?.copyWith(fontWeight: FontWeight.w800)),
          backgroundColor: WidgetStateProperty.resolveWith((states) {
            if (states.contains(WidgetState.selected)) return AppColors.accent;
            return Colors.transparent;
          }),
          foregroundColor: WidgetStateProperty.resolveWith((states) {
            if (states.contains(WidgetState.selected)) return onAccent;
            return text;
          }),
          overlayColor: const WidgetStatePropertyAll(Colors.transparent),
        ),
      ),
    );
  }

  static TextTheme _textTheme(Color text, TextTheme base) {
    final heading = GoogleFonts.archivoTextTheme(base).apply(bodyColor: text, displayColor: text);
    TextStyle h(double size, {double letterSpacing = -0.3, FontWeight weight = FontWeight.w800}) =>
        heading.bodyLarge!.copyWith(fontFamily: GoogleFonts.archivo().fontFamily, fontSize: size, fontWeight: weight, letterSpacing: letterSpacing, height: 1.15, color: text);
    TextStyle b(double size, {FontWeight weight = FontWeight.w400, double letterSpacing = 0, double? height}) =>
        heading.bodyLarge!.copyWith(fontFamily: GoogleFonts.archivo().fontFamily, fontSize: size, fontWeight: weight, letterSpacing: letterSpacing, height: height ?? 1.4, color: text);

    return TextTheme(
      displayLarge: h(38),
      displayMedium: h(32),
      displaySmall: h(28),
      headlineLarge: h(24),
      headlineMedium: h(22),
      headlineSmall: h(19),
      titleLarge: h(18),
      titleMedium: h(17),
      titleSmall: h(15),
      bodyLarge: b(15),
      bodyMedium: b(14),
      bodySmall: b(13, height: 1.5),
      labelLarge: h(14, letterSpacing: 0, weight: FontWeight.w800),
      labelMedium: b(12, weight: FontWeight.w500),
      labelSmall: b(11, weight: FontWeight.w500, letterSpacing: 0.02),
    );
  }
}
