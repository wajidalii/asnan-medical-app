import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../../core/theme/design_tokens.dart';
import '../domain/doctor_detail.dart';
import '../domain/doctors_failure.dart';
import 'doctor_detail_controller.dart';

class DoctorDetailScreen extends ConsumerWidget {
  const DoctorDetailScreen({super.key, required this.doctorId});

  final String doctorId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final asyncDoctor = ref.watch(doctorDetailProvider(doctorId));

    return Scaffold(
      appBar: AppBar(title: const Text('Doctor profile')),
      body: asyncDoctor.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) {
          final message = error is DoctorsFailure ? error.message : 'Something went wrong. Please try again.';
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(message, textAlign: TextAlign.center),
                  const SizedBox(height: 12),
                  OutlinedButton(
                    onPressed: () => ref.invalidate(doctorDetailProvider(doctorId)),
                    child: const Text('Retry'),
                  ),
                ],
              ),
            ),
          );
        },
        data: (doctor) => _DoctorDetailBody(doctor: doctor),
      ),
    );
  }
}

class _DoctorDetailBody extends StatelessWidget {
  const _DoctorDetailBody({required this.doctor});

  final DoctorDetail doctor;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final isDark = theme.brightness == Brightness.dark;
    final divider = theme.colorScheme.outlineVariant;

    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Container(
          padding: const EdgeInsets.all(20),
          decoration: BoxDecoration(border: Border.all(color: divider)),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  Container(
                    width: 72,
                    height: 72,
                    decoration: BoxDecoration(border: Border.all(color: divider), color: theme.colorScheme.surfaceContainerHighest),
                    child: Icon(Icons.person_outline, size: 32, color: textTheme.bodyMedium?.color?.withValues(alpha: 0.4)),
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(doctor.fullName, style: textTheme.titleLarge),
                        if (doctor.qualifications != null && doctor.qualifications!.isNotEmpty)
                          Text(
                            doctor.qualifications!,
                            style: textTheme.bodySmall?.copyWith(color: textTheme.bodySmall!.color!.withValues(alpha: 0.6)),
                          ),
                        if (!doctor.isAcceptingNewPatients)
                          Padding(
                            padding: const EdgeInsets.only(top: 4),
                            child: Text(
                              'Not accepting new patients',
                              style: textTheme.bodySmall?.copyWith(color: theme.colorScheme.error),
                            ),
                          ),
                      ],
                    ),
                  ),
                ],
              ),
              if (doctor.specialties.isNotEmpty) ...[
                const SizedBox(height: 14),
                Wrap(
                  spacing: 6,
                  runSpacing: 6,
                  children: [for (final s in doctor.specialties) _SpecialtyTag(label: s.name, isDark: isDark)],
                ),
              ],
              if (doctor.bio != null && doctor.bio!.isNotEmpty) ...[
                const SizedBox(height: 14),
                Text(doctor.bio!, style: textTheme.bodySmall?.copyWith(height: 1.6, color: textTheme.bodySmall!.color!.withValues(alpha: 0.85))),
              ],
              const SizedBox(height: 14),
              Container(
                padding: const EdgeInsets.only(top: 12),
                decoration: BoxDecoration(border: Border(top: BorderSide(color: divider))),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  crossAxisAlignment: CrossAxisAlignment.baseline,
                  textBaseline: TextBaseline.alphabetic,
                  children: [
                    Text('Consultation fee', style: textTheme.labelMedium),
                    Text('${doctor.currency} ${doctor.consultationFee.toStringAsFixed(2)}', style: textTheme.headlineSmall),
                  ],
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 20),
        _DetailRow(icon: Icons.school_outlined, label: 'Experience', value: _experienceLabel(doctor.yearsOfExperience)),
        _DetailRow(icon: Icons.timer_outlined, label: 'Appointment duration', value: '${doctor.appointmentDurationMinutes} minutes'),
        if (doctor.clinicAddress != null && doctor.clinicAddress!.isNotEmpty)
          _DetailRow(icon: Icons.location_on_outlined, label: 'Clinic', value: doctor.clinicAddress!),
        const SizedBox(height: 24),
        FilledButton(
          onPressed: doctor.isAcceptingNewPatients
              ? () => context.pushNamed(AppRoutes.booking, pathParameters: {'id': doctor.id})
              : null,
          child: Text(doctor.isAcceptingNewPatients ? 'Book Appointment' : 'Not accepting new patients'),
        ),
      ],
    );
  }

  String _experienceLabel(int? years) {
    if (years == null) return 'Not specified';
    return years == 1 ? '1 year' : '$years years';
  }
}

class _SpecialtyTag extends StatelessWidget {
  const _SpecialtyTag({required this.label, required this.isDark});

  final String label;
  final bool isDark;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
      decoration: BoxDecoration(color: isDark ? AppColors.accent900 : AppColors.accent100),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelSmall?.copyWith(color: isDark ? AppColors.accent300 : AppColors.accentTextOnTint),
      ),
    );
  }
}

class _DetailRow extends StatelessWidget {
  const _DetailRow({required this.icon, required this.label, required this.value});

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 20, color: Theme.of(context).colorScheme.outline),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label, style: Theme.of(context).textTheme.bodySmall),
                Text(value, style: Theme.of(context).textTheme.bodyLarge),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
