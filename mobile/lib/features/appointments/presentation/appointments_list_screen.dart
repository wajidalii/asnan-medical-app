import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
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
      final message = widget.scope == AppointmentListScope.upcoming
          ? 'No upcoming appointments. Book one from the doctor directory.'
          : 'No past appointments yet.';
      return Center(child: Padding(padding: const EdgeInsets.all(24), child: Text(message, textAlign: TextAlign.center)));
    }

    return RefreshIndicator(
      onRefresh: controller.retry,
      child: ListView.separated(
        controller: _scrollController,
        padding: const EdgeInsets.all(16),
        itemCount: state.appointments.length + (state.isLoadingMore ? 1 : 0),
        separatorBuilder: (_, _) => const SizedBox(height: 8),
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
    return Card(
      child: ListTile(
        title: Text(appointment.doctorFullName),
        subtitle: Text(_formatDateTime(appointment.slotStartUtc)),
        trailing: Text('${appointment.currency} ${appointment.consultationFee.toStringAsFixed(0)}'),
        onTap: () => context.pushNamed(AppRoutes.appointmentDetails, extra: appointment),
      ),
    );
  }
}
