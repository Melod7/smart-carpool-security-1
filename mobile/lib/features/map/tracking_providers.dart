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

/// Poll de tracking sin flicker de loading ni bucles por la caché de geometría.
///
/// Importante: no hacer `ref.watch(tripGeometryCacheProvider)` aquí — este
/// provider escribe en esa caché; un watch provoca re-fetch infinito.
class TripTrackingPoll
    extends StateNotifier<AsyncValue<TrackingPollResult>> {
  TripTrackingPoll(this.ref, this.tripId) : super(const AsyncLoading()) {
    unawaited(refresh(isInitial: true));
  }

  final Ref ref;
  final String tripId;
  Timer? _timer;
  bool _disposed = false;

  Future<void> refresh({bool isInitial = false}) async {
    if (_disposed) return;

    final seed = ref.read(tripGeometryCacheProvider)[tripId];
    final status = seed?.status;
    if (status != null && !shouldPollTracking(status)) {
      state = AsyncData(
        TrackingPollResult(
          tripNoLongerActive:
              status == 'completed' || status == 'cancelled',
        ),
      );
      return;
    }

    // Solo el primer load muestra loading; los polls mantienen el valor previo.
    if (isInitial && !state.hasValue) {
      state = const AsyncLoading();
    }

    try {
      final tracking =
          await ref.read(trackingApiProvider).getTracking(tripId);
      if (_disposed) return;

      ref.read(tripGeometryCacheProvider.notifier).put(
            TripMapSeed(
              tripId: tracking.tripId,
              status: tracking.status,
              polyline: tracking.polyline,
              waypoints: tracking.waypoints,
            ),
          );

      state = AsyncData(TrackingPollResult(tracking: tracking));
      _scheduleNext(tracking.status);
    } on ApiException catch (e, st) {
      if (_disposed) return;
      if (e.code == 'trip_not_active' || e.statusCode == 409) {
        state = const AsyncData(
          TrackingPollResult(tripNoLongerActive: true),
        );
        return;
      }
      if (!state.hasValue) {
        state = AsyncError(e, st);
      }
      // Si ya había datos, conservar y reintentar en el próximo tick.
      _scheduleNext(status ?? 'scheduled');
    } catch (e, st) {
      if (_disposed) return;
      if (!state.hasValue) {
        state = AsyncError(e, st);
      }
      _scheduleNext(status ?? 'scheduled');
    }
  }

  void _scheduleNext(String status) {
    _timer?.cancel();
    if (_disposed || !shouldPollTracking(status)) return;
    _timer = Timer(trackingPollInterval, () {
      unawaited(refresh());
    });
  }

  @override
  void dispose() {
    _disposed = true;
    _timer?.cancel();
    super.dispose();
  }
}

final tripTrackingProvider = StateNotifierProvider.autoDispose
    .family<TripTrackingPoll, AsyncValue<TrackingPollResult>, String>(
  (ref, tripId) => TripTrackingPoll(ref, tripId),
);
