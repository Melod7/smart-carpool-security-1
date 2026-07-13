import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../theme/kubix_theme.dart';

class PendingPage extends StatelessWidget {
  const PendingPage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Spacer(),
              Icon(
                Icons.hourglass_top_rounded,
                size: 64,
                color: KubixColors.utnBlue.withValues(alpha: 0.85),
              ),
              const SizedBox(height: 24),
              Text(
                'Solicitud enviada',
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.w800,
                      color: KubixColors.utnBlue,
                    ),
              ),
              const SizedBox(height: 12),
              const Text(
                'Tu cuenta está pendiente de aprobación por el coordinador de tu universidad. '
                'Cuando la activen podrás iniciar sesión.',
                textAlign: TextAlign.center,
                style: TextStyle(color: KubixColors.muted, height: 1.4),
              ),
              const Spacer(),
              FilledButton(
                onPressed: () => context.go('/login'),
                child: const Text('Volver al inicio de sesión'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
