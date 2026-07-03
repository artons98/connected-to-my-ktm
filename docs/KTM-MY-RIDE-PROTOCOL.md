# KTM MY RIDE — Bluetooth protocol & feature map

Reverse-engineering reference for talking to a KTM/Husqvarna "MY RIDE" TFT dashboard
from a custom phone app. Target of this repo: push turn-by-turn navigation onto the
dash. Owner's bike: **2021 KTM 1290 Super Duke R**.

Everything here is marked **CONFIRMED** (read from working code or the decompiled
official app), **PROBABLE** (strong circumstantial evidence), or **UNKNOWN** (needs a
capture on the actual bike). Do not treat PROBABLE/UNKNOWN as fact when coding.

Primary sources:
- Working Android reverse-eng: [`pinginfo/Connected-to-my-ktm`](https://github.com/pinginfo/Connected-to-my-ktm) — this repo's origin (proven on a **790 Adventure R 2019**).
- **OEM ground truth**: a decompile of the official KTM app, [`ethanwc/ktm-com`](https://github.com/ethanwc/ktm-com) (package `com.ktm.myride`, protocol lib `kmrc` = "KTM My Ride Connect").
- Reference app (closed source): `maps4ktm` (`com.undingen.maps4ktm`).
- 2021 1290 SD R/RR owner's manual (Art. 3214331en).

---

## 0. TL;DR — the two decisions that shape the whole project

1. **iOS is a dead end. Android only.** iOS can only reach a Classic-Bluetooth serial
   device through Apple's ExternalAccessory/MFi framework, which needs (a) an Apple auth
   chip in the dash, (b) the app whitelisted by Apple against KTM's private protocol
   string. A hobbyist has none of these, and Core Bluetooth cannot see RFCOMM/SPP at all.
   This is why every working project (this one included) is Android. The `KTMConnectedMaui`
   iOS port **cannot connect** and should be abandoned — its `com.ktm.myride` protocol
   string is a guess, and even the correct string wouldn't unblock a non-MFi app.

2. **This is a one-way, fire-and-forget push protocol.** The phone opens a socket and
   streams navigation UI updates. The dash sends **nothing back** that the phone reads
   (the official app's inbound handler is empty). No handshake, no ACK, no button events.
   You only need to write, plus detect the socket dropping.

---

## 1. Transport & connection

| Item | Value | Status |
|---|---|---|
| Bluetooth type | Classic **RFCOMM / SPP** (BR/EDR). **Not** BLE. | CONFIRMED |
| Service UUID (790) | `cc4c1fb3-482e-4389-bdeb-57b7aac889ae` | CONFIRMED on 790 |
| Service UUID (other models) | **May differ** — Duke 390 Gen3 needed the device's advertised `uuids[0]` instead. Discover at runtime. | CONFIRMED it varies |
| Role | Phone = RFCOMM **client**, dash = server. Phone connects out. | CONFIRMED |
| Discovery | Dash pairs/appears as an **A2DP audio device**; find it among bonded/A2DP devices by name. | CONFIRMED |
| Socket security | Official app uses **secure** `createRfcommSocketToServiceRecord`; this repo uses **insecure**. Both reported working. | CONFIRMED |
| Reconnect | Official app retries every **4 s** after a drop. | CONFIRMED |
| Keepalive / heartbeat | **None.** No ping/pong in the protocol. Liveness is socket-level only. | CONFIRMED |

Connect sequence (what the app does):
1. Enumerate bonded devices (this repo uses the A2DP profile proxy to find the connected dash).
2. Match by name (see §5).
3. Open an RFCOMM socket to the service UUID and `connect()`.
4. Immediately push a `Restore` state so the dash draws the default nav UI. (This repo
   sends two default frames on connect; the official app sends one `Restore`.)

---

## 2. Wire framing

Each message is a length-prefixed frame:

```
+--------+--------+--------+--------+--------+===================+
| len[3] | len[2] | len[1] | len[0] | type=1 |  UTF-8 JSON ...   |
+--------+--------+--------+--------+--------+===================+
  \___ 4-byte big-endian length = (JSON byte length + 1) ___/
```

- 4-byte **big-endian** length. The length **includes the 1 type byte** (= JSON bytes + 1).
- 1 type byte, always `0x01`.
- UTF-8 JSON body. Escaped forward slashes `\/` are un-escaped to `/` before sending.

CONFIRMED (this repo's `SendingObject`, matches the OEM app passing raw JSON bytes to its framer).

---

## 3. JSON payload

Top-level keys: **`UiContext`**, **`UpdateUI`**, **`MsgId`**. CONFIRMED (OEM `NavRepo`).

```jsonc
{
  "UiContext": "guidance",          // "default" | "guidance"  — ONLY these two exist
  "UpdateUI": {
    "TurnIcon":         { "Image": "HEAVY_LEFT", "Visibility": "full" },
    "TurnDist":         { "Text": "200",  "Visibility": "full" },
    "TurnDistUnit":     { "Text": "m",    "Visibility": "full" },
    "TurnInfo":         { "Text": "Exit 3", "Visibility": "full" },
    "TurnRoad":         { "Text": "Hauptstrasse", "Visibility": "full" },
    "ETA":              { "Text": "14:32", "Visibility": "full" },
    "Dist2Target":      { "Text": "12 km, 18 min", "Visibility": "full" },
    "GpsIcon":          { "Image": "GPS", "Visibility": "full" },
    "NotificationText": { "Text": "", "Visibility": "off" },
    "NotificationIcon": { "Visibility": "off" }
  },
  "MsgId": "gon#7"                   // "<prefix>#<counter>"
}
```

### The 10 UpdateUI widgets (this is the complete set — CONFIRMED, OEM)

| Widget | Kind | Shows |
|---|---|---|
| `TurnIcon` | icon | Next-maneuver arrow (enum, §4) |
| `TurnDist` | text | Distance to the next maneuver (number) |
| `TurnDistUnit` | text | Unit for `TurnDist` (`m`, `km`, …) |
| `TurnInfo` | text | Extra maneuver line (exit #, roundabout exit, speed) |
| `TurnRoad` | text | Name of the road you turn onto |
| `ETA` | text | Estimated time of arrival |
| `Dist2Target` | text | Remaining distance / time to destination |
| `GpsIcon` | icon | GPS-fix indicator |
| `NotificationText` | text | Free-text notification line |
| `NotificationIcon` | icon | Notification icon |

- **Text widget** serializes as `{"Text": <string>, "Visibility": <vis>}`.
- **Icon widget** serializes as `{"Image": <enum>, "Visibility": <vis>}`.
- The OEM app sends **only the widgets that changed** (delta updates); this repo always
  sends the full block. **Both work** — the dash accepts a partial `UpdateUI`.

### Visibility values (CONFIRMED)

| Value | Meaning |
|---|---|
| `full` / `FULL` | shown normally |
| `half` / `HALF` | shown greyed-out (e.g. GPS lost, rerouting) |
| `off` | hidden (value this repo uses) |
| `private` / `PRIVATE` | hidden (value the OEM app uses) |

### MsgId prefixes (CONFIRMED — full set, OEM `NavClientConnection`)

Format `<prefix>#<counter>`, counter is a monotonically increasing integer.

| Prefix | Sent when | UiContext |
|---|---|---|
| `Restore` | Full redraw — on (re)connect, or returning to default | default |
| `gon` | Guidance **on** — navigation started | guidance |
| `goff` | Guidance **off** — navigation ended | default |
| `mup` | **Maneuver update** — new turn icon / road / info | guidance |
| `lup` | **Location update** — new ETA / distances | guidance |
| `gps` | GPS fix **acquired** (GpsIcon → full) | either |
| `gpsoff` | GPS fix **lost** (GpsIcon → half/grey) | either |
| `re` | **Rerouting** (shows the rerouting notification icon) | guidance |

> This repo only ever emits `gon` and `Restore` because it pushes full snapshots
> instead of semantic deltas. That's enough to drive the display; the other prefixes
> are just more precise signalling.

---

## 4. Turn-icon enum (`TurnIcon.Image`)

Best available list, from this repo's proven 790 mapping (the OEM enum body didn't
decompile). CONFIRMED in use on the 790; PROBABLE on the 1290.

```
END
GO_STRAIGHT
KEEP_LEFT            KEEP_RIGHT
LIGHT_LEFT          LIGHT_RIGHT     # slight
QUITE_LEFT          QUITE_RIGHT     # normal (note the misspelling "QUITE")
HEAVY_LEFT          HEAVY_RIGHT     # sharp
UTURN_LEFT          UTURN_RIGHT
LEAVE_HIGHWAY_LEFT_LANE   LEAVE_HIGHWAY_RIGHT_LANE
FERRY
UNDEFINED
RAB_SECT_4_LH   RAB_SECT_4_RH       # roundabout: SECT = 2*(exits)+2, LH/RH = drive side
RAB_SECT_6_LH   RAB_SECT_6_RH
RAB_SECT_8_LH   RAB_SECT_8_RH
RAB_SECT_10_LH  RAB_SECT_10_RH
RAB_SECT_12_LH  RAB_SECT_12_RH
RAB_SECT_14_LH  RAB_SECT_14_RH
RAB_SECT_16_LH  RAB_SECT_16_RH
```

Roundabout mapping used by the OsmAnd module: OsmAnd "take exit N" → `RAB_SECT_(2N+2)_RH`;
the exit number shown on the dash = `(SECT − 2) / 2`.

`GpsIcon` / `NotificationIcon` enums did not fully decompile. Known usable values:
`GpsIcon.Image = "GPS"` (this repo), and `NotificationIcon` = `NOTIFICATION_REROUTING`
(OEM, for the `re` prefix). Others UNKNOWN.

---

## 5. Device name & the match filter

The dash advertises different names by model — KTM never documents them:

| Reported name | Model | Confidence |
|---|---|---|
| `LC8 Dashboard` | 1290 Super Duke GT / LC8 clusters | PROBABLE (best 1290-family data point) |
| `KTM SPORTMOTORCYCLE` | some Dukes | PROBABLE |
| `KTM_Duke_XXXX` | 2020 Super Duke (snippet) | LOW |
| (unnamed / "unknown") | some 790/890 | LOW |

The name matches the FCC product line "**LC8CLUSTER1**" (filed by KTM AG, Bosch test
report), so "LC8 Dashboard" for the 1290 is expected. The repo filter
(`name contains "KTM" or "LC8"`) should catch it — **but make the match
case-insensitive** (see the code-hardening note in the README / `BluetoothManager.kt`).

**Verify on the bike:** scan with any Bluetooth scanner and record the verbatim name + casing.

---

## 6. Full Bluetooth feature map

What actually rides over each channel. **Only navigation is proprietary.** Music and
calls are plain Bluetooth profiles handled by the phone OS and the dash's own stack —
there is nothing to reverse-engineer or code for those.

| Feature | Channel | In scope for this app? |
|---|---|---|
| Turn-by-turn nav (icons, road, ETA, distances) | **Proprietary RFCOMM/SPP** (this doc) | ✅ Yes — this is the whole app |
| Notification line on the dash | **Proprietary RFCOMM/SPP** (`NotificationText`/`Icon`) | ✅ Yes (e.g. forward phone notifications) |
| GPS-fix indicator | **Proprietary RFCOMM/SPP** (`GpsIcon`) | ✅ Yes |
| Music: play/pause/next/prev | Standard **AVRCP** | ❌ OS/dash handle it — handlebar controls the phone |
| Music: track / artist title on dash | Standard **AVRCP** metadata | ❌ OS/dash handle it |
| Music streaming audio | Standard **A2DP** | ❌ OS/dash handle it |
| Incoming call display / answer / reject | Standard **HFP** | ❌ OS/dash handle it |
| Handlebar wheel / mode button events | Stay **on the dash** — not relayed to the phone | ❌ Not available over this channel |

CONFIRMED: grepping the entire official app found **zero** AVRCP/A2DP/HFP/telephony
handling in the protocol layer — those are done by the standard profiles on the same
paired device, which is exactly why the dash pairs as an audio device.

---

## 7. 2021 1290 Super Duke R — specifics

- **MY RIDE is optional and must be activated.** The 2021 manual marks KTM MY RIDE,
  Audio, Pairing, Telephony and Bluetooth all "(optional)", each requiring the function
  to be "activated". It needs the **connectivity control unit** (a real part:
  "17-22 1290 Super Duke R Connectivity Control Unit") present and enabled — typically a
  **dealer software unlock**, the same mechanism as the Track/Tech packs (MY RIDE is
  **not** part of those packs). PROBABLE that enabling is dealer-side.
- **Enable path once unlocked:** bike stationary → **Settings → Bluetooth → ON** →
  **KTM MY RIDE → Pairing → Phone**. Pairing is **initiated from the dash**.
- **Cluster is the shared Bosch 5" LC8CLUSTER1** used across 790/890/1290 — so the
  protocol is *probably* the same, but **byte-parity on the 1290 is unproven**. `maps4ktm`
  user reports on the 1290 Super Duke are **mixed** (some "works fine", some "won't
  connect"). This repo has only ever been tested on the 790.
- **One phone + one headset max** can be paired at once. Watch for the reported
  "paired-but-data-not-connected" state, where audio/HFP attaches but the nav data
  channel doesn't — that's the channel this app needs.

---

## 8. What must be tested on the actual bike

The three unknowns that only the physical 1290 can answer:

1. **Exact advertised Bluetooth name + casing** — scan and record it.
2. **The SPP service UUID** — dump the dash's SDP records; is it `cc4c1fb3…` or something
   else? (The app now tries the hardcoded UUID first, then falls back to advertised UUIDs
   and logs which one connected — read logcat to capture the answer.)
3. **Do these nav frames actually render** on the SD R dash, or does it connect but show
   nothing (the Duke 390 Gen3 failure mode)? If it connects but is blank, the 1290 uses a
   different payload dialect and needs its own capture.

---

## 9. Still unknown (would need a live RFCOMM capture on the bike)

- The exact iOS ExternalAccessory / iAP2 protocol string (moot — iOS is blocked anyway).
- Full `TurnIcon` / `GpsIcon` / `NotificationIcon` enum bodies (only partial values recovered).
- Whether any dashboard firmware sends **upstream** messages (buttons/handshake). None
  exist in the app version analysed; the (paywalled) advrider thread is the only place
  that might hold a raw capture.
- Whether post-2022 / newer clusters changed the UUID or payload dialect.
