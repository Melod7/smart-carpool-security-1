import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/models.dart';
import '../../auth/auth_state.dart';
import '../../theme/kubix_theme.dart';
import '../passenger/trip_labels.dart';
import '../passenger/widgets/eco_widget.dart';
import 'driver_providers.dart';

class DrvProfilePage extends ConsumerStatefulWidget {
  const DrvProfilePage({super.key});

  @override
  ConsumerState<DrvProfilePage> createState() => _DrvProfilePageState();
}

class _DrvProfilePageState extends ConsumerState<DrvProfilePage> {
  final _makeCtrl = TextEditingController();
  final _plateCtrl = TextEditingController();
  final _colorCtrl = TextEditingController();
  final _seatsCtrl = TextEditingController();
  var _vehicleLoaded = false;
  var _savingVehicle = false;

  @override
  void dispose() {
    _makeCtrl.dispose();
    _plateCtrl.dispose();
    _colorCtrl.dispose();
    _seatsCtrl.dispose();
    super.dispose();
  }

  void _syncVehicle(Vehicle vehicle) {
    if (_vehicleLoaded) return;
    _makeCtrl.text = vehicle.makeModel;
    _plateCtrl.text = vehicle.plate;
    _colorCtrl.text = vehicle.color;
    _seatsCtrl.text = '${vehicle.seatsTotal}';
    _vehicleLoaded = true;
  }

  @override
  Widget build(BuildContext context) {
    final profileAsync = ref.watch(driverProfileProvider);
    final vehicleAsync = ref.watch(driverVehicleProvider);
    final ecoAsync = ref.watch(driverEcoProvider);

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        profileAsync.when(
          data: (profile) => _SectionCard(
            title: 'Cuenta',
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  profile.name,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 16,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  profile.email,
                  style: const TextStyle(color: KubixColors.muted),
                ),
                if (profile.career != null && profile.career!.isNotEmpty) ...[
                  const SizedBox(height: 4),
                  Text(
                    profile.career!,
                    style: const TextStyle(color: KubixColors.muted),
                  ),
                ],
              ],
            ),
          ),
          loading: () => const Padding(
            padding: EdgeInsets.all(24),
            child: Center(child: CircularProgressIndicator()),
          ),
          error: (e, _) => Text(
            e.toString(),
            style: const TextStyle(color: KubixColors.emergency),
          ),
        ),
        const SizedBox(height: 16),
        vehicleAsync.when(
          data: (vehicle) {
            _syncVehicle(vehicle);
            return _SectionCard(
              title: 'Vehículo',
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  TextField(
                    controller: _makeCtrl,
                    decoration: const InputDecoration(
                      labelText: 'Marca y modelo',
                    ),
                    textCapitalization: TextCapitalization.words,
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _plateCtrl,
                    decoration: const InputDecoration(labelText: 'Placa'),
                    textCapitalization: TextCapitalization.characters,
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _colorCtrl,
                    decoration: const InputDecoration(labelText: 'Color'),
                    textCapitalization: TextCapitalization.words,
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _seatsCtrl,
                    decoration: const InputDecoration(
                      labelText: 'Asientos totales',
                      helperText: 'Entre 1 y 8',
                    ),
                    keyboardType: TextInputType.number,
                  ),
                  const SizedBox(height: 12),
                  FilledButton(
                    onPressed: _savingVehicle ? null : _saveVehicle,
                    child: _savingVehicle
                        ? const SizedBox(
                            height: 22,
                            width: 22,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                        : const Text('Guardar vehículo'),
                  ),
                ],
              ),
            );
          },
          loading: () => const SizedBox.shrink(),
          error: (e, _) => _SectionCard(
            title: 'Vehículo',
            child: Text(
              e.toString(),
              style: const TextStyle(color: KubixColors.emergency),
            ),
          ),
        ),
        const SizedBox(height: 16),
        ecoAsync.when(
          data: (eco) {
            if (!eco.gamificationEnabled) {
              return const _SectionCard(
                title: 'Gamificación',
                child: Text(
                  'La gamificación está desactivada en tu universidad.',
                  style: TextStyle(color: KubixColors.muted),
                ),
              );
            }
            return _SectionCard(
              title: 'Gamificación',
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  EcoWidget(eco: eco),
                  const SizedBox(height: 12),
                  if (eco.transactions.isNotEmpty) ...[
                    const Text(
                      'Movimientos recientes',
                      style: TextStyle(fontWeight: FontWeight.w600),
                    ),
                    const SizedBox(height: 8),
                    for (final tx in eco.transactions.take(5))
                      Padding(
                        padding: const EdgeInsets.only(bottom: 6),
                        child: Row(
                          children: [
                            Expanded(
                              child: Text(
                                TripLabels.ecoType(tx.type),
                                style: const TextStyle(fontSize: 13),
                              ),
                            ),
                            Text(
                              '${tx.amount > 0 ? '+' : ''}${tx.amount} ECT',
                              style: TextStyle(
                                fontWeight: FontWeight.w700,
                                color: tx.amount >= 0
                                    ? KubixColors.eco
                                    : KubixColors.emergency,
                              ),
                            ),
                          ],
                        ),
                      ),
                  ],
                  const SizedBox(height: 12),
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: KubixColors.background,
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: const Row(
                      children: [
                        Icon(Icons.storefront_outlined, color: KubixColors.muted),
                        SizedBox(width: 10),
                        Expanded(
                          child: Text(
                            'Canje en cafetería y librería: próximamente.',
                            style: TextStyle(color: KubixColors.muted),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            );
          },
          loading: () => const SizedBox.shrink(),
          error: (_, __) => const SizedBox.shrink(),
        ),
        const SizedBox(height: 16),
        const _SectionCard(
          title: 'Verificación',
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _StaticRow(
                icon: Icons.badge_outlined,
                title: 'Identidad universitaria',
                subtitle: 'Vinculada a tu correo institucional',
              ),
              SizedBox(height: 8),
              _StaticRow(
                icon: Icons.directions_car_outlined,
                title: 'Conductor aprobado',
                subtitle: 'Vehículo registrado y cuenta activa',
              ),
            ],
          ),
        ),
        const SizedBox(height: 24),
        OutlinedButton.icon(
          onPressed: () => ref.read(authProvider.notifier).logout(),
          icon: const Icon(Icons.logout),
          label: const Text('Cerrar sesión'),
          style: OutlinedButton.styleFrom(
            foregroundColor: KubixColors.emergency,
            side: const BorderSide(color: KubixColors.emergency),
          ),
        ),
        const SizedBox(height: 24),
      ],
    );
  }

  Future<void> _saveVehicle() async {
    final make = _makeCtrl.text.trim();
    final plate = _plateCtrl.text.trim();
    final color = _colorCtrl.text.trim();
    final seats = int.tryParse(_seatsCtrl.text.trim());
    if (make.isEmpty || plate.isEmpty || color.isEmpty) {
      _snack('Completa marca, placa y color.');
      return;
    }
    if (seats == null || seats < 1 || seats > 8) {
      _snack('Asientos entre 1 y 8.');
      return;
    }
    setState(() => _savingVehicle = true);
    try {
      await ref.read(driverApiProvider).updateVehicle(
            makeModel: make,
            plate: plate,
            color: color,
            seatsTotal: seats,
          );
      _vehicleLoaded = false;
      ref.invalidate(driverVehicleProvider);
      _snack('Vehículo actualizado.');
    } catch (e) {
      _snack(e.toString());
    } finally {
      if (mounted) setState(() => _savingVehicle = false);
    }
  }

  void _snack(String msg) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(msg)));
  }
}

class _SectionCard extends StatelessWidget {
  const _SectionCard({required this.title, required this.child});

  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w700,
                  color: KubixColors.utnBlue,
                ),
          ),
          const SizedBox(height: 12),
          child,
        ],
      ),
    );
  }
}

class _StaticRow extends StatelessWidget {
  const _StaticRow({
    required this.icon,
    required this.title,
    required this.subtitle,
  });

  final IconData icon;
  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(icon, color: KubixColors.utnBlue),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(title, style: const TextStyle(fontWeight: FontWeight.w600)),
              Text(
                subtitle,
                style: const TextStyle(fontSize: 13, color: KubixColors.muted),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
