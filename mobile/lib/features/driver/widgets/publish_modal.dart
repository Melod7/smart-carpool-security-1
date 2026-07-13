import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../api/models.dart';
import '../../../auth/auth_state.dart';
import '../../../theme/kubix_theme.dart';
import '../../map/trip_geometry_cache.dart';
import '../../passenger/trip_labels.dart';
import '../driver_providers.dart';

/// Coordenadas por defecto (Quito) si el conductor no indica lat/lng.
const defaultOriginLat = -0.180653;
const defaultOriginLng = -78.467834;

/// Validación pura del modal de publicación (testeable sin widgets).
class PublishValidation {
  static String? validate({
    required String originText,
    required String? campusId,
    required DateTime? departureAt,
    required int? seats,
    required int? maxSeats,
  }) {
    if (originText.trim().isEmpty) {
      return 'Indica el origen del viaje.';
    }
    if (campusId == null || campusId.isEmpty) {
      return 'Selecciona el campus de destino.';
    }
    if (departureAt == null) {
      return 'Elige fecha y hora de salida.';
    }
    if (departureAt.isBefore(DateTime.now())) {
      return 'La salida debe ser en el futuro.';
    }
    if (seats == null || seats < 1) {
      return 'Indica al menos 1 asiento.';
    }
    if (maxSeats != null && seats > maxSeats) {
      return 'No puedes ofrecer más de $maxSeats asientos.';
    }
    return null;
  }
}

Future<bool> showPublishModal(BuildContext context, WidgetRef ref) async {
  final user = ref.read(authProvider).user;
  Vehicle? vehicle;
  List<CampusPublic> campuses = const [];
  try {
    vehicle = await ref.read(driverVehicleProvider.future);
  } catch (_) {
    vehicle = null;
  }
  try {
    campuses = await ref.read(campusesForDriverProvider.future);
  } catch (_) {
    campuses = const [];
  }

  final originCtrl = TextEditingController();
  final latCtrl = TextEditingController(text: defaultOriginLat.toString());
  final lngCtrl = TextEditingController(text: defaultOriginLng.toString());
  final seatsCtrl = TextEditingController(
    text: '${vehicle != null ? (vehicle.seatsTotal - 1).clamp(1, 8) : 3}',
  );

  var campusId = user?.campusId;
  if (campusId == null || !campuses.any((c) => c.id == campusId)) {
    campusId = campuses.isEmpty ? null : campuses.first.id;
  }
  var departureAt = DateTime.now().add(const Duration(hours: 1));
  // Redondear a minutos.
  departureAt = DateTime(
    departureAt.year,
    departureAt.month,
    departureAt.day,
    departureAt.hour,
    departureAt.minute,
  );

  var submitting = false;
  String? error;
  var published = false;

  if (!context.mounted) {
    originCtrl.dispose();
    latCtrl.dispose();
    lngCtrl.dispose();
    seatsCtrl.dispose();
    return false;
  }

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
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'Publicar ruta',
                    style: Theme.of(ctx).textTheme.titleLarge?.copyWith(
                          fontWeight: FontWeight.w700,
                          color: KubixColors.utnBlue,
                        ),
                  ),
                  const SizedBox(height: 4),
                  const Text(
                    'Viaje hacia el campus. Los pasajeros de tu campus lo verán.',
                    style: TextStyle(color: KubixColors.muted),
                  ),
                  const SizedBox(height: 16),
                  TextField(
                    controller: originCtrl,
                    enabled: !submitting,
                    decoration: const InputDecoration(
                      labelText: 'Origen',
                      hintText: 'Ej. Av. América y Colón',
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
                      ),
                      items: [
                        for (final c in campuses)
                          DropdownMenuItem(value: c.id, child: Text(c.name)),
                      ],
                      onChanged: submitting
                          ? null
                          : (v) => setState(() => campusId = v),
                    ),
                  const SizedBox(height: 12),
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: const Icon(
                      Icons.schedule,
                      color: KubixColors.utnBlue,
                    ),
                    title: Text(TripLabels.formatDateTime(departureAt)),
                    subtitle: const Text('Fecha y hora de salida'),
                    trailing: TextButton(
                      onPressed: submitting
                          ? null
                          : () async {
                              final date = await showDatePicker(
                                context: ctx,
                                initialDate: departureAt,
                                firstDate: DateTime.now(),
                                lastDate:
                                    DateTime.now().add(const Duration(days: 14)),
                              );
                              if (date == null || !ctx.mounted) return;
                              final time = await showTimePicker(
                                context: ctx,
                                initialTime: TimeOfDay.fromDateTime(departureAt),
                              );
                              if (time == null) return;
                              setState(() {
                                departureAt = DateTime(
                                  date.year,
                                  date.month,
                                  date.day,
                                  time.hour,
                                  time.minute,
                                );
                              });
                            },
                      child: const Text('Cambiar'),
                    ),
                  ),
                  TextField(
                    controller: seatsCtrl,
                    enabled: !submitting,
                    keyboardType: TextInputType.number,
                    decoration: InputDecoration(
                      labelText: 'Asientos disponibles',
                      helperText: vehicle == null
                          ? null
                          : 'Máx. ${vehicle.seatsTotal} (capacidad del vehículo)',
                    ),
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
                            final seats = int.tryParse(seatsCtrl.text.trim());
                            final validation = PublishValidation.validate(
                              originText: originCtrl.text,
                              campusId: campusId,
                              departureAt: departureAt,
                              seats: seats,
                              maxSeats: vehicle?.seatsTotal,
                            );
                            if (validation != null) {
                              setState(() => error = validation);
                              return;
                            }
                            final lat = double.tryParse(latCtrl.text.trim()) ??
                                defaultOriginLat;
                            final lng = double.tryParse(lngCtrl.text.trim()) ??
                                defaultOriginLng;
                            setState(() {
                              submitting = true;
                              error = null;
                            });
                            try {
                              final publishedTrip =
                                  await ref.read(driverApiProvider).publishTrip(
                                        originText: originCtrl.text.trim(),
                                        originLat: lat,
                                        originLng: lng,
                                        destinationCampusId: campusId!,
                                        departureAt: departureAt,
                                        seatsAvailable: seats!,
                                      );
                              ref
                                  .read(tripGeometryCacheProvider.notifier)
                                  .putAvailable(publishedTrip);
                              invalidateDriverTrips(ref);
                              published = true;
                              if (ctx.mounted) Navigator.pop(ctx);
                              if (context.mounted) {
                                ScaffoldMessenger.of(context).showSnackBar(
                                  const SnackBar(
                                    content: Text(
                                      'Ruta publicada. Los pasajeros ya pueden solicitarla.',
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
                        : const Text('Publicar'),
                  ),
                ],
              ),
            ),
          );
        },
      );
    },
  );

  originCtrl.dispose();
  latCtrl.dispose();
  lngCtrl.dispose();
  seatsCtrl.dispose();
  return published;
}
