import 'package:flutter/material.dart';

const apiUrl = String.fromEnvironment(
  'API_URL',
  defaultValue: 'http://127.0.0.1:8080',
);

void main() {
  runApp(const KubixApp());
}

class KubixApp extends StatelessWidget {
  const KubixApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Kubix UTN',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF003087),
          primary: const Color(0xFF003087),
        ),
        useMaterial3: true,
      ),
      home: const PantallaArranque(),
    );
  }
}

class PantallaArranque extends StatelessWidget {
  const PantallaArranque({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF0F4FA),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Kubix UTN',
                style: TextStyle(
                  fontSize: 28,
                  fontWeight: FontWeight.w800,
                  color: Color(0xFF003087),
                ),
              ),
              const SizedBox(height: 8),
              const Text(
                'Carpooling seguro — esqueleto mobile (KBX-1)',
                style: TextStyle(color: Color(0xFF5A6A8A)),
              ),
              const SizedBox(height: 24),
              Text(
                'API_URL = $apiUrl',
                style: const TextStyle(fontFamily: 'monospace', fontSize: 13),
              ),
              const Spacer(),
              const Text(
                'Autenticación, roles y flujos de viaje llegan en tickets posteriores.',
                style: TextStyle(color: Color(0xFF5A6A8A)),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
