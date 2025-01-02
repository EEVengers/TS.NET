## ShuffleI8.FourChannels (1006632960 samples)

| Platform                      | Mean      | Error    | StdDev   |
|------------------------------ |----------:|---------:|---------:|
| x64 i7-13700H 16GB [scalar]   | 232.36 ms | 0.100 ms | 0.089 ms |
| x64 i7-13700H 16GB [AVX2]     |  38.86 ms | 0.086 ms | 0.080 ms |
| ARM64 M4 4P6E 16GB [scalar]   | 127.49 ms | 0.755 ms | 0.706 ms |
| ARM64 M4 4P6E 16GB [Neon TBL] |  37.65 ms | 0.220 ms | 0.206 ms |
| ARM64 M4 4P6E 16GB [Neon LD4] |  18.67 ms | 0.108 ms | 0.101 ms |

## ShuffleI8.TwoChannels (1006632960 samples)

| Platform                      | Mean      | Error    | StdDev   |
|------------------------------ |----------:|---------:|---------:|
| x64 i7-13700H 16GB [scalar]   | 239.20 ms | 0.139 ms | 0.130 ms |
| x64 i7-13700H 16GB [AVX2]     |  39.21 ms | 0.137 ms | 0.128 ms |
| ARM64 M4 4P6E 16GB [scalar]   | 128.47 ms | 0.548 ms | 0.486 ms |
| ARM64 M4 4P6E 16GB [Neon]     |  17.03 ms | 0.065 ms | 0.058 ms |

## RisingEdgeTriggerI8 - 0% trigger throughput (1006632960 samples)

| Platform                      | Mean      | Error    | StdDev   |
|------------------------------ |----------:|---------:|---------:|
| x64 i7-13700H 16GB [scalar]   | 283.18 ms | 0.334 ms | 0.313 ms |
| x64 i7-13700H 16GB [AVX2]     |  15.66 ms | 0.014 ms | 0.013 ms |
| ARM64 M4 4P6E 16GB [scalar]   | 233.63 ms | 1.485 ms | 1.389 ms |
| ARM64 M4 4P6E 16GB [Neon]     |  17.10 ms | 0.169 ms | 0.142 ms |

## RisingEdgeTriggerI8 - 48% trigger throughput (1006632960 samples)

| Platform                      | Mean      | Error    | StdDev   |
|------------------------------ |----------:|---------:|---------:|
| x64 i7-13700H 16GB [scalar]   | 149.93 ms | 0.182 ms | 0.170 ms |
| x64 i7-13700H 16GB [AVX2]     |   9.56 ms | 0.022 ms | 0.020 ms |
| ARM64 M4 4P6E 16GB [scalar]   | 123.59 ms | 0.458 ms | 0.406 ms |
| ARM64 M4 4P6E 16GB [Neon]     |   9.42 ms | 0.056 ms | 0.500 ms |

## RisingEdgeTriggerI8 - 100% trigger throughput (1006632960 samples)

| Platform                      | Mean      | Error    | StdDev   |
|------------------------------ |----------:|---------:|---------:|
| x64 i7-13700H 16GB [scalar]   |   0.77 ms | 0.000 ms | 0.000 ms |
| x64 i7-13700H 16GB [AVX2]     |   0.77 ms | 0.000 ms | 0.000 ms |
| ARM64 M4 4P6E 16GB [scalar]   |   0.87 ms | 0.024 ms | 0.070 ms |
| ARM64 M4 4P6E 16GB [Neon]     |   0.88 ms | 0.022 ms | 0.066 ms |

## Links

https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/jit/viewing-jit-dumps.md
https://github.com/dotnet/runtime/blob/main/src/coreclr/inc/clrconfigvalues.h#L718
https://www.intel.com/content/www/us/en/docs/intrinsics-guide/index.html
https://developer.arm.com/architectures/instruction-sets/intrinsics/