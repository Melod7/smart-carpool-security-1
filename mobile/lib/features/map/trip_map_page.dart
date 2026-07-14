import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

import '../../api/models.dart';
import '../../auth/auth_state.dart';
import '../../theme/kubix_theme.dart';
import '../passenger/trip_labels.dart';
import '../sos/sos_location.dart';
import 'map_route.dart';
import 'tracking_providers.dart';
import 'trip_geometry_cache.dart';

/// Clave Maps inyectada con `--dart-define=MAPS_API_KEY=...`.
/// También debe configurarse en AndroidManifest / AppDelegate (ver README).
const mapsApiKey = String.fromEnvironment('MAPS_API_KEY', defaultValue: '');

const _defaultCenter = LatLng(-0.1807, -78.4678);

/// Pantalla de mapa del viaje (KBX-26).
class TripMapPage extends ConsumerStatefulWidget {
  const TripMapPage({
    super.key,
    required this.tripId,
    this.seed,
  });

  final String tripId;
  final TripMapSeed? seed;

  @override
  ConsumerState<TripMapPage> createState() => _TripMapPageState();
}

class _TripMapPageState extends ConsumerState<TripMapPage> {
  GoogleMapController? _mapController;
  Timer? _pingTimer;
  String _status = 'scheduled';
  bool _pingsStopped = false;
  LatLng? _ownPosition;

  @override
  void initState() {
    super.initState();
    final seed = widget.seed;
    if (seed != null) {
      _status = seed.status;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        ref.read(tripGeometryCacheProvider.notifier).put(seed);
      });
    }
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _syncLoops();
    });
  }

  @override
  void dispose() {
    _stopPings();
    _mapController?.dispose();
    super.dispose();
  }

  void _syncLoops() {
    if (!mounted) return;
    if (shouldSendPings(_status)) {
      _startPings();
    } else {
      _stopPings();
    }
  }

  void _startPings() {
    if (_pingTimer != null || _pingsStopped) return;
    _pingTimer = Timer.periodic(pingInterval, (_) => _sendPing());
    unawaited(_sendPing());
  }

  void _stopPings() {
    _pingTimer?.cancel();
    _pingTimer = null;
  }

  /// Pings fallidos se descartan (no se encolan).
  Future<void> _sendPing() async {
    if (!shouldSendPings(_status) || _pingsStopped) return;
    try {
      final coords =
          await ref.read(sosLocationSourceProvider).getCurrentCoords();
      if (!mounted) return;
      setState(() {
        _ownPosition = LatLng(coords.lat, coords.lng);
      });
      await ref.read(trackingApiProvider).postPing(
            tripId: widget.tripId,
            lat: coords.lat,
            lng: coords.lng,
          );
    } on ApiException catch (e) {
      if (e.code == 'trip_not_active' || e.statusCode == 409) {
        _onTerminalStatus('completed');
        return;
      }
      // Otros errores (red, GPS, etc.): descartar.
    } catch (_) {
      // Descartar: pérdida de red, GPS denegado, etc.
    }
  }

  void _onTerminalStatus(String status) {
    if (_pingsStopped) return;
    _pingsStopped = true;
    _status = status;
    _stopPings();
    ref.read(tripGeometryCacheProvider.notifier).put(
          TripMapSeed(tripId: widget.tripId, status: status),
        );
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            status == 'completed'
                ? 'Viaje completado. Seguimiento detenido.'
                : 'Viaje cancelado. Seguimiento detenido.',
          ),
        ),
      );
      setState(() {});
    }
  }

  void _fitBounds(Iterable<LatLng> points) {
    final controller = _mapController;
    if (controller == null) return;
    final list = points.toList();
    if (list.isEmpty) return;
    if (list.length == 1) {
      controller.animateCamera(
        CameraUpdate.newLatLngZoom(list.first, 14),
      );
      return;
    }
    var minLat = list.first.latitude;
    var maxLat = list.first.latitude;
    var minLng = list.first.longitude;
    var maxLng = list.first.longitude;
    for (final p in list.skip(1)) {
      if (p.latitude < minLat) minLat = p.latitude;
      if (p.latitude > maxLat) maxLat = p.latitude;
      if (p.longitude < minLng) minLng = p.longitude;
      if (p.longitude > maxLng) maxLng = p.longitude;
    }
    controller.animateCamera(
      CameraUpdate.newLatLngBounds(
        LatLngBounds(
          southwest: LatLng(minLat, minLng),
          northeast: LatLng(maxLat, maxLng),
        ),
        56,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(authProvider).user;
    final cache = ref.watch(tripGeometryCacheProvider)[widget.tripId];
    final seed = cache ?? widget.seed;
    final status = seed?.status ?? _status;
    if (status != _status && !_pingsStopped) {
      _status = status;
      WidgetsBinding.instance.addPostFrameCallback((_) => _syncLoops());
    }

    ref.listen<AsyncValue<TrackingPollResult>>(
      tripTrackingProvider(widget.tripId),
      (prev, next) {
        final result = next.asData?.value;
        if (result == null) return;
        if (result.tripNoLongerActive && shouldSendPings(_status)) {
          _onTerminalStatus('completed');
        }
        final tracking = result.tracking;
        if (tracking != null && tracking.isTerminal) {
          _onTerminalStatus(tracking.status);
        }
      },
    );

    final trackingAsync = shouldPollTracking(status)
        ? ref.watch(tripTrackingProvider(widget.tripId))
        : const AsyncValue<TrackingPollResult>.data(TrackingPollResult());

    final tracking = trackingAsync.asData?.value.tracking;
    final participants = tracking?.participants ?? const <TrackingParticipant>[];
    final polyline = tracking?.polyline ?? seed?.polyline;

    final waypoints = seed?.waypoints ?? const <TripWaypoint>[];
    final route = buildMapRoute(
      polyline: polyline,
      originLat: seed?.originLat,
      originLng: seed?.originLng,
      pickupLat: seed?.pickupLat,
      pickupLng: seed?.pickupLng,
      waypoints: waypoints,
      participants: participants,
    );

    final markers = <Marker>{};
    final boundsPoints = <LatLng>[...route.points];

    if (shouldPollTracking(status)) {
      for (final p in participants) {
        final pos = LatLng(p.lat, p.lng);
        boundsPoints.add(pos);
        final isSelf = user != null && p.userId == user.id;
        markers.add(
          Marker(
            markerId: MarkerId('p-${p.userId}'),
            position: pos,
            infoWindow: InfoWindow(
              title: isSelf
                  ? 'Tú'
                  : (p.name?.trim().isNotEmpty == true
                      ? p.name!
                      : (p.isDriver ? 'Conductor' : 'Pasajero')),
              snippet: p.isPickup ? 'Punto de recogida' : null,
            ),
            icon: BitmapDescriptor.defaultMarkerWithHue(
              p.isDriver
                  ? BitmapDescriptor.hueAzure
                  : (isSelf
                      ? BitmapDescriptor.hueGreen
                      : BitmapDescriptor.hueOrange),
            ),
            zIndexInt: p.isDriver ? 2 : 1,
          ),
        );
      }
      if (_ownPosition != null &&
          (user == null ||
              !participants.any((p) => p.userId == user.id))) {
        boundsPoints.add(_ownPosition!);
        markers.add(
          Marker(
            markerId: const MarkerId('own'),
            position: _ownPosition!,
            infoWindow: const InfoWindow(title: 'Tú'),
            icon: BitmapDescriptor.defaultMarkerWithHue(
              BitmapDescriptor.hueGreen,
            ),
          ),
        );
      }
    } else {
      // scheduled: waypoints numerados + pickup / origen (sin pings en vivo).
      for (var i = 0; i < waypoints.length; i++) {
        final w = waypoints[i];
        final pos = LatLng(w.lat, w.lng);
        boundsPoints.add(pos);
        markers.add(
          Marker(
            markerId: MarkerId('wp-$i'),
            position: pos,
            infoWindow: InfoWindow(
              title: '${i + 1}. ${w.label ?? (i == 0 ? 'Inicio' : 'Punto')}',
            ),
            icon: BitmapDescriptor.defaultMarkerWithHue(
              i == 0 ? BitmapDescriptor.hueGreen : BitmapDescriptor.hueAzure,
            ),
          ),
        );
      }
      if (seed?.hasPickup == true) {
        final pickup = LatLng(seed!.pickupLat!, seed.pickupLng!);
        boundsPoints.add(pickup);
        markers.add(
          Marker(
            markerId: const MarkerId('pickup'),
            position: pickup,
            infoWindow: const InfoWindow(title: 'Punto de espera / recogida'),
            icon: BitmapDescriptor.defaultMarkerWithHue(
              BitmapDescriptor.hueOrange,
            ),
            zIndexInt: 3,
          ),
        );
      } else if (waypoints.isEmpty && seed?.hasOrigin == true) {
        final origin = LatLng(seed!.originLat!, seed.originLng!);
        boundsPoints.add(origin);
        markers.add(
          Marker(
            markerId: const MarkerId('origin'),
            position: origin,
            infoWindow: const InfoWindow(title: 'Origen / recogida'),
            icon: BitmapDescriptor.defaultMarkerWithHue(
              BitmapDescriptor.hueOrange,
            ),
          ),
        );
      }
    }

    final polylines = <Polyline>{};
    if (route.isRenderable) {
      polylines.add(
        Polyline(
          polylineId: const PolylineId('route'),
          points: route.points,
          color: KubixColors.utnBlue,
          width: 4,
          patterns: route.dashed
              ? <PatternItem>[
                  PatternItem.dash(18),
                  PatternItem.gap(12),
                ]
              : const <PatternItem>[],
        ),
      );
    }

    final initial = boundsPoints.isNotEmpty
        ? boundsPoints.first
        : _defaultCenter;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Mapa del viaje'),
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: 12),
            child: Center(
              child: Text(
                TripLabels.tripStatus(status),
                style: const TextStyle(
                  fontWeight: FontWeight.w600,
                  fontSize: 13,
                ),
              ),
            ),
          ),
        ],
      ),
      body: Column(
        children: [
          if (mapsApiKey.isEmpty)
            Container(
              width: double.infinity,
              color: KubixColors.emergency.withValues(alpha: 0.1),
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
              child: const Text(
                'Falta MAPS_API_KEY. Usa --dart-define=MAPS_API_KEY=... '
                'y configúrala en Android/iOS (ver mobile/README).',
                style: TextStyle(fontSize: 13, color: KubixColors.emergency),
              ),
            ),
          if (!shouldPollTracking(status))
            Container(
              width: double.infinity,
              color: KubixColors.utnBlue.withValues(alpha: 0.08),
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
              child: Text(
                _pingsStopped
                    ? 'Seguimiento detenido.'
                    : 'Viaje programado: se muestra la ruta y el punto de '
                        'recogida. Los pings empiezan al iniciar el viaje.',
                style: const TextStyle(
                  fontSize: 13,
                  color: KubixColors.muted,
                ),
              ),
            ),
          Expanded(
            child: trackingAsync.when(
              data: (_) => GoogleMap(
                initialCameraPosition: CameraPosition(
                  target: initial,
                  zoom: 13,
                ),
                markers: markers,
                polylines: polylines,
                myLocationEnabled: false,
                myLocationButtonEnabled: false,
                compassEnabled: true,
                mapToolbarEnabled: false,
                onMapCreated: (controller) {
                  _mapController = controller;
                  WidgetsBinding.instance.addPostFrameCallback((_) {
                    _fitBounds(boundsPoints);
                  });
                },
              ),
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (e, _) => Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        e.toString(),
                        textAlign: TextAlign.center,
                        style: const TextStyle(color: KubixColors.emergency),
                      ),
                      const SizedBox(height: 12),
                      FilledButton(
                        onPressed: () =>
                            ref.invalidate(tripTrackingProvider(widget.tripId)),
                        child: const Text('Reintentar'),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
