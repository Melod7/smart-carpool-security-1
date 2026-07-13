import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../theme/kubix_theme.dart';

/// Correo de soporte por defecto (settings admin no expuesto a mobile en v1).
const defaultSupportEmail = 'soporte@utn.edu.ec';

class FaqItem {
  const FaqItem({required this.question, required this.answer});

  final String question;
  final String answer;
}

const passengerFaqs = <FaqItem>[
  FaqItem(
    question: '¿Cómo solicito un viaje?',
    answer:
        'En Inicio verás la lista de viajes disponibles hacia tu campus. '
        'Toca «Pedir», indica tu punto de recogida y envía la solicitud. '
        'El conductor debe aceptarla para confirmar el viaje.',
  ),
  FaqItem(
    question: '¿Puedo cancelar una solicitud?',
    answer:
        'Sí, desde la API el pasajero puede cancelar una solicitud pendiente '
        'o aceptada (antes de que el viaje empiece). En esta versión de la app '
        'la acción de cancelar desde Mis Viajes llega en una iteración próxima; '
        'si el conductor cancela, lo verás reflejado en el estado del viaje.',
  ),
  FaqItem(
    question: '¿Qué son los EcoTokens?',
    answer:
        'Son puntos por viajar compartido y calificar. Subes de nivel '
        '(Bronce, Plata, Oro, Platino) según tu acumulado. El canje en '
        'cafetería o librería estará disponible próximamente.',
  ),
  FaqItem(
    question: '¿Cómo funciona el SOS?',
    answer:
        'Desde la pantalla de inicio podrás activar una alerta de emergencia '
        'que notifica a seguridad del campus con tu ubicación. '
        'Tus contactos de emergencia son informativos para el coordinador; '
        'no se llaman automáticamente en v1.',
  ),
  FaqItem(
    question: '¿Quién ve mi ubicación?',
    answer:
        'Durante un viaje en curso, el conductor y tú pueden ver el mapa '
        'según las reglas de visibilidad. El coordinador puede monitorear '
        'viajes activos por seguridad. No compartimos ubicación fuera de eso.',
  ),
];

class PaxHelpPage extends StatelessWidget {
  const PaxHelpPage({
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
        ...passengerFaqs.map(
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
