using AlbionDataAvalonia.Auth.Models;
using AlbionDataAvalonia.DB;
using AlbionDataAvalonia.Settings;
using AlbionDataAvalonia.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using Serilog;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AlbionDataAvalonia.Auth.Services
{
    public class AuthService
    {
        private readonly PlayerState _playerState;
        private readonly SettingsManager _settingsManager;
        private readonly LocalContext _dbContext;

        private FirebaseAuthResponse? _firebaseUser = null;

        public Action<FirebaseAuthResponse?>? FirebaseUserChanged;

        public string? FirebaseUserId => _firebaseUser?.LocalId;

        public FirebaseAuthResponse? CurrentFirebaseUser => _firebaseUser;

        public AuthService(SettingsManager settingsManager, PlayerState playerState)
        {
            _settingsManager = settingsManager;
            _playerState = playerState;
            _dbContext = new LocalContext();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            }
        }

        private async void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    switch (e.Mode)
                    {
                        case PowerModes.Suspend:
                            break;
                        case PowerModes.Resume:
                            Log.Information("System resumed from sleep. Waiting 10 seconds before re-validating the stored token.");
                            await Task.Delay(TimeSpan.FromSeconds(10));
                            await ForceTokenRefreshAsync();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error handling power mode change event");
                }
            }
        }

        public async Task<bool> TryAutoLoginAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var storedAuth = await _dbContext.UserAuth.FirstOrDefaultAsync(cancellationToken);
                if (storedAuth != null && !string.IsNullOrEmpty(storedAuth.RefreshToken))
                {
                    Log.Debug($"Found stored token for user: {storedAuth.UserId}. Validating with the server.");
                    var profile = await GetTrimsSilverProfileAsync(storedAuth.RefreshToken, cancellationToken);
                    UpdateFirebaseUser(profile, storedAuth.RefreshToken);
                    OnFirebaseUserChanged(_firebaseUser);
                    Log.Information($"Auto-login succeeded for user: {_firebaseUser?.HiddenEmail}");
                    return true;
                }
            }
            catch (AuthServiceException ex) when (ex.IsInvalidToken)
            {
                Log.Warning("Stored token rejected during auto-login. Initiating logout.");
                await LogOut();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Auto-login failed");
            }
            return false;
        }

        // TrimsSilver API tokens are long-lived and don't expire on a schedule, so there is
        // nothing to refresh — this only confirms a token is currently loaded. forceRefresh
        // is kept for call-site compatibility with the old Firebase-refresh contract.
        public Task<bool> EnsureValidTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            var hasToken = _firebaseUser != null && !string.IsNullOrEmpty(_firebaseUser.IdToken);
            return Task.FromResult(hasToken);
        }

        public async Task<bool> TryRecoverFromUnauthorizedAsync(CancellationToken cancellationToken = default)
        {
            var recovered = await RevalidateTokenAsync(cancellationToken);
            if (!recovered)
            {
                Log.Warning("Failed to recover from unauthorized response. User may need to sign in again.");
            }

            return recovered;
        }

        // A bearer token is either still accepted by the server or it isn't — there is no
        // refresh grant to redeem, so "recovery" just re-checks and logs out on a confirmed
        // rejection (401), while leaving the session alone on a transient network error.
        private async Task<bool> RevalidateTokenAsync(CancellationToken cancellationToken)
        {
            if (_firebaseUser == null || string.IsNullOrEmpty(_firebaseUser.IdToken))
            {
                return false;
            }

            try
            {
                var profile = await GetTrimsSilverProfileAsync(_firebaseUser.IdToken, cancellationToken);
                UpdateFirebaseUser(profile, _firebaseUser.IdToken);
                OnFirebaseUserChanged(_firebaseUser);
                return true;
            }
            catch (AuthServiceException ex) when (ex.IsInvalidToken)
            {
                Log.Warning("Token rejected by server ({StatusCode}). Logging out user.", ex.StatusCode);
                await LogOut();
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Token validation failed");
                return false;
            }
        }

        private async Task StoreRefreshToken(string userId, string token, CancellationToken cancellationToken = default)
        {
            // Remove any existing tokens
            var existingAuth = await _dbContext.UserAuth.FirstOrDefaultAsync(cancellationToken);
            if (existingAuth != null)
            {
                _dbContext.UserAuth.Remove(existingAuth);
            }

            // Store the new token
            var userAuth = new UserAuth
            {
                UserId = userId,
                RefreshToken = token
            };
            await _dbContext.UserAuth.AddAsync(userAuth);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task SignInAsync()
        {
            Log.Debug("Starting sign in...");
            try
            {
                // Start listening for the redirect in a separate task
                var tokenTask = HandleRedirectAndGetTokenAsync();

                // Open the browser to the server's Discord sign-in + consent page
                OpenBrowserForSignIn();

                // Await the token retrieval
                var token = await tokenTask;

                var profile = await GetTrimsSilverProfileAsync(token);
                UpdateFirebaseUser(profile, token);

                await StoreRefreshToken(_firebaseUser!.LocalId, token);

                OnFirebaseUserChanged(_firebaseUser);

                Log.Information($"User signed in: {_firebaseUser?.HiddenEmail}");
            }
            catch (Exception ex)
            {
                Log.Error($"Sign-in failed: {ex.Message}");
                throw;
            }
        }

        public void OpenBrowserForSignIn()
        {
            var authUrl = $"{_settingsManager.AppSettings.TrimsSilverAuthUrl}" +
                          $"?redirect_uri={Uri.EscapeDataString(_settingsManager.AppSettings.TrimsSilverAuthRedirectUri)}";

            // Open the browser for the user to authenticate
            Process.Start(new ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });

            Log.Information("Browser opened for Discord sign-in.");
        }

        private async Task<TrimsSilverProfile> GetTrimsSilverProfileAsync(string token, CancellationToken cancellationToken = default)
        {
            var url = $"{_settingsManager.AppSettings.TrimsSilverIngestApiBase.TrimEnd('/')}/me";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var profile = await response.Content.ReadFromJsonAsync<TrimsSilverProfile>(cancellationToken: cancellationToken);
                if (profile == null)
                {
                    throw new AuthServiceException("TrimsSilver server returned an empty profile response.");
                }
                return profile;
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw AuthServiceException.TokenRejectedError(response.StatusCode, errorContent);
            }
            throw AuthServiceException.ProfileFetchError(response.StatusCode, errorContent);
        }

        private void UpdateFirebaseUser(TrimsSilverProfile profile, string token)
        {
            _firebaseUser = new FirebaseAuthResponse
            {
                LocalId = profile.Id,
                Email = profile.Email ?? string.Empty,
                FullName = profile.Name ?? string.Empty,
                PhotoUrl = profile.Image ?? string.Empty,
                EmailVerified = !string.IsNullOrEmpty(profile.Email),
                IdToken = token,
                RefreshToken = token
            };
        }

        private async Task<string> HandleRedirectAndGetTokenAsync()
        {
            Log.Debug("Listening for the auth redirect...");
            using var listener = new HttpListener();
            listener.Prefixes.Add(_settingsManager.AppSettings.TrimsSilverAuthRedirectUri);
            listener.Start();

            try
            {
                // Wait for the redirect
                var context = await listener.GetContextAsync();
                var query = context.Request.QueryString;

                var token = query["token"];
                var error = query["error"];

                if (!string.IsNullOrEmpty(error))
                {
                    throw new InvalidOperationException($"Sign-in error: {error}");
                }

                Log.Debug("Received token from the auth redirect.");

                if (string.IsNullOrEmpty(token))
                {
                    throw new InvalidOperationException("Token not found in the redirect.");
                }

                // Send a response back to the browser
                using var response = context.Response;
                string responseString = "TrimsSilver sign-in successful. You can close this window.";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer);
                response.OutputStream.Close();

                return token;
            }
            catch (Exception ex)
            {
                Log.Error($"Error during token handling: {ex.Message}");
                throw;
            }
            finally
            {
                listener.Stop();
            }
        }

        public async Task ForceTokenRefreshAsync(CancellationToken cancellationToken = default)
        {
            if (_firebaseUser == null)
            {
                Log.Debug("Cannot re-validate token: No user is logged in.");
                return;
            }

            Log.Information("Re-validating stored token...");
            var recovered = await RevalidateTokenAsync(cancellationToken);
            if (!recovered)
            {
                Log.Warning("Token re-validation did not succeed.");
            }
        }

        public async Task LogOut()
        {
            // Clear the user information
            _firebaseUser = null;

            _playerState.UploadToTrimsSilverOnly = false;

            // Clear the table
            var userAuths = await _dbContext.UserAuth.ToListAsync();
            _dbContext.UserAuth.RemoveRange(userAuths);
            await _dbContext.SaveChangesAsync();
            OnFirebaseUserChanged(_firebaseUser);

            Log.Information("User has been logged out.");
        }

        private void OnFirebaseUserChanged(FirebaseAuthResponse? user)
        {
            FirebaseUserChanged?.Invoke(user);
        }
    }
}
