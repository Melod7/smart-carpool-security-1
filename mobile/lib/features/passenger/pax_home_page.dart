import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

import '../../api/models.dart';
import '../../auth/auth_state.dart';
import '../../theme/kubix_theme.dart';
import '../sos/sos_button.dart';
import '../sos/sos_overlay.dart';
import '../map/map_camera.dart';
import '../map/map_route.dart';
import '../map/open_trip_map.dart';
import '../map/trip_geometry_cache.dart';
import '../map/trip_map_page.dart' show mapsApiKey;
import 'passenger_providers.dart';
import 'suggested_wait_labels.dart';
import 'trip_labels.dart';
import 'widgets/eco_widget.dart';

class PaxHomePage extends ConsumerStatefulWidget {
  const PaxHomePage({super.key});

  @override
  ConsumerState<PaxHomePage> createState() => _PaxHomePageState();
}

class _PaxHomePageState extends ConsumerState<PaxHomePage> {
  final Set<String> _rejectAlertsShown = {};

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(authProvider).user;
    final mine = ref.watch(myTripsProvider('total'));
    final available = ref.watch(availableTripsProvider);
    final eco = ref.watch(ecoSummaryProvider);

    ref.listen(myTripsProvider('total'), (prev, next) {
      final trips = next.asData?.value.trips;
      if (trips == null) return;
      final rejected = trips.where(
        (t) => t.isUpcoming && t.requestStatus == 'rejected',
      );
      for (final t in rejected) {
        if (_rejectAlertsShown.contains(t.id)) continue;
        _rejectAlertsShown.add(t.id);
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (!mounted) return;
          _showRejectedDialog(t);
        });
      }
    });

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
              final trips =
                  ref.read(myTripsProvider('total')).asData?.value.trips;
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
              final rejected = data.trips
                  .where(
                    (t) => t.isUpcoming && t.requestStatus == 'rejected',
                  )
                  .toList();
              return Column(
                children: [
                  if (rejected.isNotEmpty) ...[
                    for (final t in rejected) ...[
                      _RejectedBanner(
                        trip: t,
                        onRetry: () => _retryRejected(t),
                      ),
                      const SizedBox(height: 8),
                    ],
                  ],
                  if (next == null)
                    const _InfoCard(
                      title: 'Sin viaje confirmado',
                      subtitle:
                          'Cuando un conductor acepte tu solicitud, aparecerá aquí.',
                      icon: Icons.event_busy_outlined,
                    )
                  else
                    _NextTripCard(
                      trip: next,
                      onOpenMap: () =>
                          openTripMapFromMyTrip(context, ref, next),
                    ),
                ],
              );
            },
            loading: () => const SizedBox(
              height: 88,
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (e, _) => Text(
              e.toString(),
              style: const TextStyle(color: KubixColors.emergency),
            ),
          ),
          const SizedBox(height: 16),
          eco.when(
            data: (summary) => EcoWidget(eco: summary),
            loading: () => const SizedBox.shrink(),
            error: (_, __) => const SizedBox.shrink(),
          ),
          const SizedBox(height: 16),
          Text(
            'Viajes disponibles',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w700,
                  color: KubixColors.utnBlue,
                ),
          ),
          const SizedBox(height: 8),
          available.when(
            data: (result) {
              return Column(
                children: [
                  if (!result.hasGps && result.gpsMessage != null)
                    _GpsHintCard(message: result.gpsMessage!),
                  if (result.trips.isEmpty)
                    const _InfoCard(
                      title: 'No hay viajes ahora',
                      subtitle:
                          'Cuando un conductor publique una ruta a tu campus, '
                          'aparecerá aquí.',
                      icon: Icons.directions_car_outlined,
                    )
                  else
                    for (final trip in result.trips) ...[
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
            error: (e, _) => Text(
              e.toString(),
              style: const TextStyle(color: KubixColors.emergency),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _showRejectedDialog(MyTrip trip) async {
    final retry = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Abordaje rechazado'),
        content: Text(
          'El conductor rechazó tu punto de abordaje para '
          '${trip.originText.isEmpty ? 'el viaje' : trip.originText}. '
          'Puedes elegir otro punto sobre la misma ruta.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Después'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Elegir otro punto'),
          ),
        ],
      ),
    );
    if (retry == true && mounted) {
      await _retryRejected(trip);
    }
  }

  Future<void> _retryRejected(MyTrip trip) async {
    final available =
        ref.read(availableTripsProvider).asData?.value.trips ?? const [];
    AvailableTrip? match;
    for (final t in available) {
      if (t.id == trip.id) {
        match = t;
        break;
      }
    }
    if (match == null) {
      ref.invalidate(availableTripsProvider);
      try {
        final result = await ref.read(availableTripsProvider.future);
        for (final t in result.trips) {
          if (t.id == trip.id) {
            match = t;
            break;
          }
        }
      } catch (_) {}
    }
    if (!mounted) return;
    if (match == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Ese viaje ya no está disponible. Busca otro en la lista.',
          ),
        ),
      );
      return;
    }
    _rejectAlertsShown.remove(trip.id);
    await _openRequestSheet(context, ref, match);
  }

  Future<void> _openRequestSheet(
    BuildContext context,
    WidgetRef ref,
    AvailableTrip trip,
  ) async {
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (ctx) => _RequestWaitSheet(trip: trip, parentContext: context),
    );
  }
}

class _RejectedBanner extends StatelessWidget {
  const _RejectedBanner({required this.trip, required this.onRetry});

  final MyTrip trip;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: KubixColors.emergency.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: KubixColors.emergency.withValues(alpha: 0.35)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Abordaje rechazado',
            style: TextStyle(
              fontWeight: FontWeight.w700,
              color: KubixColors.emergency,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            'El conductor rechazó tu punto. Puedes pedir otro sobre la ruta.',
            style: TextStyle(
              fontSize: 13,
              color: KubixColors.muted,
            ),
          ),
          const SizedBox(height: 10),
          Align(
            alignment: Alignment.centerRight,
            child: FilledButton.tonal(
              onPressed: onRetry,
              child: const Text('Elegir otro punto'),
            ),
          ),
        ],
      ),
    );
  }
}

class _RequestWaitSheet extends ConsumerStatefulWidget {
  const _RequestWaitSheet({
    required this.trip,
    required this.parentContext,
  });

  final AvailableTrip trip;
  final BuildContext parentContext;

  @override
  ConsumerState<_RequestWaitSheet> createState() => _RequestWaitSheetState();
}

class _RequestWaitSheetState extends ConsumerState<_RequestWaitSheet> {
  late final TextEditingController _pickupCtrl;
  late double _waitLat;
  late double _waitLng;
  late bool _tooFar;
  late double? _distanceM;
  var _submitting = false;
  var _snapping = false;
  String? _error;
  GoogleMapController? _mapController;
  bool _mapReady = false;

  AvailableTrip get trip => widget.trip;

  @override
  void initState() {
    super.initState();
    final wait = trip.suggestedWait;
    _pickupCtrl = TextEditingController(
      text: SuggestedWaitLabels.pickupTextDefault(wait),
    );
    _waitLat = wait?.lat ?? trip.originLat;
    _waitLng = wait?.lng ?? trip.originLng;
    _tooFar = wait?.tooFar ?? false;
    _distanceM = wait?.distanceM;
  }

  @override
  void dispose() {
    _pickupCtrl.dispose();
    _mapController?.dispose();
    super.dispose();
  }

  MapRoutePath get _route => buildMapRoute(
        polyline: trip.polyline,
        originLat: trip.originLat,
        originLng: trip.originLng,
        waypoints: trip.waypoints,
        pickupLat: _waitLat,
        pickupLng: _waitLng,
      );

  Future<void> _fitCamera() async {
    final points = <LatLng>[
      ..._route.points,
      LatLng(_waitLat, _waitLng),
      for (final w in trip.waypoints) LatLng(w.lat, w.lng),
    ];
    await fitMapToPoints(_mapController, points, padding: 48);
  }

  Future<void> _snapToRoute(double lat, double lng) async {
    setState(() {
      _snapping = true;
      _error = null;
    });
    try {
      final wait = await ref.read(passengerApiProvider).getSuggestedPickup(
            tripId: trip.id,
            lat: lat,
            lng: lng,
          );
      if (!mounted) return;
      setState(() {
        _waitLat = wait.lat;
        _waitLng = wait.lng;
        _tooFar = wait.tooFar;
        _distanceM = wait.distanceM;
        _snapping = false;
      });
      await _fitCamera();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _waitLat = lat;
        _waitLng = lng;
        _snapping = false;
        _error = e.toString();
      });
    }
  }

  Set<Marker> get _markers {
    final markers = <Marker>{
      Marker(
        markerId: const MarkerId('wait'),
        position: LatLng(_waitLat, _waitLng),
        draggable: !_submitting && !_snapping,
        infoWindow: const InfoWindow(title: 'Espera aquí'),
        icon: BitmapDescriptor.defaultMarkerWithHue(
          BitmapDescriptor.hueOrange,
        ),
        zIndexInt: 5,
        onDragEnd: (pos) => _snapToRoute(pos.latitude, pos.longitude),
      ),
    };
    for (var i = 0; i < trip.waypoints.length; i++) {
      markers.add(
        Marker(
          markerId: MarkerId('wp-$i'),
          position: LatLng(trip.waypoints[i].lat, trip.waypoints[i].lng),
          infoWindow: InfoWindow(
            title: '${i + 1}. ${trip.waypoints[i].label ?? 'Ruta'}',
          ),
          icon: BitmapDescriptor.defaultMarkerWithHue(
            i == 0 ? BitmapDescriptor.hueGreen : BitmapDescriptor.hueAzure,
          ),
          zIndexInt: 1,
        ),
      );
    }
    return markers;
  }

  Set<Polyline> get _polylines {
    final route = _route;
    if (!route.isRenderable) return {};
    return {
      Polyline(
        polylineId: const PolylineId('route'),
        points: route.points,
        color: KubixColors.utnBlue,
        width: 5,
        patterns: route.dashed
            ? [PatternItem.dash(16), PatternItem.gap(10)]
            : const [],
      ),
    };
  }

  Future<void> _submit() async {
    final text = _pickupCtrl.text.trim();
    if (text.isEmpty) {
      setState(() => _error = 'Indica una descripción del punto de espera.');
      return;
    }
    if (!_route.isRenderable) {
      setState(
        () => _error =
            'Este viaje no tiene geometría de ruta. Pide al conductor republicarlo.',
      );
      return;
    }
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      await ref.read(passengerApiProvider).requestRide(
            tripId: trip.id,
            pickupText: text,
            pickupLat: _waitLat,
            pickupLng: _waitLng,
          );
      ref.read(tripGeometryCacheProvider.notifier).putAvailable(trip);
      ref.read(tripGeometryCacheProvider.notifier).putPickup(
            tripId: trip.id,
            pickupLat: _waitLat,
            pickupLng: _waitLng,
            status: trip.status,
          );
      invalidatePassengerTrips(ref);
      if (mounted) Navigator.pop(context);
      if (widget.parentContext.mounted) {
        ScaffoldMessenger.of(widget.parentContext).showSnackBar(
          const SnackBar(
            content: Text(
              'Solicitud enviada. Espera la confirmación del conductor.',
            ),
          ),
        );
      }
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _error = e.toString();
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final bottom = MediaQuery.viewInsetsOf(context).bottom;
    final height = MediaQuery.sizeOf(context).height * 0.92;
    final waitSummary = _distanceM == null
        ? null
        : SuggestedWaitLabels.summary(
            SuggestedWait(
              lat: _waitLat,
              lng: _waitLng,
              distanceM: _distanceM!,
              segmentIndex: trip.suggestedWait?.segmentIndex ?? 0,
              tooFar: _tooFar,
            ),
          );
    final tooFarMsg = SuggestedWaitLabels.tooFarWarning(
      SuggestedWait(
        lat: _waitLat,
        lng: _waitLng,
        distanceM: _distanceM ?? 0,
        segmentIndex: 0,
        tooFar: _tooFar,
      ),
    );
    final hasRoute = _route.isRenderable;
    final routeHint = !hasRoute
        ? 'Sin ruta en este viaje (polyline/waypoints vacíos).'
        : (trip.polyline == null || trip.polyline!.isEmpty)
            ? 'Ruta aproximada por puntos del conductor.'
            : 'Ruta válida del conductor.';

    return SizedBox(
      height: height,
      child: Padding(
        padding: EdgeInsets.fromLTRB(16, 12, 16, 12 + bottom),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Center(
              child: Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: KubixColors.muted.withValues(alpha: 0.4),
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
            ),
            const SizedBox(height: 12),
            Text(
              'Confirmar punto de espera',
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
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
            if (waitSummary != null) ...[
              const SizedBox(height: 8),
              Text(
                waitSummary,
                style: const TextStyle(
                  fontWeight: FontWeight.w600,
                  color: KubixColors.utnBlue,
                ),
              ),
            ],
            Text(
              routeHint,
              style: TextStyle(
                fontSize: 12,
                color: hasRoute ? KubixColors.muted : KubixColors.emergency,
              ),
            ),
            if (tooFarMsg != null) ...[
              const SizedBox(height: 6),
              Text(
                tooFarMsg,
                style: const TextStyle(
                  fontSize: 13,
                  color: KubixColors.emergency,
                ),
              ),
            ],
            const SizedBox(height: 10),
            Expanded(
              flex: 3,
              child: mapsApiKey.isEmpty
                  ? Container(
                      alignment: Alignment.center,
                      color: KubixColors.background,
                      child: const Text(
                        'Falta GOOGLE_MAPS_API_KEY. Usa: make mobile',
                        textAlign: TextAlign.center,
                      ),
                    )
                  : DecoratedBox(
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(
                          color: KubixColors.utnBlue.withValues(alpha: 0.25),
                        ),
                      ),
                      child: ClipRRect(
                        borderRadius: BorderRadius.circular(12),
                        child: GoogleMap(
                          initialCameraPosition: CameraPosition(
                            target: LatLng(_waitLat, _waitLng),
                            zoom: 13.5,
                          ),
                          markers: _mapReady ? _markers : _markers,
                          polylines: _mapReady ? _polylines : _polylines,
                          myLocationEnabled: true,
                          myLocationButtonEnabled: true,
                          zoomControlsEnabled: false,
                          mapToolbarEnabled: false,
                          compassEnabled: true,
                          onMapCreated: (c) async {
                            _mapController = c;
                            setState(() => _mapReady = true);
                            await Future<void>.delayed(
                              const Duration(milliseconds: 250),
                            );
                            if (mounted) await _fitCamera();
                          },
                          onTap: (_submitting || _snapping)
                              ? null
                              : (pos) =>
                                  _snapToRoute(pos.latitude, pos.longitude),
                        ),
                      ),
                    ),
            ),
            const SizedBox(height: 8),
            Text(
              _snapping
                  ? 'Alineando punto a la ruta…'
                  : 'Pin naranja = abordaje. Azules = ruta del conductor. '
                      'Toca el mapa o arrastra el pin.',
              style: const TextStyle(fontSize: 12, color: KubixColors.muted),
            ),
            const SizedBox(height: 8),
            Flexible(
              flex: 2,
              child: SingleChildScrollView(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    TextField(
                      controller: _pickupCtrl,
                      enabled: !_submitting,
                      decoration: const InputDecoration(
                        labelText: 'Descripción del punto',
                        hintText: 'Ej. Esquina norte del parque',
                        isDense: true,
                      ),
                      textCapitalization: TextCapitalization.sentences,
                    ),
                    if (_error != null) ...[
                      const SizedBox(height: 8),
                      Text(
                        _error!,
                        style: const TextStyle(color: KubixColors.emergency),
                      ),
                    ],
                    const SizedBox(height: 12),
                    FilledButton(
                      onPressed:
                          (_submitting || _snapping || _tooFar) ? null : _submit,
                      child: _submitting
                          ? const SizedBox(
                              height: 22,
                              width: 22,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: Colors.white,
                              ),
                            )
                          : const Text('Confirmar y solicitar'),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _GpsHintCard extends StatelessWidget {
  const _GpsHintCard({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: KubixColors.gold.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Row(
        children: [
          const Icon(Icons.location_off, color: KubixColors.gold, size: 22),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              message,
              style: const TextStyle(fontSize: 13, color: KubixColors.muted),
            ),
          ),
        ],
      ),
    );
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
    final wait = trip.suggestedWait;
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
                    if (wait != null) ...[
                      const SizedBox(height: 4),
                      Text(
                        SuggestedWaitLabels.summary(wait),
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          color: wait.tooFar
                              ? KubixColors.emergency
                              : KubixColors.eco,
                        ),
                      ),
                    ],
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
