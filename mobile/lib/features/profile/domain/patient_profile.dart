import 'gender.dart';

/// Mirrors the backend's `PatientProfileDto`. Email/Mobile are read-only —
/// sourced from the verified login identity, not editable via this screen.
class PatientProfile {
  const PatientProfile({
    required this.userId,
    required this.email,
    required this.mobile,
    required this.fullName,
    required this.dateOfBirth,
    required this.gender,
    required this.phone,
    required this.addressLine,
    required this.emergencyContactName,
    required this.emergencyContactPhone,
    required this.hasPhoto,
  });

  final String userId;
  final String? email;
  final String? mobile;
  final String fullName;
  final DateTime? dateOfBirth;
  final Gender? gender;
  final String? phone;
  final String? addressLine;
  final String? emergencyContactName;
  final String? emergencyContactPhone;
  final bool hasPhoto;

  factory PatientProfile.fromJson(Map<String, dynamic> json) => PatientProfile(
        userId: json['userId'] as String,
        email: json['email'] as String?,
        mobile: json['mobile'] as String?,
        fullName: json['fullName'] as String,
        dateOfBirth: json['dateOfBirth'] == null ? null : DateTime.parse(json['dateOfBirth'] as String),
        gender: json['gender'] == null ? null : Gender.fromValue(json['gender'] as int),
        phone: json['phone'] as String?,
        addressLine: json['addressLine'] as String?,
        emergencyContactName: json['emergencyContactName'] as String?,
        emergencyContactPhone: json['emergencyContactPhone'] as String?,
        hasPhoto: json['hasPhoto'] as bool,
      );
}
