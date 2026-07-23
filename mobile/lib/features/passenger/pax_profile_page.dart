import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/models.dart';
import '../../auth/auth_state.dart';
import '../../theme/kubix_theme.dart';
import '../../widgets/image_picker_field.dart';
import 'passenger_providers.dart';
import 'trip_labels.dart';
import 'widgets/eco_widget.dart';

class PaxProfilePage extends ConsumerStatefulWidget {
  const PaxProfilePage({super.key});

  @override
  ConsumerState<PaxProfilePage> createState() => _PaxProfilePageState();
}

class _PaxProfilePageState extends ConsumerState<PaxProfilePage> {
  final _nameCtrl = TextEditingController();
  final _careerCtrl = TextEditingController();
  var _profileLoaded = false;
  var _savingProfile = false;
  var _switchingMode = false;
  String? _gender;
  String? _profileImage;
  var _profileImageChanged = false;

  @override
  void dispose() {
    _nameCtrl.dispose();
    _careerCtrl.dispose();
    super.dispose();
  }

  void _syncProfileFields(UserProfile profile) {
    if (_profileLoaded) return;
    _nameCtrl.text = profile.name;
    _careerCtrl.text = profile.career ?? '';
    _gender = profile.gender;
    _profileImage = profile.profileImage;
    _profileLoaded = true;
  }

  @override
  Widget build(BuildContext context) {
    final profileAsync = ref.watch(profileProvider);
    final contactsAsync = ref.watch(emergencyContactsProvider);
    final ecoAsync = ref.watch(ecoSummaryProvider);

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        profileAsync.when(
          data: (profile) {
            _syncProfileFields(profile);
            return _SectionCard(
              title: 'Datos personales',
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  ImagePickerField(
                    label: 'Imagen de perfil',
                    value: _profileImage,
                    onChanged: (value) => setState(() {
                      _profileImage = value;
                      _profileImageChanged = true;
                    }),
                  ),
                  const SizedBox(height: 12),
                  Text(
                    profile.email,
                    style: const TextStyle(color: KubixColors.muted),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _nameCtrl,
                    decoration: const InputDecoration(labelText: 'Nombre'),
                    textCapitalization: TextCapitalization.words,
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _careerCtrl,
                    decoration: const InputDecoration(labelText: 'Carrera'),
                    textCapitalization: TextCapitalization.sentences,
                  ),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String>(
                    key: ValueKey(_gender),
                    initialValue: _gender,
                    decoration: const InputDecoration(labelText: 'Género'),
                    items: const [
                      DropdownMenuItem(value: 'female', child: Text('Mujer')),
                      DropdownMenuItem(value: 'male', child: Text('Hombre')),
                    ],
                    onChanged: (value) => setState(() => _gender = value),
                  ),
                  const SizedBox(height: 12),
                  const Text(
                    'Los cambios se aplicarán cuando el coordinador los apruebe.',
                    style: TextStyle(
                      fontSize: 12,
                      color: KubixColors.muted,
                    ),
                  ),
                  const SizedBox(height: 12),
                  FilledButton(
                    onPressed: _savingProfile ? null : _saveProfile,
                    child: _savingProfile
                        ? const SizedBox(
                            height: 22,
                            width: 22,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                        : const Text('Enviar solicitud de cambio'),
                  ),
                ],
              ),
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
        const SizedBox(height: 16),
        contactsAsync.when(
          data: (contacts) => _SectionCard(
            title: 'Contactos de emergencia',
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text(
                  'Máximo 3 contactos. Se muestran a seguridad del campus en un SOS.',
                  style: TextStyle(fontSize: 13, color: KubixColors.muted),
                ),
                const SizedBox(height: 12),
                if (contacts.isEmpty)
                  const Text(
                    'Aún no tienes contactos.',
                    style: TextStyle(color: KubixColors.muted),
                  ),
                for (final c in contacts) ...[
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    title: Text(
                      c.name,
                      style: const TextStyle(fontWeight: FontWeight.w600),
                    ),
                    subtitle: Text('${c.relationship} · ${c.phone}'),
                    trailing: IconButton(
                      tooltip: 'Eliminar',
                      onPressed: () => _deleteContact(c),
                      icon: const Icon(
                        Icons.delete_outline,
                        color: KubixColors.emergency,
                      ),
                    ),
                  ),
                ],
                if (contacts.length < 3) ...[
                  const SizedBox(height: 8),
                  OutlinedButton.icon(
                    onPressed: _addContact,
                    icon: const Icon(Icons.person_add_alt_1),
                    label: const Text('Agregar contacto'),
                  ),
                ],
              ],
            ),
          ),
          loading: () => const SizedBox.shrink(),
          error: (e, _) => Text(e.toString()),
        ),
        const SizedBox(height: 16),
        ecoAsync.when(
          data: (eco) {
            if (!eco.gamificationEnabled) {
              return const _SectionCard(
                title: 'EcoTokensUTN',
                child: Text(
                  'La gamificación está desactivada en tu universidad.',
                  style: TextStyle(color: KubixColors.muted),
                ),
              );
            }
            return _SectionCard(
              title: 'EcoTokensUTN',
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  EcoWidget(
                    eco: eco,
                    onRedeem: (prize) => _redeemPrize(prize),
                  ),
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
                        Icon(Icons.storefront_outlined,
                            color: KubixColors.muted),
                        SizedBox(width: 10),
                        Expanded(
                          child: Text(
                            'Tras canjear, retira tu premio con el coordinador del campus.',
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
                icon: Icons.verified_user_outlined,
                title: 'Cuenta activa',
                subtitle: 'Aprobada por el coordinador del campus',
              ),
            ],
          ),
        ),
        const SizedBox(height: 24),
        FilledButton.icon(
          onPressed: _switchingMode ? null : _requestDriverMode,
          icon: const Icon(Icons.directions_car_outlined),
          label: Text(
            _switchingMode ? 'Enviando solicitud…' : 'Cambiar a modo conductor',
          ),
        ),
        const SizedBox(height: 12),
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

  Future<void> _saveProfile() async {
    final name = _nameCtrl.text.trim();
    if (name.isEmpty) {
      _snack('El nombre es obligatorio.');
      return;
    }
    if (_gender == null) {
      _snack('Selecciona tu género para encontrar viajes compatibles.');
      return;
    }
    final career = _careerCtrl.text.trim();
    if (career.isEmpty) {
      _snack('La carrera es obligatoria.');
      return;
    }
    setState(() => _savingProfile = true);
    try {
      await ref.read(passengerApiProvider).requestProfileChange(
            name: name,
            career: career,
            gender: _gender!,
            profileImage: _profileImageChanged ? _profileImage : null,
          );
      _profileImageChanged = false;
      _snack('Solicitud enviada al coordinador.');
    } catch (e) {
      _snack(e.toString());
    } finally {
      if (mounted) setState(() => _savingProfile = false);
    }
  }

  Future<void> _redeemPrize(EcoPrize prize) async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Canjear premio'),
        content: Text(
          '¿Canjear ${prize.name} por ${prize.cost} EcoTokensUTN?\n'
          'Luego retíralo con el coordinador.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Canjear'),
          ),
        ],
      ),
    );
    if (confirm != true) return;
    try {
      final result =
          await ref.read(passengerApiProvider).redeemPrize(prize.code);
      ref.invalidate(ecoSummaryProvider);
      if (!mounted) return;
      _snack(result.message.isNotEmpty
          ? result.message
          : 'Canje realizado. Nuevo saldo: ${result.balance} ECT.');
    } catch (e) {
      if (!mounted) return;
      _snack(e.toString());
    }
  }

  Future<void> _requestDriverMode() async {
    final makeCtrl = TextEditingController();
    final plateCtrl = TextEditingController();
    final colorCtrl = TextEditingController();
    final seatsCtrl = TextEditingController(text: '4');
    String? image;

    final vehicle = await showDialog<VehicleRegister>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) => AlertDialog(
          title: const Text('Datos del vehículo'),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text(
                  'Un coordinador aprobará el cambio a conductor.',
                  style: TextStyle(fontSize: 13, color: KubixColors.muted),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: makeCtrl,
                  decoration:
                      const InputDecoration(labelText: 'Marca y modelo'),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: plateCtrl,
                  decoration: const InputDecoration(labelText: 'Placa'),
                  textCapitalization: TextCapitalization.characters,
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: colorCtrl,
                  decoration: const InputDecoration(labelText: 'Color'),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: seatsCtrl,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(
                    labelText: 'Asientos totales',
                    helperText: 'Entre 1 y 8',
                  ),
                ),
                const SizedBox(height: 12),
                ImagePickerField(
                  label: 'Imagen del vehículo',
                  value: image,
                  onChanged: (value) => setDialogState(() => image = value),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: () {
                final seats = int.tryParse(seatsCtrl.text.trim());
                if (makeCtrl.text.trim().isEmpty ||
                    plateCtrl.text.trim().isEmpty ||
                    colorCtrl.text.trim().isEmpty ||
                    seats == null ||
                    seats < 1 ||
                    seats > 8 ||
                    image == null) {
                  ScaffoldMessenger.of(ctx).showSnackBar(
                    const SnackBar(
                      content: Text('Completa todos los datos del vehículo.'),
                    ),
                  );
                  return;
                }
                Navigator.pop(
                  ctx,
                  VehicleRegister(
                    makeModel: makeCtrl.text.trim(),
                    plate: plateCtrl.text.trim(),
                    color: colorCtrl.text.trim(),
                    seatsTotal: seats,
                    image: image!,
                  ),
                );
              },
              child: const Text('Enviar solicitud'),
            ),
          ],
        ),
      ),
    );

    makeCtrl.dispose();
    plateCtrl.dispose();
    colorCtrl.dispose();
    seatsCtrl.dispose();
    if (vehicle == null || !mounted) return;

    setState(() => _switchingMode = true);
    try {
      final response =
          await ref.read(passengerApiProvider).requestDriverMode(vehicle);
      _snack(
        response.message.isNotEmpty
            ? response.message
            : 'Solicitud enviada al coordinador.',
      );
    } catch (e) {
      _snack(e.toString());
    } finally {
      if (mounted) setState(() => _switchingMode = false);
    }
  }

  Future<void> _addContact() async {
    final nameCtrl = TextEditingController();
    final relCtrl = TextEditingController();
    final phoneCtrl = TextEditingController();
    var submitting = false;
    String? error;

    await showDialog<void>(
      context: context,
      builder: (ctx) {
        return StatefulBuilder(
          builder: (ctx, setState) {
            return AlertDialog(
              title: const Text('Nuevo contacto'),
              content: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    TextField(
                      controller: nameCtrl,
                      decoration: const InputDecoration(labelText: 'Nombre'),
                    ),
                    const SizedBox(height: 8),
                    TextField(
                      controller: relCtrl,
                      decoration: const InputDecoration(labelText: 'Relación'),
                    ),
                    const SizedBox(height: 8),
                    TextField(
                      controller: phoneCtrl,
                      keyboardType: TextInputType.phone,
                      decoration: const InputDecoration(labelText: 'Teléfono'),
                    ),
                    if (error != null) ...[
                      const SizedBox(height: 8),
                      Text(
                        error!,
                        style: const TextStyle(color: KubixColors.emergency),
                      ),
                    ],
                  ],
                ),
              ),
              actions: [
                TextButton(
                  onPressed: submitting ? null : () => Navigator.pop(ctx),
                  child: const Text('Cancelar'),
                ),
                FilledButton(
                  onPressed: submitting
                      ? null
                      : () async {
                          final name = nameCtrl.text.trim();
                          final rel = relCtrl.text.trim();
                          final phone = phoneCtrl.text.trim();
                          if (name.isEmpty || rel.isEmpty || phone.isEmpty) {
                            setState(
                              () => error = 'Completa todos los campos.',
                            );
                            return;
                          }
                          setState(() {
                            submitting = true;
                            error = null;
                          });
                          try {
                            await ref
                                .read(passengerApiProvider)
                                .createEmergencyContact(
                                  name: name,
                                  relationship: rel,
                                  phone: phone,
                                );
                            ref.invalidate(emergencyContactsProvider);
                            if (ctx.mounted) Navigator.pop(ctx);
                          } catch (e) {
                            setState(() {
                              submitting = false;
                              error = e.toString();
                            });
                          }
                        },
                  child: const Text('Guardar'),
                ),
              ],
            );
          },
        );
      },
    );

    nameCtrl.dispose();
    relCtrl.dispose();
    phoneCtrl.dispose();
  }

  Future<void> _deleteContact(EmergencyContact contact) async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Eliminar contacto'),
        content: Text('¿Eliminar a ${contact.name}?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: FilledButton.styleFrom(
              backgroundColor: KubixColors.emergency,
            ),
            child: const Text('Eliminar'),
          ),
        ],
      ),
    );
    if (confirm != true) return;
    try {
      await ref.read(passengerApiProvider).deleteEmergencyContact(contact.id);
      ref.invalidate(emergencyContactsProvider);
      _snack('Contacto eliminado.');
    } catch (e) {
      _snack(e.toString());
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
