# HealthSyncy

Ask “How many steps did I take today?” or “How did my activity change this week?” HealthSyncy reads daily activity totals on your iPhone and lets an authorized MCP client query them on your own server. The companion can also share today’s totals as plain text through the iOS share sheet before you set up a server.

This is a single-person, single-iPhone implementation. Each person runs their own instance. It uses C# and .NET 11, ASP.NET Core, the official MCP C# SDK, and MongoDB. The software is licensed under MIT. MongoDB Community Server has its own [SSPL license](https://www.mongodb.com/legal/licensing/server-side-public-license).

## How it fits together

| Component | Responsibility | Access |
| --- | --- | --- |
| iPhone companion | Asks for HealthKit read permission, calculates daily totals, sends snapshots to your sync host | Selected HealthKit types and a device upload token |
| Sync | Validates uploads and keeps the newest snapshot for each local date and time zone | Writes snapshots and reads the pause setting |
| Connector | Answers daily and seven-day queries through MCP and exposes a daily REST endpoint | Reads snapshots only |
| Admin | Displays activity, capture and receipt times, freshness alerts, and a seven-day history. Lets the owner pause uploads and set the alert threshold | Reads snapshots and manages settings |

The three services live in `src/Services/HealthSyncy.*/HealthSyncy.*.EntryPoint`. The HTTP APIs use versioned attribute-routed controllers with constructor injection. The shared contracts and storage code live in `src/Libraries/HealthSyncy.Core`. The iOS project links the contract source and uses native HealthKit and UIKit bindings. Each service connects directly to MongoDB.

### What gets synced

Steps, active and resting energy (kcal), exercise minutes, stand hours, and the three ring goals when available. The app uses Apple’s activity summaries for the rings and HealthKit statistics for steps and resting energy. For Move Time accounts it can return active energy from the quantity data and leaves the kcal goal empty. Every result includes the local date, IANA time zone, capture time, and server receipt time.

HealthKit runs on the phone. The companion requests hourly background delivery for step and energy changes. iOS schedules the actual wakes, and the phone may be locked, offline or suspended. Opening the app or tapping **Sync now** also refreshes it. The admin status is evaluated when you load the page. Check the capture time when asking about today. [Apple’s background delivery guide](https://developer.apple.com/documentation/healthkit/executing-observer-queries).

Each refresh covers today and the preceding six days in the phone’s current time zone. The server retains earlier snapshots until the operator deletes them. Historical edits or outages outside that window need a separate backfill. A missing value is unknown. A measured zero is zero. A newer snapshot replaces a previous one and may contain lower totals or missing fields. When the entire day is unreadable, the client skips the upload and the previous capture time remains visible. Changing time zone can create a second set of daily buckets. Query the relevant zone and avoid adding overlapping buckets.

## Run the synthetic tests first

Use a .NET 11 SDK, Docker, Bash 4 or newer, OpenSSL, Python 3 and curl. Run these commands from the repository root. The integration suite starts an authenticated, disposable MongoDB 8 container bound to loopback, loads the production role definitions, and removes the container on exit.

```sh
dotnet build HealthSyncy.slnx -c Release
checks/mongo.sh
python3 checks/manifests.py
```

The storage checks cover conflicting concurrent updates, replays, corrections, missing days, and settings. The HTTP suite starts the three services and a temporary loopback-only OAuth issuer. It checks writes, OAuth scope, owner, expiration, audience, MCP discovery and tool calls. It uses generated keys and synthetic metrics. The iOS build runs separately on macOS.

## Deploy to Kubernetes

The scripts target an existing K3s cluster with Traefik, an enforcing NetworkPolicy implementation, a default persistent-volume storage class, `kubectl` access and public HTTPS. Choose a base domain and point `sync`, `connector`, and `admin` under that domain at the ingress. The installer creates private MongoDB networking and one persistent volume each for MongoDB and the admin cookie keys. Set up encrypted storage, Kubernetes secret encryption, and encrypted backups appropriate to your host. The supplied MongoDB deployment is single-instance and uses plain TCP on the restricted cluster network.

1. Configure an HTTPS OIDC provider you control. Register an admin web client with authorization code flow, PKCE and redirect URI `https://admin.<domain>/signin-oidc`. Register a separate OAuth client for ChatGPT with authorization code flow, PKCE S256, and the callback URL shown in your ChatGPT app setup. Configure the provider to accept the MCP resource parameter and issue signed JWT access tokens with audience `https://connector.<domain>/mcp`, the owner’s subject and the `activity.read` scope. The issuer URL must match its discovery document exactly. The connector uses issuer discovery and JWKS. Opaque access tokens will fail.
2. Export `HEALTHSYNCY_DOMAIN`, `OAUTH_ISSUER`, `OWNER_SUBJECT`, `ADMIN_SUBJECT`, `ADMIN_CLIENT_ID`, and `ADMIN_CLIENT_SECRET` from your secret manager. The two subjects are the exact `sub` claims for your account in the respective clients. Set `REPORTING_ZONE` to the phone’s IANA zone so Admin shows the same days. Its default is UTC.
3. Set `DEVICE_TOKEN_FILE` to a new path in a private directory outside the checkout. Run `scripts/bootstrap.sh` once with Bash 4 or newer. It generates the device token, MongoDB credentials and separate Kubernetes Secrets in the `healthsyncy` namespace. The sibling `.bootstrap.json` is a sensitive recovery copy containing credentials. Move both files into protected storage. If initial Secret creation is interrupted, finish creating just the missing objects from that backup with the original credential values.
4. Provision a TLS certificate covering all three hosts as the namespace Secret `healthsyncy-tls`. Use your current certificate automation or `kubectl -n healthsyncy create secret tls healthsyncy-tls --cert=chain.pem --key=key.pem`. The cluster must be able to pull the service images. For GHCR, grant public read access to all three packages or configure the namespace’s registry pull credentials.
5. A push to `main` runs CI and publishes three commit-tagged GHCR images for amd64 and arm64. In GitHub, add a `production` environment, the variable `HEALTHSYNCY_DOMAIN`, and a secret `KUBECONFIG_B64` containing a base64-encoded deployment kubeconfig with access scoped to this namespace. The runner must be able to reach the Kubernetes API. Run **CI/CD** on `main` with **deploy** selected. The job applies the manifests and waits for all four rollouts. New pushes by themselves publish images.
6. To deploy directly, export `IMAGE_PREFIX=ghcr.io/<owner>/<repository>` and `IMAGE_TAG` as the published 40-character commit SHA, then run `scripts/deploy.sh`. Using a previous published SHA redeploys that version. You can inspect all manifests offline with `python3 scripts/render.py --domain example.org --registry ghcr.io/example/healthsyncy --tag <full-sha> --output /tmp/healthsyncy-manifests`.

### Share an existing K3s node

For a fresh install on an existing node, set the production environment variable `HEALTHSYNCY_DEPLOYMENT_PROFILE` to `shared-node`. Set the same shell variable for a manual deployment. To inspect the generated manifests, pass `--shared-node` to `scripts/render.py`.

The profile uses the node's existing `local-path` storage and Traefik ingress. The deployment identity needs read access to the single StorageClass named `local-path` to verify its provisioner. The four containers request 300 millicores and 896 MiB in total. During rolling updates the namespace can use up to 2 CPU cores and 2,304 MiB. The two claims request 8 GiB and 64 MiB on the node. Check free disk space on the backing filesystem and monitor it as usage grows. Local Path Provisioner does not enforce a filesystem size limit.

The profile denies pod traffic by default. It admits application traffic from the standard K3s Traefik pods in `kube-system` and allows the three applications to reach their private MongoDB. The apps use standard K3s CoreDNS. Admin and Connector also need a publicly reachable HTTPS OAuth issuer. Verify the cluster enforces NetworkPolicy and that an ordinary pod in another namespace cannot connect directly to the database or app ports before sending personal data. The host and cluster administrators retain access. Restoring after loss of the node requires an independent backup.

### Connect the phone

On a Mac, install the .NET 11 iOS workload and its matching Xcode release. CI uses SDK 11.0.100, workload set 11.0.100.1 and Xcode 27.2. Configure an Apple signing identity, bundle ID and provisioning profile for your device with the HealthKit and background delivery capabilities. The app targets iPhone, iOS 18 or later. Replace the example bundle ID for a signed device build. [Microsoft’s Xcode requirements](https://learn.microsoft.com/en-us/dotnet/ios/troubleshooting/xcode-requirement).

```sh
cd mobile
export DEVELOPER_DIR=/Applications/Xcode_27.2.app/Contents/Developer
dotnet workload install ios --version 11.0.100.1
dotnet build HealthSyncy.iOS/HealthSyncy.iOS.csproj -c Debug -r iossimulator-arm64
dotnet build HealthSyncy.iOS/HealthSyncy.iOS.csproj -c Debug -r ios-arm64 -t:Run \
  -p:ApplicationId="your.bundle.id" -p:CodesignKey="<your signing identity>" \
  -p:CodesignProvision="<your provisioning profile>"
```

On your iPhone, enter `https://sync.<domain>` and the generated device token. Tap **Pair and sync**, approve the Health categories you want to share, then compare the first snapshot with Apple Health for the same day and time zone. Check an unlocked-device sync, a delayed sync after locking, and a reconnect after going offline before relying on automatic updates. The simulator can build the app. Background HealthKit delivery needs a physical device. [Apple’s observer-query documentation](https://developer.apple.com/documentation/healthkit/hkobserverquery).

### Connect ChatGPT

On ChatGPT web, open Plugins → + → Add custom MCP server if your account or workspace offers it. Choose OAuth and enter `https://connector.<domain>/mcp` and your registered ChatGPT OAuth client details. Approve the owner account and enable the app in a chat. Try “Get my activity for YYYY-MM-DD in America/New_York. Include the capture time.” The service is independent of the ChatGPT Health account feature. OpenAI documents custom MCP server setup on the web. Availability depends on your account and workspace. In the native iPhone app, use the companion’s **Share today’s summary** and the iOS **Copy** action to paste formatted text into a regular chat. The share action works with local HealthKit permission even before pairing. [Connecting custom MCP servers in ChatGPT](https://help.openai.com/en/articles/12584461-connecting-custom-mcp-servers-in-chatgpt).

| Interface | Calls |
| --- | --- |
| Phone upload | `PUT /api/v1/days/{yyyy-MM-dd}` on Sync with the device bearer token |
| REST read | `GET /api/v1/activity/{yyyy-MM-dd}?timeZone=America/New_York` on Connector with an owner access token |
| MCP | `get_activity_day(day, timeZone)` and `get_activity_week(endingOn, timeZone)` through `/mcp` |
| OAuth discovery | `/.well-known/oauth-protected-resource/mcp` on Connector |
| Operations | `/healthz` liveness and `/readyz` Mongo connectivity on each service |

An identical upload is safe to replay. An older capture is superseded. Conflicting values with the same millisecond timestamp receive HTTP 409. A paused service returns 423 for incoming uploads. An in-flight upload can complete while you pause. The device token has upload access only. Admin uses an owner-checked OIDC session and antiforgery protection. The connector validates the owner, issuer, audience, lifetime and requested scope before serving activity.

## Privacy and operations

The server stores daily totals and timestamps. Raw samples, GPS routes, medical records and Apple credentials are outside the schema. The iPhone keeps its upload token in the device-only Keychain, accessible while unlocked. You choose which MCP client receives an answer. Data returned to a chat is subject to that client’s settings.

To stop sharing, pause Sync in Admin and revoke the HealthKit permissions on the iPhone. Disconnect the ChatGPT app and revoke its authorization at your identity provider to stop new connector sessions. Issued access tokens can remain valid until expiry. To rotate the device token, generate a new random 32-byte token, change `UploadTokenSha256` in `sync-config` to its SHA-256 hex digest, restart the sync deployment, then re-pair the phone. Revocation stops future access. Delete existing records and backups separately according to your retention policy.

The project has no independent security audit or established deployment history. Before using personal data, run the synthetic suite locally, test a synthetic snapshot with your own deployed OAuth client and check that only your intended identity can read it. Keep secrets, provisioning profiles, database backups and personal activity out of public issues and source control.
