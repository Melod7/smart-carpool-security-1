import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../auth/auth_state.dart';
import '../../theme/kubix_theme.dart';

class PassengerShell extends ConsumerStatefulWidget {
  const PassengerShell({super.key});

  @override
  ConsumerState<PassengerShell> createState() => _PassengerShellState();
}

class _PassengerShellState extends ConsumerState<PassengerShell> {
  int _index = 0;

  static const _titles = ['Inicio', 'Mis Viajes', 'Perfil', 'Ayuda'];

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(authProvider).user;

    return Scaffold(
      appBar: AppBar(
        title: Text(_titles[_index]),
        actions: [
          if (_index == 2)
            IconButton(
              tooltip: 'Cerrar sesión',
              onPressed: () => ref.read(authProvider.notifier).logout(),
              icon: const Icon(Icons.logout),
            ),
        ],
      ),
      body: IndexedStack(
        index: _index,
        children: [
          _PlaceholderTab(
            title: 'Inicio',
            subtitle:
                'Hola ${user?.name ?? ''}. Aquí verás viajes disponibles (KBX-23).',
          ),
          const _PlaceholderTab(
            title: 'Mis Viajes',
            subtitle: 'Próximos e historial — próximamente en KBX-23.',
          ),
          _PlaceholderTab(
            title: 'Perfil',
            subtitle: user?.email ?? '',
            child: FilledButton(
              onPressed: () => ref.read(authProvider.notifier).logout(),
              child: const Text('Cerrar sesión'),
            ),
          ),
          const _PlaceholderTab(
            title: 'Ayuda',
            subtitle: 'FAQ y correo de soporte — próximamente en KBX-23.',
          ),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.home_outlined),
            selectedIcon: Icon(Icons.home),
            label: 'Inicio',
          ),
          NavigationDestination(
            icon: Icon(Icons.route_outlined),
            selectedIcon: Icon(Icons.route),
            label: 'Mis Viajes',
          ),
          NavigationDestination(
            icon: Icon(Icons.person_outline),
            selectedIcon: Icon(Icons.person),
            label: 'Perfil',
          ),
          NavigationDestination(
            icon: Icon(Icons.help_outline),
            selectedIcon: Icon(Icons.help),
            label: 'Ayuda',
          ),
        ],
      ),
    );
  }
}

class _PlaceholderTab extends StatelessWidget {
  const _PlaceholderTab({
    required this.title,
    required this.subtitle,
    this.child,
  });

  final String title;
  final String subtitle;
  final Widget? child;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: const TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.w800,
              color: KubixColors.utnBlue,
            ),
          ),
          const SizedBox(height: 8),
          Text(subtitle, style: const TextStyle(color: KubixColors.muted)),
          if (child != null) ...[
            const SizedBox(height: 24),
            child!,
          ],
        ],
      ),
    );
  }
}
