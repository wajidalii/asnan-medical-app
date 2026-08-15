import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../../core/widgets/status_pill.dart';
import '../domain/appointment_list_scope.dart';
import '../domain/appointment_summary.dart';
import 'appointments_list_controller.dart';

String _formatDateTime(DateTime utc) {
  final local = utc.toLocal();
  final hour12 = local.hour % 12 == 0 ? 12 : local.hour % 12;
  final minute = local.minute.toString().padLeft(2, '0');
  final period = local.hour < 12 ? 'AM' : 'PM';
  return '${local.year}-${local.month.toString().padLeft(2, '0')}-${local.day.toString().padLeft(2, '0')} · $hour12:$minute $period';
}

/// Upcoming/past appointment lists (#26) — a tab per AppointmentListScope,
/// each backed by its own AppointmentsListController instance so switching
/// tabs doesn't re-fetch or lose scroll position.
class AppointmentsListScreen extends StatelessWidget {
  const AppointmentsListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('My Appointments'),
          bottom: const TabBar(tabs: [Tab(text: 'Upcoming'), Tab(text: 'Past')]),
        ),
        body: const TabBarView(
          children: [
            _AppointmentsTab(scope: AppointmentListScope.upcoming),
            _AppointmentsTab(scope: AppointmentListScope.past),
          ],
        ),
      ),
    );
  }
}

class _AppointmentsTab extends ConsumerStatefulWidget {
  const _AppointmentsTab({required this.scope});

  final AppointmentListScope scope;

  @override
  ConsumerState<_AppointmentsTab> createState() => _AppointmentsTabState();
}

class _AppointmentsTabState extends ConsumerState<_AppointmentsTab> {
  final _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(appointmentsListControllerProvider(widget.scope).notifier).loadInitial();
    });
    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _scrollController.removeListener(_onScroll);
    _scrollController.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (_scrollController.position.pixels >= _scrollController.position.maxScrollExtent - 200) {
      ref.read(appointmentsListControllerProvider(widget.scope).notifier).loadMore();
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(appointmentsListControllerProvider(widget.scope));
    final controller = ref.read(appointmentsListControllerProvider(widget.scope).notifier);

    if (state.isLoading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (state.failure != null) {
      final textTheme = Theme.of(context).textTheme;
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('Couldn’t load appointments', style: textTheme.titleMedium, textAlign: TextAlign.center),
              const SizedBox(height: 6),
              Text(
                state.failure!.message,
                style: textTheme.bodySmall?.copyWith(color: textTheme.bodySmall!.color!.withValues(alpha: 0.6)),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 16),
              OutlinedButton(onPressed: controller.retry, child: const Text('Retry')),
            ],
          ),
        ),
      );
    }

    if (state.isEmpty) {
      final textTheme = Theme.of(context).textTheme;
      final title = widget.scope == AppointmentListScope.upcoming ? 'No appointments yet' : 'No past appointments';
      final message = widget.scope == AppointmentListScope.upcoming
          ? 'Book your first appointment to see it here.'
          : 'Appointments you’ve completed will show up here.';
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(title, style: textTheme.titleMedium, textAlign: TextAlign.center),
              const SizedBox(height: 6),
              Text(
                message,
                style: textTheme.bodySmall?.copyWith(color: textTheme.bodySmall!.color!.withValues(alpha: 0.6)),
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: controller.retry,
      child: ListView.separated(
        controller: _scrollController,
        padding: const EdgeInsets.all(20),
        itemCount: state.appointments.length + (state.isLoadingMore ? 1 : 0),
        separatorBuilder: (_, _) => const SizedBox(height: 10),
        itemBuilder: (context, index) {
          if (index >= state.appointments.length) {
            return const Padding(padding: EdgeInsets.all(16), child: Center(child: CircularProgressIndicator()));
          }

          final appointment = state.appointments[index];
          return _AppointmentTile(appointment: appointment);
        },
      ),
    );
  }
}

class _AppointmentTile extends StatelessWidget {
  const _AppointmentTile({required this.appointment});

  final AppointmentSummary appointment;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final divider = theme.colorScheme.outlineVariant;
    return InkWell(
      onTap: () => context.pushNamed(AppRoutes.appointmentDetails, extra: appointment),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(border: Border.all(color: divider)),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              width: 44,
              height: 44,
              decoration: BoxDecoration(border: Border.all(color: divider), color: theme.colorScheme.surfaceContainerHighest),
              child: Icon(Icons.person_outline, color: theme.textTheme.bodyMedium?.color?.withValues(alpha: 0.4)),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(appointment.doctorFullName, style: theme.textTheme.titleSmall),
                  const SizedBox(height: 2),
                  Text(
                    _formatDateTime(appointment.slotStartUtc),
                    style: theme.textTheme.bodySmall?.copyWith(color: theme.textTheme.bodySmall!.color!.withValues(alpha: 0.6)),
                  ),
                  const SizedBox(height: 6),
                  StatusPill.forStatus(appointment.status),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
