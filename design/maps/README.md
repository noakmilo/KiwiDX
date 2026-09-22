# Bundled Airspy directory map assets

KiwiDX renders the live Airspy directory on a local Leaflet vector map. Only receiver discovery requires a network request; the map makes no tile requests and needs no API key.

- `leaflet.js`, `leaflet.css`: Leaflet 1.9.4, BSD 2-Clause. Original distribution: https://unpkg.com/leaflet@1.9.4/dist/ . Copyright and redistribution conditions are reproduced in `Leaflet-LICENSE.txt`, shipped with the application. These files are unmodified.
- `world.geojson`: Natural Earth 1:110m country outlines, public domain. Source: https://github.com/nvkelso/natural-earth-vector/blob/master/geojson/ne_110m_admin_0_countries.geojson . Only the country name is retained in feature properties. Terms: https://www.naturalearthdata.com/about/terms-of-use/ . This is an overview map, not street-level cartography.

Assets are embedded in the .NET assembly. Server addresses are never bundled here: `AirspyDirectoryService` fetches the current directory separately.
