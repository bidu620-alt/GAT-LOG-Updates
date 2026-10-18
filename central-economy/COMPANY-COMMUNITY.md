# Empresas BiduLog

`docs/empresas.html` replaces the old company entry at `economia.html#empresa` and links from the existing driver ranking. Authentication continues to use the existing account session.

`company-community.js` adds public directory, period ranking, profile, feed, comments, gallery, image and fleet reads under `/api/companies/`. Writes require a valid, enabled account session and an idempotency key. Personalization and RH role assignment require the actual company owner; managers can decide join requests and dismiss drivers. Only current members can post, like or comment. Reading remains public.

`company-community.sql` is additive. Existing company ids, memberships, wallets and deliveries remain intact. Membership tenures are initialized from current `joined_at` values and maintained by triggers for both legacy and new company routes. Rankings aggregate stored delivery points and distance only inside those intervals, using the same São Paulo month/year boundaries and points tiebreaks as the driver ranking. Historical memberships that were deleted before this migration cannot be reconstructed.

Images are validated PNG/JPEG/WebP, compressed client-side and limited to the existing 256 KiB HTTP body limit. Binary images are served from a separate public route; galleries use posts as their source. Uploads are member-scoped and rate-limited. Text is stored literally and escaped on rendering. Theme colors and Discord invitation URLs are validated server-side.

For the existing Windows local central, run `scripts/install-company-community.ps1` in an administrator PowerShell. It preserves unrelated local Worker fixes, validates syntax before replacing the entrypoint, and restores the prior code if startup verification fails. It does not deploy the paused legacy Cloudflare API. Publish the `docs/` changes through the existing GitHub Pages workflow.

Validation: `node --test central-economy/tests/*.test.mjs`. Community tests cover outsider restrictions, owner-only customization, RH isolation, free creation, media scope, idempotency, membership changes, month/year boundaries, live freshness and HTTP authorization.
