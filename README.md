# SimAirport Timelapse mod

## Disclaimer
This is `proof-of-concept` mod, it's not pretty, but works. To install it you need to have basic knowlege about modding.

**Mod is compatible only with `Edge` build!** `Stable` build was not tested!

# How To install
Download or compile `dll` from: https://github.com/KapitanOczywisty/Screenshot/releases/

Insert `dll` into `Managed` in your game folder for example: 
`J:\SteamLibrary\steamapps\common\SimAirport\SimAirport_Data\Managed`

Download and extract `dnSpy.zip` from https://github.com/0xd4d/dnSpy/releases to any other folder (like desktop)

Run `dnSpy.exe` and:
1. open `Assembly-CSharp.dll` from `J:\SteamLibrary\steamapps\common\SimAirport\SimAirport_Data\Managed`
2. click `Search`
3. find `CircleLoading Awake`
4. click on `Awake` method
5. select and click `Edit Method`

![step 1](/docs/step1.png)

In new window:
1. type `using Screenshot;`
2. type `new ScreenshotRun();`
3. click on `Add Assembly Reference`
4. select inserted `Screenshot.dll`
5. open
6. click `Compile`

![step 2](/docs/step2.png)

Save modification
1. Click `File -> Save Module..`
2. Confirm with `OK`

![step 3](/docs/step3.png)

# Configuration
After first game start, mod will create new config file `Screenshot-config.json` in
`C:\Users\[user]\AppData\LocalLow\LVGameDev LLC\SimAirport` or easy to paste: `%APPDATA%\..\LocalLow\LVGameDev LLC\SimAirport`

Example config:
```json
{
    "enabled": true,
    "pixels_per_tile": 10.0,
    "frequency_in_minutes": 2.0,
    "only_current_floor": false,
    "selected_floors": [
        0,
        1,
        -1
    ],
    "debug_messages": false,
    "disable_zone_labels": true,
    "disable_daynightcycle": true,
    "disable_weather": true
}
```

- `enabled` - `true` or `false` if mod is enabled or disabled
- `pixels_per_tile` - how big image should be, basic medium map has 197x120 tiles, so by default image 1970x1200 px
- `frequency_in_minutes` - how often take screenshot in minutes, can be fraction
- `only_current_floor` - `true` if mod should take screenshots of currently viewed floor or go to floors specified in the next variable
- `selected_floors` - list of selected floors, from -2 to 2 inclusive `0` - ground, `1` - 2nd floor, `2` - 3rd floor
- `debug_messages` - write some time related info into `output_log.txt` in `%APPDATA%\..\LocalLow\LVGameDev LLC\SimAirport`
- `disable_zone_labels` - hide zone labels on screenshots
- `disable_daynightcycle` - disable day/night on screenshots
- `disable_weather` - disable weather effects on screenshots

Screenshots are stored in `%APPDATA%\..\LocalLow\LVGameDev LLC\SimAirport\Screen`

*CC-BY-SA*
