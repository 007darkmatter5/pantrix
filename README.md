# Pantrix

<img src="docs/images/icon.png" alt="" width="96" align="right">

A self-hosted kitchen planner: recipes, what's in the fridge and pantry, meal plans, and a shopping list that writes itself from the three.

- **Recipes and ingredients.** Recipes scale by servings. An ingredient line can accept substitutes ("penne or elbow pasta"), and an ingredient can record the exact product you usually buy.
- **Inventory.** What you have in the refrigerator, freezer and pantry, with expiry dates and a "keep at least" amount per ingredient.
- **Meal plans.** Put a recipe, a single ingredient (a packaged meal), or "eating out" on each day, and close out days as you finish planning them.
- **A live shopping list.** Always current: what upcoming meals need, plus anything below its minimum, less what's in inventory. Add your own items too.
- **Stores and aisles.** Record which aisle each item is in at each store you use, and the list sorts into walking order for the store you're in. Each item can link to its page on the store's website.

Pantrix is behind a username and password. The first time you open a fresh install, it asks you to create them.

## Unraid

Install **Pantrix** from Community Applications, or add the template by hand from
[`007darkmatter5/unraid-templates`](https://github.com/007darkmatter5/unraid-templates/blob/main/templates/pantrix.xml).

The template maps two folders. Back them up together:

| Container path | Default on Unraid | Holds |
| --- | --- | --- |
| `/data` | `/mnt/user/appdata/pantrix/data` | The database, `pantrix.db` |
| `/keys` | `/mnt/user/appdata/pantrix/keys` | The keys that encrypt sign-in cookies |

To reach Pantrix from outside your network, put it behind a reverse proxy that handles HTTPS and set
**Behind a reverse proxy** to `true`.

## Docker

```bash
docker run -d --name pantrix \
  -p 8080:8080 \
  -v /path/to/data:/data \
  -v /path/to/keys:/keys \
  -e PUID=1000 -e PGID=1000 \
  ghcr.io/007darkmatter5/pantrix:production
```

Then open `http://<host>:8080` and create your account.

| Tag | Built from | For |
| --- | --- | --- |
| `production`, `latest`, `<version>` | `main` | Everyday use |
| `beta`, `<version>-beta` | `beta` | Trying changes before they reach `main` |

| Variable | Default | Meaning |
| --- | --- | --- |
| `PUID` / `PGID` | `1654` | The user and group Pantrix runs as and that own `/data` and `/keys` (Unraid: `99` / `100`) |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `false` | Set to `true` behind a reverse proxy that handles HTTPS |

`/healthz` answers without signing in, for health checks.

## Address lookup

When you look up a store's address, what you typed is sent to [OpenStreetMap](https://www.openstreetmap.org/copyright)'s
Nominatim service, and to [Photon](https://photon.komoot.io) if that finds nothing. Nothing else leaves your server.

## Development

Requires the .NET 10 SDK.

```powershell
dotnet run --project src/Pantrix     # http://localhost:5057
dotnet test
dotnet ef migrations add <Name> --project src/Pantrix
```

The database is created on first run (`src/Pantrix/pantrix.db`) and migrations apply on startup. Point the app at a
throwaway database with `ConnectionStrings__Pantrix="Data Source=<path>"`.

Day-to-day work happens on `beta`; merging `beta` into `main` releases to Production. Every push to either branch
runs the tests, builds the image for amd64 and arm64, starts it the ways it is deployed, and only then publishes it.

## License

[MIT](LICENSE)
