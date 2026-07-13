import Flutter
import GoogleMaps
import UIKit

@main
@objc class AppDelegate: FlutterAppDelegate, FlutterImplicitEngineDelegate {
  override func application(
    _ application: UIApplication,
    didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?
  ) -> Bool {
    // Clave Maps: Info.plist GMSApiKey. Usar la misma que --dart-define=MAPS_API_KEY=...
    let key = (Bundle.main.object(forInfoDictionaryKey: "GMSApiKey") as? String)?
      .trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
    if !key.isEmpty {
      GMSServices.provideAPIKey(key)
    }
    return super.application(application, didFinishLaunchingWithOptions: launchOptions)
  }

  func didInitializeImplicitFlutterEngine(_ engineBridge: FlutterImplicitEngineBridge) {
    GeneratedPluginRegistrant.register(with: engineBridge.pluginRegistry)
  }
}
