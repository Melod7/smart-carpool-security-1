import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/models.dart';
import '../../auth/auth_state.dart';
import '../../theme/kubix_theme.dart';
import '../map/open_trip_map.dart';
import 'passenger_providers.dart';
import 'trip_labels.dart';
import 'widgets/rating_dialog.dart';

class PaxTripsPage extends ConsumerStatefulWidget {
  const PaxTripsPage({super.key});

  @override
  ConsumerState<PaxTripsPage> createState() => _PaxTripsPageState();
}

class _PaxTripsPageState extends ConsumerState<PaxTripsPage> {
  String _period = 'week';

  @override
  Widget build(BuildContext context) {
    final mine = ref.watch(myTripsProvider(_period));
    final pending = ref.watch(pendingRatingsProvider);
    final eco = ref.watch(ecoSummaryProvider);

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(myTripsProvider(_period));
        ref.invalidate(pendingRatingsProvider);
        ref.invalidate(ecoSummaryProvider);
        await Future.wait([
          ref.read(myTripsProvider(_period).future),
          ref.read(pendingRatingsProvider.future),
          ref.read(ecoSummaryProvider.future),
        ]);
      },
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(
            'Periodo',
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  color: KubixColors.muted,
                  fontWeight: FontWeight.w600,
                ),
          ),
          const SizedBox(height: 8),
          SegmentedButton<String>(
            segments: const [
              ButtonSegment(value: 'week', label: Text('Semana')),
              ButtonSegment(value: 'month', label: Text('Mes')),
              ButtonSegment(value: 'total', label: Text('Total')),
            ],
            selected: {_period},
            onSelectionChanged: (s) => setState(() => _period = s.first),
          ),
          const SizedBox(height: 16),
          mine.when(
            data: (data) => _StatsRow(
              stats: data.stats,
              ecoBalance: eco.asData?.value.gamificationEnabled == true
                  ? eco.asData!.value.balance
                  : null,
            ),
            loading: () => const SizedBox(
              height: 88,
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (e, _) => Text(
              e.toString(),
              style: const TextStyle(color: KubixColors.emergency),
            ),
          ),
          const SizedBox(height: 16),
          pending.when(
            data: (list) {
              if (list.isEmpty) return const SizedBox.shrink();
              return Column(
                children: [
                  for (final item in list) ...[
                    _PendingRatingBanner(
                      pending: item,
                      onRate: () => _rate(item),
                    ),
                    const SizedBox(height: 8),
                  ],
                ],
              );
            },
            loading: () => const SizedBox.shrink(),
            error: (_, __) => const SizedBox.shrink(),
          ),
          const SizedBox(height: 8),
          mine.when(
            data: (data) {
              final upcoming = TripLabels.upcomingTrips(data.trips);
              final history = TripLabels.historyTrips(data.trips);
              return Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _SectionTitle('Próximos'),
                  if (upcoming.isEmpty)
                    const _EmptyHint('No tienes viajes próximos.')
                  else
                    ...upcoming.map(
                      (t) => _TripTile(
                        trip: t,
                        onOpenMap: () =>
                            openTripMapFromMyTrip(context, ref, t),
                      ),
                    ),
                  const SizedBox(height: 16),
                  _SectionTitle('Historial'),
                  if (history.isEmpty)
                    const _EmptyHint('Aún no hay viajes en el historial.')
                  else
                    ...history.map((t) => _TripTile(trip: t)),
                ],
              );
            },
            loading: () => const SizedBox.shrink(),
            error: (_, __) => const SizedBox.shrink(),
          ),
        ],
      ),
    );
  }

  Future<void> _rate(PendingRating pending) async {
    final ok = await showRatingDialog(
      context,
      pending: pending,
      onSubmit: (stars, comment) async {
        await ref.read(passengerApiProvider).submitRating(
              tripId: pending.tripId,
              ratedUserId: pending.ratedUserId,
              stars: stars,
              comment: comment,
            );
      },
    );
    if (ok) {
      invalidatePassengerTrips(ref);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Calificación enviada. ¡Gracias!')),
        );
      }
    }
  }
}

class _StatsRow extends StatelessWidget {
  const _StatsRow({required this.stats, this.ecoBalance});

  final TripStats stats;
  final int? ecoBalance;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Row(
          children: [
            Expanded(
              child: _StatBox(
                label: 'Viajes',
                value: '${stats.trips}',
                icon: Icons.route,
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: _StatBox(
                label: 'Km',
                value: stats.km.toStringAsFixed(1),
                icon: Icons.straighten,
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        Row(
          children: [
            Expanded(
              child: _StatBox(
                label: 'CO₂',
                value: '${stats.co2Kg.toStringAsFixed(1)} kg',
                icon: Icons.eco,
                accent: KubixColors.eco,
              ),
            ),
            if (ecoBalance != null) ...[
              const SizedBox(width: 8),
              Expanded(
                child: _StatBox(
                  label: 'ECT',
                  value: '$ecoBalance',
                  icon: Icons.stars_outlined,
                  accent: KubixColors.gold,
                ),
              ),
            ] else
              const Expanded(child: SizedBox.shrink()),
          ],
        ),
      ],
    );
  }
}

class _StatBox extends StatelessWidget {
  const _StatBox({
    required this.label,
    required this.value,
    required this.icon,
    this.accent = KubixColors.utnBlue,
  });

  final String label;
  final String value;
  final IconData icon;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, color: accent, size: 20),
          const SizedBox(height: 8),
          Text(
            value,
            style: TextStyle(
              fontWeight: FontWeight.w800,
              fontSize: 16,
              color: accent,
            ),
          ),
          Text(
            label,
            style: const TextStyle(fontSize: 12, color: KubixColors.muted),
          ),
        ],
      ),
    );
  }
}

class _PendingRatingBanner extends StatelessWidget {
  const _PendingRatingBanner({required this.pending, required this.onRate});

  final PendingRating pending;
  final VoidCallback onRate;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: KubixColors.gold.withValues(alpha: 0.15),
      borderRadius: BorderRadius.circular(12),
      child: InkWell(
        onTap: onRate,
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(
            children: [
              const Icon(Icons.star, color: KubixColors.gold),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  'Califica a ${pending.ratedUserName}',
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
              ),
              TextButton(onPressed: onRate, child: const Text('Calificar')),
            ],
          ),
        ),
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Text(
        text,
        style: Theme.of(context).textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w700,
              color: KubixColors.utnBlue,
            ),
      ),
    );
  }
}

class _EmptyHint extends StatelessWidget {
  const _EmptyHint(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Text(text, style: const TextStyle(color: KubixColors.muted)),
    );
  }
}

class _TripTile extends StatelessWidget {
  const _TripTile({required this.trip, this.onOpenMap});

  final MyTrip trip;
  final VoidCallback? onOpenMap;

  @override
  Widget build(BuildContext context) {
    final req = TripLabels.requestStatus(trip.requestStatus);
    return Container(
      width: double.infinity,
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            trip.originText,
            style: const TextStyle(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 2),
          Text(
            '→ ${trip.destinationCampusName}',
            style: const TextStyle(color: KubixColors.muted, fontSize: 13),
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 4,
            children: [
              _Chip(TripLabels.formatDateTime(trip.departureAt)),
              _Chip(TripLabels.tripStatus(trip.status)),
              if (req.isNotEmpty) _Chip(req),
              if (trip.status == 'completed')
                _Chip(TripLabels.formatCo2(trip.co2SavedKg)),
            ],
          ),
          if (onOpenMap != null) ...[
            const SizedBox(height: 10),
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                onPressed: onOpenMap,
                icon: const Icon(Icons.map_outlined, size: 18),
                label: const Text('Ver mapa'),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: KubixColors.background,
        borderRadius: BorderRadius.circular(6),
      ),
      child: Text(text, style: const TextStyle(fontSize: 12)),
    );
  }
}
