plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.glowbook.app"
    compileSdk = 34

    defaultConfig {
        applicationId = "com.glowbook.app"
        minSdk = 24
        targetSdk = 34
        versionCode = 4
        versionName = "1.3"
    }

    signingConfigs {
        create("release") {
            storeFile = rootProject.file("glowbook-release.jks")
            storePassword = "glowbook123"
            keyAlias = "glowbook"
            keyPassword = "glowbook123"
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            signingConfig = signingConfigs.getByName("release")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }
}

dependencies {
    // none — only Android SDK WebView
}
