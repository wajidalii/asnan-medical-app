import 'package:flutter/material.dart';

import '../domain/doctor.dart';

/// No photo/rating data exists on the backend yet (ARCHITECTURE.md doesn't
/// model either) — the avatar and rating slots are visual placeholders, not
/// fabricated values, so the layout is ready for both once that data exists.
class DoctorCard extends StatelessWidget {
  const DoctorCard({super.key, required this.doctor, required this.onTap});

  final Doctor doctor;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final specialtyLabel = doctor.specialties.map((s) => s.name).join(', ');

    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
      child: ListTile(
        onTap: onTap,
        contentPadding: const EdgeInsets.all(12),
        leading: const CircleAvatar(radius: 28, child: Icon(Icons.person, size: 28)),
        title: Text(doctor.fullName, style: Theme.of(context).textTheme.titleMedium),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (specialtyLabel.isNotEmpty)
              Text(specialtyLabel, maxLines: 1, overflow: TextOverflow.ellipsis),
            const SizedBox(height: 4),
            Row(
              children: [
                Icon(Icons.star_border, size: 16, color: Theme.of(context).colorScheme.outline),
                const SizedBox(width: 2),
                Text('No ratings yet', style: Theme.of(context).textTheme.bodySmall),
                if (!doctor.isAcceptingNewPatients) ...[
                  const SizedBox(width: 8),
                  Text(
                    'Not accepting new patients',
                    style: Theme.of(context)
                        .textTheme
                        .bodySmall
                        ?.copyWith(color: Theme.of(context).colorScheme.error),
                  ),
                ],
              ],
            ),
          ],
        ),
        trailing: Text(
          '${doctor.currency} ${doctor.consultationFee.toStringAsFixed(0)}',
          style: Theme.of(context).textTheme.titleMedium,
        ),
        isThreeLine: true,
      ),
    );
  }
}
