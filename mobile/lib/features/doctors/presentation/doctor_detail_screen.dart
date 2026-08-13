import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
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
      appBar: AppBar(title: const Text('Doctor Profile')),
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
                  FilledButton(
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
    final textTheme = Theme.of(context).textTheme;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Row(
          children: [
            const CircleAvatar(radius: 36, child: Icon(Icons.person, size: 36)),
            const SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(doctor.fullName, style: textTheme.titleLarge),
                  if (doctor.qualifications != null && doctor.qualifications!.isNotEmpty)
                    Text(doctor.qualifications!, style: textTheme.bodyMedium),
                  if (!doctor.isAcceptingNewPatients)
                    Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Text(
                        'Not accepting new patients',
                        style: textTheme.bodySmall?.copyWith(color: Theme.of(context).colorScheme.error),
                      ),
                    ),
                ],
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
        if (doctor.specialties.isNotEmpty)
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [for (final s in doctor.specialties) Chip(label: Text(s.name))],
          ),
        const SizedBox(height: 16),
        if (doctor.bio != null && doctor.bio!.isNotEmpty) ...[
          Text('About', style: textTheme.titleMedium),
          const SizedBox(height: 4),
          Text(doctor.bio!),
          const SizedBox(height: 16),
        ],
        _DetailRow(icon: Icons.school, label: 'Experience', value: _experienceLabel(doctor.yearsOfExperience)),
        _DetailRow(
          icon: Icons.payments,
          label: 'Consultation fee',
          value: '${doctor.currency} ${doctor.consultationFee.toStringAsFixed(0)}',
        ),
        _DetailRow(
          icon: Icons.timer,
          label: 'Appointment duration',
          value: '${doctor.appointmentDurationMinutes} minutes',
        ),
        if (doctor.clinicAddress != null && doctor.clinicAddress!.isNotEmpty)
          _DetailRow(icon: Icons.location_on, label: 'Clinic', value: doctor.clinicAddress!),
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
