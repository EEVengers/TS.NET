## ShuffleI8.FourChannels (1006632960 samples)

| Platform                                      | Mean      | Error    | StdDev   |
|---------------------------------------------- |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB DDR5 4800MT/s [scalar]    | 232.36 ms | 0.100 ms | 0.089 ms |
| x64 i7-13700H 2x8GB DDR5 4800MT/s [SSSE3]     |  33.95 ms | 0.073 ms | 0.068 ms |
| x64 i7-13700H 2x8GB DDR5 4800MT/s [AVX2]      |  29.72 ms | 0.046 ms | 0.043 ms |
| x64 5800X 2x32GB DDR4 3200MT/s [scalar]       | 384.34 ms | 0.523 ms | 0.489 ms |
| x64 5800X 2x32GB DDR4 3200MT/s [SSSE3] *      |  21.93 ms | 0.437 ms | 0.680 ms |
| x64 5800X 2x32GB DDR4 3200MT/s [AVX2] *       |  16.90 ms | 0.153 ms | 0.128 ms |
| x64 7800X3D 2x32GB DDR5 5200MT/s [scalar]     | 203.18 ms | 0.937 ms | 1.590 ms |
| x64 7800X3D 2x32GB DDR5 5200MT/s [SSSE3] *    |  17.91 ms | 0.091 ms | 0.081 ms |
| x64 7800X3D 2x32GB DDR5 5200MT/s [AVX2] *     |  18.04 ms | 0.079 ms | 0.070 ms |
| ARM64 M4 4P6E 2x8GB LPDDR5X 7500MT/s [scalar] | 127.49 ms | 0.755 ms | 0.706 ms |
| ARM64 M4 4P6E 2x8GB LPDDR5X 7500MT/s [Neon]   |  18.67 ms | 0.108 ms | 0.101 ms |
| ARM64 M2 Air 2x12GB LPDDR5 6400MT/s [scalar]  | 282.80 ms | 0.704 ms | 0.624 ms |
| ARM64 M2 Air 2x12GB LPDDR5 6400MT/s [Neon]    |  41.77 ms | 0.119 ms | 0.105 ms |

## ShuffleI8.TwoChannels (1006632960 samples)

| Platform                                      | Mean      | Error    | StdDev   |
|---------------------------------------------- |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB DDR5 4800MT/s [scalar]    | 239.20 ms | 0.139 ms | 0.130 ms |
| x64 i7-13700H 2x8GB DDR5 4800MT/s [SSSE3]     |  25.90 ms | 0.056 ms | 0.052 ms |
| x64 i7-13700H 2x8GB DDR5 4800MT/s [AVX2]      |  24.65 ms | 0.062 ms | 0.058 ms |
| x64 5800X 2x32GB DDR4 3200MT/s [scalar]       | 479.38 ms | 1.066 ms | 0.890 ms |
| x64 5800X 2x32GB DDR4 3200MT/s [SSSE3] *      |  18.07 ms | 0.341 ms | 0.511 ms |
| x64 5800X 2x32GB DDR4 3200MT/s [AVX2] *       |  15.53 ms | 0.294 ms | 0.315 ms |
| x64 7800X3D 2x32GB DDR5 5200MT/s [scalar]     | 209.88 ms | 3.152 ms | 3.372 ms |
| x64 7800X3D 2x32GB DDR5 5200MT/s [SSSE3] *    |  16.99 ms | 0.086 ms | 0.080 ms |
| x64 7800X3D 2x32GB DDR5 5200MT/s [AVX2] *     |  16.05 ms | 0.085 ms | 0.075 ms |
| ARM64 M4 4P6E 2x8GB LPDDR5X 7500MT/s [scalar] | 128.47 ms | 0.548 ms | 0.486 ms |
| ARM64 M4 4P6E 2x8GB LPDDR5X 7500MT/s [Neon]   |  17.03 ms | 0.065 ms | 0.058 ms |
| ARM64 M2 Air 2x12GB LPDDR5 6400MT/s [scalar]  | 275.43 ms | 0.890 ms | 0.789 ms |
| ARM64 M2 Air 2x12GB LPDDR5 6400MT/s [Neon]    |  60.83 ms | 1.177 ms | 1.401 ms |

* = Code improvements since benchmark run

## RisingEdgeTriggerI8 - 0% trigger throughput (1006632960 samples)

| Platform                     | Mean      | Error    | StdDev   |
|----------------------------- |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB [scalar] | 283.18 ms | 0.334 ms | 0.313 ms |
| x64 i7-13700H 2x8GB [AVX2]   |  15.66 ms | 0.014 ms | 0.013 ms |
| ARM64 M4 4P6E 16GB [scalar]  | 233.63 ms | 1.485 ms | 1.389 ms |
| ARM64 M4 4P6E 16GB [Neon]    |  15.77 ms | 0.045 ms | 0.040 ms |

## RisingEdgeTriggerI8 - 48% trigger throughput (1006632960 samples)

| Platform                     | Mean      | Error    | StdDev   |
|----------------------------- |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB [scalar] | 149.93 ms | 0.182 ms | 0.170 ms |
| x64 i7-13700H 2x8GB [AVX2]   |   9.56 ms | 0.022 ms | 0.020 ms |
| ARM64 M4 4P6E 16GB [scalar]  | 123.59 ms | 0.458 ms | 0.406 ms |
| ARM64 M4 4P6E 16GB [Neon]    |   8.90 ms | 0.054 ms | 0.051 ms |

## RisingEdgeTriggerI8 - 100% trigger throughput (1006632960 samples)

| Platform                     | Mean      | Error    | StdDev   |
|----------------------------- |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB [scalar] |   0.77 ms | 0.000 ms | 0.000 ms |
| x64 i7-13700H 2x8GB [AVX2]   |   0.77 ms | 0.000 ms | 0.000 ms |
| ARM64 M4 4P6E 16GB [scalar]  |   0.87 ms | 0.024 ms | 0.070 ms |
| ARM64 M4 4P6E 16GB [Neon]    |   0.88 ms | 0.022 ms | 0.066 ms |

## FallingEdgeTriggerI8 - 0% trigger throughput (1006632960 samples)

| Platform                      | Mean      | Error    | StdDev   |
|------------------------------ |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB [scalar]  |   ms |  ms |  ms |
| x64 i7-13700H 2x8GB [AVX2]    |   ms |  ms |  ms |
| ARM64 M4 4P6E 16GB [scalar]   | 231.36 ms | 1.308 ms | 1.160 ms |
| ARM64 M4 4P6E 16GB [Neon]     |  15.70 ms | 0.057 ms | 0.044 ms |

## FallingEdgeTriggerI8 - 48% trigger throughput (1006632960 samples)

| Platform                      | Mean      | Error    | StdDev   |
|------------------------------ |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB [scalar]  |   ms |  ms |  ms |
| x64 i7-13700H 2x8GB [AVX2]    |   ms |  ms |  ms |
| ARM64 M4 4P6E 16GB [scalar]   | 122.49 ms | 0.742 ms | 0.694 ms |
| ARM64 M4 4P6E 16GB [Neon]     |   8.50 ms | 0.040 ms | 0.037 ms |

## FallingEdgeTriggerI8 - 100% trigger throughput (1006632960 samples)

| Platform                      | Mean      | Error    | StdDev   |
|------------------------------ |----------:|---------:|---------:|
| x64 i7-13700H 2x8GB [scalar]  |    ms |  ms |  ms |
| x64 i7-13700H 2x8GB [AVX2]    |    ms |  ms |  ms |
| ARM64 M4 4P6E 16GB [scalar]   |   0.78 ms | 0.022 ms | 0.065 ms |
| ARM64 M4 4P6E 16GB [Neon]     |   0.83 ms | 0.020 ms | 0.058 ms |

## Links

https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/jit/viewing-jit-dumps.md  
https://github.com/dotnet/runtime/blob/main/src/coreclr/inc/clrconfigvalues.h#L718  
https://www.intel.com/content/www/us/en/docs/intrinsics-guide/index.html  
https://developer.arm.com/architectures/instruction-sets/intrinsics/  
https://lwn.net/Articles/255364/  

## ASM

### ShuffleI8.FourChannels x64 [AVX2] hot loop
```
vmovntdqa ymm2, ymmword ptr [rax]
vmovntdqa ymm3, ymmword ptr [rax+0x20]
vmovntdqa ymm4, ymmword ptr [rax+0x40]
vmovntdqa ymm5, ymmword ptr [rax+0x60]
vpshufb  ymm2, ymm2, ymm0
vpshufb  ymm3, ymm3, ymm0
vpshufb  ymm5, ymm5, ymm0
vpunpckldq ymm6, ymm2, ymm3
vpunpckhdq ymm2, ymm2, ymm3
vpshufb  ymm3, ymm4, ymm0
vpunpckldq ymm4, ymm3, ymm5
vpunpckhdq ymm3, ymm3, ymm5
vpunpckldq ymm5, ymm6, ymm4
vpunpckldq ymm7, ymm2, ymm3
vpunpckhdq ymm2, ymm2, ymm3
vpermd   ymm3, ymm1, ymm5
vpunpckhdq ymm4, ymm6, ymm4
vpermd   ymm4, ymm1, ymm4
vpermd   ymm5, ymm1, ymm7
vmovntdq ymmword ptr [r8], ymm3
movsxd   r11, ecx
vmovntdq ymmword ptr [r8+r11], ymm4
movsxd   r11, r10d
vmovntdq ymmword ptr [r8+r11], ymm5
movsxd   r11, r9d
vpermd   ymm2, ymm1, ymm2
vmovntdq ymmword ptr [r8+r11], ymm2
add      rax, 128
add      r8, 32
cmp      rax, rdx
```

### ShuffleI8.TwoChannels x64 [AVX2] hot loop
```
vmovntdqa ymm2, ymmword ptr [rax]
vpshufb  ymm2, ymm2, ymm0
vpermd   ymm2, ymm1, ymm2
vmovntdqa ymm3, ymmword ptr [rax+0x20]
vpshufb  ymm3, ymm3, ymm0
vpermd   ymm3, ymm1, ymm3
vperm2i128 ymm4, ymm2, ymm3, 32
vmovntdq ymmword ptr [r8], ymm4
vperm2i128 ymm2, ymm2, ymm3, 49
vmovntdq ymmword ptr [r8+rcx], ymm2
add      rax, 64
add      r8, 32
cmp      rax, rdx
```