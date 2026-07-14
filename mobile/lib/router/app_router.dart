import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../auth/auth_state.dart';
import '../features/auth/change_password_page.dart';
import '../features/auth/login_page.dart';
import '../features/auth/pending_page.dart';
import '../features/auth/register_wizard_page.dart';
import '../features/driver/publish_route_map_page.dart';
import '../features/map/trip_map_page.dart';
import '../api/models.dart';
import '../features/shell/driver_shell.dart';
import '../features/shell/passenger_shell.dart';

/// Notifica a go_router cuando cambia el estado de auth.
class GoRouterRefresh extends ChangeNotifier {
  void ping() => notifyListeners();
}

final goRouterRefreshProvider = Provider<GoRouterRefresh>((ref) {
  final refresh = GoRouterRefresh();
  ref.listen<AuthState>(authProvider, (_, __) => refresh.ping());
  return refresh;
});

String homeForRole(String role) {
  switch (role) {
    case 'driver':
      return '/driver';
    case 'passenger':
      return '/passenger';
    default:
      return '/login';
  }
}

final appRouterProvider = Provider<GoRouter>((ref) {
  final refresh = ref.watch(goRouterRefreshProvider);

  return GoRouter(
    initialLocation: '/login',
    refreshListenable: refresh,
    redirect: (context, state) {
      final auth = ref.read(authProvider);
      final loc = state.matchedLocation;
      final loggingIn = loc == '/login';
      final registering = loc == '/register';
      final pending = loc == '/pending';
      final changingPassword = loc == '/change-password';
      final publicRoute = loggingIn || registering || pending;

      if (auth.status == AuthStatus.unknown) {
        // Mantener splash implícito en login mientras restaura sesión.
        return null;
      }

      if (auth.status == AuthStatus.unauthenticated) {
        if (publicRoute) return null;
        return '/login';
      }

      final user = auth.user!;
      if (auth.mustChangePassword && !changingPassword) {
        return '/change-password';
      }
      if (!auth.mustChangePassword && changingPassword) {
        return homeForRole(user.role);
      }

      if (loggingIn || registering) {
        return homeForRole(user.role);
      }

      if (loc.startsWith('/driver') && !user.isDriver) {
        return homeForRole(user.role);
      }
      if (loc.startsWith('/passenger') && !user.isPassenger) {
        return homeForRole(user.role);
      }

      if (loc == '/') {
        return homeForRole(user.role);
      }

      return null;
    },
    routes: [
      GoRoute(
        path: '/',
        redirect: (_, __) => '/login',
      ),
      GoRoute(
        path: '/login',
        builder: (_, __) => const LoginPage(),
      ),
      GoRoute(
        path: '/register',
        builder: (_, __) => const RegisterWizardPage(),
      ),
      GoRoute(
        path: '/pending',
        builder: (_, __) => const PendingPage(),
      ),
      GoRoute(
        path: '/change-password',
        builder: (_, __) => const ChangePasswordPage(),
      ),
      GoRoute(
        path: '/passenger',
        builder: (_, __) => const PassengerShell(),
      ),
      GoRoute(
        path: '/driver',
        builder: (_, __) => const DriverShell(),
      ),
      GoRoute(
        path: '/driver/publish-route',
        builder: (_, __) => const PublishRouteMapPage(),
      ),
      GoRoute(
        path: '/trips/:tripId/map',
        builder: (context, state) {
          final tripId = state.pathParameters['tripId']!;
          final extra = state.extra;
          final seed = extra is TripMapSeed ? extra : null;
          return TripMapPage(tripId: tripId, seed: seed);
        },
      ),
    ],
  );
});
