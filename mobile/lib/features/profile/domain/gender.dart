/// Mirrors the backend's `Gender` enum (int-valued in JSON).
enum Gender {
  male(1, 'Male'),
  female(2, 'Female'),
  other(3, 'Other'),
  preferNotToSay(4, 'Prefer not to say');

  const Gender(this.value, this.label);

  final int value;
  final String label;

  static Gender fromValue(int value) => Gender.values.firstWhere(
        (g) => g.value == value,
        orElse: () => throw ArgumentError('Unknown Gender value: $value'),
      );
}
