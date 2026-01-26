# SCPI Command Reference

This document lists the SCPI commands currently implemented by `TS.NET.Engine/Threads/ScpiServer.cs`.

## Transport / framing

- SCPI over raw TCP/IP socket, only supports 1 client at a time.
- Commands are parsed as UTF-8 text.
- Delimiter is `\n`. `\r\n` is supported. `\r` is not supported.
- Query responses are UTF-8 and end with `\n`.
- Whilst parsing/responses is UTF-8, at the moment only ASCII characters are used.

## Notes on parsing

- A leading `:` is accepted and removed (for example `:RUN` works like `RUN`).
- A single subsystem level is supported via `SUBJECT:COMMAND` (for example `TRIG:SOU CHAN1`).
- Abbreviations are accepted via `StartsWith(...)` checks in several places (for example `TRIG:SOU...`, `TRIG:DEL...`, `CHAN1:BAND...`).
- Many commands require an argument separated by a space (`COMMAND <arg>`).
- Unknown commands log a warning and return no response.

## Global (no subject)

### Commands

| Command | Args | Description |
|---|---:|---|
| `RUN` | - | Start acquisition/processing. |
| `STOP` | - | Stop acquisition/processing. |
| `FORCE` | - | Force a trigger. |
| `SINGLE` | - | Set mode to `Single`. |
| `NORMAL` | - | Set mode to `Normal`. |
| `AUTO` | - | Set mode to `Auto`. |
| `STREAM` | - | Set mode to `Stream`. |

### Queries

| Command | Args | Example response | Description |
|---|---:|---|---|
| `*IDN?` | - | `EEVengers,ThunderScope,TS0001,0.1.0` | Standard identification string. |
| `STATE?` | - | `RUN` or `STOP` | Current run state. |
| `MODE?` | - | `SINGLE`, `NORMAL`, `AUTO`, `STREAM` | Current acquisition mode. |

## Acquisition subsystem (`ACQ...`)

Subject matches `ACQ`/`ACQuisition` abbreviations via `subject.StartsWith("ACQ")`.

### Commands

| Command | Args | Description |
|---|---:|---|
| `ACQ:RATE <rateHz>` | `ulong` | Set sample rate (Hz). |
| `ACQ:DEPTH <samples>` | `int` | Set capture depth/length. |
| `ACQ:RES <bits>` | `int` | Set ADC resolution. Supported: `8` or `12`. Unsupported values default to 8-bit. |

### Queries

| Command | Args | Response | Description |
|---|---:|---|---|
| `ACQ:RATE?` | none | `<rateHz>` | Get current sample rate. |
| `ACQ:DEPTH?` | none | `<samples>` | Get current depth. |
| `ACQ:RES?` | none | `8` or `12` | Get ADC resolution bits. |
| `ACQ:RATES?` | none | `<r1>,<r2>,...` | List supported sample rates. |
| `ACQ:DEPTHS?` | none | `<d1>,<d2>,...` | List supported depths. |

## Trigger subsystem (`TRIG...`)

Subject matches `TRIG`/`TRIGger` abbreviations via `subject.StartsWith("TRIG")`.

### Commands

| Command | Args | Description |
|---|---:|---|
| `TRIG:SOU <CHAN1..CHAN4\|NONE>` | `string` | Set trigger source channel or `NONE`. |
| `TRIG:TYPE <EDGE\|BURST>` | `string` | Set trigger type. |
| `TRIG:DEL <femtoseconds>` | `long` | Set trigger delay in femtoseconds. Negative values clamp to 0 (to be reviewed). |
| `TRIG:HOLD <femtoseconds>` | `long` | Set trigger holdoff in femtoseconds. |
| `TRIG:INTER <true\|false>` | `bool` | Enable/disable trigger interpolation. `<1\|0>` is supported. |
| `TRIG:EDGE:LEV <volts>` | `float` | Set edge trigger level in volts. |
| `TRIG:EDGE:DIR <RISING\|FALLING\|ANY>` | `string` | Set edge direction. |

### Queries

| Command | Args | Response | Description |
|---|---:|---|---|
| `TRIG:SOU?` | none | `CHAN<1..4>` or `NONE` | Get trigger source. (Formatting is `CHAN{(int)channel}`). |
| `TRIG:TYPE?` | none | `<EDGE\|BURST>` | Get trigger type as uppercase enum name. |
| `TRIG:DEL?` | none | `<femtoseconds>` | Get trigger delay. |
| `TRIG:HOLD?` | none | `<femtoseconds>` | Get trigger holdoff. |
| `TRIG:INTER?` | none | `true` or `false` | Get trigger interpolation enabled. |
| `TRIG:EDGE:LEV?` | none | `<volts>` | Get edge trigger level (formatted `0.######`). |
| `TRIG:EDGE:DIR?` | none | `RISING`, `FALLING`, `ANY` | Get edge trigger direction as uppercase enum name. |

## Channel subsystem (`CHAN<n>...`)

Subject matches `CHAN`/`CHANnel` abbreviations via `subject.StartsWith("CHAN")` and requires the subject to end in a digit.

- Channels are `1` to `4` (`CHAN1` ... `CHAN4`).

### Commands

| Command | Args | Description |
|---|---:|---|
| `CHAN<n>:ON` | none | Enable channel `<n>`. |
| `CHAN<n>:OFF` | none | Disable channel `<n>`. |
| `CHAN<n>:BAND <FULL\|750M\|650M\|350M\|200M\|100M\|20M>` | `string` | Set channel bandwidth limit/filter. |
| `CHAN<n>:COUP <DC\|AC>` | `string` | Set channel coupling. |
| `CHAN<n>:TERM <1M\|50>` | `string` | Set channel termination. |
| `CHAN<n>:OFFS <volts>` | `float` | Set channel voltage offset. Clamped to `[-50, 50]`. |
| `CHAN<n>:RANG <volts>` | `float` | Set channel full-scale range. Clamped to `[-50, 50]`. |

### Queries

| Command | Args | Response | Description |
|---|---:|---|---|
| `CHAN<n>:STATE?` | none | `ON` or `OFF` | Get whether channel `<n>` is enabled. |
| `CHAN<n>:BAND?` | none | `FULL`, `750M`, `650M`, `350M`, `200M`, `100M`, `20M` | Get channel bandwidth (maps from enum). |
| `CHAN<n>:COUP?` | none | `DC` or `AC` | Get channel coupling. |
| `CHAN<n>:TERM?` | none | `1M` or `50` | Get channel termination (returns *actual* termination, may not match commanded termination). |
| `CHAN<n>:OFFS?` | none | `<volts>` | Get requested voltage offset (formatted `0.######`). |
| `CHAN<n>:RANG?` | none | `<volts>` | Get requested full-scale range (formatted `0.######`). |

## Processing subsystem (`PRO...`)

Subject matches `PRO` via `subject.StartsWith("PRO")`.

### Commands

| Command | Args | Description |
|---|---:|---|
| `tbd` | `tbd` | tbd |

## Calibration subsystem (`CAL...`)

Subject matches `CAL` via `subject.StartsWith("CAL")`.

### Commands

| Command | Args | Description |
|---|---:|---|
| `CAL:FRONTEND <channel> <coupling> <termination> <attenuator> <dac> <dpot> <pgaLadderAttenuation> <pgaHighGain> <pgaFilter>` | 9 tokens | Manually configure frontend parameters for a channel. Example from code: `CAL:FRONTEND CHAN1 DC 1M 0 2147 4 0 1 FULL`. |
| `CAL:ADC <v1> <v2> <v3> <v4> <v5> <v6> <v7> <v8>` | 8 tokens | Set ADC calibration fine gain branch values. Each value is parsed as signed byte, masked with `0x7F`, and stored as `byte`. |

## Error behavior

To be reviewed. Currently:

- Many invalid parameter cases log warnings and return no response.
- Many query failures return: `Error: No/bad response from channel.`
- Unknown commands log: `Unknown SCPI command: <message>` and return no response.
