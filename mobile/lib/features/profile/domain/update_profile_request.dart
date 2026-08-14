import 'gender.dart';

/// Mirrors the backend's `UpdatePatientProfileDto`.
class UpdateProfileRequest {
  const UpdateProfileRequest({
    required this.fullName,
    this.dateOfBirth,
    this.gender,
    this.phone,
    this.addressLine,
    this.emergencyContactName,
    this.emergencyContactPhone,
  });

  final String fullName;
  final DateTime? dateOfBirth;
  final Gender? gender;
  final String? phone;
  final String? addressLine;
  final String? emergencyContactName;
  final String? emergencyContactPhone;

  Map<String, dynamic> toJson() => {
        'fullName': fullName,
        'dateOfBirth': dateOfBirth == null ? null : _formatDate(dateOfBirth!),
        'gender': gender?.value,
        'phone': phone,
        'addressLine': addressLine,
        'emergencyContactName': emergencyContactName,
        'emergencyContactPhone': emergencyContactPhone,
      };

  static String _formatDate(DateTime date) =>
      '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
}
