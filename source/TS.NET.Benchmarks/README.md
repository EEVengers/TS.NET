## Four channel shuffle I8 run length 1

| Platform                       | Mean      | Error    | StdDev   |
|------------------------------- |----------:|---------:|---------:|
| x64 i7-13700H 16GB [scalar]    | 224.23 ms | 0.258 ms | 0.216 ms |
| x64 i7-13700H 16GB [AVX2]      |  35.16 ms | 0.151 ms | 0.118 ms |
| ARM64 M4 10C 10G 16GB [scalar] | 127.49 ms | 0.755 ms | 0.706 ms |
| ARM64 M4 10C 10G 16GB [Neon]   |  37.65 ms | 0.220 ms | 0.206 ms |

## ShuffleI8

Scalar processing

| Method                             | Mean     | Error   | StdDev  | Allocated |
|----------------------------------- |---------:|--------:|--------:|----------:|
| 'Four channel shuffle (125 x 8MS)' | 226.7 ms | 0.37 ms | 0.31 ms |     133 B |
| 'Two channel shuffle (125 x 8MS)'  | 238.2 ms | 0.12 ms | 0.10 ms |      21 B |

AVX2 processing

| Method                             | Mean     | Error    | StdDev   | Allocated |
|----------------------------------- |---------:|---------:|---------:|----------:|
| 'Four channel shuffle (125 x 8MS)' | 34.85 ms | 0.052 ms | 0.047 ms |       7 B |
| 'Two channel shuffle (125 x 8MS)'  | 37.77 ms | 0.099 ms | 0.092 ms |      29 B |

## RisingEdgeTriggerI8

Scalar processing

| Method                                                                              | Mean       | Error     | StdDev    | Allocated |
|------------------------------------------------------------------------------------ |-----------:|----------:|----------:|----------:|
| 'Rising edge (hysteresis: 10), width: 1us, signal: DC (0), run length: 125 x 8M'    | 268.701 ms | 1.8045 ms | 1.6879 ms |      32 B |
| 'Rising edge (hysteresis: 10), width: 1us, signal: DC (50), run length: 125 x 8M'   | 265.516 ms | 1.3546 ms | 1.1311 ms |     200 B |
| 'Rising edge (hysteresis: 10), width: 1us, signal: 1KHz sine, run length: 125 x 8M' | 263.654 ms | 1.4123 ms | 1.3211 ms |     200 B |
| 'Rising edge (hysteresis: 10), width: 1us, signal: 1MHz sine, run length: 125 x 8M' | 138.792 ms | 1.2891 ms | 1.2058 ms |      16 B |
| 'Rising edge (hysteresis: 10), width: 1ms, signal: DC (0), run length: 125 x 8M'    | 264.236 ms | 1.2231 ms | 1.1441 ms |      32 B |
| 'Rising edge (hysteresis: 10), width: 1ms, signal: DC (50), run length: 125 x 8M'   | 264.919 ms | 2.3736 ms | 2.2203 ms |      32 B |
| 'Rising edge (hysteresis: 10), width: 1ms, signal: 1KHz sine, run length: 125 x 8M' | 132.259 ms | 0.5868 ms | 0.5489 ms |     100 B |
| 'Rising edge (hysteresis: 10), width: 1ms, signal: 1MHz sine, run length: 125 x 8M' |   1.028 ms | 0.0066 ms | 0.0062 ms |       1 B |

AVX2 processing

| Method                                                                              | Mean        | Error     | StdDev    | Allocated |
|------------------------------------------------------------------------------------ |------------:|----------:|----------:|----------:|
| 'Rising edge (hysteresis: 10), width: 1us, signal: DC (0), run length: 125 x 8M'    | 17,805.4 us | 134.66 us | 125.96 us |      12 B |
| 'Rising edge (hysteresis: 10), width: 1us, signal: DC (50), run length: 125 x 8M'   | 17,419.0 us | 139.13 us | 130.14 us |      12 B |
| 'Rising edge (hysteresis: 10), width: 1us, signal: 1KHz sine, run length: 125 x 8M' | 18,567.1 us |  83.83 us |  74.32 us |      12 B |
| 'Rising edge (hysteresis: 10), width: 1us, signal: 1MHz sine, run length: 125 x 8M' | 18,423.6 us | 199.72 us | 166.77 us |      12 B |
| 'Rising edge (hysteresis: 10), width: 1ms, signal: DC (0), run length: 125 x 8M'    | 17,332.0 us | 126.19 us | 111.87 us |      12 B |
| 'Rising edge (hysteresis: 10), width: 1ms, signal: DC (50), run length: 125 x 8M'   | 17,268.6 us | 101.62 us |  95.06 us |      12 B |
| 'Rising edge (hysteresis: 10), width: 1ms, signal: 1KHz sine, run length: 125 x 8M' |  9,537.0 us |  37.05 us |  34.65 us |       6 B |
| 'Rising edge (hysteresis: 10), width: 1ms, signal: 1MHz sine, run length: 125 x 8M' |    815.0 us |   3.61 us |   3.20 us |         - |

## FallingEdgeI8Trigger

Scalar processing

| Method                                                                               | Mean       | Error     | StdDev    | Allocated |
|------------------------------------------------------------------------------------- |-----------:|----------:|----------:|----------:|
| 'Falling edge (hysteresis: 10), width: 1us, signal: DC (0), run length: 125 x 8M'    | 273.406 ms | 2.0984 ms | 1.9629 ms |     200 B |
| 'Falling edge (hysteresis: 10), width: 1us, signal: DC (50), run length: 125 x 8M'   | 266.990 ms | 2.4998 ms | 2.3383 ms |     200 B |
| 'Falling edge (hysteresis: 10), width: 1us, signal: 1KHz sine, run length: 125 x 8M' | 263.267 ms | 1.1823 ms | 1.0481 ms |      56 B |
| 'Falling edge (hysteresis: 10), width: 1us, signal: 1MHz sine, run length: 125 x 8M' | 141.185 ms | 0.4615 ms | 0.4316 ms |     100 B |
| 'Falling edge (hysteresis: 10), width: 1ms, signal: DC (0), run length: 125 x 8M'    | 263.917 ms | 0.8668 ms | 0.8108 ms |     200 B |
| 'Falling edge (hysteresis: 10), width: 1ms, signal: DC (50), run length: 125 x 8M'   | 264.022 ms | 1.1193 ms | 1.0470 ms |     200 B |
| 'Falling edge (hysteresis: 10), width: 1ms, signal: 1KHz sine, run length: 125 x 8M' | 132.400 ms | 0.4703 ms | 0.4399 ms |      16 B |
| 'Falling edge (hysteresis: 10), width: 1ms, signal: 1MHz sine, run length: 125 x 8M' |   1.025 ms | 0.0086 ms | 0.0067 ms |       1 B |

AVX2 processing

| Method                                                                               | Mean        | Error     | StdDev    | Allocated |
|------------------------------------------------------------------------------------- |------------:|----------:|----------:|----------:|
| 'Falling edge (hysteresis: 10), width: 1us, signal: DC (0), run length: 125 x 8M'    | 18,352.7 us | 226.13 us | 188.83 us |      12 B |
| 'Falling edge (hysteresis: 10), width: 1us, signal: DC (50), run length: 125 x 8M'   | 17,660.1 us | 106.32 us |  88.78 us |      12 B |
| 'Falling edge (hysteresis: 10), width: 1us, signal: 1KHz sine, run length: 125 x 8M' | 18,748.8 us | 186.44 us | 174.40 us |      12 B |
| 'Falling edge (hysteresis: 10), width: 1us, signal: 1MHz sine, run length: 125 x 8M' | 18,273.9 us | 164.83 us | 146.12 us |      12 B |
| 'Falling edge (hysteresis: 10), width: 1ms, signal: DC (0), run length: 125 x 8M'    | 17,362.2 us | 164.68 us | 145.98 us |      12 B |
| 'Falling edge (hysteresis: 10), width: 1ms, signal: DC (50), run length: 125 x 8M'   | 17,197.7 us |  66.53 us |  51.94 us |      12 B |
| 'Falling edge (hysteresis: 10), width: 1ms, signal: 1KHz sine, run length: 125 x 8M' |  9,495.0 us |  37.35 us |  34.94 us |       6 B |
| 'Falling edge (hysteresis: 10), width: 1ms, signal: 1MHz sine, run length: 125 x 8M' |    811.7 us |   3.62 us |   3.39 us |         - |

## AnyEdgeI8Trigger

Scalar processing

| Method                                                                           | Mean         | Error       | StdDev      | Allocated |
|--------------------------------------------------------------------------------- |-------------:|------------:|------------:|----------:|
| 'Any edge (hysteresis: 10), width: 1us, signal: DC (0), run length: 125 x 8M'    | 268,632.7 us | 1,693.87 us | 1,584.45 us |     200 B |
| 'Any edge (hysteresis: 10), width: 1us, signal: DC (50), run length: 125 x 8M'   | 269,505.9 us | 2,489.15 us | 2,328.35 us |     200 B |
| 'Any edge (hysteresis: 10), width: 1us, signal: 1KHz sine, run length: 125 x 8M' | 265,351.6 us | 1,046.32 us |   873.72 us |     200 B |
| 'Any edge (hysteresis: 10), width: 1us, signal: 1MHz sine, run length: 125 x 8M' |   3,027.9 us |    30.86 us |    28.86 us |       2 B |
| 'Any edge (hysteresis: 10), width: 1ms, signal: DC (0), run length: 125 x 8M'    | 266,232.0 us | 1,694.59 us | 1,415.06 us |     200 B |
| 'Any edge (hysteresis: 10), width: 1ms, signal: DC (50), run length: 125 x 8M'   | 265,793.0 us | 1,799.20 us | 1,682.97 us |     200 B |
| 'Any edge (hysteresis: 10), width: 1ms, signal: 1KHz sine, run length: 125 x 8M' |     777.8 us |     4.02 us |     3.76 us |         - |
| 'Any edge (hysteresis: 10), width: 1ms, signal: 1MHz sine, run length: 125 x 8M' |     778.4 us |     4.73 us |     4.42 us |         - |

AVX2 processing

| Method                                                                           | Mean        | Error     | StdDev    | Allocated |
|--------------------------------------------------------------------------------- |------------:|----------:|----------:|----------:|
| 'Any edge (hysteresis: 10), width: 1us, signal: DC (0), run length: 125 x 8M'    | 18,017.8 us | 353.40 us | 347.08 us |      12 B |
| 'Any edge (hysteresis: 10), width: 1us, signal: DC (50), run length: 125 x 8M'   | 17,488.2 us | 284.37 us | 369.76 us |       2 B |
| 'Any edge (hysteresis: 10), width: 1us, signal: 1KHz sine, run length: 125 x 8M' | 18,821.4 us | 235.44 us | 208.71 us |      12 B |
| 'Any edge (hysteresis: 10), width: 1us, signal: 1MHz sine, run length: 125 x 8M' |  4,109.1 us |  63.19 us |  52.76 us |       3 B |
| 'Any edge (hysteresis: 10), width: 1ms, signal: DC (0), run length: 125 x 8M'    | 17,157.8 us | 114.11 us | 101.16 us |      12 B |
| 'Any edge (hysteresis: 10), width: 1ms, signal: DC (50), run length: 125 x 8M'   | 17,391.9 us | 287.89 us | 308.04 us |      12 B |
| 'Any edge (hysteresis: 10), width: 1ms, signal: 1KHz sine, run length: 125 x 8M' |    777.0 us |   3.17 us |   2.97 us |         - |
| 'Any edge (hysteresis: 10), width: 1ms, signal: 1MHz sine, run length: 125 x 8M' |    773.8 us |   1.51 us |   1.18 us |         - |