import 'specialty.dart';

/// Full profile shown on the doctor detail screen — mirrors the backend's
/// `DoctorDetailDto`. Deliberately has no "upcoming available dates": that
/// requires the availability model, which doesn't exist until Milestone 4
/// (see backend PR #53) — flagged there, not faked here either.
class DoctorDetail {
  const DoctorDetail({
    required this.id,
    required this.fullName,
    required this.bio,
    required this.qualifications,
    required this.specialties,
    required this.yearsOfExperience,
    required this.consultationFee,
    required this.currency,
    required this.clinicAddress,
    required this.appointmentDurationMinutes,
    required this.isAcceptingNewPatients,
  });

  final String id;
  final String fullName;
  final String? bio;
  final String? qualifications;
  final List<Specialty> specialties;
  final int? yearsOfExperience;
  final double consultationFee;
  final String currency;
  final String? clinicAddress;
  final int appointmentDurationMinutes;
  final bool isAcceptingNewPatients;

  factory DoctorDetail.fromJson(Map<String, dynamic> json) => DoctorDetail(
        id: json['id'] as String,
        fullName: json['fullName'] as String,
        bio: json['bio'] as String?,
        qualifications: json['qualifications'] as String?,
        specialties: (json['specialties'] as List<dynamic>)
            .map((e) => Specialty.fromJson(e as Map<String, dynamic>))
            .toList(),
        yearsOfExperience: json['yearsOfExperience'] as int?,
        consultationFee: (json['consultationFee'] as num).toDouble(),
        currency: json['currency'] as String,
        clinicAddress: json['clinicAddress'] as String?,
        appointmentDurationMinutes: json['appointmentDurationMinutes'] as int,
        isAcceptingNewPatients: json['isAcceptingNewPatients'] as bool,
      );
}
