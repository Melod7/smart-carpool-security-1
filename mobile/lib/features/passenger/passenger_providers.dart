import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/models.dart';
import '../../api/passenger_api.dart';
import '../../auth/auth_state.dart';
import '../map/trip_geometry_cache.dart';
import '../sos/sos_location.dart';
import 'trip_labels.dart';

const pollInterval = Duration(seconds: 15);

/// Refresco periódico: invalida el provider cada [interval].
void _schedulePoll(Ref ref, Duration interval) {
  final timer = Timer.periodic(interval, (_) {
    ref.invalidateSelf();
  });
  ref.onDispose(timer.cancel);
}

/// Resultado de disponibles + si se pudo usar GPS para suggestedWait.
class AvailableTripsResult {
  const AvailableTripsResult({
    required this.trips,
    required this.hasGps,
    this.gpsMessage,
  });

  final List<AvailableTrip> trips;
  final bool hasGps;
  final String? gpsMessage;
}

/// Poll de disponibles: 30s (PLAN §3). Pasa GPS si hay permiso (KBX-33).
final availableTripsProvider =
    FutureProvider.autoDispose<AvailableTripsResult>((ref) async {
  _schedulePoll(ref, const Duration(seconds: 30));
  double? lat;
  double? lng;
  String? gpsMessage;
  var hasGps = false;
  try {
    final coords =
        await ref.read(sosLocationSourceProvider).getCurrentCoords();
    lat = coords.lat;
    lng = coords.lng;
    hasGps = true;
  } on SosLocationException catch (e) {
    gpsMessage = e.message;
  } catch (_) {
    gpsMessage =
        'Sin ubicación GPS: los puntos de espera sugeridos no estarán disponibles.';
  }

  final list = await ref.watch(passengerApiProvider).getAvailableTrips(
        lat: lat,
        lng: lng,
      );
  ref.read(tripGeometryCacheProvider.notifier).putAvailableList(list);
  return AvailableTripsResult(
    trips: list,
    hasGps: hasGps,
    gpsMessage: gpsMessage,
  );
});

final myTripsProvider =
    FutureProvider.autoDispose.family<MyTripsResponse, String>((ref, period) async {
  final data = await ref.watch(passengerApiProvider).getMyTrips(period: period);
  // Poll más agresivo si hay viaje próximo pending/accepted.
  if (TripLabels.hasActiveUpcoming(data.trips)) {
    _schedulePoll(ref, pollInterval);
  } else {
    _schedulePoll(ref, const Duration(seconds: 30));
  }
  return data;
});

final ecoSummaryProvider = FutureProvider.autoDispose<EcoSummary>((ref) async {
  return ref.watch(passengerApiProvider).getEco();
});

final profileProvider = FutureProvider.autoDispose<UserProfile>((ref) async {
  return ref.watch(passengerApiProvider).getProfile();
});

final emergencyContactsProvider =
    FutureProvider.autoDispose<List<EmergencyContact>>((ref) async {
  return ref.watch(passengerApiProvider).getEmergencyContacts();
});

final pendingRatingsProvider =
    FutureProvider.autoDispose<List<PendingRating>>((ref) async {
  _schedulePoll(ref, pollInterval);
  return ref.watch(passengerApiProvider).getPendingRatings();
});

/// Invalida providers de viajes tras mutaciones (request, rating).
void invalidatePassengerTrips(WidgetRef ref) {
  ref.invalidate(availableTripsProvider);
  ref.invalidate(myTripsProvider);
  ref.invalidate(pendingRatingsProvider);
  ref.invalidate(ecoSummaryProvider);
}

PassengerApi passengerApiOf(WidgetRef ref) => ref.read(passengerApiProvider);
