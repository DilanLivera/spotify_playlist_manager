# Spotify Playlist Manager

A Blazor Server application to manage and organize Spotify playlists with AI-powered filtering, genre-based sorting, and deep audio feature analytics.

## Getting Started

### Prerequisites

- .NET SDK. Please refer to the "src/UI/UI.csproj" file to find the .NET SDK version.
- A Spotify account (free or premium)

### Create an app in Spotify Dashboard

1. Go to the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
2. Log in with your Spotify account
3. Click "Create App"
4. Fill in the required information:
    - App name: Spotify Playlist Sorter (or your preferred name)
    - App description: A tool to sort playlist songs by genre
    - Redirect URI: `http://127.0.0.1:5155/callback`. Please refer to the [Redirect URIs](https://developer.spotify.com/documentation/web-api/concepts/redirect_uri) to find more details about redirect URIs.
5. Accept the terms and conditions
6. Once created, you'll see your Client ID on the dashboard
7. Click "Show Client Secret" to reveal your Client Secret
8. Copy both the Client ID and Client Secret

### Application Configuration

1. Open `src/UI/appsettings.json` file
2. Update the Spotify configuration section:
   ```json
   "Spotify": {
     "ClientId": "your-client-id-here",
     "ClientSecret": "your-client-secret-here",
     "RedirectUri": "http://127.0.0.1:5155/callback"
   }
   ```
3. set dummy values to the Google authentication's ClientId and ClientSecret properties.
   ```json
   "Authentication": {
     "Google": {
       "ClientId": "",
       "ClientSecret": ""
     }
   }
   ```
4. Configure AI-powered filtering (optional):
   The application supports both **Ollama** (local) and **Azure OpenAI** (cloud).

   **Option A: Ollama (Default/Local)**
   By default, the app is configured to use Ollama. If you're running it via Docker Compose (see below), the default settings in `appsettings.json` will work:
   ```json
   "AIChat": {
     "OllamaEndpoint": "http://localhost:11434",
     "ModelName": "llama3.2"
   }
   ```

   **Option B: Azure OpenAI**
   To use Azure OpenAI, update `appsettings.json` with your deployment details:
   - Go to [Azure AI Foundry](https://ai.azure.com/)
   - Create a project and deploy a model (e.g., `gpt-4o`)
   - Obtain the **Endpoint** and **Key** from the deployment details
   - For more details, refer to the [Azure AI Foundry documentation](https://ai.azure.com/doc/azure/ai-foundry/how-to/develop/sdk-overview?tid=f36634bf-850d-4dae-9140-6432f216a02a#prerequisites)
   - Update configuration:
     ```json
     "AIChat": {
       "AzureOpenAI": {
         "AzureKeyCredential": "your-api-key",
         "Endpoint": "your-endpoint-url",
         "Model": "your-deployment-name"
       }
     }
     ```
   Note: If both are configured, Azure OpenAI takes precedence.

5. Configure Observability (optional):
   The application is configured to export telemetry to an OTLP endpoint (e.g., Aspire Dashboard).
   ```json
   "Observability": {
     "OtlpEndpoint": "http://localhost:18889",
     "HttpLogging": {
       "Enabled": true,
       "MaxBodyLength": 5000,
       "LogHeaders": true,
       "LogRequestBody": true,
       "LogResponseBody": true
     }
   }
   ```

### Running the Application

1. Open a shell/powershell window and navigate to the project root directory
2. Run the application using the HTTP profile. We must run the application using the HTTP profile because the Spotify redirect URI uses the HTTP port.
   ```
   dotnet run --project src/UI/UI.csproj --launch-profile http
   ```
3. Open your browser to `http://127.0.0.1:5155`
4. Click "Connect to Spotify" to authenticate
5. Grant the requested permissions to your application
6. Start managing and sorting your playlists by genre

### Running with Docker Compose (Optional)

Docker Compose provides optional infrastructure services for enhanced functionality:

- **Aspire Dashboard**: Visualize application telemetry (traces, metrics, logs) in real-time
- **Ollama**: AI-powered natural language track filtering

#### Prerequisites

- Docker and Docker Compose installed
- The application still runs locally (not containerized) - only infrastructure services run in Docker

#### Starting Services

1. Start Aspire Dashboard and Ollama:
   ```bash
   docker compose up -d aspire-dashboard ollama
   ```

2. Pull the Ollama model (first time only):
   ```bash
   docker exec -it ollama ollama pull llama3.2
   ```

3. Run the application locally:
   ```bash
   dotnet run --project src/UI/UI.csproj --launch-profile http
   ```

4. Access the services:
   - Application: `http://127.0.0.1:5155`
   - Aspire Dashboard: `http://127.0.0.1:18888`

#### Stopping Services

```bash
docker compose down
```

#### Running Everything in Docker (Alternative)

To run the entire application stack in Docker:

```bash
docker compose up -d --force-recreate --build
```

**Note**: When running in Docker, you'll need to configure your Spotify app's redirect URI to use the container's network or expose the app appropriately.

## Development Setup

### Git Hooks

This project uses git hooks to enforce [Conventional Commits](https://www.conventionalcommits.org/) format. To enable the hooks, run:

```bash
git config core.hooksPath .githooks
```

The commit-msg hook validates:

- Conventional commit format (`type: description`)
- Subject line maximum 72 characters
- No trailing period on subject line

## Troubleshooting

- If you encounter authentication errors, verify that your redirect URI in the Spotify Dashboard exactly matches the one in your appsettings.json file
- Make sure you're using `http://127.0.0.1:5155/callback` not `http://localhost:5155/callback` (Spotify does not allow 'localhost' in redirect URIs)
- Ensure your Client ID and Client Secret are correctly copied without any extra spaces

## Features

- View all your Spotify playlists
- Sort playlist tracks by genre or decade
- **Comprehensive Audio Features**: View 11 detailed audio characteristics including Tempo, Key, Loudness, Valence, Energy, Danceability, Acousticness, Instrumentalness, Liveness, Speechiness, and Mode
- Compact and detailed views of your organized music
- Copy tracks to another playlist using configurable filters (e.g., songs from 2000 onwards)

## Architecture

This application follows **Vertical Slice Architecture** with **Domain-Driven Design (DDD)** principles:

### Structure

```
src/UI/
├── Features/
│   ├── Shared/Domain/      # Domain entities (Track, Playlist)
│   └── {Feature}/          # Feature-specific pages and logic
├── Infrastructure/         # Technical concerns (Spotify API, Auth, Observability)
└── App/                    # Application shell
```

### Domain Layer

Domain entities are pure C# classes located in `Features/Shared/Domain/`:

- **Track**: Represents a music track with business logic for decade calculation, artist display, etc.
- **Playlist**: Represents a Spotify playlist aggregate

Domain entities encapsulate business rules and are independent of infrastructure concerns.

### Infrastructure Layer

The Infrastructure layer handles technical details:

- **Spotify API Client**: HTTP communication with Spotify Web API
- **DTOs**: Data Transfer Objects for API serialization (decorated with `[JsonPropertyName]`)
- **Mapping**: Extension methods to convert DTOs to Domain entities (`MapToDomain()`)
- **Batching**: Artist genre requests are batched (up to 50 per request) to minimize API calls and improve performance

#### API Optimization

The `SpotifyService` uses bulk fetching to minimize API calls:

- **Bulk Artist Genres**: Uses Spotify's `/v1/artists?ids=...` endpoint to fetch up to 50 artist genres per request
- **Audio Features from ReccoBeats**: Fetches audio features from [ReccoBeats API](https://reccobeats.com/docs/apis/reccobeats-api) with rate limiting support
- **Automatic Loading**: Playlist tracks are automatically loaded in the background in batches of 100
- **Cancellation Support**: Background loading stops immediately when the user navigates away using `CancellationToken`

This reduces genre API calls by ~98% compared to individual requests per track. Audio features are fetched sequentially from ReccoBeats with built-in rate limiting and retry logic.

## Track Filter Architecture

The track filter system uses a Strategy pattern to allow flexible, composable filtering of playlist tracks.

### Filter Interface

All filters implement `ITrackFilter`:

```csharp
public interface ITrackFilter
{
    string Name { get; }
    string SuggestedPlaylistName { get; }
    bool Matches(Track track);  // Uses Domain entity
}
```

### Available Filters

| Filter            | Description                                       |
|-------------------|---------------------------------------------------|
| `YearRangeFilter` | Filter tracks by release year range (e.g., 2000+) |
| `CompositeFilter` | Combine multiple filters with AND logic           |

### Adding New Filters

To add a new filter, create a class implementing `ITrackFilter`:

```csharp
public sealed class GenreFilter(string genre) : ITrackFilter
{
    public string Name => $"Genre: {genre}";
    public string SuggestedPlaylistName => $"{genre} Tracks";
    public bool Matches(Track track) => track.Genre.Equals(genre, StringComparison.OrdinalIgnoreCase);
}
```

### Combining Multiple Filters

Use `CompositeFilter` to combine filters with AND logic:

```csharp
ITrackFilter filter = new CompositeFilter([
    new YearRangeFilter(minYear: 2000),
    new GenreFilter("Rock")
]);
// Matches tracks from 2000+ AND genre is Rock
```
