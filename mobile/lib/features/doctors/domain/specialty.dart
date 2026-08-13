class Specialty {
  const Specialty({required this.id, required this.name, this.description});

  final String id;
  final String name;
  final String? description;

  factory Specialty.fromJson(Map<String, dynamic> json) => Specialty(
        id: json['id'] as String,
        name: json['name'] as String,
        description: json['description'] as String?,
      );
}
