import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/models.dart';
import '../../auth/auth_state.dart';
import '../../theme/kubix_theme.dart';
import '../sos/sos_button.dart';
import '../sos/sos_overlay.dart';
import '../map/open_trip_map.dart';
import '../map/trip_geometry_cache.dart';
import 'passenger_providers.dart';
import 'trip_labels.dart';
import 'widgets/eco_widget.dart';

class PaxHomePage extends ConsumerWidget {
  const PaxHomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(authProvider).user;
    final mine = ref.watch(myTripsProvider('total'));
    final available = ref.watch(availableTripsProvider);
    final eco = ref.watch(ecoSummaryProvider);

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(myTripsProvider('total'));
        ref.invalidate(availableTripsProvider);
        ref.invalidate(ecoSummaryProvider);
        await Future.wait([
          ref.read(myTripsProvider('total').future),
          ref.read(availableTripsProvider.future),
          ref.read(ecoSummaryProvider.future),
        ]);
      },
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(
            'Hola, ${user?.name.split(' ').first ?? 'pasajero'}',
            style: const TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.w800,
              color: KubixColors.utnBlue,
            ),
          ),
          const SizedBox(height: 4),
          const Text(
            'Encuentra tu próximo viaje al campus.',
            style: TextStyle(color: KubixColors.muted),
          ),
          const SizedBox(height: 16),
          SosButton(
            onPressed: () {
              final trips = ref.read(myTripsProvider('total')).asData?.value.trips;
              final active = trips == null
                  ? null
                  : TripLabels.activeTripForSos(trips, asDriver: false);
              showSosOverlay(context, tripId: active?.id);
            },
          ),
          const SizedBox(height: 16),
          mine.when(
            data: (data) {
              final next = TripLabels.nextAcceptedTrip(data.trips);
              if (next == null) {
                return const _InfoCard(
                  title: 'Sin viaje confirmado',
                  subtitle:
                      'Cuando un conductor acepte tu solicitud, aparecerá aquí.',
                  icon: Icons.event_busy_outlined,
                );
              }
              return _NextTripCard(
                trip: next,
                onOpenMap: () => openTripMapFromMyTrip(context, ref, next),
              );
            },
            loading: () => const _LoadingCard(),
            error: (e, _) => _ErrorCard(message: e.toString()),
          ),
          const SizedBox(height: 16),
          eco.when(
            data: (data) => EcoWidget(eco: data),
            loading: () => const SizedBox.shrink(),
            error: (_, __) => const SizedBox.shrink(),
          ),
          const SizedBox(height: 20),
          Row(
            children: [
              Text(
                'Viajes disponibles',
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.w700,
                      color: KubixColors.utnBlue,
                    ),
              ),
              const Spacer(),
              available.maybeWhen(
                data: (list) => Text(
                  '${list.length}',
                  style: const TextStyle(color: KubixColors.muted),
                ),
                orElse: () => const SizedBox.shrink(),
              ),
            ],
          ),
          const SizedBox(height: 8),
          available.when(
            data: (list) {
              if (list.isEmpty) {
                return const _InfoCard(
                  title: 'No hay viajes ahora',
                  subtitle:
                      'Vuelve más tarde o tira hacia abajo para actualizar.',
                  icon: Icons.directions_car_outlined,
                );
              }
              return Column(
                children: [
                  for (final trip in list) ...[
                    _AvailableTripTile(
                      trip: trip,
                      onRequest: () => _openRequestSheet(context, ref, trip),
                      onMap: () => openTripMap(
                        context,
                        ref,
                        tripId: trip.id,
                        status: trip.status,
                        available: trip,
                      ),
                    ),
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
      ),
    );
  }

  Future<void> _openRequestSheet(
    BuildContext context,
    WidgetRef ref,
    AvailableTrip trip,
  ) async {
    final pickupCtrl = TextEditingController(text: trip.originText);
    final latCtrl = TextEditingController(text: trip.originLat.toString());
    final lngCtrl = TextEditingController(text: trip.originLng.toString());
    var submitting = false;
    String? error;

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (ctx) {
        return StatefulBuilder(
          builder: (ctx, setState) {
            final bottom = MediaQuery.of(ctx).viewInsets.bottom;
            return Padding(
              padding: EdgeInsets.fromLTRB(20, 20, 20, 20 + bottom),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'Solicitar viaje',
                    style: Theme.of(ctx).textTheme.titleLarge?.copyWith(
                          fontWeight: FontWeight.w700,
                          color: KubixColors.utnBlue,
                        ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'Sale ${TripLabels.formatDateTime(trip.departureAt)} · '
                    '${trip.seatsAvailable} asientos',
                    style: const TextStyle(color: KubixColors.muted),
                  ),
                  const SizedBox(height: 16),
                  TextField(
                    controller: pickupCtrl,
                    enabled: !submitting,
                    decoration: const InputDecoration(
                      labelText: 'Punto de recogida',
                      hintText: 'Ej. Av. Universitaria y Gaspar de Villarroel',
                    ),
                    textCapitalization: TextCapitalization.sentences,
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: TextField(
                          controller: latCtrl,
                          enabled: !submitting,
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                            signed: true,
                          ),
                          decoration: const InputDecoration(
                            labelText: 'Lat (opcional)',
                          ),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: TextField(
                          controller: lngCtrl,
                          enabled: !submitting,
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                            signed: true,
                          ),
                          decoration: const InputDecoration(
                            labelText: 'Lng (opcional)',
                          ),
                        ),
                      ),
                    ],
                  ),
                  if (error != null) ...[
                    const SizedBox(height: 8),
                    Text(
                      error!,
                      style: const TextStyle(color: KubixColors.emergency),
                    ),
                  ],
                  const SizedBox(height: 16),
                  FilledButton(
                    onPressed: submitting
                        ? null
                        : () async {
                            final text = pickupCtrl.text.trim();
                            if (text.isEmpty) {
                              setState(
                                () => error = 'Indica el punto de recogida.',
                              );
                              return;
                            }
                            final lat = double.tryParse(latCtrl.text.trim()) ??
                                trip.originLat;
                            final lng = double.tryParse(lngCtrl.text.trim()) ??
                                trip.originLng;
                            setState(() {
                              submitting = true;
                              error = null;
                            });
                            try {
                              await ref.read(passengerApiProvider).requestRide(
                                    tripId: trip.id,
                                    pickupText: text,
                                    pickupLat: lat,
                                    pickupLng: lng,
                                  );
                              ref
                                  .read(tripGeometryCacheProvider.notifier)
                                  .putAvailable(trip);
                              ref
                                  .read(tripGeometryCacheProvider.notifier)
                                  .putPickup(
                                    tripId: trip.id,
                                    pickupLat: lat,
                                    pickupLng: lng,
                                    status: trip.status,
                                  );
                              invalidatePassengerTrips(ref);
                              if (ctx.mounted) Navigator.pop(ctx);
                              if (context.mounted) {
                                ScaffoldMessenger.of(context).showSnackBar(
                                  const SnackBar(
                                    content: Text(
                                      'Solicitud enviada. Espera la confirmación del conductor.',
                                    ),
                                  ),
                                );
                              }
                            } catch (e) {
                              setState(() {
                                submitting = false;
                                error = e.toString();
                              });
                            }
                          },
                    child: submitting
                        ? const SizedBox(
                            height: 22,
                            width: 22,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                        : const Text('Enviar solicitud'),
                  ),
                ],
              ),
            );
          },
        );
      },
    );

    pickupCtrl.dispose();
    latCtrl.dispose();
    lngCtrl.dispose();
  }
}

class _NextTripCard extends StatelessWidget {
  const _NextTripCard({required this.trip, required this.onOpenMap});

  final MyTrip trip;
  final VoidCallback onOpenMap;

  @override
  Widget build(BuildContext context) {
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
          const Text(
            'Próximo viaje',
            style: TextStyle(
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
        ],
      ),
    );
  }
}

class _AvailableTripTile extends StatelessWidget {
  const _AvailableTripTile({
    required this.trip,
    required this.onRequest,
    required this.onMap,
  });

  final AvailableTrip trip;
  final VoidCallback onRequest;
  final VoidCallback onMap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(12),
      child: InkWell(
        onTap: onRequest,
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(
            children: [
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  color: KubixColors.utnBlue.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: const Icon(
                  Icons.directions_car,
                  color: KubixColors.utnBlue,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      trip.originText,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      '${TripLabels.formatDateTime(trip.departureAt)} · '
                      '${trip.seatsAvailable} asientos · '
                      '${TripLabels.formatKm(trip.distanceKm)}',
                      style: const TextStyle(
                        fontSize: 12,
                        color: KubixColors.muted,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 4),
              IconButton(
                onPressed: onMap,
                tooltip: 'Ver mapa',
                icon: const Icon(Icons.map_outlined, color: KubixColors.utnBlue),
              ),
              FilledButton(
                onPressed: onRequest,
                style: FilledButton.styleFrom(
                  minimumSize: const Size(88, 40),
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                ),
                child: const Text('Pedir'),
              ),
            ],
          ),
        ),
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
