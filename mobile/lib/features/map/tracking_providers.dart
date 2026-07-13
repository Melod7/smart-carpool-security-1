import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/models.dart';
import '../../auth/auth_state.dart';
import 'map_route.dart';
import 'trip_geometry_cache.dart';

const trackingPollInterval = Duration(seconds: 10);
const pingInterval = Duration(seconds: 10);

/// Resultado del poll de tracking (incluye señal de trip ya no activo).
class TrackingPollResult {
  const TrackingPollResult({this.tracking, this.tripNoLongerActive = false});

  final TripTracking? tracking;
  final bool tripNoLongerActive;
}

/// Poll de tracking cada 10s mientras el trip esté `in_progress`.
final tripTrackingProvider =
    FutureProvider.autoDispose.family<TrackingPollResult, String>(
        (ref, tripId) async {
  final seed = ref.watch(tripGeometryCacheProvider)[tripId];
  final status = seed?.status;

  // Sin pings/poll si no está activo (battery-safe).
  if (status != null && !shouldPollTracking(status)) {
    return TrackingPollResult(
      tripNoLongerActive: status == 'completed' || status == 'cancelled',
    );
  }

  try {
    final tracking = await ref.watch(trackingApiProvider).getTracking(tripId);
    ref.read(tripGeometryCacheProvider.notifier).put(
          TripMapSeed(
            tripId: tracking.tripId,
            status: tracking.status,
            polyline: tracking.polyline,
          ),
        );
    if (shouldPollTracking(tracking.status)) {
      final timer = Timer(trackingPollInterval, () {
        ref.invalidateSelf();
      });
      ref.onDispose(timer.cancel);
    }
    return TrackingPollResult(tracking: tracking);
  } on ApiException catch (e) {
    // trip_not_active → complete/cancel o scheduled; detener loops.
    if (e.code == 'trip_not_active' || e.statusCode == 409) {
      return const TrackingPollResult(tripNoLongerActive: true);
    }
    rethrow;
  }
});
