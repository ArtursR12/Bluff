# Bluff — MVP Publishing Checklist

## ⚠️ iOS Hard Requirement: You Need a Mac

Xcode only runs on macOS. Unity exports an Xcode project, then Xcode builds the final `.ipa`.
**You cannot build for iOS on Windows.**

Options if you don't have a Mac:
- **Codemagic** (codemagic.io) — free tier, builds iOS in the cloud, uploads to App Store
- **Unity Cloud Build** — ~$9/month
- Borrow a friend's Mac for 1 hour (just need to run the build)

---

## Status Summary

| Area | Status | Effort |
|------|--------|--------|
| iOS Post-build script | ✅ Created | Done |
| Android Manifest | ✅ Created | Done |
| Bundle Identifier | ❌ Not set | 2 min — Unity Editor |
| App Icon | ❌ Missing | 30 min — design + import |
| Apple Developer Account | ❌ Need to register | $99/yr |
| iOS Signing / Team ID | ❌ Not configured | 5 min — Unity Editor |
| Target iOS version | ❌ Not set | 1 min |
| Audio clips | ❌ No .mp3/.wav files | Variable |
| Splash screen | ⚠️ Unity default | Optional |
| IL2CPP backend | ✅ Configured | Done |
| Portrait lock | ✅ In code | Done |
| Photon App ID | ⚠️ Check is valid | 2 min |

---

---

## iOS Publishing Steps

### iOS Step 1 — Apple Developer Account

Register at developer.apple.com — **$99/year**, paid before you can submit anything.
Takes 1–2 days to activate.

You'll need it for:
- Signing certificate (to prove it's your app)
- Provisioning profile (to install on devices / submit to App Store)
- App Store Connect access

---

### iOS Step 2 — Bundle Identifier (Unity Editor, iOS tab)

**Player Settings → Player → iOS tab → Other Settings**

| Setting | Value |
|---------|-------|
| Bundle Identifier | `com.artursrizijs.bluff` |
| Version | `1.0` |
| Build | `1` |
| Target minimum iOS Version | `14.0` |
| Architecture | ARM64 |
| Scripting Backend | IL2CPP |

---

### iOS Step 3 — Signing (Unity Editor, on Mac only)

**Player Settings → Player → iOS tab → Other Settings**

- **Automatically Sign**: ✅ (easiest for first build)
- **Signing Team ID**: paste your 10-character Apple Team ID from developer.apple.com → Account → Membership

Or configure manually after Xcode opens with provisioning profile.

---

### iOS Step 4 — App Icon (Unity Editor)

**Player Settings → Player → iOS tab → Icon**

Need these sizes (Unity generates from a single 1024×1024 source):
- Import `icon_1024.png` into `Assets/_Project/Art/Icons/`
- Assign as Default icon — Unity auto-generates all required sizes (20px → 1024px)

**App Store requires exactly 1024×1024 PNG, no alpha, no rounded corners.**

---

### iOS Step 5 — Info.plist (ALREADY DONE ✅)

`Assets/Editor/iOSPostBuild.cs` runs automatically on iOS build and adds:
- `NSLocalNetworkUsageDescription` — Photon Fusion requires this on iOS 14+
- `NSBonjourServices` with `_fusion-peer._tcp` — for local peer discovery

No manual action needed.

---

### iOS Step 6 — Build (on Mac / Codemagic)

1. File → Build Settings → switch to iOS
2. **Build** (not Build and Run) → choose output folder
3. Unity exports an Xcode project folder
4. Open `Unity-iPhone.xcodeproj` in Xcode
5. Select your Apple ID in Signing & Capabilities
6. Product → Archive → Distribute App → App Store Connect

---

### iOS Step 7 — App Store Connect

1. Go to appstoreconnect.apple.com
2. Create new App → iOS → paste bundle ID `com.artursrizijs.bluff`
3. Fill in:
   - **Name**: Bluff — Card Game
   - **Subtitle** (30 chars): "Bet, bluff, and challenge"
   - **Description**: explain Believe / Bluff mechanics, 2–6 players
   - **Screenshots**: required — 6.5" (iPhone 14 Pro Max: 1290×2796) and 5.5" (iPhone 8 Plus: 1242×2208)
   - **App category**: Games → Card
4. Upload build via Xcode or Transporter app
5. Submit for TestFlight first (your own device) → then App Review

---

### iOS Step 8 — TestFlight (Test on your iPhone before public release)

1. Upload build to App Store Connect
2. App Store Connect → TestFlight tab → add your Apple ID as internal tester
3. Install TestFlight app on your iPhone
4. Install the build — full game loop test before public submission

---

### Privacy Policy (Required for App Store)

Apple **requires** a privacy policy URL for any app.
- Minimum: state that Photon servers receive game session data
- No personal data collected (player name stays on-device in PlayerPrefs)

Free generator: app-privacy-policy-generator.firebaseapp.com
Host for free: GitHub Pages (5 minutes to set up)

---

## Android Publishing Steps

## Step 1 — Bundle Identifier (Unity Editor)

**Window → Player Settings → Player → Android tab → Other Settings**

- **Package Name:** `com.artursrizijs.bluff`
  _(must be unique on Google Play — check play.google.com/console first)_
- **Version:** `1.0`
- **Bundle Version Code:** `1` (increment with every Play Store upload)

Same for iOS tab if targeting Apple:
- **Bundle Identifier:** `com.artursrizijs.bluff`

---

## Step 2 — App Icon

### Design spec
- **512 × 512 px PNG** — Google Play store listing
- **1024 × 1024 px PNG** — Apple App Store
- **Foreground layer 108 × 108dp** + **background layer** — Android adaptive icon

### Suggested quick icon
A green felt texture background with a large gold card suit (♠ or ♣) and "BLUFF" in bold gold text.

### Unity setup
**Player Settings → Android → Icon**
1. Import your icon PNG into `Assets/_Project/Art/Icons/`
2. Assign it to **Default Icon** slot (Unity will generate all sizes)
3. For adaptive icon: set separate foreground + background layers

---

## Step 3 — Generate & Configure Keystore

Every Google Play upload must be signed with the same keystore — **never lose this file**.

### Generate (run in terminal)
```bash
keytool -genkey -v \
  -keystore bluff-release.keystore \
  -alias bluff \
  -keyalg RSA \
  -keysize 2048 \
  -validity 10000
```
Follow prompts. Store `bluff-release.keystore` in a safe place (NOT inside the Unity project repo).

### Configure in Unity
**Player Settings → Android → Publishing Settings**
- **Keystore Name:** browse to `bluff-release.keystore`
- **Keystore Password:** your password
- **Key Alias:** `bluff`
- **Key Password:** your password

---

## Step 4 — Target SDK / Build Settings (Unity Editor)

**Player Settings → Android → Other Settings**

| Setting | Value |
|---------|-------|
| Minimum API Level | 25 (Android 7.1) |
| Target API Level | 34 (Android 14) — required by Google Play |
| Scripting Backend | IL2CPP ✅ already set |
| Target Architectures | ARM64 ✅ (fine for Play Store) |
| Internet Access | Require |

---

## Step 5 — Fix AndroidManifest.xml Package Name

Open `Assets/Plugins/Android/AndroidManifest.xml` and change:
```xml
package="com.CHANGEME.bluff"
```
to match your Bundle Identifier exactly:
```xml
package="com.artursrizijs.bluff"
```

---

## Step 6 — Audio Clips

AudioManager has 13 clip slots, all unassigned. The game runs silently without them.
Assign in Inspector on the **AudioManager** GameObject in GameScene.

**Free sources:** freesound.org, pixabay.com/sound-effects, zapsplat.com

| Slot | Suggested source clip |
|------|-----------------------|
| `_clipCardClick` | Short paper/card tap — 0.05s |
| `_clipCardDeal` | Card whoosh — 0.2s |
| `_clipBetPlaced` | Chip drop / thump — 0.3s |
| `_clipBelieveCorrect` | Positive chime / coin — 0.4s |
| `_clipBelieveWrong` | Low buzzer / thud — 0.4s |
| `_clipBluffCaught` | Triumphant sting — 0.5s |
| `_clipBluffWrong` | Sad trombone / fail — 0.5s |
| `_clipYourTurn` | Soft notification ping — 0.2s |
| `_clipCountdownTick` | Tick / click — 0.1s |
| `_clipTimerWarning` | Urgent beep — 0.3s |
| `_clipGameWin` | Victory fanfare — 2s |
| `_clipGameLose` | Defeat sting — 1.5s |
| `_clipMenuMusic` | Ambient loop — 60–120s |
| `_clipGameMusic` | Card-game loop — 60–120s |

Import clips into `Assets/_Project/Audio/` as `.mp3` or `.ogg`.
Set **Load Type = Compressed In Memory** for short SFX, **Streaming** for music.

---

## Step 7 — Photon App ID

**Assets/Photon/Fusion/Resources/PhotonAppSettings.asset**
- Verify `App Id Fusion` is set to your real Photon Dashboard App ID
- Check the Fusion plan: free tier allows 20 CCU — fine for MVP

---

## Step 8 — Final Build

### Android (AAB for Google Play)
1. File → Build Settings → Android
2. Switch Platform (if needed)
3. Check **Build App Bundle (Google Play)**
4. Click **Build** → name it `bluff-v1.0.aab`

### Test before upload
- Install on physical device via ADB or share `.apk` for smoke test:
  ```bash
  # Build APK for device testing
  # Uncheck "Build App Bundle" → Build → bluff-v1.0.apk
  adb install bluff-v1.0.apk
  ```

---

## Step 9 — Google Play Console

1. Create account: play.google.com/console ($25 one-time fee)
2. Create app → Internal Testing → upload AAB
3. Fill store listing:
   - **Short description** (80 chars): "A fast multiplayer bluffing card game for 2–6 players"
   - **Full description** (4000 chars): describe gameplay, bet/bluff mechanics
   - **Screenshots**: at least 2 portrait phone screenshots (1080×1920 or similar)
   - **Feature graphic**: 1024×500 px banner
   - **App icon**: 512×512 PNG (same as Step 2)
4. Content rating questionnaire (should be Everyone / PEGI 3)
5. Privacy Policy URL — required; host a simple page or use a generator

---

## Step 10 — Privacy Policy (Required by Both Stores)

Minimum required fields:
- What data is collected (player name via PlayerPrefs — local only)
- Photon data: game session data sent to Photon servers (EU/US)
- No personal data sold or shared

**Quick generator:** app-privacy-policy-generator.firebaseapp.com

Host on GitHub Pages (free): create a repo → `gh-pages` branch → `index.html`

---

## Optional Polish Before Launch

- [ ] Splash screen: import gold card design, disable Unity logo (requires Unity Pro/Plus)
  or keep Unity splash — it's free and acceptable
- [ ] Test on 3+ Android devices (different screen sizes / OS versions)
- [ ] Test offline Practice vs Bots mode
- [ ] Test multiplayer 2-player flow end-to-end with 2 real devices
- [ ] Verify Photon region (set to closest in PhotonAppSettings — `eu` or `us`)

---

## What's Already Done ✅

- IL2CPP scripting backend
- ARM64 architecture
- Portrait orientation locked in NetworkManager + AndroidManifest
- `targetFrameRate = 60`, `sleepTimeout = NeverSleep` (battery-optimized)
- Android back-button double-tap to exit
- SafeArea notch handling
- PlayerPrefs persistence (name, stats, history)
- All game features functional (multiplayer, offline bot, spectator, reconnect)
