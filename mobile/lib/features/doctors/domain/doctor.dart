import 'specialty.dart';

/// A doctor as shown in the directory list — the same fields the backend's
/// `DoctorListItemDto` exposes. No photo/rating fields exist yet on either
/// side; `DoctorCard` renders placeholders for those, not fabricated data.
class Doctor {
  const Doctor({
    required this.id,
    required this.fullName,
    required this.bio,
    required this.consultationFee,
    required this.currency,
    required this.yearsOfExperience,
    required this.clinicAddress,
    required this.isAcceptingNewPatients,
    required this.specialties,
  });

  final String id;
  final String fullName;
  final String? bio;
  final double consultationFee;
  final String currency;
  final int? yearsOfExperience;
  final String? clinicAddress;
  final bool isAcceptingNewPatients;
  final List<Specialty> specialties;

  factory Doctor.fromJson(Map<String, dynamic> json) => Doctor(
        id: json['id'] as String,
        fullName: json['fullName'] as String,
        bio: json['bio'] as String?,
        consultationFee: (json['consultationFee'] as num).toDouble(),
        currency: json['currency'] as String,
        yearsOfExperience: json['yearsOfExperience'] as int?,
        clinicAddress: json['clinicAddress'] as String?,
        isAcceptingNewPatients: json['isAcceptingNewPatients'] as bool,
        specialties: (json['specialties'] as List<dynamic>)
            .map((e) => Specialty.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}
