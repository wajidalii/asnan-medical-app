import 'dart:collection';

import 'package:device_calendar/device_calendar.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'calendar_event_data.dart';
import 'calendar_write_result.dart';

/// Wraps the `device_calendar` plugin behind an app-level interface — issue
/// #26. Client-side event creation only, no calendar-provider OAuth/sync,
/// per ARCHITECTURE.md §11. The interface (rather than calling the plugin
/// directly from the UI) is what lets a fake implementation stand in for
/// widget tests, which have no platform channels to talk to a real device
/// calendar — see the issue's testing requirement.
abstract class CalendarService {
  Future<CalendarWriteResult> addEvent(CalendarEventData data);
}

class DeviceCalendarService implements CalendarService {
  DeviceCalendarService([DeviceCalendarPlugin? plugin]) : _plugin = plugin ?? DeviceCalendarPlugin();

  final DeviceCalendarPlugin _plugin;

  @override
  Future<CalendarWriteResult> addEvent(CalendarEventData data) async {
    if (!await _ensurePermissions()) {
      return const CalendarWriteResult(CalendarWriteStatus.permissionDenied);
    }

    final calendarsResult = await _plugin.retrieveCalendars();
    final calendars = calendarsResult.data ?? UnmodifiableListView<Calendar>(const []);
    final writable = calendars.where((c) => c.isReadOnly != true).toList();
    if (writable.isEmpty) {
      return const CalendarWriteResult(CalendarWriteStatus.noWritableCalendar);
    }
    final target = writable.firstWhere((c) => c.isDefault == true, orElse: () => writable.first);

    final event = Event(
      target.id,
      title: data.title,
      start: TZDateTime.from(data.startUtc, UTC),
      end: TZDateTime.from(data.endUtc, UTC),
      location: data.location,
      description: data.description,
    );

    final createResult = await _plugin.createOrUpdateEvent(event);
    final eventId = createResult?.data;
    if (eventId == null || (createResult?.hasErrors ?? true)) {
      return const CalendarWriteResult(CalendarWriteStatus.failure);
    }

    return CalendarWriteResult(CalendarWriteStatus.success, eventId: eventId);
  }

  Future<bool> _ensurePermissions() async {
    final hasResult = await _plugin.hasPermissions();
    if (hasResult.data == true) return true;

    final requestResult = await _plugin.requestPermissions();
    return requestResult.data == true;
  }
}

final calendarServiceProvider = Provider<CalendarService>((ref) => DeviceCalendarService());
