import 'package:flutter/material.dart';

import '../../../api/models.dart';
import '../../../theme/kubix_theme.dart';

Future<bool> showRatingDialog(
  BuildContext context, {
  required PendingRating pending,
  required Future<void> Function(int stars, String? comment) onSubmit,
}) async {
  var stars = 5;
  final commentCtrl = TextEditingController();
  var submitting = false;
  String? error;

  final result = await showDialog<bool>(
    context: context,
    barrierDismissible: false,
    builder: (ctx) {
      return StatefulBuilder(
        builder: (ctx, setState) {
          return AlertDialog(
            title: const Text('Calificar viaje'),
            content: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Califica a ${pending.ratedUserName}',
                    style: const TextStyle(fontWeight: FontWeight.w600),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: List.generate(5, (i) {
                      final value = i + 1;
                      return IconButton(
                        onPressed: submitting
                            ? null
                            : () => setState(() => stars = value),
                        icon: Icon(
                          value <= stars ? Icons.star : Icons.star_border,
                          color: KubixColors.gold,
                          size: 32,
                        ),
                      );
                    }),
                  ),
                  TextField(
                    controller: commentCtrl,
                    enabled: !submitting,
                    maxLines: 3,
                    decoration: const InputDecoration(
                      labelText: 'Comentario (opcional)',
                      alignLabelWithHint: true,
                    ),
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
                onPressed: submitting ? null : () => Navigator.pop(ctx, false),
                child: const Text('Después'),
              ),
              FilledButton(
                onPressed: submitting
                    ? null
                    : () async {
                        setState(() {
                          submitting = true;
                          error = null;
                        });
                        try {
                          final comment = commentCtrl.text.trim();
                          await onSubmit(
                            stars,
                            comment.isEmpty ? null : comment,
                          );
                          if (ctx.mounted) Navigator.pop(ctx, true);
                        } catch (e) {
                          setState(() {
                            submitting = false;
                            error = e.toString();
                          });
                        }
                      },
                child: submitting
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text('Enviar'),
              ),
            ],
          );
        },
      );
    },
  );

  commentCtrl.dispose();
  return result ?? false;
}
