# SCPI socket Reference

This document lists the SCPI commands supported by the SCPI socket (typically port 5025).

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

| Command | Description | Version |
| --- | --- | --- |
| `RUN` | Start acquisition/processing. | ≥ 0.1.0 |
| `STOP` | Stop acquisition/processing. | ≥ 0.1.0 |
| `FORCE` | Force a trigger. | ≥ 0.1.0 |
| `SINGLE` | Set mode to `Single`. | ≥ 0.1.0 |
| `NORMAL` | Set mode to `Normal`. | ≥ 0.1.0 |
| `AUTO` | Set mode to `Auto`. | ≥ 0.1.0 |
| `STREAM` | Set mode to `Stream`. | ≥ 0.1.0 |

### Queries

| Query | Response | Type | Description | Version |
| --- | --- | --- | --- | --- |
| `*IDN?` | `EEVengers,ThunderScope,TS0001,0.1.0` | string | Standard identification string. | ≥ 0.1.0 |
| `STATE?` | `RUN`, `STOP` | enum | Current run state. | ≥ 0.1.0 |
| `MODE?` | `SINGLE`, `NORMAL`, `AUTO`, `STREAM` | enum | Current acquisition mode. | ≥ 0.1.0 |
| `SEQNUM?` | `12345` | u32 | The last sequence number sent on the data server socket. | ≥ 0.1.0 |
| `TEMP?` | `25.0` | f32 | FPGA temperature (formatted `F1`). | ≥ 0.1.0 |

## Acquisition subsystem (`ACQ...`)

Subject matches `ACQ`/`ACQuisition` abbreviations via `subject.StartsWith("ACQ")`.

### Commands

| Command | Type | Description | Version |
| --- | ---: | --- | --- |
| `ACQ:RATE <rateHz>` | u64 | Set sample rate (Hz). | ≥ 0.1.0 |
| `ACQ:DEPTH <samples>` | u32 | Set capture depth/length. | ≥ 0.1.0 |
| `ACQ:RES <8\|12>` | enum | Set ADC resolution. Unsupported values default to 8-bit. | ≥ 0.1.0 |

### Queries

| Query | Response | Type | Description | Version |
| --- | --- | --- | --- | --- |
| `ACQ:RATE?` | `1000000000` | u64 | Get current sample rate. | ≥ 0.1.0 |
| `ACQ:DEPTH?` | `1000000` | u32 | Get current depth. | ≥ 0.1.0 |
| `ACQ:RES?` | `8`, `12` | enum | Get ADC resolution bits. | ≥ 0.1.0 |
| `ACQ:RATES?` | `<r1>,<r2>,...` | [u64] | List supported sample rates. | ≥ 0.1.0 |
| `ACQ:DEPTHS?` | `<d1>,<d2>,...` | [u32] | List supported depths. | ≥ 0.1.0 |

## Trigger subsystem (`TRIG...`)

Subject matches `TRIG`/`TRIGger` abbreviations via `subject.StartsWith("TRIG")`.

### Commands

| Command | Type | Description | Version |
| --- | ---: | --- | --- |
| `TRIG:SOU <CHAN1\|CHAN2\|CHAN3\|CHAN4\|EXT\|NONE>` | enum | Set trigger source channel, external (`EXT`), or `NONE`. | `CHAN1\|CHAN2\|CHAN3\|CHAN4\|NONE` ≥ 0.1.0<br>`EXT` ≥ 0.3.0 |
| `TRIG:TYPE <EDGE\|BURST>` | enum | Set trigger type. | ≥ 0.1.0 |
| `TRIG:DEL <femtoseconds>` | i64 | Set trigger delay in femtoseconds. Negative values clamp to 0 (to be reviewed). | ≥ 0.1.0 |
| `TRIG:HOLD <femtoseconds>` | u64 | Set trigger holdoff in femtoseconds. | ≥ 0.1.0 |
| `TRIG:INTER <true\|false>` | bool | Enable/disable trigger interpolation. `<1\|0>` is supported. | ≥ 0.1.0 |
| `TRIG:EDGE:LEV <volts>` | f32 | Set edge trigger level in volts. | ≥ 0.1.0 |
| `TRIG:EDGE:DIR <RISING\|FALLING\|ANY>` | enum | Set edge direction. | ≥ 0.1.0 |
| `TRIG:EDGE:HYS <percent>` | f32 | Set edge-trigger hysteresis as a percentage of the full-scale range. Enter the numeric value only; for example, `5` means 5%. | ≥ 0.1.0 |
| `TRIG:BURST:LEV <volts>` | f32 | Set burst trigger level in volts. | ≥ 0.1.0 |
| `TRIG:BURST:DIR <RISING\|FALLING>` | enum | Set burst trigger edge direction. | ≥ 0.1.0 |
| `TRIG:BURST:HYS <percent>` | f32 | Set burst-trigger hysteresis as a percentage of the full-scale range. Enter the numeric value only; for example, `5` means 5%. | ≥ 0.1.0 |
| `TRIG:BURST:QUIET:UPPER <volts>` | f32 | Set the upper bound of the burst trigger quiet window. | ≥ 0.1.0 |
| `TRIG:BURST:QUIET:LOWER <volts>` | f32 | Set the lower bound of the burst trigger quiet window. | ≥ 0.1.0 |
| `TRIG:BURST:QUIET:TIME <femtoseconds>` | i64 | Set the required quiet-window duration before burst arming. | ≥ 0.1.0 |

### Queries

| Query | Response | Type | Description | Version |
| --- | --- | --- | --- | --- |
| `TRIG:SOU?` | `CHAN1`, `CHAN2`, `CHAN3`, `CHAN4`, `EXT`, `NONE` | enum | Get trigger source. | `CHAN1\|CHAN2\|CHAN3\|CHAN4\|NONE` ≥ 0.1.0<br>`EXT` ≥ 0.3.0 |
| `TRIG:TYPE?` | `EDGE, BURST` | enum | Get trigger type as uppercase enum name. | ≥ 0.1.0 |
| `TRIG:DEL?` | `<femtoseconds>` | i64 | Get trigger delay. | ≥ 0.1.0 |
| `TRIG:HOLD?` | `<femtoseconds>` | u64 | Get trigger holdoff. | ≥ 0.1.0 |
| `TRIG:INTER?` | `true`, `false` | bool | Get trigger interpolation enabled. | ≥ 0.1.0 |
| `TRIG:EDGE:LEV?` | `<volts>` | f32 | Get edge trigger level (formatted `0.######`). | ≥ 0.1.0 |
| `TRIG:EDGE:DIR?` | `RISING`, `FALLING`, `ANY` | enum | Get edge trigger direction as uppercase enum name. | ≥ 0.1.0 |
| `TRIG:EDGE:HYS?` | `<percent>` | f32 | Get edge-trigger hysteresis as a numeric percentage (formatted `0.######`, without `%`). | ≥ 0.1.0 |
| `TRIG:BURST:LEV?` | `<volts>` | f32 | Get burst trigger level (formatted `0.######`). | ≥ 0.1.0 |
| `TRIG:BURST:DIR?` | `RISING`, `FALLING` | enum | Get burst trigger edge direction as uppercase enum name. | ≥ 0.1.0 |
| `TRIG:BURST:EDGE?` | `RISING`, `FALLING` | enum | Alias for `TRIG:BURST:DIR?`. | ≥ 0.1.0 |
| `TRIG:BURST:HYS?` | `<percent>` | f32 | Get burst-trigger hysteresis as a numeric percentage (formatted `0.######`, without `%`). | ≥ 0.1.0 |
| `TRIG:BURST:QUIET:UPPER?` | `<volts>` | f32 | Get the upper quiet-window bound (formatted `0.######`). | ≥ 0.1.0 |
| `TRIG:BURST:QUIET:LOWER?` | `<volts>` | f32 | Get the lower quiet-window bound (formatted `0.######`). | ≥ 0.1.0 |
| `TRIG:BURST:QUIET:TIME?` | `<femtoseconds>` | i64 | Get the required quiet-window duration. | ≥ 0.1.0 |

## Channel subsystem (`CHAN<n>...`)

Subject matches `CHAN`/`CHANnel` abbreviations via `subject.StartsWith("CHAN")` and requires the subject to end in a digit.

- Channels are `1` to `4` (`CHAN1` ... `CHAN4`).

### Commands

| Command | Type | Description | Version |
| --- | ---: | --- | --- |
| `CHAN<n>:ON` | - | Enable channel `<n>`. | ≥ 0.1.0 |
| `CHAN<n>:OFF` | - | Disable channel `<n>`. | ≥ 0.1.0 |
| `CHAN<n>:BAND <FULL\|750M\|650M\|350M\|200M\|100M\|20M>` | enum | Set channel bandwidth limit/filter. | ≥ 0.1.0 |
| `CHAN<n>:COUP <DC\|AC>` | enum | Set channel coupling. | ≥ 0.1.0 |
| `CHAN<n>:TERM <1M\|50>` | enum | Set channel termination. | ≥ 0.1.0 |
| `CHAN<n>:OFFS <volts>` | f32 | Set channel voltage offset. Clamped to `[-50, 50]`. | ≥ 0.1.0 |
| `CHAN<n>:RANG <volts>` | f32 | Set channel full-scale range. Clamped to `[-50, 50]`. | ≥ 0.1.0 |

### Queries

| Query | Response | Type | Description | Version |
| --- | --- | --- | --- | --- |
| `CHAN<n>:STATE?` | `ON`, `OFF` | enum | Get whether channel `<n>` is enabled. | ≥ 0.1.0 |
| `CHAN<n>:BAND?` | `FULL`, `750M`, `650M`, `350M`, `200M`, `100M`, `20M` | enum | Get channel bandwidth (maps from enum). | ≥ 0.1.0 |
| `CHAN<n>:COUP?` | `DC`, `AC` | enum | Get channel coupling. | ≥ 0.1.0 |
| `CHAN<n>:TERM?` | `1M`, `50` | enum | Get requested channel termination. | ≥ 0.1.0 |
| `CHAN<n>:OFFS?` | `<volts>` | f32 | Get requested voltage offset (formatted `0.######`). | ≥ 0.1.0 |
| `CHAN<n>:RANG?` | `<volts>` | f32 | Get requested full-scale range (formatted `0.######`). | ≥ 0.1.0 |
| `CHAN<n>:TERM:ACT?` | `1M`, `50` | enum | Get actual channel termination (driver may coerce termination). | ≥ 0.3.0 |
| `CHAN<n>:OFFS:ACT?` | `<volts>` | f32 | Get actual voltage offset (formatted `0.######`). | ≥ 0.3.0 |
| `CHAN<n>:RANG:ACT?` | `<volts>` | f32 | Get actual full-scale range (formatted `0.######`). | ≥ 0.3.0 |

## Reference clock subsystem (`REFCL...`)

Subject matches `REFCL` via `subject.StartsWith("REFCL")`.

### Commands

| Command | Type | Description | Version |
| --- | ---: | --- | --- |
| `REFCL:MODE <IN\|OUT\|OFF>` | enum | Set mode of REFCLK IN/OUT BNC. | ≥ 0.1.0 |
| `REFCL:FREQ <frequency>` | u32 | Set the input clock frequency if in IN mode, or output frequency if in OUT mode. | ≥ 0.1.0 |

## Processing subsystem (`PRO...`)

Subject matches `PRO` via `subject.StartsWith("PRO")`.

### Commands

| Command | Type | Description |
|---|---:|---|
| `tbd` | `tbd` | tbd |
