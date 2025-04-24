# Spotify Playlist Sorter

Sort Spotify playlist songs by genre.

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

## Troubleshooting

- If you encounter authentication errors, verify that your redirect URI in the Spotify Dashboard exactly matches the one in your appsettings.json file
- Make sure you're using `http://127.0.0.1:5155/callback` not `http://localhost:5155/callback` (Spotify does not allow 'localhost' in redirect URIs)
- Ensure your Client ID and Client Secret are correctly copied without any extra spaces

## Features

- View all your Spotify playlists
- Sort playlist tracks by genre
- Compact and detailed views of your organized music
