import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

import '../../api/models.dart';
import '../../auth/auth_state.dart';
import '../../theme/kubix_theme.dart';
import '../map/decode_polyline.dart';
import '../map/map_camera.dart';
import '../map/trip_geometry_cache.dart';
import '../map/trip_map_page.dart' show mapsApiKey;
import '../passenger/trip_labels.dart';
import '../sos/sos_location.dart';
import 'driver_providers.dart';
import 'publish_route_validation.dart';

const _defaultCenter = LatLng(-0.1807, -78.4678);

/// Pantalla fullscreen para publicar ruta con waypoints (KBX-33).
class PublishRouteMapPage extends ConsumerStatefulWidget {
  const PublishRouteMapPage({super.key});

  @override
  ConsumerState<PublishRouteMapPage> createState() =>
      _PublishRouteMapPageState();
}

class _PublishRouteMapPageState extends ConsumerState<PublishRouteMapPage> {
  GoogleMapController? _mapController;
  LatLng _center = _defaultCenter;
  final List<TripWaypoint> _waypoints = [];
  String? _campusId;
  DateTime _departureAt = DateTime.now().add(const Duration(hours: 1));
  final _seatsCtrl = TextEditingController(text: '3');
  final _seatsFocus = FocusNode();
  Vehicle? _vehicle;
  List<CampusPublic> _campuses = const [];
  bool _loadingGps = true;
  String? _gpsError;
  bool _submitting = false;
  String? _error;
  bool _panelExpanded = true;
  String? _previewPolyline;
  bool _previewLoading = false;
  Timer? _previewDebounce;

  @override
  void initState() {
    super.initState();
    _departureAt = DateTime(
      _departureAt.year,
      _departureAt.month,
      _departureAt.day,
      _departureAt.hour,
      _departureAt.minute,
    );
    _seatsFocus.addListener(() {
      if (_seatsFocus.hasFocus && _panelExpanded) {
        setState(() => _panelExpanded = false);
      }
    });
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _bootstrap();
    });
  }

  @override
  void dispose() {
    _previewDebounce?.cancel();
    _seatsCtrl.dispose();
    _seatsFocus.dispose();
    _mapController?.dispose();
    super.dispose();
  }

  Future<void> _bootstrap() async {
    final user = ref.read(authProvider).user;
    try {
      _vehicle = await ref.read(driverVehicleProvider.future);
      if (_vehicle != null) {
        _seatsCtrl.text =
            '${(_vehicle!.seatsTotal - 1).clamp(1, 8)}';
      }
    } catch (_) {
      _vehicle = null;
    }
    try {
      _campuses = await ref.read(campusesForDriverProvider.future);
    } catch (_) {
      _campuses = const [];
    }
    var campusId = user?.campusId;
    if (campusId == null || !_campuses.any((c) => c.id == campusId)) {
      campusId = _campuses.isEmpty ? null : _campuses.first.id;
    }
    if (mounted) {
      setState(() => _campusId = campusId);
    }
    await _recenterOnGps();
  }

  Future<void> _recenterOnGps() async {
    setState(() {
      _loadingGps = true;
      _gpsError = null;
    });
    try {
      final coords =
          await ref.read(sosLocationSourceProvider).getCurrentCoords();
      final target = LatLng(coords.lat, coords.lng);
      if (!mounted) return;
      setState(() {
        _center = target;
        _loadingGps = false;
      });
      await _mapController?.animateCamera(
        CameraUpdate.newLatLngZoom(target, 15),
      );
    } on SosLocationException catch (e) {
      if (!mounted) return;
      setState(() {
        _loadingGps = false;
        _gpsError = e.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loadingGps = false;
        _gpsError =
            'No se pudo obtener tu ubicación. Activa el GPS para centrar el mapa.';
      });
    }
  }

  void _schedulePreview() {
    _previewDebounce?.cancel();
    if (_waypoints.length < 2 || _campusId == null) {
      setState(() {
        _previewPolyline = null;
        _previewLoading = false;
      });
      return;
    }
    setState(() => _previewLoading = true);
    _previewDebounce = Timer(const Duration(milliseconds: 550), () {
      unawaited(_fetchPreview());
    });
  }

  Future<void> _fetchPreview() async {
    final campusId = _campusId;
    if (campusId == null || _waypoints.length < 2) return;
    final snapshot = List<TripWaypoint>.of(_waypoints);
    try {
      final preview = await ref.read(driverApiProvider).previewRoute(
            destinationCampusId: campusId,
            waypoints: snapshot,
          );
      if (!mounted) return;
      if (snapshot.length != _waypoints.length) return;
      setState(() {
        _previewPolyline = preview.polyline;
        _previewLoading = false;
        if (!preview.directionsOk || preview.polyline == null) {
          _error =
              'Directions no devolvió ruta. Se muestra trazo aproximado entre puntos. '
              'Revisa GoogleMaps__ApiKey / Directions API.';
        } else {
          // Limpia aviso previo de preview si ya hay ruta válida.
          if (_error != null && _error!.contains('Directions')) {
            _error = null;
          }
        }
      });
      await _fitPreviewCamera();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _previewPolyline = null;
        _previewLoading = false;
        _error = 'No se pudo calcular la ruta: $e';
      });
    }
  }

  Future<void> _fitPreviewCamera() async {
    final points = <LatLng>[
      for (final w in _waypoints) LatLng(w.lat, w.lng),
    ];
    final encoded = _previewPolyline;
    if (encoded != null && encoded.isNotEmpty) {
      points
        ..clear()
        ..addAll(decodePolyline(encoded));
    }
    await fitMapToPoints(_mapController, points, padding: 64);
  }

  void _addWaypoint(LatLng pos) {
    if (!PublishRouteValidation.canAddWaypoint(_waypoints.length)) {
      setState(
        () => _error =
            'Máximo ${PublishRouteValidation.maxWaypoints} waypoints.',
      );
      return;
    }
    setState(() {
      _error = null;
      final n = _waypoints.length + 1;
      _waypoints.add(
        TripWaypoint(
          lat: pos.latitude,
          lng: pos.longitude,
          label: n == 1 ? 'Inicio' : 'Punto $n',
          seq: _waypoints.length,
        ),
      );
    });
    _schedulePreview();
  }

  void _removeWaypoint(int index) {
    setState(() {
      _waypoints.removeAt(index);
      for (var i = 0; i < _waypoints.length; i++) {
        _waypoints[i] = _waypoints[i].copyWith(
          seq: i,
          label: i == 0
              ? (_waypoints[i].label == 'Inicio' ||
                      (_waypoints[i].label?.startsWith('Punto') ?? false)
                  ? 'Inicio'
                  : _waypoints[i].label)
              : (_waypoints[i].label?.startsWith('Punto') == true ||
                      _waypoints[i].label == 'Inicio'
                  ? 'Punto ${i + 1}'
                  : _waypoints[i].label),
        );
      }
    });
    _schedulePreview();
  }

  Set<Marker> get _markers {
    return {
      for (var i = 0; i < _waypoints.length; i++)
        Marker(
          markerId: MarkerId('wp-$i'),
          position: LatLng(_waypoints[i].lat, _waypoints[i].lng),
          infoWindow: InfoWindow(
            title: '${i + 1}. ${_waypoints[i].label ?? 'Punto'}',
          ),
          icon: BitmapDescriptor.defaultMarkerWithHue(
            i == 0 ? BitmapDescriptor.hueGreen : BitmapDescriptor.hueAzure,
          ),
          onTap: () => _confirmDeleteWaypoint(i),
        ),
    };
  }

  Set<Polyline> get _polylines {
    final encoded = _previewPolyline;
    if (encoded != null && encoded.isNotEmpty) {
      final decoded = decodePolyline(encoded);
      if (decoded.length >= 2) {
        return {
          Polyline(
            polylineId: const PolylineId('valid'),
            points: decoded,
            color: KubixColors.utnBlue,
            width: 5,
          ),
        };
      }
    }
    if (_waypoints.length < 2) return {};
    return {
      Polyline(
        polylineId: const PolylineId('draft'),
        points: [
          for (final w in _waypoints) LatLng(w.lat, w.lng),
        ],
        color: KubixColors.utnBlue.withValues(alpha: 0.55),
        width: 4,
        patterns: [
          PatternItem.dash(16),
          PatternItem.gap(10),
        ],
      ),
    };
  }

  Future<void> _confirmDeleteWaypoint(int index) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Eliminar punto'),
        content: Text(
          '¿Quitar el punto ${index + 1}'
          '${_waypoints[index].label != null ? ' (${_waypoints[index].label})' : ''}?',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Eliminar'),
          ),
        ],
      ),
    );
    if (ok == true) _removeWaypoint(index);
  }

  Future<void> _pickDeparture() async {
    final date = await showDatePicker(
      context: context,
      initialDate: _departureAt,
      firstDate: DateTime.now(),
      lastDate: DateTime.now().add(const Duration(days: 14)),
    );
    if (date == null || !mounted) return;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(_departureAt),
    );
    if (time == null || !mounted) return;
    setState(() {
      _departureAt = DateTime(
        date.year,
        date.month,
        date.day,
        time.hour,
        time.minute,
      );
    });
  }

  Future<void> _publish() async {
    final seats = int.tryParse(_seatsCtrl.text.trim());
    final wpError = PublishRouteValidation.validateWaypoints(_waypoints);
    final metaError = PublishRouteValidation.validateMeta(
      campusId: _campusId,
      departureAt: _departureAt,
      seats: seats,
      maxSeats: _vehicle?.seatsTotal,
    );
    final validation = wpError ?? metaError;
    if (validation != null) {
      setState(() {
        _error = validation;
        _panelExpanded = true;
      });
      return;
    }

    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      final published = await ref.read(driverApiProvider).publishTrip(
            waypoints: List.of(_waypoints),
            originText:
                PublishRouteValidation.originTextFromWaypoints(_waypoints),
            destinationCampusId: _campusId!,
            departureAt: _departureAt,
            seatsAvailable: seats!,
          );
      ref.read(tripGeometryCacheProvider.notifier).putAvailable(published);
      invalidateDriverTrips(ref);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Ruta publicada. Los pasajeros ya pueden solicitarla.',
          ),
        ),
      );
      context.pop(true);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _error = e.toString();
        _panelExpanded = true;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final keyboard = MediaQuery.viewInsetsOf(context).bottom;
    final panelBottom = (_panelExpanded ? 220.0 : 96.0) +
        keyboard.clamp(0, 120) +
        MediaQuery.paddingOf(context).bottom;

    return Scaffold(
      resizeToAvoidBottomInset: true,
      appBar: AppBar(
        title: const Text('Publicar ruta'),
        actions: [
          if (_previewLoading)
            const Padding(
              padding: EdgeInsets.only(right: 8),
              child: Center(
                child: SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: Colors.white,
                  ),
                ),
              ),
            ),
          IconButton(
            tooltip: 'Centrar en GPS',
            onPressed: _loadingGps ? null : _recenterOnGps,
            icon: _loadingGps
                ? const SizedBox(
                    width: 22,
                    height: 22,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: Colors.white,
                    ),
                  )
                : const Icon(Icons.my_location),
          ),
        ],
      ),
      body: Stack(
        children: [
          GoogleMap(
            initialCameraPosition: CameraPosition(target: _center, zoom: 14),
            markers: _markers,
            polylines: _polylines,
            myLocationEnabled: true,
            myLocationButtonEnabled: false,
            compassEnabled: true,
            mapToolbarEnabled: false,
            onMapCreated: (c) {
              _mapController = c;
              unawaited(_fitPreviewCamera());
            },
            onTap: _submitting ? null : _addWaypoint,
            onLongPress: _submitting ? null : _addWaypoint,
          ),
          if (mapsApiKey.isEmpty)
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              child: Material(
                color: KubixColors.emergency.withValues(alpha: 0.12),
                child: const Padding(
                  padding: EdgeInsets.all(10),
                  child: Text(
                    'Falta GOOGLE_MAPS_API_KEY. Corre: make sync-env',
                    style: TextStyle(
                      fontSize: 12,
                      color: KubixColors.emergency,
                    ),
                  ),
                ),
              ),
            ),
          if (_gpsError != null)
            Positioned(
              top: mapsApiKey.isEmpty ? 44 : 8,
              left: 12,
              right: 12,
              child: Material(
                elevation: 2,
                borderRadius: BorderRadius.circular(10),
                color: Colors.white,
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Text(
                    _gpsError!,
                    style: const TextStyle(
                      fontSize: 13,
                      color: KubixColors.emergency,
                    ),
                  ),
                ),
              ),
            ),
          Positioned(
            right: 16,
            bottom: panelBottom,
            child: FloatingActionButton.small(
              heroTag: 'recenter',
              tooltip: 'Recentrar',
              onPressed: _loadingGps ? null : _recenterOnGps,
              child: const Icon(Icons.gps_fixed),
            ),
          ),
          Align(
            alignment: Alignment.bottomCenter,
            child: _PublishPanel(
              expanded: _panelExpanded,
              onToggle: () =>
                  setState(() => _panelExpanded = !_panelExpanded),
              waypoints: _waypoints,
              onRemoveWaypoint: _removeWaypoint,
              campuses: _campuses,
              campusId: _campusId,
              onCampusChanged: (v) {
                setState(() => _campusId = v);
                _schedulePreview();
              },
              departureAt: _departureAt,
              onPickDeparture: _pickDeparture,
              seatsCtrl: _seatsCtrl,
              seatsFocus: _seatsFocus,
              maxSeats: _vehicle?.seatsTotal,
              submitting: _submitting,
              error: _error,
              onPublish: _publish,
              hasValidRoute:
                  _previewPolyline != null && _previewPolyline!.isNotEmpty,
            ),
          ),
        ],
      ),
    );
  }
}

class _PublishPanel extends StatelessWidget {
  const _PublishPanel({
    required this.expanded,
    required this.onToggle,
    required this.waypoints,
    required this.onRemoveWaypoint,
    required this.campuses,
    required this.campusId,
    required this.onCampusChanged,
    required this.departureAt,
    required this.onPickDeparture,
    required this.seatsCtrl,
    required this.seatsFocus,
    required this.maxSeats,
    required this.submitting,
    required this.error,
    required this.onPublish,
    required this.hasValidRoute,
  });

  final bool expanded;
  final VoidCallback onToggle;
  final List<TripWaypoint> waypoints;
  final void Function(int index) onRemoveWaypoint;
  final List<CampusPublic> campuses;
  final String? campusId;
  final ValueChanged<String?> onCampusChanged;
  final DateTime departureAt;
  final VoidCallback onPickDeparture;
  final TextEditingController seatsCtrl;
  final FocusNode seatsFocus;
  final int? maxSeats;
  final bool submitting;
  final String? error;
  final VoidCallback onPublish;
  final bool hasValidRoute;

  @override
  Widget build(BuildContext context) {
    final maxH = MediaQuery.sizeOf(context).height * 0.42;
    return Material(
      elevation: 8,
      borderRadius: const BorderRadius.vertical(top: Radius.circular(16)),
      color: Colors.white,
      child: SafeArea(
        top: false,
        child: ConstrainedBox(
          constraints: BoxConstraints(maxHeight: maxH),
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                GestureDetector(
                  onTap: onToggle,
                  behavior: HitTestBehavior.opaque,
                  child: Column(
                    children: [
                      Container(
                        width: 40,
                        height: 4,
                        decoration: BoxDecoration(
                          color: KubixColors.muted.withValues(alpha: 0.4),
                          borderRadius: BorderRadius.circular(2),
                        ),
                      ),
                      const SizedBox(height: 8),
                      Row(
                        children: [
                          Expanded(
                            child: Text(
                              hasValidRoute
                                  ? 'Ruta válida calculada · '
                                      '${waypoints.length} puntos'
                                  : 'Toca el mapa para añadir puntos '
                                      '(${waypoints.length}/${PublishRouteValidation.maxWaypoints})',
                              style: const TextStyle(
                                fontWeight: FontWeight.w600,
                                color: KubixColors.utnBlue,
                              ),
                            ),
                          ),
                          Icon(
                            expanded
                                ? Icons.keyboard_arrow_down
                                : Icons.keyboard_arrow_up,
                            color: KubixColors.muted,
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
                if (waypoints.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  SizedBox(
                    height: 36,
                    child: ListView.separated(
                      scrollDirection: Axis.horizontal,
                      itemCount: waypoints.length,
                      separatorBuilder: (_, __) => const SizedBox(width: 6),
                      itemBuilder: (_, i) {
                        final w = waypoints[i];
                        return InputChip(
                          label: Text('${i + 1}. ${w.label ?? 'Punto'}'),
                          onDeleted:
                              submitting ? null : () => onRemoveWaypoint(i),
                          deleteIconColor: KubixColors.emergency,
                        );
                      },
                    ),
                  ),
                ],
                if (expanded) ...[
                  const SizedBox(height: 12),
                  if (campuses.isEmpty)
                    const Text(
                      'No hay campuses disponibles.',
                      style: TextStyle(color: KubixColors.emergency),
                    )
                  else
                    DropdownButtonFormField<String>(
                      initialValue: campusId,
                      decoration: const InputDecoration(
                        labelText: 'Campus destino',
                        isDense: true,
                      ),
                      items: [
                        for (final c in campuses)
                          DropdownMenuItem(value: c.id, child: Text(c.name)),
                      ],
                      onChanged: submitting ? null : onCampusChanged,
                    ),
                  const SizedBox(height: 8),
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    dense: true,
                    leading: const Icon(
                      Icons.schedule,
                      color: KubixColors.utnBlue,
                    ),
                    title: Text(TripLabels.formatDateTime(departureAt)),
                    subtitle: const Text('Salida'),
                    trailing: TextButton(
                      onPressed: submitting ? null : onPickDeparture,
                      child: const Text('Cambiar'),
                    ),
                  ),
                  TextField(
                    controller: seatsCtrl,
                    focusNode: seatsFocus,
                    enabled: !submitting,
                    keyboardType: TextInputType.number,
                    decoration: InputDecoration(
                      labelText: 'Asientos disponibles',
                      isDense: true,
                      helperText: maxSeats == null
                          ? 'Toca ↑ para reducir el panel'
                          : 'Máx. $maxSeats · toca ↑ para achicar el panel',
                    ),
                  ),
                ],
                if (error != null) ...[
                  const SizedBox(height: 8),
                  Text(
                    error!,
                    style: const TextStyle(
                      color: KubixColors.emergency,
                      fontSize: 13,
                    ),
                  ),
                ],
                const SizedBox(height: 10),
                FilledButton(
                  onPressed: submitting ? null : onPublish,
                  child: submitting
                      ? const SizedBox(
                          height: 22,
                          width: 22,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Text('Publicar ruta'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
