import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/models.dart';
import '../../auth/auth_state.dart';
import '../../theme/kubix_theme.dart';
import 'sos_location.dart';

/// Copy de éxito: seguridad del campus (NO contactos de emergencia).
const sosCampusNotifiedCopy =
    'Tu ubicación fue compartida con seguridad del campus. '
    'El equipo de seguridad ha sido notificado.';

enum SosOverlayPhase { confirm, sending, active, closing }

/// Overlay fullscreen rojo: confirmar → GPS + POST /sos → «Estoy a salvo».
class SosOverlay extends ConsumerStatefulWidget {
  const SosOverlay({
    super.key,
    this.tripId,
    this.onClosed,
  });

  final String? tripId;
  final VoidCallback? onClosed;

  @override
  ConsumerState<SosOverlay> createState() => SosOverlayState();
}

class SosOverlayState extends ConsumerState<SosOverlay> {
  SosOverlayPhase _phase = SosOverlayPhase.confirm;
  SosAlert? _alert;
  String? _error;
  bool _busy = false;

  Future<void> _fire() async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _error = null;
      _phase = SosOverlayPhase.sending;
    });

    try {
      final coords =
          await ref.read(sosLocationSourceProvider).getCurrentCoords();
      final alert = await ref.read(sosApiProvider).createSos(
            lat: coords.lat,
            lng: coords.lng,
            tripId: widget.tripId,
          );
      if (!mounted) return;
      setState(() {
        _alert = alert;
        _phase = SosOverlayPhase.active;
        _busy = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString();
        _phase = SosOverlayPhase.confirm;
        _busy = false;
      });
    }
  }

  Future<void> _closeSafe() async {
    final alert = _alert;
    if (alert == null || _busy) return;
    setState(() {
      _busy = true;
      _error = null;
      _phase = SosOverlayPhase.closing;
    });

    try {
      await ref.read(sosApiProvider).closeSos(alert.id);
      if (!mounted) return;
      widget.onClosed?.call();
      Navigator.of(context).maybePop();
    } catch (e) {
      if (!mounted) return;
      if (e is ApiException && e.code == 'sos_already_resolved') {
        widget.onClosed?.call();
        Navigator.of(context).maybePop();
        return;
      }
      setState(() {
        _error = e.toString();
        _phase = SosOverlayPhase.active;
        _busy = false;
      });
    }
  }

  void _dismissConfirm() {
    if (_busy) return;
    widget.onClosed?.call();
    Navigator.of(context).maybePop();
  }

  @override
  Widget build(BuildContext context) {
    return Material(
      color: KubixColors.emergency.withValues(alpha: 0.97),
      child: SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 28),
          child: switch (_phase) {
            SosOverlayPhase.confirm => _ConfirmView(
                error: _error,
                busy: _busy,
                onCancel: _dismissConfirm,
                onConfirm: _fire,
              ),
            SosOverlayPhase.sending => const _SendingView(),
            SosOverlayPhase.active || SosOverlayPhase.closing => _ActiveView(
                error: _error,
                closing: _phase == SosOverlayPhase.closing,
                onSafe: _closeSafe,
              ),
          },
        ),
      ),
    );
  }
}

class _ConfirmView extends StatelessWidget {
  const _ConfirmView({
    required this.error,
    required this.busy,
    required this.onCancel,
    required this.onConfirm,
  });

  final String? error;
  final bool busy;
  final VoidCallback onCancel;
  final VoidCallback onConfirm;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        const Icon(Icons.shield_outlined, color: Colors.white, size: 64),
        const SizedBox(height: 20),
        const Text(
          '¿Activar alerta SOS?',
          textAlign: TextAlign.center,
          style: TextStyle(
            color: Colors.white,
            fontSize: 24,
            fontWeight: FontWeight.w900,
          ),
        ),
        const SizedBox(height: 12),
        Text(
          'Se enviará tu ubicación GPS a seguridad del campus. '
          'Úsalo solo en una emergencia real.',
          textAlign: TextAlign.center,
          style: TextStyle(
            color: Colors.white.withValues(alpha: 0.85),
            fontSize: 14,
            height: 1.4,
          ),
        ),
        if (error != null) ...[
          const SizedBox(height: 16),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: Colors.black.withValues(alpha: 0.2),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Text(
              error!,
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white, fontSize: 13),
            ),
          ),
        ],
        const SizedBox(height: 32),
        SizedBox(
          width: double.infinity,
          child: FilledButton(
            onPressed: busy ? null : onConfirm,
            style: FilledButton.styleFrom(
              backgroundColor: Colors.white,
              foregroundColor: KubixColors.emergency,
              disabledBackgroundColor: Colors.white70,
            ),
            child: busy
                ? const SizedBox(
                    height: 22,
                    width: 22,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: KubixColors.emergency,
                    ),
                  )
                : const Text('Enviar alerta'),
          ),
        ),
        const SizedBox(height: 12),
        TextButton(
          onPressed: busy ? null : onCancel,
          style: TextButton.styleFrom(foregroundColor: Colors.white),
          child: const Text('Cancelar'),
        ),
      ],
    );
  }
}

class _SendingView extends StatelessWidget {
  const _SendingView();

  @override
  Widget build(BuildContext context) {
    return const Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        CircularProgressIndicator(color: Colors.white),
        SizedBox(height: 24),
        Text(
          'Obteniendo ubicación…',
          style: TextStyle(
            color: Colors.white,
            fontSize: 16,
            fontWeight: FontWeight.w600,
          ),
        ),
      ],
    );
  }
}

class _ActiveView extends StatelessWidget {
  const _ActiveView({
    required this.error,
    required this.closing,
    required this.onSafe,
  });

  final String? error;
  final bool closing;
  final VoidCallback onSafe;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        const Icon(Icons.shield, color: Colors.white, size: 56),
        const SizedBox(height: 16),
        const Text(
          'ALERTA ENVIADA',
          textAlign: TextAlign.center,
          style: TextStyle(
            color: Colors.white,
            fontSize: 24,
            fontWeight: FontWeight.w900,
          ),
        ),
        const SizedBox(height: 12),
        Text(
          sosCampusNotifiedCopy,
          textAlign: TextAlign.center,
          style: TextStyle(
            color: Colors.white.withValues(alpha: 0.85),
            fontSize: 14,
            height: 1.4,
          ),
        ),
        const SizedBox(height: 16),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
          decoration: BoxDecoration(
            color: Colors.white.withValues(alpha: 0.15),
            borderRadius: BorderRadius.circular(16),
          ),
          child: const Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.wifi, color: Colors.white, size: 16),
              SizedBox(width: 8),
              Text(
                'Seguridad del campus notificada',
                style: TextStyle(color: Colors.white, fontSize: 12),
              ),
            ],
          ),
        ),
        if (error != null) ...[
          const SizedBox(height: 16),
          Text(
            error!,
            textAlign: TextAlign.center,
            style: const TextStyle(color: Colors.white, fontSize: 13),
          ),
        ],
        const SizedBox(height: 36),
        TextButton(
          onPressed: closing ? null : onSafe,
          style: TextButton.styleFrom(
            foregroundColor: Colors.white,
            backgroundColor: Colors.white.withValues(alpha: 0.2),
            padding: const EdgeInsets.symmetric(horizontal: 28, vertical: 14),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(16),
            ),
          ),
          child: closing
              ? const SizedBox(
                  height: 20,
                  width: 20,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: Colors.white,
                  ),
                )
              : const Text(
                  'Estoy a salvo · Cerrar alerta',
                  style: TextStyle(fontWeight: FontWeight.w700),
                ),
        ),
      ],
    );
  }
}

/// Abre el overlay SOS a pantalla completa.
Future<void> showSosOverlay(
  BuildContext context, {
  String? tripId,
}) {
  return showGeneralDialog<void>(
    context: context,
    barrierDismissible: false,
    barrierColor: Colors.black54,
    transitionDuration: const Duration(milliseconds: 200),
    pageBuilder: (ctx, _, __) {
      return SosOverlay(tripId: tripId);
    },
  );
}
