import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';

/// Coordenadas GPS para disparar SOS.
typedef SosCoords = ({double lat, double lng});

/// Abstracción de ubicación (inyectable en tests).
abstract class SosLocationSource {
  Future<SosCoords> getCurrentCoords();
}

class GeolocatorSosLocationSource implements SosLocationSource {
  @override
  Future<SosCoords> getCurrentCoords() async {
    final serviceEnabled = await Geolocator.isLocationServiceEnabled();
    if (!serviceEnabled) {
      throw const SosLocationException(
        'Activa el GPS del dispositivo para enviar la alerta.',
      );
    }

    var permission = await Geolocator.checkPermission();
    if (permission == LocationPermission.denied) {
      permission = await Geolocator.requestPermission();
    }

    if (permission == LocationPermission.denied) {
      throw const SosLocationException(
        'Se necesita permiso de ubicación para enviar la alerta SOS.',
      );
    }
    if (permission == LocationPermission.deniedForever) {
      throw const SosLocationException(
        'El permiso de ubicación está bloqueado. Actívalo en Ajustes.',
      );
    }

    final position = await Geolocator.getCurrentPosition(
      locationSettings: const LocationSettings(
        accuracy: LocationAccuracy.high,
        timeLimit: Duration(seconds: 15),
      ),
    );
    return (lat: position.latitude, lng: position.longitude);
  }
}

class SosLocationException implements Exception {
  const SosLocationException(this.message);

  final String message;

  @override
  String toString() => message;
}

final sosLocationSourceProvider = Provider<SosLocationSource>((ref) {
  return GeolocatorSosLocationSource();
});
