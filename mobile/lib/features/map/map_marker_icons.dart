import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

import '../../theme/kubix_theme.dart';

/// Iconos custom para marcadores del mapa (auto / persona / pin de abordaje).
///
/// Cada pasajero tiene un color estable; su ubicación en vivo y su punto de
/// abordaje usan el mismo color.
class MapMarkerIcons {
  MapMarkerIcons._();

  /// Paleta “tomate” primero, luego otros para varios pasajeros.
  static const passengerPalette = <Color>[
    Color(0xFFE65100), // tomate
    Color(0xFF00838F), // teal
    Color(0xFF6A1B9A), // violeta
    Color(0xFFC62828), // rojo
    Color(0xFF2E7D32), // verde
    Color(0xFF4527A0), // índigo
    Color(0xFFEF6C00), // naranja
    Color(0xFF00695C), // verde azulado
  ];

  static BitmapDescriptor? driver;
  static BitmapDescriptor? selfDriver;
  static BitmapDescriptor? selfPassenger;
  static final Map<int, BitmapDescriptor> _people = {};
  static final Map<int, BitmapDescriptor> _boardingPins = {};

  static bool get isReady =>
      driver != null &&
      selfDriver != null &&
      selfPassenger != null &&
      _people.length == passengerPalette.length &&
      _boardingPins.length == passengerPalette.length;

  static Future<void> ensureLoaded() async {
    if (isReady) return;
    driver = await _paintCar(KubixColors.utnBlue);
    selfDriver = await _paintCar(KubixColors.eco);
    selfPassenger = await _paintPerson(KubixColors.eco);
    for (var i = 0; i < passengerPalette.length; i++) {
      final color = passengerPalette[i];
      _people[i] = await _paintPerson(color);
      _boardingPins[i] = await _paintBoardingPin(color);
    }
  }

  static int colorIndexForUser(String userId) =>
      userId.hashCode.abs() % passengerPalette.length;

  static Color colorForUser(String userId) =>
      passengerPalette[colorIndexForUser(userId)];

  static BitmapDescriptor forParticipant({
    required String userId,
    required bool isDriver,
    required bool isBoardingPoint,
    required bool isSelf,
  }) {
    if (isDriver) {
      return (isSelf ? selfDriver : driver) ??
          BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueAzure);
    }
    final idx = colorIndexForUser(userId);
    if (isBoardingPoint) {
      return _boardingPins[idx] ??
          BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueOrange);
    }
    if (isSelf) {
      return selfPassenger ??
          BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueGreen);
    }
    return _people[idx] ??
        BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueOrange);
  }

  /// Pin de abordaje (fallback semilla sin userId).
  static BitmapDescriptor defaultBoardingPin() =>
      _boardingPins[0] ??
      BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueOrange);

  static Future<BitmapDescriptor> _paintCar(Color color) {
    return _toDescriptor((canvas, size) {
      final cx = size / 2;
      final cy = size / 2;

      final disc = Paint()..color = color.withValues(alpha: 0.18);
      canvas.drawCircle(Offset(cx, cy), size * 0.46, disc);

      final body = Paint()..color = color;
      final accent = Paint()..color = Colors.white.withValues(alpha: 0.92);

      final bodyRect = RRect.fromRectAndRadius(
        Rect.fromCenter(
          center: Offset(cx, cy + size * 0.04),
          width: size * 0.62,
          height: size * 0.28,
        ),
        Radius.circular(size * 0.08),
      );
      canvas.drawRRect(bodyRect, body);

      final cabin = RRect.fromRectAndRadius(
        Rect.fromCenter(
          center: Offset(cx, cy - size * 0.08),
          width: size * 0.38,
          height: size * 0.22,
        ),
        Radius.circular(size * 0.06),
      );
      canvas.drawRRect(cabin, body);

      canvas.drawRRect(
        RRect.fromRectAndRadius(
          Rect.fromCenter(
            center: Offset(cx - size * 0.08, cy - size * 0.08),
            width: size * 0.12,
            height: size * 0.12,
          ),
          Radius.circular(size * 0.02),
        ),
        accent,
      );
      canvas.drawRRect(
        RRect.fromRectAndRadius(
          Rect.fromCenter(
            center: Offset(cx + size * 0.08, cy - size * 0.08),
            width: size * 0.12,
            height: size * 0.12,
          ),
          Radius.circular(size * 0.02),
        ),
        accent,
      );

      final wheel = Paint()..color = const Color(0xFF1A2438);
      canvas.drawCircle(
        Offset(cx - size * 0.18, cy + size * 0.18),
        size * 0.07,
        wheel,
      );
      canvas.drawCircle(
        Offset(cx + size * 0.18, cy + size * 0.18),
        size * 0.07,
        wheel,
      );
      canvas.drawCircle(
        Offset(cx - size * 0.18, cy + size * 0.18),
        size * 0.03,
        accent,
      );
      canvas.drawCircle(
        Offset(cx + size * 0.18, cy + size * 0.18),
        size * 0.03,
        accent,
      );
    });
  }

  static Future<BitmapDescriptor> _paintPerson(Color color) {
    return _toDescriptor((canvas, size) {
      final cx = size / 2;
      final cy = size / 2;

      final disc = Paint()..color = color.withValues(alpha: 0.18);
      canvas.drawCircle(Offset(cx, cy), size * 0.46, disc);

      final fill = Paint()..color = color;
      canvas.drawCircle(Offset(cx, cy - size * 0.16), size * 0.12, fill);

      final torso = RRect.fromRectAndRadius(
        Rect.fromCenter(
          center: Offset(cx, cy + size * 0.1),
          width: size * 0.36,
          height: size * 0.34,
        ),
        Radius.circular(size * 0.14),
      );
      canvas.drawRRect(torso, fill);
    });
  }

  /// Pin de mapa (gota) del color del pasajero = punto de abordaje.
  static Future<BitmapDescriptor> _paintBoardingPin(Color color) {
    return _toDescriptor(
      (canvas, size) {
        final cx = size / 2;
        final tipY = size * 0.88;
        final headCy = size * 0.38;
        final headR = size * 0.28;

        final fill = Paint()..color = color;
        final stroke = Paint()
          ..color = Colors.white
          ..style = PaintingStyle.stroke
          ..strokeWidth = size * 0.04;

        final path = Path()
          ..moveTo(cx, tipY)
          ..quadraticBezierTo(
            cx - headR * 1.15,
            headCy + headR * 0.35,
            cx - headR,
            headCy,
          )
          ..arcToPoint(
            Offset(cx + headR, headCy),
            radius: Radius.circular(headR),
            clockwise: true,
          )
          ..quadraticBezierTo(
            cx + headR * 1.15,
            headCy + headR * 0.35,
            cx,
            tipY,
          )
          ..close();

        canvas.drawPath(path, fill);
        canvas.drawPath(path, stroke);

        // Punto blanco = “parada”
        canvas.drawCircle(
          Offset(cx, headCy),
          size * 0.1,
          Paint()..color = Colors.white,
        );
      },
      width: 36,
      height: 48,
      logical: 72,
    );
  }

  static Future<BitmapDescriptor> _toDescriptor(
    void Function(Canvas canvas, double size) paint, {
    double width = 40,
    double height = 40,
    double logical = 64,
  }) async {
    final recorder = ui.PictureRecorder();
    final canvas = Canvas(recorder);
    paint(canvas, logical);
    final picture = recorder.endRecording();
    final image = await picture.toImage(logical.toInt(), logical.toInt());
    final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
    image.dispose();
    return BitmapDescriptor.bytes(
      bytes!.buffer.asUint8List(),
      imagePixelRatio: 2,
      width: width,
      height: height,
    );
  }
}
