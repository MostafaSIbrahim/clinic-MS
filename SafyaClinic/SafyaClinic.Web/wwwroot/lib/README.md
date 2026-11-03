# Third-party files served by the app (F6)

Served locally so the app works on clinic machines without internet access. No CDN links remain.
To update a library, download the package from npm (`npm pack <name>@<version>`), copy the files listed here, and update this table.

| Folder | Package | Version | Files used | Licence |
|---|---|---|---|---|
| `bootstrap/` | bootstrap | 5.3.3 | `dist/css/*`, `dist/js/*` | MIT (`bootstrap/LICENSE`) |
| `bootstrap-icons/` | bootstrap-icons | 1.11.3 | `font/bootstrap-icons.min.css`, `font/fonts/*` | MIT (`bootstrap-icons/LICENSE`) |
| `jquery/` | jquery | 3.7.1 | `dist/*` | MIT (`jquery/LICENSE.txt`) |
| `jquery-validation/` | jquery-validation | 1.20.1 | `dist/*` | MIT (`jquery-validation/LICENSE.md`) |
| `jquery-validation-unobtrusive/` | jquery-validation-unobtrusive | 4.0.0 | `jquery.validate.unobtrusive(.min).js` | MIT |
| `fonts/inter/` | @fontsource-variable/inter | 5.1.0 | latin and latin-ext variable `woff2` | SIL OFL 1.1 (`fonts/inter/LICENSE`) |
| `fonts/ibm-plex-sans-arabic/` | @fontsource/ibm-plex-sans-arabic | 5.1.0 | arabic 400–700 `woff2` | SIL OFL 1.1 (`fonts/ibm-plex-sans-arabic/LICENSE`) |

Font faces are declared in `wwwroot/css/fonts.css`. Font Awesome is no longer used (Bootstrap Icons only).
