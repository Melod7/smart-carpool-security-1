import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/models.dart';
import '../../api/passenger_api.dart';
import '../../auth/auth_state.dart';
import '../map/trip_geometry_cache.dart';
import 'trip_labels.dart';

const pollInterval = Duration(seconds: 15);

/// Refresco periódico: invalida el provider cada [interval].
void _schedulePoll(Ref ref, Duration interval) {
  final timer = Timer.periodic(interval, (_) {
    ref.invalidateSelf();
  });
  ref.onDispose(timer.cancel);
}

/// Poll de disponibles: 30s (PLAN §3).
final availableTripsProvider =
    FutureProvider.autoDispose<List<AvailableTrip>>((ref) async {
  _schedulePoll(ref, const Duration(seconds: 30));
  final list = await ref.watch(passengerApiProvider).getAvailableTrips();
  ref.read(tripGeometryCacheProvider.notifier).putAvailableList(list);
  return list;
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
