import 'package:flutter/material.dart';

import '../theme/design_tokens.dart';

/// The system's strong `.hr` rule — 2px, used between major sections. The
/// app-wide `DividerTheme` covers ordinary 1px component borders/list
/// separators; this is for the deliberately heavier structural rule.
class SectionDivider extends StatelessWidget {
  const SectionDivider({super.key});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(height: AppBorders.strong, color: isDark ? AppColors.darkDivider : AppColors.lightDivider);
  }
}
