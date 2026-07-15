import Flutter
import GoogleMaps
import UIKit

@main
@objc class AppDelegate: FlutterAppDelegate, FlutterImplicitEngineDelegate {
  override func application(
    _ application: UIApplication,
    didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?
  ) -> Bool {
    // Clave Maps: Info.plist GMSApiKey ← ios/Flutter/MapsSecrets.xcconfig (sync desde .env).
    let raw = (Bundle.main.object(forInfoDictionaryKey: "GMSApiKey") as? String)?
      .trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
    let key = (raw.isEmpty || raw.hasPrefix("$(")) ? "" : raw
    if !key.isEmpty {
      GMSServices.provideAPIKey(key)
    } else {
      NSLog("Kubix: GMSApiKey vacío — corre mobile/tool/sync_maps_key_from_env.sh")
    }
    return super.application(application, didFinishLaunchingWithOptions: launchOptions)
  }

  func didInitializeImplicitFlutterEngine(_ engineBridge: FlutterImplicitEngineBridge) {
    GeneratedPluginRegistrant.register(with: engineBridge.pluginRegistry)
  }
}
