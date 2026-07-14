# Kubix mobile

App Flutter para conductores y pasajeros.

La Fase 1 entregó el shell + `API_URL`. **KBX-22+** (bucket B5): auth, registro y shells por rol.

## Requisitos previos

- [Flutter](https://docs.flutter.dev/get-started/install) 3.x (`flutter doctor` limpio para las plataformas que necesites)
- API local corriendo en el puerto `8080` (ver [README](../README.md) raíz)
- **iOS:** Xcode, CocoaPods (`brew install cocoapods`), Apple ID para firmar dispositivos
- **Android:** Android Studio / SDK + un emulador o dispositivo USB con depuración USB

El deployment target mínimo de iOS es **14.0** (`google_maps_flutter_ios`).

## Configuración

```bash
cd mobile
flutter pub get
```

Si faltan `android/` o `ios/`:

```bash
flutter create . --project-name kubix_mobile --platforms=android,ios
```

Luego vuelve a aplicar iOS 14.0 en `ios/Podfile` (`platform :ios, '14.0'`) si Flutter regeneró un target más antiguo.

## API URL por dispositivo

| Target | `API_URL` |
|---|---|
| Emulador Android | `http://10.0.2.2:8080` |
| iOS Simulator | `http://127.0.0.1:8080` |
| Teléfono físico (misma Wi‑Fi que el Mac) | `http://<MAC_LAN_IP>:8080` |

Obtener la IP LAN del Mac:

```bash
ipconfig getifaddr en0
```

## IDs de dispositivo (`-d`)

Listar dispositivos conectados; el **id** es la segunda columna (entre `•`):

```bash
flutter devices
```

Ejemplo de salida:

```text
Fernando’s iPhone (mobile) • 00008110-000A7D020140401E • ios            • iOS 26.5
sdk gphone64 arm64         • emulator-5554              • android-arm64  • Android 15
iPhone 16 (mobile)         • A1B2C3D4-E5F6-...          • ios            • com.apple.CoreSimulator...
macOS (desktop)            • macos                      • darwin-arm64   • macOS ...
Chrome (web)               • chrome                     • web-javascript • Google Chrome ...
```

Usar ese id con `-d`:

```bash
flutter run -d 00008110-000A7D020140401E --dart-define=API_URL=http://192.168.0.106:8080
flutter run -d emulator-5554 --dart-define=API_URL=http://10.0.2.2:8080
flutter run -d chrome --dart-define=API_URL=http://127.0.0.1:8080
```

> **CORS (Chrome):** Flutter web corre en un puerto efímero (`localhost:xxxxx`). En `Development` la API acepta origins `localhost` / `127.0.0.1`. Si ves `CORS error` en Network, reinicia la API tras el fix de CORS y recarga Chrome (no hace falta otro `API_URL`).

También funciona un fragmento único del nombre (`-d chrome`, `-d iphone`).

Si no aparece nada útil:

```bash
# Emuladores Android
flutter emulators
flutter emulators --launch <emulator_id>
flutter devices

# iOS Simulator (requiere un runtime iOS en Xcode → Settings → Platforms)
open -a Simulator
flutter devices
```

## Android (Mac → teléfono o emulador)

### Teléfono físico (recomendado)

1. En el Android: **Ajustes → Acerca del teléfono** → toca 7× **Número de compilación** (activa opciones de desarrollador).
2. **Ajustes → Opciones de desarrollador** → activa **Depuración USB**.
3. Conecta el USB al Mac; acepta “¿Permitir depuración USB?” en el teléfono.
4. Misma Wi‑Fi que el Mac. La API debe estar arriba (`http://<IP-Mac>:8080/health`).
5. Corre:

```bash
# IP LAN del Mac (ej. en0)
ipconfig getifaddr en0

adb devices          # debe listar el dispositivo (no "unauthorized")
flutter devices

cd mobile
flutter run -d <android-id> \
  --dart-define=API_URL=http://192.168.1.180:8080
```

Sustituye `192.168.1.180` por tu IP real. **No uses** `127.0.0.1` ni `localhost` en el teléfono (eso apunta al propio Android).

Si `adb devices` está vacío: prueba otro cable/puerto, o **Revocar autorizaciones USB** en opciones de desarrollador y reconecta.

### Emulador Android

`10.0.2.2` es el alias del localhost del Mac desde el emulador:

```bash
flutter emulators
flutter emulators --launch <emulator_id>
flutter run -d emulator-5554 --dart-define=API_URL=http://10.0.2.2:8080
```

Si no hay AVDs: instala **Android Studio → Device Manager → Create Device**, o SDK Platform + Emulator vía `sdkmanager`.

## iOS

### Simulador

Instalar un runtime iOS en **Xcode → Settings → Platforms**, luego:

```bash
open -a Simulator
flutter devices
flutter run -d <simulator-id> --dart-define=API_URL=http://127.0.0.1:8080
```

### iPhone físico (firmado de una sola vez)

1. Abrir el workspace y configurar un Development Team:

```bash
open ios/Runner.xcworkspace
```

En Xcode: **Runner → Signing & Capabilities → Team** (iniciar sesión con tu Apple ID). Usar un Bundle ID único si hace falta.

2. Confiar el certificado de desarrollador en el teléfono: **Settings → General → VPN & Device Management**.

3. Ejecutar (teléfono y Mac en la misma Wi‑Fi):

```bash
flutter devices
flutter run -d <iphone-id> --dart-define=API_URL=http://192.168.x.x:8080
```

Si `pod install` falla después de clonar:

```bash
cd ios && pod install --repo-update && cd ..
```

### Android + iPhone a la vez

Deja el Android corriendo en una terminal y abre **otra** terminal para el iPhone (misma `API_URL` = IP LAN del Mac):

```bash
# Terminal A (Android) — ya lo tienes
flutter run -d R5CXC28VKTF --dart-define=API_URL=http://192.168.1.180:8080

# Terminal B (iPhone wireless)
flutter run -d 00008110-000A7D020140401E --dart-define=API_URL=http://192.168.1.180:8080
```

Cada sesión tiene su propio hot reload (`r` / `R` en esa terminal). La API en el Mac atiende a ambos.
## Notas

- Sin `-d`, Flutter puede elegir un iPhone físico y fallar si el firmado no está configurado. Preferir `flutter devices` y luego un `-d` explícito.
- `127.0.0.1` en un teléfono físico es el propio teléfono, no tu Mac.
- CocoaPods es obligatorio para builds de plugins iOS/macOS; Chrome/web no lo necesita.

## Google Maps (`MAPS_API_KEY`)

La pantalla de mapa de viaje (KBX-26) usa `google_maps_flutter`. Pasa la clave en el run:

```bash
flutter run -d <device-id> \
  --dart-define=API_URL=http://127.0.0.1:8080 \
  --dart-define=MAPS_API_KEY=AIza...
```

- **Android:** `build.gradle.kts` inyecta `MAPS_API_KEY` desde dart-define (o env / `-PMAPS_API_KEY=`) en el `AndroidManifest`.
- **iOS:** define `MAPS_API_KEY` en el esquema Xcode / `xcconfig` para que `Info.plist` (`GMSApiKey`) y `AppDelegate` la reciban. Restringe la clave por bundle id.
- Sin clave, la app arranca pero el mapa muestra un aviso y el tile nativo fallará.
