<h1>Ani-Sync Jellyfin Plugin</h1>

## About

Ani-Sync lets you synchorinze your Jellyfin Anime watch progress to popular services. Please [create a discussion](https://github.com/vosmiic/jellyfin-ani-sync/discussions/new/choose) for new feature ideas.

While I may not commit to the plugin too often, I am still maintaining it. Please do not presume the project is dead, and if you still have any ideas for the plugin or find an errors please do let me know by [creating a discussion](https://github.com/vosmiic/jellyfin-ani-sync/discussions/new/choose).

## Installation

### Automatic (recommended)
1. Navigate to Settings > Admin Dashboard > Plugins > Repositories
2. Add a new repository with a `Repository URL` of `https://raw.githubusercontent.com/vosmiic/jellyfin-ani-sync/master/manifest.json`. The name can be anything you like.
3. Save, and navigate to Catalogue.
4. Ani-Sync should be present. Click on it and install the latest version.

### Manual

[See the official Jellyfin documentation for install instructions](https://jellyfin.org/docs/general/server/plugins/index.html#installing).

1. Download a version from the [releases tab](https://github.com/vosmiic/jellyfin-ani-sync/releases) that matches your Jellyfin version.
2. Copy the `meta.json` and `jellyfin-ani-sync.dll` files into `plugins/ani-sync` (see above official documentation on where to find the `plugins` folder).
3. Restart your Jellyfin instance.
4. Navigate to Plugins in Jellyfin (Settings > Admin Dashboard > Plugins).
5. Adjust the settings accordingly. I would advise following the detailed instructions on the [wiki page](https://github.com/vosmiic/jellyfin-ani-sync/wiki).

## Build

1. To build this plugin you will need [.Net 6.x](https://dotnet.microsoft.com/download/dotnet/6.0).

2. Build plugin with following command
  ```
  dotnet publish --configuration Release --output bin
  ```

3. Place the dll-file in the `plugins/ani-sync` folder (you might need to create the folders) of your JF install

## Services/providers
### Currently supported
1. MyAnimeList
2. AniList
3. (Beta) Kitsu
4. (Limited support) Annict

## External tools
### Anime Lists
We use the XML documents in the [anime lists repo](https://github.com/Anime-Lists/anime-lists) to find the anime you are watching on each provider we support.

Please help the project by contributing to the lists of anime, it helps everyone!
### Anime Offline Database/arm server
We use the API offered by the [arm server repo](https://github.com/BeeeQueue/arm-server) which accesses the [anime offline database repo](https://github.com/manami-project/anime-offline-database) that we use to fetch our providers IDs so we can update your progress.

Please also help these projects by contributing to the anime database/helping with the API server.

## Development
Unit tests can be found [here](https://github.com/vosmiic/jellyfin-ani-sync-unit-tests).
