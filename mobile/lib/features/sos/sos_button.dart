import 'package:flutter/material.dart';

import '../../theme/kubix_theme.dart';

/// Botón de emergencia estilo Figma (`SOS · EMERGENCIA`).
class SosButton extends StatelessWidget {
  const SosButton({super.key, required this.onPressed});

  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onPressed,
        borderRadius: BorderRadius.circular(16),
        child: Ink(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            gradient: const LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: [KubixColors.emergency, Color(0xFFA00020)],
            ),
            boxShadow: [
              BoxShadow(
                color: KubixColors.emergency.withValues(alpha: 0.35),
                blurRadius: 16,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: const Padding(
            padding: EdgeInsets.symmetric(vertical: 14),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Icon(Icons.shield, color: Colors.white, size: 20),
                SizedBox(width: 8),
                Text(
                  'SOS · EMERGENCIA',
                  style: TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w800,
                    fontSize: 14,
                    letterSpacing: 2.2,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
