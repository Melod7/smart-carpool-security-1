plugins {
    id("com.android.application")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

fun dartDefinesMap(): Map<String, String> {
    val raw = project.findProperty("dart-defines") as? String ?: return emptyMap()
    return raw.split(",")
        .mapNotNull { entry ->
            if (entry.isBlank()) return@mapNotNull null
            val decoded = String(java.util.Base64.getDecoder().decode(entry))
            val idx = decoded.indexOf('=')
            if (idx <= 0) return@mapNotNull null
            decoded.substring(0, idx) to decoded.substring(idx + 1)
        }
        .toMap()
}

android {
    namespace = "com.example.kubix_mobile"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    defaultConfig {
        // TODO: Specify your own unique Application ID (https://developer.android.com/studio/build/application-id.html).
        applicationId = "com.example.kubix_mobile"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName

        val fromDart = dartDefinesMap()["MAPS_API_KEY"]
        val fromEnv = System.getenv("MAPS_API_KEY")
        val fromProps = (project.findProperty("MAPS_API_KEY") as? String)?.trim()
        val mapsApiKey = listOf(fromDart, fromEnv, fromProps)
            .firstOrNull { !it.isNullOrBlank() }
            ?: ""
        manifestPlaceholders["MAPS_API_KEY"] = mapsApiKey
    }

    buildTypes {
        release {
            // TODO: Add your own signing config for the release build.
            // Signing with the debug keys for now, so `flutter run --release` works.
            signingConfig = signingConfigs.getByName("debug")
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}
