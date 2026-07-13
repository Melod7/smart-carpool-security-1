import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../theme/kubix_theme.dart';
import '../passenger/pax_help_page.dart' show FaqItem, defaultSupportEmail;

const driverFaqs = <FaqItem>[
  FaqItem(
    question: '¿Cómo publico una ruta?',
    answer:
        'En Inicio toca el botón +. Indica origen, campus de destino, hora '
        'y asientos disponibles. Los pasajeros de tu campus verán el viaje '
        'en su lista de disponibles.',
  ),
  FaqItem(
    question: '¿Cómo acepto o rechazo solicitudes?',
    answer:
        'Cuando hay una ruta programada, las solicitudes pendientes aparecen '
        'en Inicio y se actualizan cada 15 segundos. Usa Aceptar o Rechazar '
        'antes de iniciar el viaje.',
  ),
  FaqItem(
    question: '¿Cuándo inicio y completo el viaje?',
    answer:
        'Cuando los pasajeros aceptados estén listos, toca «Iniciar viaje». '
        'Al llegar al campus, «Completar viaje» cierra la ruta y acredita '
        'EcoTokens (+8 ECT por viaje completado si la gamificación está activa).',
  ),
  FaqItem(
    question: '¿Qué pasa si alcanzo el límite diario?',
    answer:
        'Cada universidad define un máximo de viajes por día para conductores. '
        'Si lo superas al publicar, verás un mensaje de error amigable.',
  ),
  FaqItem(
    question: '¿Cómo funciona el SOS?',
    answer:
        'Desde la pantalla de inicio podrás activar una alerta de emergencia '
        'que notifica a seguridad del campus con tu ubicación. '
        'Disponible en una próxima entrega (KBX-25).',
  ),
];

class DrvHelpPage extends StatelessWidget {
  const DrvHelpPage({
    super.key,
    this.supportEmail = defaultSupportEmail,
  });

  final String supportEmail;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text(
          'Preguntas frecuentes',
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.w700,
                color: KubixColors.utnBlue,
              ),
        ),
        const SizedBox(height: 8),
        ...driverFaqs.map(
          (faq) => Card(
            margin: const EdgeInsets.only(bottom: 8),
            elevation: 0,
            color: Colors.white,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12),
            ),
            child: ExpansionTile(
              tilePadding: const EdgeInsets.symmetric(horizontal: 16),
              childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
              title: Text(
                faq.question,
                style: const TextStyle(fontWeight: FontWeight.w600),
              ),
              children: [
                Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    faq.answer,
                    style: const TextStyle(
                      color: KubixColors.muted,
                      height: 1.4,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),
        Container(
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
                'Soporte',
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.w700,
                      color: KubixColors.utnBlue,
                    ),
              ),
              const SizedBox(height: 8),
              const Text(
                'Si necesitas ayuda, escribe al correo de soporte de tu universidad. '
                'No hay chat en la app en esta versión.',
                style: TextStyle(color: KubixColors.muted),
              ),
              const SizedBox(height: 12),
              Material(
                color: Colors.transparent,
                child: ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: const Icon(
                    Icons.email_outlined,
                    color: KubixColors.utnBlue,
                  ),
                  title: Text(
                    supportEmail,
                    style: const TextStyle(
                      fontWeight: FontWeight.w600,
                      color: KubixColors.utnBlue,
                    ),
                  ),
                  subtitle: const Text('Toca para copiar el correo'),
                  onTap: () => _copyEmail(context, supportEmail),
                  trailing: IconButton(
                    tooltip: 'Copiar',
                    onPressed: () => _copyEmail(context, supportEmail),
                    icon: const Icon(Icons.copy),
                  ),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  Future<void> _copyEmail(BuildContext context, String email) async {
    await Clipboard.setData(ClipboardData(text: email));
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Correo copiado: $email')),
      );
    }
  }
}
