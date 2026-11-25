# Install the iPhone companion

You can try local sharing with just a Mac and an iPhone running iOS 18 or later. Pairing with a server is optional.

## Build and install

The CI toolchain uses .NET SDK 11.0.100, iOS workload set 11.0.100.1 and Xcode 27.2. For a physical device, configure an Apple signing identity and a provisioning profile for your bundle ID with HealthKit and background delivery enabled. [CI configuration](../.github/workflows/ci.yml) · [Microsoft’s Xcode requirements](https://learn.microsoft.com/en-us/dotnet/ios/troubleshooting/xcode-requirement).

From the repository root, select Xcode and install the workload.

```sh
cd src/HealthSyncy.Mobile/HealthSyncy.Mobile.EntryPoint
export DEVELOPER_DIR=/Applications/Xcode_27.2.app/Contents/Developer
dotnet workload install ios --version 11.0.100.1
```

Connect your iPhone. Replace the three signing values below with your own, then build and launch the companion.

```sh
dotnet build -c Debug -r ios-arm64 -t:Run \
  -p:ApplicationId="your.bundle.id" -p:CodesignKey="<your signing identity>" \
  -p:CodesignProvision="<your provisioning profile>"
```

For a simulator build on Apple silicon, run `dotnet build -c Debug -r iossimulator-arm64`. Test background HealthKit delivery on a physical iPhone.

## Share a summary from your iPhone

Open HealthSyncy, tap **Share today’s summary** and grant read access to the categories you want to share. Choose **Copy** in the iOS share sheet to paste the summary into a message. You can leave the server and token fields empty. If you see “No readable activity yet,” check that Apple Health has data for today and that HealthSyncy can read the relevant categories.

## Pair with your server

After you [deploy the services](self-hosting.md), enter `sync.<domain>` and paste the generated device token. Tap **Pair and sync**, approve the Health categories you want to share, then compare the first snapshot with Apple Health for the same day and time zone. A replacement pairing is saved after a sync uploads at least one readable day. If verification fails, the previous pairing stays active. To check background updates, compare the capture time in Admin after an unlocked sync, a period with the phone locked, and a reconnect after going offline. iOS schedules delivery. [Apple’s observer-query documentation](https://developer.apple.com/documentation/healthkit/hkobserverquery).
