# SCPI Command Reference

This document lists the SCPI commands currently implemented by `TS.NET.Engine/Threads/ScpiServer.cs`.

## Transport / framing

- SCPI over raw TCP/IP socket, only supports 1 client at a time.
- Commands are parsed as ASCII text.
- Delimiter is `\n`. `\r\n` is supported. `\r` is not supported.
- Query responses are ASCII and end with `\n`.

## Notes on parsing

- A leading `:` is accepted and removed (for example `:RUN` works like `RUN`).
- A single subsystem level is supported via `SUBJECT:COMMAND` (for example `TRIG:SOU CHAN1`).
- Abbreviations are accepted via `StartsWith(...)` checks in several places (for example `TRIG:SOU...`, `TRIG:DEL...`, `CHAN1:BAND...`).
- Many commands require an argument separated by a space (`COMMAND <arg>`).
- Unknown commands log a warning and return no response.

## Subsystems

Commands/queries are logically grouped into subsystems, with the exception of the global namespace (for commands like `RUN` & `STOP`).

| Subsystem | Description |
|---|---|
| `-` | Global namespace for commands like `RUN` & `STOP`. |
| `ACQ` | Acquisition subsystem for ADC & capture configuration. |
| `TRIG` | Trigger subsystem for trigger configuration. |
| `CHAN` | Channel subsystem for input frontend configuration. |
| `REFCL` | Reference clock subsystem for REFCLK IN/OUT BNC configuration. |
| `PRO` | Processing subsystem for data processing configuration. |

## Global namespace

### Commands

| Command | Description |
|---|---|
| `RUN` | Start acquisition/processing. |
| `STOP` | Stop acquisition/processing. |
| `FORCE` | Force a trigger. |
| `SINGLE` | Set mode to `Single`. |
| `NORMAL` | Set mode to `Normal`. |
| `AUTO` | Set mode to `Auto`. |
| `STREAM` | Set mode to `Stream`. |

### Queries

| Query | Response | Type  | Description |
|---|---|---|---|
| `*IDN?` | `EEVengers,ThunderScope,TS0001,0.1.0` | string | Standard identification string. |
| `STATE?` | `RUN`, `STOP` | enum | Current run state. |
| `MODE?` | `SINGLE`, `NORMAL`, `AUTO`, `STREAM` | enum | Current acquisition mode. |
| `SEQNUM?` | `12345` | u32 | The last sequence number sent on the data server socket. |
| `TEMP?` | `25.0` | f32 | FPGA temperature (formatted `F1`). |

## Acquisition subsystem (`ACQ...`)

Subject matches `ACQ`/`ACQuisition` abbreviations via `subject.StartsWith("ACQ")`.

### Commands

| Command | Type | Description |
|---|---:|---|
| `ACQ:RATE <rateHz>` | u64 | Set sample rate (Hz). |
| `ACQ:DEPTH <samples>` | u32 | Set capture depth/length. |
| `ACQ:RES <8\|12>` | enum | Set ADC resolution. Unsupported values default to 8-bit. |

### Queries

| Query | Response | Type | Description |
|---|---|---|---|
| `ACQ:RATE?` | `1000000000` | u64 | Get current sample rate. |
| `ACQ:DEPTH?` | `1000000` | u32 | Get current depth. |
| `ACQ:RES?` | `8`, `12` | enum | Get ADC resolution bits. |
| `ACQ:RATES?` | `<r1>,<r2>,...` | [u64] | List supported sample rates. |
| `ACQ:DEPTHS?` | `<d1>,<d2>,...` | [u32] | List supported depths. |

## Trigger subsystem (`TRIG...`)

Subject matches `TRIG`/`TRIGger` abbreviations via `subject.StartsWith("TRIG")`.

### Commands

| Command | Type | Description |
|---|---:|---|
| `TRIG:SOU <CHAN1\|CHAN2\|CHAN3\|CHAN4\|NONE>` | enum | Set trigger source channel or `NONE`. |
| `TRIG:TYPE <EDGE\|BURST>` | enum | Set trigger type. |
| `TRIG:DEL <femtoseconds>` | i64 | Set trigger delay in femtoseconds. Negative values clamp to 0 (to be reviewed). |
| `TRIG:HOLD <femtoseconds>` | u64 | Set trigger holdoff in femtoseconds. |
| `TRIG:INTER <true\|false>` | bool | Enable/disable trigger interpolation. `<1\|0>` is supported. |
| `TRIG:EDGE:LEV <volts>` | f32 | Set edge trigger level in volts. |
| `TRIG:EDGE:DIR <RISING\|FALLING\|ANY>` | enum | Set edge direction. |

### Queries

| Query | Response | Type | Description |
|---|---|---|---|
| `TRIG:SOU?` | `CHAN1`, `CHAN2`, `CHAN3`, `CHAN4`, `NONE` | enum | Get trigger source. (Formatting is `CHAN{(uint)channel}`). |
| `TRIG:TYPE?` | `EDGE, BURST` | enum | Get trigger type as uppercase enum name. |
| `TRIG:DEL?` | `<femtoseconds>` | i64 | Get trigger delay. |
| `TRIG:HOLD?` | `<femtoseconds>` | u64 | Get trigger holdoff. |
| `TRIG:INTER?` | `true`, `false` | bool | Get trigger interpolation enabled. |
| `TRIG:EDGE:LEV?` | `<volts>` | f32 | Get edge trigger level (formatted `0.######`). |
| `TRIG:EDGE:DIR?` | `RISING`, `FALLING`, `ANY` | enum | Get edge trigger direction as uppercase enum name. |

## Channel subsystem (`CHAN<n>...`)

Subject matches `CHAN`/`CHANnel` abbreviations via `subject.StartsWith("CHAN")` and requires the subject to end in a digit.

- Channels are `1` to `4` (`CHAN1` ... `CHAN4`).

### Commands

| Command | Type | Description |
|---|---:|---|
| `CHAN<n>:ON` | - | Enable channel `<n>`. |
| `CHAN<n>:OFF` | - | Disable channel `<n>`. |
| `CHAN<n>:BAND <FULL\|750M\|650M\|350M\|200M\|100M\|20M>` | enum | Set channel bandwidth limit/filter. |
| `CHAN<n>:COUP <DC\|AC>` | enum | Set channel coupling. |
| `CHAN<n>:TERM <1M\|50>` | enum | Set channel termination. |
| `CHAN<n>:OFFS <volts>` | f32 | Set channel voltage offset. Clamped to `[-50, 50]`. |
| `CHAN<n>:RANG <volts>` | f32 | Set channel full-scale range. Clamped to `[-50, 50]`. |

### Queries

| Query | Response | Type | Description |
|---|---|---|---|
| `CHAN<n>:STATE?` | `ON`, `OFF` | enum | Get whether channel `<n>` is enabled. |
| `CHAN<n>:BAND?` | `FULL`, `750M`, `650M`, `350M`, `200M`, `100M`, `20M` | enum | Get channel bandwidth (maps from enum). |
| `CHAN<n>:COUP?` | `DC`, `AC` | enum | Get channel coupling. |
| `CHAN<n>:TERM?` | `1M`, `50` | enum | Get requested channel termination. |
| `CHAN<n>:OFFS?` | `<volts>` | f32 | Get requested voltage offset (formatted `0.######`). |
| `CHAN<n>:RANG?` | `<volts>` | f32 | Get requested full-scale range (formatted `0.######`). |
| `CHAN<n>:TERM:ACT?` | `1M`, `50` | enum | Get actual channel termination (driver may coerce termination). |
| `CHAN<n>:OFFS:ACT?` | `<volts>` | f32 | Get actual voltage offset (formatted `0.######`). |
| `CHAN<n>:RANG:ACT?` | `<volts>` | f32 | Get actual full-scale range (formatted `0.######`). |

## Reference clock subsystem (`REFCL...`)

Subject matches `REFCL` via `subject.StartsWith("REFCL")`.

### Commands

| Command | Type | Description |
|---|---:|---|
| `REFCL:MODE <IN\|OUT\|OFF>` | enum | Set mode of REFCLK IN/OUT BNC. |
| `REFCL:FREQ <frequency>` | u32 | Set the input clock frequency if in IN mode, or output frequency if in OUT mode. |

## Processing subsystem (`PRO...`)

Subject matches `PRO` via `subject.StartsWith("PRO")`.

### Commands

| Command | Type | Description |
|---|---:|---|
| `tbd` | `tbd` | tbd |
