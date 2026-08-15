import 'package:flutter/material.dart';

import '../../../core/theme/design_tokens.dart';
import '../domain/doctor.dart';

/// No photo data exists on the backend yet (ARCHITECTURE.md doesn't model
/// it) — the avatar slot is a visual placeholder, not a fabricated value.
class DoctorCard extends StatelessWidget {
  const DoctorCard({super.key, required this.doctor, required this.onTap});

  final Doctor doctor;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final isDark = theme.brightness == Brightness.dark;
    final divider = theme.colorScheme.outlineVariant;
    final specialtyLabel = doctor.specialties.map((s) => s.name).join(', ');

    final hasTags = doctor.specialties.isNotEmpty || doctor.yearsOfExperience != null || !doctor.isAcceptingNewPatients;

    return InkWell(
      onTap: onTap,
      child: Container(
        margin: const EdgeInsets.fromLTRB(20, 0, 20, 12),
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(border: Border.all(color: divider)),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            Container(
              width: 52,
              height: 52,
              decoration: BoxDecoration(border: Border.all(color: divider), color: theme.colorScheme.surfaceContainerHighest),
              child: Icon(Icons.person_outline, size: 24, color: theme.textTheme.bodyMedium?.color?.withValues(alpha: 0.4)),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(doctor.fullName, style: theme.textTheme.titleMedium, maxLines: 1, overflow: TextOverflow.ellipsis),
                  const SizedBox(height: 3),
                  Text(
                    specialtyLabel.isNotEmpty ? specialtyLabel : 'General practice',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.bodySmall?.copyWith(color: theme.textTheme.bodySmall!.color!.withValues(alpha: 0.6)),
                  ),
                  if (hasTags) ...[
                    const SizedBox(height: 6),
                    Wrap(
                      spacing: 6,
                      runSpacing: 6,
                      children: [
                        for (final specialty in doctor.specialties.take(2)) _Tag(label: specialty.name, emphasis: true, isDark: isDark),
                        if (doctor.yearsOfExperience != null)
                          _Tag(label: '${doctor.yearsOfExperience} yrs exp.', emphasis: false, isDark: isDark),
                        if (!doctor.isAcceptingNewPatients)
                          _Tag(label: 'Not accepting new patients', emphasis: true, isDark: isDark),
                      ],
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(width: 8),
            Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Text(
                  doctor.currency,
                  style: theme.textTheme.labelSmall?.copyWith(color: theme.textTheme.bodySmall!.color!.withValues(alpha: 0.5)),
                ),
                Text(
                  doctor.consultationFee.toStringAsFixed(0),
                  style: theme.textTheme.titleMedium?.copyWith(color: isDark ? AppColors.accent300 : AppColors.accent700),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _Tag extends StatelessWidget {
  const _Tag({required this.label, required this.emphasis, required this.isDark});

  final String label;
  final bool emphasis;
  final bool isDark;

  @override
  Widget build(BuildContext context) {
    final Color bg;
    final Color fg;
    if (emphasis) {
      bg = isDark ? AppColors.accent900 : AppColors.accent100;
      fg = isDark ? AppColors.accent300 : AppColors.accentTextOnTint;
    } else {
      bg = isDark ? AppColors.neutral800 : AppColors.neutral100;
      fg = isDark ? AppColors.neutral300 : AppColors.neutralTextOnTint;
    }
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
      decoration: BoxDecoration(color: bg),
      child: Text(label, style: Theme.of(context).textTheme.labelSmall?.copyWith(color: fg)),
    );
  }
}
