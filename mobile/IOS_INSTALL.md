# Guía de instalación en iOS

Esta guía cubre el simulador de Xcode y un iPhone físico. Un iPhone no instala
archivos APK ni aplicaciones compiladas para el simulador. Para instalar Kubix
en hardware físico, la aplicación debe estar firmada por Apple.

## Elegir el método

- **Simulador local:** recomendado para desarrollo diario.
- **ZIP de GitHub Actions:** permite probar la compilación de CI en un simulador.
- **iPhone desde Xcode:** recomendado para demostraciones en uno o pocos equipos.
- **TestFlight:** recomendado para distribuir la aplicación a varias personas.

Los valores `API_URL` y `GOOGLE_MAPS_API_KEY` se incorporan durante la
compilación. Si se modifica `.env`, hay que volver a ejecutar o compilar la
aplicación.

## Opción 1: simulador de iOS

### Requisitos

- macOS con Xcode y Flutter 3.x.
- iOS Simulator instalado desde Xcode.
- API de Kubix local o desplegada.
- `GOOGLE_MAPS_API_KEY` con **Maps SDK for iOS** habilitado.

### Ejecutar desde el código fuente

Desde la raíz:

```bash
cp .env.example .env       # solo la primera vez
# Configura GOOGLE_MAPS_API_KEY en .env
make sync-env
cd mobile && flutter pub get && cd ..
```

Para usar la API local, deja `MOBILE_API_URL` vacío y ejecuta la API en otra
terminal:

```bash
make api
```

Después inicia el simulador y ejecuta Kubix:

```bash
open -a Simulator
flutter devices
make mobile-ios DEVICE="iPhone 16 Pro"
```

También se puede usar el identificador mostrado por `flutter devices`:

```bash
make mobile DEVICE=<ID_DEL_SIMULADOR>
```

En un simulador, el script configura automáticamente
`API_URL=http://localhost:8080`.

### Instalar el ZIP generado por GitHub Actions

El artefacto `kubix-ios-simulator-app` contiene
`Kubix-UTN-2.0-iOS-Simulator.zip`. No sirve para un iPhone físico.

```bash
open -a Simulator
unzip Kubix-UTN-2.0-iOS-Simulator.zip
xcrun simctl install booted Runner.app
xcrun simctl launch booted com.example.kubixMobile
```

La aplicación utilizará la URL de API y la clave de Google Maps incorporadas
por GitHub Actions.

## Opción 2: instalar en un iPhone desde Xcode

Esta opción sirve para desarrollo, demostraciones y pruebas en uno o pocos
iPhone.

### Requisitos

- Un Mac con Xcode y Flutter instalados.
- Un Apple ID agregado en **Xcode → Settings → Accounts**.
- El iPhone conectado por cable o emparejado para desarrollo inalámbrico.
- iOS 14 o posterior.
- La clave de Google Maps y la URL HTTPS de la API.

Una cuenta gratuita de Apple normalmente firma la aplicación por siete días.
Después se debe volver a instalar desde Xcode. Una membresía de Apple Developer
permite usar perfiles de desarrollo de mayor duración y habilita TestFlight.

### 1. Preparar las variables

Desde la raíz del repositorio:

```bash
cp .env.example .env
```

Configura la clave de mapas y elige una forma de acceder a la API:

```dotenv
GOOGLE_MAPS_API_KEY=tu_clave

# API desplegada:
MOBILE_API_URL=https://d2dgmlbp00gdhh.cloudfront.net

# Para API local, deja MOBILE_API_URL vacío.
```

Con una API local, el Mac y el iPhone deben estar en la misma red Wi-Fi. Ejecuta
`make api`; el servidor escucha en `0.0.0.0:8080` y el script móvil detecta la
IP LAN del Mac. Revisa también que el firewall permita esa conexión.

Luego ejecuta:

```bash
make sync-env
cd mobile
flutter pub get
cd ..
```

### 2. Habilitar el iPhone para desarrollo

1. Conecta el iPhone al Mac.
2. Acepta **Confiar en este ordenador** en el iPhone.
3. Activa **Ajustes → Privacidad y seguridad → Modo de desarrollador**.
4. Reinicia el iPhone si iOS lo solicita.
5. Comprueba que Flutter lo detecta:

```bash
flutter devices
```

### 3. Configurar la firma

Abre el workspace, no el archivo `.xcodeproj`:

```bash
open mobile/ios/Runner.xcworkspace
```

En Xcode:

1. Selecciona **Runner** y luego el target **Runner**.
2. Abre **Signing & Capabilities**.
3. Activa **Automatically manage signing**.
4. Selecciona tu equipo de Apple en **Team**.
   El equipo guardado en el proyecto (`D9L3VGJUY7`) pertenece al entorno de
   desarrollo original y debe sustituirse si no tienes acceso.
5. Si Xcode indica que el identificador ya existe, cambia
   **Bundle Identifier** por uno único, por ejemplo
   `com.tuorganizacion.kubix`.
6. Selecciona el iPhone como dispositivo de ejecución.
7. Pulsa **Run**.

También se puede ejecutar desde la terminal:

```bash
make mobile DEVICE=<UDID_DEL_IPHONE>
```

### 4. Autorizar la aplicación

Si iOS bloquea al desarrollador:

1. Abre **Ajustes → General → VPN y gestión de dispositivos**.
2. Selecciona el Apple ID utilizado para firmar.
3. Pulsa **Confiar**.
4. Vuelve a abrir Kubix.

## Opción 3: distribuir con TestFlight

Esta es la opción recomendada para entregar la aplicación a varias personas sin
conectar cada iPhone al Mac.

Requiere:

- membresía activa de Apple Developer;
- una aplicación creada en App Store Connect;
- certificado y perfil de distribución;
- un Bundle Identifier registrado.

Proceso:

1. Abre `mobile/ios/Runner.xcworkspace`.
2. Selecciona **Any iOS Device (arm64)**.
3. Ejecuta **Product → Archive**.
4. En Organizer, elige **Distribute App → App Store Connect → Upload**.
5. En App Store Connect, habilita la compilación en TestFlight.
6. Agrega testers internos o externos.
7. Los usuarios instalan **TestFlight** desde App Store y aceptan la invitación.

Las compilaciones de TestFlight están disponibles durante 90 días.

El workflow actual no produce un IPA firmado. Para automatizar TestFlight se
deben configurar en GitHub Actions el certificado Apple, el provisioning
profile, sus contraseñas y las credenciales de App Store Connect.

## Problemas frecuentes

- **Untrusted Developer:** autoriza el Apple ID en VPN y gestión de dispositivos.
- **No profiles found:** selecciona un Team y un Bundle Identifier único.
- **Developer Mode disabled:** activa el modo de desarrollador y reinicia.
- **No aparece ningún simulador:** abre Xcode, instala un runtime de iOS y
  ejecuta `open -a Simulator`.
- **El ZIP no se instala:** confirma que existe un simulador iniciado con
  `xcrun simctl list devices`.
- **Google Maps muestra error:** verifica `GOOGLE_MAPS_API_KEY` y ejecuta
  `make sync-env`; habilita Maps SDK for iOS para el Bundle Identifier usado.
- **La API no responde:** confirma que `MOBILE_API_URL` usa HTTPS y vuelve a
  compilar. Para API local, revisa la red Wi-Fi, el firewall y que `make api`
  siga ejecutándose.
- **La app dejó de abrir después de varios días:** la firma gratuita expiró;
  vuelve a ejecutarla desde Xcode.
