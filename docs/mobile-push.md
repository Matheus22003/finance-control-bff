# Mobile push device registration

The BFF stores mobile push registrations separately from Web Push subscriptions at:

- `GET /api/v1/notifications/mobile-devices`
- `POST /api/v1/notifications/mobile-devices`
- `DELETE /api/v1/notifications/mobile-devices/{installationId}/{platform}`

All routes require the authenticated user. A registration is unique per `(userId,
installationId, platform)`, where `platform` is `android` or `ios`. The POST contract accepts
an Expo push token (`ExponentPushToken[...]` or `ExpoPushToken[...]`) plus optional device and
app metadata. The token is stored server-side and is intentionally omitted from list/upsert
responses.

This release only handles registration and revocation. It does not call Expo. The future send
boundary is a server-side provider adapter that reads active tokens for the intended user,
constructs the Expo request, and uses provider credentials held by the BFF. Mobile clients must
not call Expo with BFF/provider credentials. Push remains a hint; clients reread official state
through the BFF after handling a notification.
