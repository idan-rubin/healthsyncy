# Self-host HealthSyncy

The scripts target an existing K3s cluster with Traefik, an enforcing NetworkPolicy implementation, a default persistent-volume storage class, `kubectl` access and public HTTPS. Choose a base domain and point `sync`, `connector`, and `admin` under that domain at the ingress. The installer creates private MongoDB networking and one persistent volume each for MongoDB and the admin cookie keys. Set up encrypted storage, Kubernetes secret encryption, and encrypted backups appropriate to your host. The supplied MongoDB deployment is single-instance and uses plain TCP on the restricted cluster network.

## Register two sign-in clients

Use an HTTPS OpenID Connect provider you control. The issuer URL must match its discovery document. Connector uses discovery and JWKS to verify signed JWT access tokens.

| Client | Register with the provider |
| --- | --- |
| Admin dashboard | Authorization code flow with PKCE. Use `https://admin.<domain>/signin-oidc` as the redirect URI. |
| ChatGPT | Authorization code flow with PKCE S256 and the callback URL shown during ChatGPT setup. Accept the MCP resource parameter and issue a token with audience `https://connector.<domain>/mcp`, the owner’s subject and the `activity.read` scope. |

## Bootstrap the namespace

From your secret manager, export the following variables in the shell where you will run the installer.

| Variable | Value |
| --- | --- |
| `HEALTHSYNCY_DOMAIN` | Base domain shared by the three public hosts |
| `OAUTH_ISSUER` | Exact issuer URL from the provider’s discovery document |
| `OWNER_SUBJECT`, `ADMIN_SUBJECT` | Your account’s exact `sub` claim for the connector and admin clients, respectively |
| `ADMIN_CLIENT_ID`, `ADMIN_CLIENT_SECRET` | Credentials for the admin web client |
| `REPORTING_ZONE` | Optional IANA time zone for the dashboard. Use your phone’s zone to compare days. Defaults to UTC. |
| `DEVICE_TOKEN_FILE` | New file path in a private directory outside the checkout |

Run `scripts/bootstrap.sh` once from the repository root with Bash 4 or newer. It generates the phone token and database credentials and creates four Kubernetes Secrets in the `healthsyncy` namespace. Protect both the token file and its sibling `.bootstrap.json` recovery copy. If initial Secret creation is interrupted, use that copy to create the missing objects with the original credentials.

Provision a certificate for all three hosts in the namespace Secret `healthsyncy-tls`. Use your certificate automation or run `kubectl -n healthsyncy create secret tls healthsyncy-tls --cert=chain.pem --key=key.pem` with your own certificate chain and key.

## Deploy a published revision

When a push to `main` changes the services, their images or the deployment files, GitHub Actions checks it, publishes Sync, Connector and Admin images for amd64 and arm64 tagged with the full commit SHA, and deploys them. A service change or revert in the push triggers deployment. Other pushes deploy if production is behind or the latest production deployment has no confirmed success. The cluster needs read access to all three packages. Use public packages or configure registry pull credentials in the namespace.

- **From GitHub Actions.** Configure a `production` environment with the variable `HEALTHSYNCY_DOMAIN` and the secret `KUBECONFIG_B64`, a base64-encoded kubeconfig scoped to the deployment. The runner needs network access to the Kubernetes API. To redeploy without a new commit, run **CI/CD** on `main`.
- **From your shell.** Export `IMAGE_PREFIX` (for this repository, `ghcr.io/idan-rubin/healthsyncy`) and `IMAGE_TAG` (a published 40-character commit SHA). Run `scripts/deploy.sh`. To redeploy an earlier revision, use its published SHA.

Both routes wait for MongoDB, Sync, Connector and Admin to roll out. To preview the manifests offline, set the same image variables and run the renderer.

```sh
python3 scripts/render.py --domain example.org --registry "$IMAGE_PREFIX" \
  --tag "$IMAGE_TAG" --output /tmp/healthsyncy-manifests
```

### Share an existing K3s node

For a fresh install on an existing node, set the production environment variable `HEALTHSYNCY_DEPLOYMENT_PROFILE` to `shared-node`. Set the same shell variable for a manual deployment. To inspect the generated manifests, pass `--shared-node` to `scripts/render.py`.

The profile uses the node's existing `local-path` storage and Traefik ingress. The deployment identity needs read access to the single StorageClass named `local-path` to verify its provisioner. The four containers request 300 millicores and 896 MiB in total. During rolling updates the namespace can use up to 2 CPU cores and 2,304 MiB. The two claims request 8 GiB and 64 MiB on the node. Check free disk space on the backing filesystem and monitor it as usage grows. Local Path Provisioner does not enforce a filesystem size limit.

The profile denies pod traffic by default. It admits application traffic from the standard K3s Traefik pods in `kube-system` and allows the three applications to reach their private MongoDB. The apps use standard K3s CoreDNS. Admin and Connector also need a publicly reachable HTTPS OAuth issuer. Verify the cluster enforces NetworkPolicy and that an ordinary pod in another namespace cannot connect directly to the database or app ports before sending personal data. The host and cluster administrators retain access. Restoring after loss of the node requires an independent backup.

## Pair the phone

Install the companion, enter the Sync URL and device token, then compare the first capture in Admin with the same day on the phone. [iPhone guide](iphone.md).

## Connect ChatGPT

On ChatGPT web, open **Plugins → + → Add custom MCP server** with an account or workspace that allows custom servers. Register `https://connector.<domain>/mcp` as a custom server with OAuth, using the OAuth client you registered for ChatGPT. Sign in with the owner account, approve access, and select the connection in a chat. Check that the tool list shows `get_activity_day` and `get_activity_week`. [OpenAI’s setup guide](https://help.openai.com/en/articles/12584461-connecting-custom-mcp-servers-in-chatgpt).

Ask “How many steps did I take yesterday in America/New_York, and when was the total last updated?” Use the phone’s time zone. Compare the returned total and capture time with the same day in Admin.

## Privacy and operations

The server stores daily totals and timestamps. Raw samples, GPS routes, medical records and Apple credentials are outside the schema. The iPhone keeps its upload token in the device-only Keychain, accessible while unlocked. You choose which MCP client receives an answer. Data returned to a chat is subject to that client’s settings.

To stop sharing, pause Sync in Admin and revoke the HealthKit permissions on the iPhone. Disconnect the ChatGPT app and revoke its authorization at your identity provider to stop new connector sessions. Issued access tokens can remain valid until expiry. To rotate the device token, generate a new random 32-byte token, change `UploadTokenSha256` in `sync-config` to its SHA-256 hex digest, restart the sync deployment, then re-pair the phone. Revocation stops future access. Delete existing records and backups separately according to your retention policy.

The project has no independent security audit or established deployment history. Before using personal data, run the synthetic suite locally, test a synthetic snapshot with your own deployed OAuth client and check that only your intended identity can read it. Keep secrets, provisioning profiles, database backups and personal activity out of public issues and source control.
