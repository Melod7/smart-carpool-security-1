import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/models.dart';
import '../../auth/auth_state.dart';
import '../../theme/kubix_theme.dart';
import '../passenger/trip_labels.dart';
import '../sos/sos_button.dart';
import '../sos/sos_overlay.dart';
import '../map/open_trip_map.dart';
import '../map/trip_geometry_cache.dart';
import 'driver_providers.dart';

class DrvHomePage extends ConsumerWidget {
  const DrvHomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(authProvider).user;
    final mine = ref.watch(driverMyTripsProvider('total'));
    final eco = ref.watch(driverEcoProvider);

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(driverMyTripsProvider('total'));
        ref.invalidate(driverEcoProvider);
        await Future.wait([
          ref.read(driverMyTripsProvider('total').future),
          ref.read(driverEcoProvider.future),
        ]);
      },
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(
            'Hola, ${user?.name.split(' ').first ?? 'conductor'}',
            style: const TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.w800,
              color: KubixColors.utnBlue,
            ),
          ),
          const SizedBox(height: 4),
          const Text(
            'Gestiona tu ruta y las solicitudes de pasajeros.',
            style: TextStyle(color: KubixColors.muted),
          ),
          const SizedBox(height: 16),
          SosButton(
            onPressed: () {
              final trips =
                  ref.read(driverMyTripsProvider('total')).asData?.value.trips;
              final active = trips == null
                  ? null
                  : TripLabels.activeTripForSos(trips, asDriver: true);
              showSosOverlay(context, tripId: active?.id);
            },
          ),
          const SizedBox(height: 16),
          mine.when(
            data: (data) {
              final active = TripLabels.activeDriverTrip(data.trips);
              if (active == null) {
                return const _InfoCard(
                  title: 'Sin ruta activa',
                  subtitle:
                      'Publica una ruta con el botón + para empezar a recibir solicitudes.',
                  icon: Icons.route_outlined,
                );
              }
              return _ActiveRouteCard(
                trip: active,
                onStart: () => _start(context, ref, active),
                onComplete: () => _complete(context, ref, active),
                onOpenMap: () => openTripMapFromMyTrip(context, ref, active),
              );
            },
            loading: () => const _LoadingCard(),
            error: (e, _) => _ErrorCard(message: e.toString()),
          ),
          const SizedBox(height: 16),
          eco.when(
            data: (data) {
              if (!data.gamificationEnabled) return const SizedBox.shrink();
              final today = TripLabels.ectTodayAmount(data.transactions);
              return _EctTodayCard(amount: today, balance: data.balance);
            },
            loading: () => const SizedBox.shrink(),
            error: (_, __) => const SizedBox.shrink(),
          ),
          const SizedBox(height: 20),
          mine.when(
            data: (data) {
              final active = TripLabels.activeDriverTrip(data.trips);
              if (active == null) return const SizedBox.shrink();
              return _RequestsSection(tripId: active.id);
            },
            loading: () => const SizedBox.shrink(),
            error: (_, __) => const SizedBox.shrink(),
          ),
          const SizedBox(height: 72),
        ],
      ),
    );
  }

  Future<void> _start(
    BuildContext context,
    WidgetRef ref,
    MyTrip trip,
  ) async {
    try {
      final started = await ref.read(driverApiProvider).startTrip(trip.id);
      ref.read(tripGeometryCacheProvider.notifier).putAvailable(started);
      invalidateDriverTrips(ref);
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Viaje iniciado.')),
        );
      }
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(e.toString())),
        );
      }
    }
  }

  Future<void> _complete(
    BuildContext context,
    WidgetRef ref,
    MyTrip trip,
  ) async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Completar viaje'),
        content: const Text(
          '¿Confirmas que el viaje terminó? Se acreditarán EcoTokens.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Completar'),
          ),
        ],
      ),
    );
    if (confirm != true) return;
    try {
      final completed = await ref.read(driverApiProvider).completeTrip(trip.id);
      ref.read(tripGeometryCacheProvider.notifier).putAvailable(completed);
      invalidateDriverTrips(ref);
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Viaje completado. ¡Buen trabajo!')),
        );
      }
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(e.toString())),
        );
      }
    }
  }
}

class _ActiveRouteCard extends StatelessWidget {
  const _ActiveRouteCard({
    required this.trip,
    required this.onStart,
    required this.onComplete,
    required this.onOpenMap,
  });

  final MyTrip trip;
  final VoidCallback onStart;
  final VoidCallback onComplete;
  final VoidCallback onOpenMap;

  @override
  Widget build(BuildContext context) {
    final inProgress = trip.status == 'in_progress';
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: KubixColors.utnBlue,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            inProgress ? 'Ruta en curso' : 'Ruta programada',
            style: const TextStyle(
              color: Colors.white70,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            trip.originText,
            style: const TextStyle(
              color: Colors.white,
              fontSize: 18,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            '→ ${trip.destinationCampusName}',
            style: const TextStyle(color: Colors.white),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              const Icon(Icons.schedule, color: Colors.white70, size: 18),
              const SizedBox(width: 6),
              Text(
                TripLabels.formatDateTime(trip.departureAt),
                style: const TextStyle(color: Colors.white),
              ),
              const Spacer(),
              Text(
                TripLabels.tripStatus(trip.status),
                style: const TextStyle(
                  color: Colors.white,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),
          OutlinedButton.icon(
            onPressed: onOpenMap,
            style: OutlinedButton.styleFrom(
              foregroundColor: Colors.white,
              side: const BorderSide(color: Colors.white70),
            ),
            icon: const Icon(Icons.map_outlined, size: 18),
            label: const Text('Ver mapa'),
          ),
          const SizedBox(height: 8),
          if (trip.status == 'scheduled')
            FilledButton(
              onPressed: onStart,
              style: FilledButton.styleFrom(
                backgroundColor: Colors.white,
                foregroundColor: KubixColors.utnBlue,
              ),
              child: const Text('Iniciar viaje'),
            )
          else if (inProgress)
            FilledButton(
              onPressed: onComplete,
              style: FilledButton.styleFrom(
                backgroundColor: KubixColors.eco,
                foregroundColor: Colors.white,
              ),
              child: const Text('Completar viaje'),
            ),
        ],
      ),
    );
  }
}

class _EctTodayCard extends StatelessWidget {
  const _EctTodayCard({required this.amount, required this.balance});

  final int amount;
  final int balance;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: KubixColors.gold.withValues(alpha: 0.35)),
      ),
      child: Row(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: KubixColors.gold.withValues(alpha: 0.2),
              borderRadius: BorderRadius.circular(10),
            ),
            child: const Icon(Icons.stars, color: KubixColors.gold),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'ECT hoy',
                  style: TextStyle(
                    fontWeight: FontWeight.w700,
                    color: KubixColors.utnBlue,
                  ),
                ),
                Text(
                  amount >= 0 ? '+$amount ECT' : '$amount ECT',
                  style: const TextStyle(
                    fontSize: 20,
                    fontWeight: FontWeight.w800,
                    color: KubixColors.eco,
                  ),
                ),
                Text(
                  'Saldo: $balance ECT',
                  style: const TextStyle(
                    fontSize: 12,
                    color: KubixColors.muted,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _RequestsSection extends ConsumerWidget {
  const _RequestsSection({required this.tripId});

  final String tripId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final requests = ref.watch(tripRequestsProvider(tripId));

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Solicitudes',
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.w700,
                color: KubixColors.utnBlue,
              ),
        ),
        const SizedBox(height: 8),
        requests.when(
          data: (list) {
            final pending =
                list.where((r) => r.status == 'pending').toList();
            final accepted =
                list.where((r) => r.status == 'accepted').toList();
            if (pending.isEmpty && accepted.isEmpty) {
              return const _InfoCard(
                title: 'Sin solicitudes',
                subtitle:
                    'Cuando un pasajero pida asiento, aparecerá aquí (actualización cada 15 s).',
                icon: Icons.person_search_outlined,
              );
            }
            return Column(
              children: [
                for (final r in pending) ...[
                  _RequestCard(
                    request: r,
                    onAccept: () => _respond(context, ref, r, accept: true),
                    onReject: () => _respond(context, ref, r, accept: false),
                  ),
                  const SizedBox(height: 8),
                ],
                for (final r in accepted) ...[
                  _RequestCard(request: r),
                  const SizedBox(height: 8),
                ],
              ],
            );
          },
          loading: () => const Padding(
            padding: EdgeInsets.all(24),
            child: Center(child: CircularProgressIndicator()),
          ),
          error: (e, _) => _ErrorCard(message: e.toString()),
        ),
      ],
    );
  }

  Future<void> _respond(
    BuildContext context,
    WidgetRef ref,
    RideRequest request, {
    required bool accept,
  }) async {
    try {
      final api = ref.read(driverApiProvider);
      if (accept) {
        await api.acceptRequest(request.id);
        ref.read(tripGeometryCacheProvider.notifier).putPickup(
              tripId: tripId,
              pickupLat: request.pickupLat,
              pickupLng: request.pickupLng,
            );
      } else {
        await api.rejectRequest(request.id);
      }
      invalidateDriverTrips(ref);
      ref.invalidate(tripRequestsProvider(tripId));
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              accept ? 'Solicitud aceptada.' : 'Solicitud rechazada.',
            ),
          ),
        );
      }
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(e.toString())),
        );
      }
    }
  }
}

class _RequestCard extends StatelessWidget {
  const _RequestCard({
    required this.request,
    this.onAccept,
    this.onReject,
  });

  final RideRequest request;
  final VoidCallback? onAccept;
  final VoidCallback? onReject;

  @override
  Widget build(BuildContext context) {
    final pending = request.status == 'pending';
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(
                pending ? Icons.hourglass_top : Icons.check_circle_outline,
                color: pending ? KubixColors.gold : KubixColors.eco,
                size: 22,
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  request.pickupText,
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
              ),
              Text(
                TripLabels.requestStatus(request.status),
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  color: pending ? KubixColors.gold : KubixColors.eco,
                ),
              ),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            'Recogida · ${request.pickupLat.toStringAsFixed(4)}, '
            '${request.pickupLng.toStringAsFixed(4)}',
            style: const TextStyle(fontSize: 12, color: KubixColors.muted),
          ),
          if (pending && onAccept != null && onReject != null) ...[
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: onReject,
                    style: OutlinedButton.styleFrom(
                      foregroundColor: KubixColors.emergency,
                      side: const BorderSide(color: KubixColors.emergency),
                    ),
                    child: const Text('Rechazar'),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: FilledButton(
                    onPressed: onAccept,
                    child: const Text('Aceptar'),
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

class _InfoCard extends StatelessWidget {
  const _InfoCard({
    required this.title,
    required this.subtitle,
    required this.icon,
  });

  final String title;
  final String subtitle;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          Icon(icon, color: KubixColors.muted, size: 32),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: const TextStyle(fontWeight: FontWeight.w700)),
                const SizedBox(height: 2),
                Text(
                  subtitle,
                  style: const TextStyle(
                    color: KubixColors.muted,
                    fontSize: 13,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _LoadingCard extends StatelessWidget {
  const _LoadingCard();

  @override
  Widget build(BuildContext context) {
    return const SizedBox(
      height: 88,
      child: Center(child: CircularProgressIndicator()),
    );
  }
}

class _ErrorCard extends StatelessWidget {
  const _ErrorCard({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: KubixColors.emergency.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Text(message, style: const TextStyle(color: KubixColors.emergency)),
    );
  }
}
