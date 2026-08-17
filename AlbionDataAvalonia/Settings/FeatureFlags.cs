namespace AlbionDataAvalonia.Settings;

public static class FeatureFlags
{
    // AFM's real backend (api.albionfreemarket.com) doesn't recognize this client's
    // bearer token since the Discord/TrimsSilver auth rewrite — TrimsSilverUploader's
    // token is a TrimsSilver-only opaque token, not the Firebase ID token AFM expects.
    // Portfolio, the Legendary item sale/Discord-posting flow, and EMV lookups from AFM
    // all depend on that auth and would just fail with 401s, so they're disabled here
    // until there's a decision on restoring or replacing that integration.
    // static readonly, not const: a const bool would let the compiler prove branches
    // guarded by this flag are unreachable dead code (e.g. in switch expressions).
    public static readonly bool AfmIntegrationEnabled = false;
}
