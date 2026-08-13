/// Mirrors the backend's `PagedResult<T>` envelope: `{ items, page, pageSize, totalCount }`.
class PagedResult<T> {
  const PagedResult({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
  });

  final List<T> items;
  final int page;
  final int pageSize;
  final int totalCount;

  bool get hasMore => page * pageSize < totalCount;

  factory PagedResult.fromJson(Map<String, dynamic> json, T Function(Map<String, dynamic>) fromJson) => PagedResult(
        items: (json['items'] as List<dynamic>).map((e) => fromJson(e as Map<String, dynamic>)).toList(),
        page: json['page'] as int,
        pageSize: json['pageSize'] as int,
        totalCount: json['totalCount'] as int,
      );
}
