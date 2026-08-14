import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../auth/presentation/auth_controller.dart';
import '../../chat/presentation/unread_badge_provider.dart';
import '../domain/doctor_sort_by.dart';
import 'doctor_card.dart';
import 'doctor_list_controller.dart';
import 'doctor_list_state.dart';

class DoctorListScreen extends ConsumerStatefulWidget {
  const DoctorListScreen({super.key});

  @override
  ConsumerState<DoctorListScreen> createState() => _DoctorListScreenState();
}

class _DoctorListScreenState extends ConsumerState<DoctorListScreen> {
  final _searchController = TextEditingController();
  final _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(doctorListControllerProvider.notifier).loadInitial();
    });
    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _searchController.dispose();
    _scrollController.removeListener(_onScroll);
    _scrollController.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (_scrollController.position.pixels >= _scrollController.position.maxScrollExtent - 200) {
      ref.read(doctorListControllerProvider.notifier).loadMore();
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(doctorListControllerProvider);
    final controller = ref.read(doctorListControllerProvider.notifier);
    final unreadCount = ref.watch(unreadBadgeProvider).maybeWhen(data: (count) => count, orElse: () => 0);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Find a Doctor'),
        actions: [
          IconButton(
            icon: Badge.count(
              count: unreadCount,
              isLabelVisible: unreadCount > 0,
              child: const Icon(Icons.event_note),
            ),
            tooltip: 'My appointments',
            onPressed: () async {
              await context.pushNamed(AppRoutes.appointments);
              ref.invalidate(unreadBadgeProvider);
            },
          ),
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Sign out',
            onPressed: () => ref.read(authControllerProvider.notifier).logout(),
          ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            child: TextField(
              controller: _searchController,
              textInputAction: TextInputAction.search,
              decoration: InputDecoration(
                hintText: 'Search by name or specialty',
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _searchController.text.isEmpty
                    ? null
                    : IconButton(
                        icon: const Icon(Icons.clear),
                        onPressed: () {
                          _searchController.clear();
                          controller.setSearchText('');
                        },
                      ),
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
              ),
              onSubmitted: controller.setSearchText,
            ),
          ),
          if (state.specialties.isNotEmpty)
            SizedBox(
              height: 40,
              child: ListView.separated(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                scrollDirection: Axis.horizontal,
                itemCount: state.specialties.length,
                separatorBuilder: (_, _) => const SizedBox(width: 8),
                itemBuilder: (context, index) {
                  final specialty = state.specialties[index];
                  final selected = state.selectedSpecialtyIds.contains(specialty.id);
                  return FilterChip(
                    label: Text(specialty.name),
                    selected: selected,
                    onSelected: (_) => controller.toggleSpecialty(specialty.id),
                  );
                },
              ),
            ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            child: Row(
              children: [
                const Text('Sort by:'),
                const SizedBox(width: 8),
                DropdownButton<DoctorSortBy>(
                  value: state.sortBy,
                  items: DoctorSortBy.values
                      .map((sort) => DropdownMenuItem(value: sort, child: Text(sort.label)))
                      .toList(),
                  onChanged: (sort) {
                    if (sort != null) controller.setSortBy(sort);
                  },
                ),
                IconButton(
                  icon: Icon(state.descending ? Icons.arrow_downward : Icons.arrow_upward),
                  tooltip: state.descending ? 'Descending' : 'Ascending',
                  onPressed: () => controller.setSortBy(state.sortBy),
                ),
              ],
            ),
          ),
          Expanded(child: _buildBody(context, state, controller)),
        ],
      ),
    );
  }

  Widget _buildBody(BuildContext context, DoctorListState state, DoctorListController controller) {
    if (state.isLoading && state.doctors.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }

    if (state.failure != null && state.doctors.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(state.failure!.message, textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton(onPressed: controller.retry, child: const Text('Retry')),
            ],
          ),
        ),
      );
    }

    if (state.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Text(
            'No doctors match your search.',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodyLarge,
          ),
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: controller.retry,
      child: ListView.builder(
        controller: _scrollController,
        itemCount: state.doctors.length + (state.isLoadingMore ? 1 : 0),
        itemBuilder: (context, index) {
          if (index >= state.doctors.length) {
            return const Padding(
              padding: EdgeInsets.all(16),
              child: Center(child: CircularProgressIndicator()),
            );
          }

          final doctor = state.doctors[index];
          return DoctorCard(
            doctor: doctor,
            onTap: () => context.pushNamed(AppRoutes.doctorDetail, pathParameters: {'id': doctor.id}),
          );
        },
      ),
    );
  }
}
