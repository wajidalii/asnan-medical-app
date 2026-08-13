import 'availability_slot.dart';

class DoctorAvailability {
  const DoctorAvailability({required this.doctorId, required this.timeZoneId, required this.slots});

  final String doctorId;
  final String timeZoneId;
  final List<AvailabilitySlot> slots;

  factory DoctorAvailability.fromJson(Map<String, dynamic> json) => DoctorAvailability(
        doctorId: json['doctorId'] as String,
        timeZoneId: json['timeZoneId'] as String,
        slots: (json['slots'] as List<dynamic>)
            .map((e) => AvailabilitySlot.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}
