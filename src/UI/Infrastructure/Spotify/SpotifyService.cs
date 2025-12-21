using System.Diagnostics;
using System.Net.Http.Headers;
using UI.Features.Shared.Domain;
using UI.Infrastructure.Observability;
using UI.Infrastructure.ReccoBeats;

namespace UI.Infrastructure.Spotify;

public sealed class SpotifyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SpotifyService> _logger;
    private readonly SpotifyAuthSessionManager _spotifyAuthSessionManager;
    private readonly SpotifyAuthService _spotifyAuthService;
    private readonly ReccoBeatsService _reccoBeatsService;

    public SpotifyService(
        HttpClient httpClient,
        ILogger<SpotifyService> logger,
        SpotifyAuthSessionManager spotifyAuthSessionManager,
        SpotifyAuthService spotifyAuthService,
        ReccoBeatsService reccoBeatsService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _spotifyAuthSessionManager = spotifyAuthSessionManager;
        _spotifyAuthService = spotifyAuthService;
        _reccoBeatsService = reccoBeatsService;

        _httpClient.BaseAddress = new Uri("https://api.spotify.com/v1/");
        UpdateAuthorizationHeader();
    }

    public async Task<List<Playlist>> GetUserPlaylistsAsync()
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("GetUserPlaylists");

        _logger.LogDebug("Fetching user playlists from Spotify API");

        try
        {
            Func<Task<List<Playlist>>> apiCall = async () =>
            {
                UserPlaylistsResponse playlistsResponse = await _httpClient.GetFromJsonAsync<UserPlaylistsResponse>(requestUri: "me/playlists") ?? throw new InvalidOperationException("Response can not be null");

                return playlistsResponse.Items.MapToDomain().ToList();
            };

            List<Playlist> playlists = await ExecuteWithTokenRefreshAsync(apiCall);

            _logger.LogInformation("Successfully fetched {PlaylistCount} playlists", playlists.Count);
            activity?.SetTag("playlist.count", playlists.Count);

            return playlists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user playlists");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    public async Task<IReadOnlyList<Track>> GetPlaylistTracksAsync(string playlistId, int offset, int limit, CancellationToken cancellationToken)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("GetPlaylistTracks");
        activity?.SetTag("playlist.id", playlistId);
        activity?.SetTag("playlist.offset", offset);
        activity?.SetTag("playlist.limit", limit);

        _logger.LogDebug("Fetching tracks for playlist {PlaylistId} (offset: {Offset}, limit: {Limit})",
            playlistId, offset, limit);

        try
        {
            Func<Task<Track[]>> apiCall = async () =>
            {
                string requestUri = $"playlists/{playlistId}/tracks?offset={offset}&limit={limit}";

                PlaylistTrackResponse trackResponse = await _httpClient.GetFromJsonAsync<PlaylistTrackResponse>(requestUri, cancellationToken) ?? throw new InvalidOperationException("Response can not be null");

                SpotifyTrack[] dtoTracks = trackResponse.Items
                                                     .Select(i => i.Track)
                                                     .ToArray();

                // Extract unique artist IDs
                string[] uniqueArtistIds = dtoTracks
                    .Where(t => t.Artists.Count > 0)
                    .Select(t => t.Artists[0].Id)
                    .Distinct()
                    .ToArray();

                // Bulk fetch genres for all unique artists
                Dictionary<string, string> artistGenres = await GetArtistsGenresAsync(uniqueArtistIds, cancellationToken);

                // Fetch audio features from ReccoBeats for all tracks
                string[] trackIds = dtoTracks.Select(t => t.Id).ToArray();
                Dictionary<string, ReccoBeatsAudioFeatures> audioFeatures = await GetAudioFeaturesFromReccoBeatsAsync(trackIds, cancellationToken);

                // Assign genres to tracks
                foreach (SpotifyTrack track in dtoTracks)
                {
                    if (track.Artists.Count > 0)
                    {
                        string artistId = track.Artists[0].Id;
                        track.Genre = artistGenres.GetValueOrDefault(artistId, "unknown");
                    }
                }

                return dtoTracks.MapToDomain(audioFeatures).ToArray();
            };

            Track[] tracks = await ExecuteWithTokenRefreshAsync(apiCall);

            _logger.LogInformation("Fetched {TrackCount} tracks for playlist {PlaylistId}",
                tracks.Length, playlistId);
            activity?.SetTag("track.count", tracks.Length);

            return tracks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                             "Error getting playlist tracks for playlist {PlaylistId} (offset: {Offset}, limit: {Limit})",
                             playlistId,
                             offset,
                             limit);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<Playlist> GetPlaylistAsync(string playlistId)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("GetPlaylist");
        activity?.SetTag("playlist.id", playlistId);

        _logger.LogDebug("Fetching playlist details for {PlaylistId}", playlistId);

        try
        {
            Func<Task<Playlist>> apiCall = async () =>
            {
                string requestUri = $"playlists/{playlistId}";

                SpotifyPlaylist playlistDto = await _httpClient.GetFromJsonAsync<SpotifyPlaylist>(requestUri) ??
                                           throw new InvalidOperationException("Response can not be null");

                return playlistDto.MapToDomain();
            };

            Playlist playlist = await ExecuteWithTokenRefreshAsync(apiCall);

            _logger.LogInformation("Fetched playlist {PlaylistName} ({PlaylistId})",
                playlist.Name, playlistId);
            activity?.SetTag("playlist.name", playlist.Name);

            return playlist;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                             "Error getting playlist details for playlist {PlaylistId}",
                             playlistId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<SpotifyUser> GetCurrentUserAsync()
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("GetCurrentUser");

        _logger.LogDebug("Fetching current user from Spotify API");

        try
        {
            Func<Task<SpotifyUser>> apiCall = async () =>
            {
                SpotifyUser user = await _httpClient.GetFromJsonAsync<SpotifyUser>(requestUri: "me")
                    ?? throw new InvalidOperationException("Response can not be null");

                return user;
            };

            SpotifyUser user = await ExecuteWithTokenRefreshAsync(apiCall);

            _logger.LogInformation("Fetched current user {UserId}", user.Id);
            activity?.SetTag("user.id", user.Id);

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    public async Task<Playlist> CreatePlaylistAsync(string name, string? description = null)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("CreatePlaylist");
        activity?.SetTag("playlist.name", name);

        _logger.LogDebug("Creating playlist {PlaylistName}", name);

        try
        {
            Func<Task<Playlist>> apiCall = async () =>
            {
                SpotifyUser user = await GetCurrentUserAsync();
                string requestUri = $"users/{user.Id}/playlists";

                CreatePlaylistRequest request = new()
                {
                    Name = name,
                    Description = description,
                    Public = false
                };

                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(requestUri, request);
                response.EnsureSuccessStatusCode();

                SpotifyPlaylist playlistDto = await response.Content.ReadFromJsonAsync<SpotifyPlaylist>()
                    ?? throw new InvalidOperationException("Response can not be null");

                return playlistDto.MapToDomain();
            };

            Playlist playlist = await ExecuteWithTokenRefreshAsync(apiCall);

            _logger.LogInformation("Created playlist {PlaylistName} ({PlaylistId})", playlist.Name, playlist.Id);
            activity?.SetTag("playlist.id", playlist.Id);

            return playlist;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating playlist {PlaylistName}", name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    public async Task AddTracksToPlaylistAsync(string playlistId, IEnumerable<string> trackUris)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("AddTracksToPlaylist");
        activity?.SetTag("playlist.id", playlistId);

        List<string> uriList = trackUris.ToList();
        _logger.LogDebug("Adding {TrackCount} tracks to playlist {PlaylistId}", uriList.Count, playlistId);

        try
        {
            Func<Task<bool>> apiCall = async () =>
            {
                string requestUri = $"playlists/{playlistId}/tracks";

                AddTracksRequest request = new() { Uris = uriList };

                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(requestUri, request);
                response.EnsureSuccessStatusCode();

                return true;
            };

            await ExecuteWithTokenRefreshAsync(apiCall);

            _logger.LogInformation("Added {TrackCount} tracks to playlist {PlaylistId}", uriList.Count, playlistId);
            activity?.SetTag("track.count", uriList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tracks to playlist {PlaylistId}", playlistId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    public async Task<Playlist?> FindPlaylistByNameAsync(string name)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("FindPlaylistByName");
        activity?.SetTag("playlist.name", name);

        _logger.LogDebug("Searching for playlist by name: {PlaylistName}", name);

        List<Playlist> playlists = await GetUserPlaylistsAsync();
        Playlist? playlist = playlists.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (playlist != null)
        {
            _logger.LogInformation("Found existing playlist {PlaylistName} ({PlaylistId})", playlist.Name, playlist.Id);
            activity?.SetTag("playlist.id", playlist.Id);
            activity?.SetTag("playlist.found", true);
        }
        else
        {
            _logger.LogInformation("Playlist {PlaylistName} not found", name);
            activity?.SetTag("playlist.found", false);
        }

        return playlist;
    }

    public async Task<Playlist> GetOrCreatePlaylistAsync(string name, string? description = null)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("GetOrCreatePlaylist");
        activity?.SetTag("playlist.name", name);

        _logger.LogDebug("Getting or creating playlist: {PlaylistName}", name);

        Playlist? existingPlaylist = await FindPlaylistByNameAsync(name);

        if (existingPlaylist != null)
        {
            activity?.SetTag("playlist.created", false);
            return existingPlaylist;
        }

        Playlist newPlaylist = await CreatePlaylistAsync(name, description);
        activity?.SetTag("playlist.created", true);

        return newPlaylist;
    }

    private async Task<Dictionary<string, ReccoBeatsAudioFeatures>> GetAudioFeaturesFromReccoBeatsAsync(string[] trackIds, CancellationToken cancellationToken)
    {
        Dictionary<string, ReccoBeatsAudioFeatures> audioFeaturesMap = new();

        if (trackIds.Length == 0)
        {
            return audioFeaturesMap;
        }

        _logger.LogDebug("Fetching audio features for {TrackCount} tracks from ReccoBeats", trackIds.Length);

        try
        {
            // ReccoBeats API requires individual requests per track (no bulk endpoint)
            // Process tracks sequentially to respect rate limits
            foreach (string trackId in trackIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ReccoBeatsAudioFeatures? features = await _reccoBeatsService.GetAudioFeaturesAsync(trackId, cancellationToken);

                if (features != null)
                {
                    audioFeaturesMap[trackId] = features;
                }
            }

            _logger.LogInformation("Fetched audio features for {SuccessCount}/{TotalCount} tracks from ReccoBeats",
                audioFeaturesMap.Count, trackIds.Length);

            return audioFeaturesMap;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Error getting audio features from ReccoBeats, returning partial results");
            return audioFeaturesMap;
        }
    }

    private async Task<Dictionary<string, string>> GetArtistsGenresAsync(string[] artistIds, CancellationToken cancellationToken)
    {
        Dictionary<string, string> artistGenres = new();

        if (artistIds.Length == 0)
        {
            return artistGenres;
        }

        _logger.LogDebug("Fetching genres for {ArtistCount} artists in bulk", artistIds.Length);

        try
        {
            // Spotify allows up to 50 artist IDs per request
            const int batchSize = 50;
            List<string[]> batches = artistIds
                .Select((id, index) => new { id, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.id).ToArray())
                .ToList();

            foreach (string[] batch in batches)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Func<Task<Dictionary<string, string>>> apiCall = async () =>
                {
                    string ids = string.Join(",", batch);
                    string requestUri = $"artists?ids={ids}";

                    ArtistsResponse response = await _httpClient.GetFromJsonAsync<ArtistsResponse>(requestUri, cancellationToken)
                        ?? throw new InvalidOperationException("Response can not be null");

                    Dictionary<string, string> batchGenres = new();
                    foreach (SpotifyArtist artist in response.Artists)
                    {
                        if (artist != null)
                        {
                            batchGenres[artist.Id] = artist.Genres.FirstOrDefault() ?? "unknown";
                        }
                    }

                    return batchGenres;
                };

                Dictionary<string, string> batchResult = await ExecuteWithTokenRefreshAsync(apiCall);
                foreach ((string artistId, string genre) in batchResult)
                {
                    artistGenres[artistId] = genre;
                }
            }

            _logger.LogInformation("Fetched genres for {ArtistCount} artists", artistGenres.Count);

            return artistGenres;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Error getting artist genres in bulk, returning partial results");
            return artistGenres;
        }
    }

    private async Task<T> ExecuteWithTokenRefreshAsync<T>(Func<Task<T>> apiCall)
    {
        try
        {
            return await apiCall();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Access token expired, attempting to refresh");

            if (await _spotifyAuthService.RefreshTokenAsync())
            {
                UpdateAuthorizationHeader();
                _logger.LogDebug("Token refreshed successfully, retrying API call");

                return await apiCall();
            }

            _logger.LogError("Failed to refresh access token, authentication required");
            throw;
        }
    }

    private void UpdateAuthorizationHeader()
    {
        string accessToken = _spotifyAuthSessionManager.GetAccessToken();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", accessToken);
    }
}