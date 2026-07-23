import 'package:flutter/material.dart';

import '../../../api/models.dart';
import '../../../theme/kubix_theme.dart';

/// Widget de EcoTokensUTN (ocultar si [eco.gamificationEnabled] es false).
class EcoWidget extends StatelessWidget {
  const EcoWidget({
    super.key,
    required this.eco,
    this.onRedeem,
  });

  final EcoSummary eco;
  final Future<void> Function(EcoPrize prize)? onRedeem;

  @override
  Widget build(BuildContext context) {
    if (!eco.gamificationEnabled) return const SizedBox.shrink();

    final progress = eco.progress;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: [
            KubixColors.eco.withValues(alpha: 0.12),
            KubixColors.gold.withValues(alpha: 0.18),
          ],
        ),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: KubixColors.eco.withValues(alpha: 0.25)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(Icons.eco, color: KubixColors.eco),
              const SizedBox(width: 8),
              Text(
                'EcoTokensUTN',
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.w700,
                      color: KubixColors.eco,
                    ),
              ),
              const Spacer(),
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: KubixColors.gold.withValues(alpha: 0.25),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Text(
                  eco.level,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF6D4C00),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Text(
            '${eco.balance} ECT',
            style: const TextStyle(
              fontSize: 28,
              fontWeight: FontWeight.w800,
              color: KubixColors.utnBlue,
            ),
          ),
          Text(
            'Vitalicio: ${eco.lifetime} ECT',
            style: const TextStyle(color: KubixColors.muted, fontSize: 13),
          ),
          if (progress != null) ...[
            const SizedBox(height: 12),
            ClipRRect(
              borderRadius: BorderRadius.circular(4),
              child: LinearProgressIndicator(
                value: progress.clamp(0.0, 1.0),
                minHeight: 8,
                backgroundColor: Colors.white,
                color: KubixColors.eco,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              'Progreso al siguiente nivel: ${(progress * 100).round()}%',
              style: const TextStyle(fontSize: 12, color: KubixColors.muted),
            ),
          ],
          if (onRedeem != null && eco.prizes.isNotEmpty) ...[
            const SizedBox(height: 16),
            const Text(
              'Canjear premios UTN',
              style: TextStyle(fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 8),
            for (final prize in eco.prizes)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.center,
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            prize.name,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(fontWeight: FontWeight.w600),
                          ),
                          Text(
                            '${prize.cost} ECT',
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              fontSize: 12,
                              color: KubixColors.muted,
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 8),
                    // El tema usa Size.fromHeight(48) (= ancho infinito); hay que
                    // acotar el botón o aplasta el texto a 1 carácter de ancho.
                    FilledButton(
                      style: FilledButton.styleFrom(
                        minimumSize: const Size(96, 40),
                        padding: const EdgeInsets.symmetric(horizontal: 14),
                        tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                      ),
                      onPressed: eco.balance < prize.cost
                          ? null
                          : () => onRedeem!(prize),
                      child: const Text('Canjear'),
                    ),
                  ],
                ),
              ),
          ],
        ],
      ),
    );
  }
}
